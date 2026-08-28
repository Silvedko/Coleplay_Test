using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Owns the round loop. One round is one asynchronous texture load applied to the target object.
    ///
    /// A tap on the object starts the next round immediately, even while the previous load is still
    /// in flight; only a tap on an image the player has seen scores. The previous round is cancelled
    /// and guarded twice - by its cancellation token and by a round number checked after every await
    /// - so a stale result can never overwrite a newer one.
    /// </summary>
    public sealed class GameSession : IDisposable
    {
        private readonly IImageSource _images;
        private readonly ITargetHitTester _hitTester;
        private readonly IHudView _hud;
        private readonly ScoreCounter _score;

        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private CancellationTokenSource _roundCts;
        private IAssetHandle<Texture2D> _current;
        private ITargetView _target;
        private int _roundId;
        private bool _imageOnTarget;
        private bool _disposed;

        public GameSession(IImageSource images, ITargetHitTester hitTester, IHudView hud, ScoreCounter score)
        {
            _images = images ?? throw new ArgumentNullException(nameof(images));
            _hitTester = hitTester ?? throw new ArgumentNullException(nameof(hitTester));
            _hud = hud ?? throw new ArgumentNullException(nameof(hud));
            _score = score ?? throw new ArgumentNullException(nameof(score));
        }

        /// <summary>Connects the object spawned at start-up. Called once, before the first round.</summary>
        public void Attach(ITargetView target)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        public void OnTapped(Vector2 screenPosition)
        {
            if (_disposed || _target == null)
            {
                return;
            }

            if (_hitTester.IsHit(screenPosition))
            {
                // A point is for an image the player has actually seen. A tap that lands while the
                // round is still loading scores nothing - it only restarts the round, which is what
                // keeps the cancellation path reachable in normal play.
                if (_imageOnTarget)
                {
                    _hud.SetScore(_score.Increment());
                }

                StartNextRound();
            }
            else
            {
                _target.PlayErrorFeedback();
                _hud.ShowMiss();
            }
        }

        /// <summary>Cancels the round in flight, if any, and starts a new one.</summary>
        public void StartNextRound()
        {
            if (_disposed)
            {
                return;
            }

            // The image on the object belongs to the round that is being replaced, and it has already
            // been paid for by the tap that ended it.
            _imageOnTarget = false;

            ReplaceRoundToken();
            RunRoundAsync(++_roundId, _roundCts.Token).Forget();
        }

        private async UniTaskVoid RunRoundAsync(int roundId, CancellationToken ct)
        {
            _hud.SetStatus(GameStatus.Loading);

            RoundImage image = default;
            try
            {
                image = await _images.NextAsync(ct);

                // Second line of defence: this continuation may have been queued before the cancellation.
                // _target == null guards a caller that starts a round before Attach - none exists today,
                // but StartNextRound is public and OnTapped already guards the same thing.
                if (_disposed || ct.IsCancellationRequested || roundId != _roundId || _target == null)
                {
                    image.Handle?.Dispose();
                    return;
                }

                _target.SetTexture(image.Handle.Value);
                _imageOnTarget = true;

                // Release the previous texture only once the new one is on screen.
                _current?.Dispose();
                _current = image.Handle;

                _hud.SetStatus(image.IsFallback ? GameStatus.Fallback : GameStatus.Ready);
            }
            catch (OperationCanceledException)
            {
                // Normal path: a newer round took over. The provider has already released the handle.
                image.Handle?.Dispose();
            }
            catch (Exception exception)
            {
                image.Handle?.Dispose();
                Debug.LogException(exception);

                // The object keeps its previous texture and the game stays playable.
                if (!_disposed && roundId == _roundId)
                {
                    _hud.SetStatus(GameStatus.Ready);
                }
            }
        }

        /// <summary>
        /// Cancel, then dispose right away - safe because <see cref="IImageSource"/> contracts
        /// implementations to observe cancellation by polling, never by registering a callback that
        /// could fire after the source is gone. Disposing here also matters on its own: this is a
        /// linked source, and skipping it would leak a registration on _lifetime for every round played.
        /// </summary>
        private void ReplaceRoundToken()
        {
            if (_roundCts != null)
            {
                _roundCts.Cancel();
                _roundCts.Dispose();
            }

            _roundCts = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _lifetime.Cancel();
            _roundCts?.Dispose();
            _lifetime.Dispose();

            _current?.Dispose();
            _current = null;
        }
    }
}
