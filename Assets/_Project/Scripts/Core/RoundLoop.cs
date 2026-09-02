using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Runs one round at a time: asks for the next texture, hands it to whoever draws it, and owns the
    /// handle until the following texture is on screen.
    ///
    /// A new round may still be started while the previous load is in flight - the rules no longer do
    /// that, but this class does not rely on their restraint. Starting a round cancels the one before
    /// it, and every await is followed by a fresh read of the token, so a round that was replaced can
    /// never draw over the one that replaced it.
    /// </summary>
    public sealed class RoundLoop : IDisposable
    {
        private readonly IImageSource _images;
        private readonly IHudView _hud;
        private readonly Action<Texture2D> _draw;

        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private CancellationTokenSource _roundCts;
        private IAssetHandle<Texture2D> _current;
        private bool _loading;
        private bool _disposed;

        /// <summary>
        /// True from the moment a round starts until its image is on the board or its failure is
        /// handled. The rules read this to keep the board out of play while it is not showing what
        /// the player is meant to be searching.
        /// </summary>
        public bool IsLoading => _loading;

        /// <param name="draw">
        /// Where a loaded texture goes. A delegate rather than an interface because drawing is the
        /// whole of what this class needs from the board - and all it should be able to reach.
        /// </param>
        public RoundLoop(IImageSource images, IHudView hud, Action<Texture2D> draw)
        {
            _images = images ?? throw new ArgumentNullException(nameof(images));
            _hud = hud ?? throw new ArgumentNullException(nameof(hud));
            _draw = draw ?? throw new ArgumentNullException(nameof(draw));
        }

        /// <summary>Cancels the round in flight, if any, and starts a new one.</summary>
        public void StartNext()
        {
            if (_disposed)
            {
                return;
            }

            _loading = true;

            ReplaceRoundToken();
            RunRoundAsync(_roundCts.Token).Forget();
        }

        private async UniTaskVoid RunRoundAsync(CancellationToken ct)
        {
            _hud.SetStatus(GameStatus.Loading);

            RoundImage image = default;
            try
            {
                image = await _images.NextAsync(ct);

                // Re-read rather than trusted from before the await: a load that finishes in the same
                // frame it is replaced resumes on a continuation queued before the cancellation landed.
                ct.ThrowIfCancellationRequested();

                _draw(image.Handle.Value);

                // Release the previous texture only once the new one is on screen.
                _current?.Dispose();
                _current = image.Handle;

                _hud.SetStatus(image.IsFallback ? GameStatus.Fallback : GameStatus.Ready);
            }
            catch (OperationCanceledException)
            {
                // Normal path: a newer round took over, either while the load was running - the
                // provider released the handle on its way out - or just after it produced one, which
                // makes the handle ours to drop.
                image.Handle?.Dispose();
            }
            catch (Exception exception)
            {
                image.Handle?.Dispose();
                Debug.LogException(exception);

                // The board keeps its previous image and the game stays playable.
                if (!ct.IsCancellationRequested)
                {
                    _hud.SetStatus(GameStatus.Ready);
                }
            }
            finally
            {
                // Only the newest round may hand the board back to the player: a round that was
                // replaced is finishing into a game that has already moved on.
                if (!ct.IsCancellationRequested)
                {
                    _loading = false;
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
