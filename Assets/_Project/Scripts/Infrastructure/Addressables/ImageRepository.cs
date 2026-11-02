using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

namespace Game.Infrastructure
{
    /// <summary>
    /// Loads the texture an <see cref="IImagePicker"/> points at and owns the fallback policy: a
    /// failed load is not an error for the caller, it simply yields the fallback texture.
    ///
    /// The fallback itself is loaded once and lives until teardown, so it is handed out wrapped in a
    /// non-owning handle - the round loop disposes handles freely and must not release a shared asset.
    /// </summary>
    public sealed class ImageRepository : IImageSource, IDisposable
    {
        private readonly IImagePicker _picker;
        private readonly IAssetLoader _loader;
        private readonly AssetKey _fallbackKey;

        private IAssetHandle<Texture2D> _fallback;
        private bool _disposed;

        public ImageRepository(IImagePicker picker, IAssetLoader loader, AssetKey fallbackKey)
        {
            _picker = picker ?? throw new ArgumentNullException(nameof(picker));
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _fallbackKey = fallbackKey;
        }

        /// <summary>
        /// Loads the fallback once and returns it. The composition root also uses it as the
        /// board's first image, so the texture has a single handle and a single owner.
        /// </summary>
        public async UniTask<Texture2D> PreloadFallbackAsync(CancellationToken ct)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ImageRepository));
            }

            if (_fallback == null)
            {
                var loaded = await _loader.LoadAsync<Texture2D>(_fallbackKey, ct);

                // Disposed while loading: nobody would release this handle later.
                if (_disposed)
                {
                    loaded.Dispose();
                    throw new ObjectDisposedException(nameof(ImageRepository));
                }

                _fallback = loaded;
            }

            return _fallback.Value;
        }

        public async UniTask<RoundImage> NextAsync(CancellationToken ct)
        {
            try
            {
                // The picker's catalog lookup is guarded together with the load on purpose: when the
                // remote content is unreachable the round fails before there is even a key to ask
                // for, and that has to end on the fallback texture just like a failed download does.
                var key = await _picker.NextKeyAsync(ct);
                var handle = await _loader.LoadAsync<Texture2D>(key, ct);
                return new RoundImage(handle, isFallback: false);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException))
            {
                Debug.LogWarning("The round image could not be loaded, showing the fallback texture. "
                                 + exception.Message);

                var fallback = await RequireFallbackAsync(ct, exception);
                return new RoundImage(new SharedHandle<Texture2D>(fallback), isFallback: true);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _fallback?.Dispose();
            _fallback = null;
        }

        /// <summary>
        /// Hands out the shared fallback, loading it if the boot-time preload never ran. Refuses to
        /// return an empty handle: without a texture the caller is better off keeping the current one.
        /// </summary>
        private async UniTask<IAssetHandle<Texture2D>> RequireFallbackAsync(CancellationToken ct, Exception cause)
        {
            await PreloadFallbackAsync(ct);

            if (_fallback == null)
            {
                throw new AssetLoadException(_fallbackKey, cause);
            }

            return _fallback;
        }

        /// <summary>Borrowed asset: the round loop may dispose it, the owner keeps it alive.</summary>
        private sealed class SharedHandle<T> : IAssetHandle<T>
        {
            private readonly IAssetHandle<T> _owner;

            public SharedHandle(IAssetHandle<T> owner) => _owner = owner;

            public T Value => _owner.Value;

            public void Dispose()
            {
            }
        }
    }
}
