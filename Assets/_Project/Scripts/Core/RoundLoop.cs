using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Runs one round at a time: loads the next texture, hands it to <c>draw</c> and owns its handle
    /// until the next texture is shown. Starting a round cancels the previous one, so a replaced
    /// round can never draw over a newer one.
    /// </summary>
    public sealed class RoundLoop : IRoundLoop, IDisposable
    {
        private readonly IImageSource _images;
        private readonly IHudView _hud;
        private readonly Action<Texture2D> _draw;

        private readonly CancellationTokenSource _lifetime = new CancellationTokenSource();

        private CancellationTokenSource _roundCts;
        private IAssetHandle<Texture2D> _current;
        private bool _disposed;

        /// <param name="draw">Receives every loaded texture. A delegate, because drawing is all this class needs.</param>
        public RoundLoop(IImageSource images, IHudView hud, Action<Texture2D> draw)
        {
            _images = images ?? throw new ArgumentNullException(nameof(images));
            _hud = hud ?? throw new ArgumentNullException(nameof(hud));
            _draw = draw ?? throw new ArgumentNullException(nameof(draw));
        }

        public bool IsLoading { get; private set; }

        public void StartNext()
        {
            if (_disposed)
            {
                return;
            }

            IsLoading = true;

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

                // The continuation may have been queued before the round was cancelled.
                ct.ThrowIfCancellationRequested();

                _draw(image.Handle.Value);

                // Release the previous texture only once the new one is on screen.
                _current?.Dispose();
                _current = image.Handle;

                _hud.SetStatus(image.IsFallback ? GameStatus.Fallback : GameStatus.Ready);
            }
            catch (OperationCanceledException)
            {
                // A newer round took over. If the load had already produced a handle, it is ours to release.
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
                // A replaced round must not unlock the board for the round that replaced it.
                if (!ct.IsCancellationRequested)
                {
                    IsLoading = false;
                }
            }
        }

        /// <summary>
        /// Cancel and dispose right away. Safe because <see cref="IImageSource"/> implementations
        /// observe cancellation by polling, not by registering callbacks. Disposing is required:
        /// the source is linked to <c>_lifetime</c> and would otherwise leak a registration per round.
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
