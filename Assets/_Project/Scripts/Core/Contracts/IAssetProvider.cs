using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Core
{
    /// <summary>
    /// Asynchronous asset loading. Implementations must never block the main thread.
    /// </summary>
    public interface IAssetProvider
    {
        /// <summary>
        /// Loads a single asset.
        /// Throws <see cref="AssetLoadException"/> when the asset cannot be loaded and
        /// <see cref="System.OperationCanceledException"/> when the token is cancelled.
        /// In both cases the underlying handle is released by the implementation.
        /// </summary>
        UniTask<IAssetHandle<T>> LoadAsync<T>(AssetKey key, CancellationToken ct) where T : Object;

        /// <summary>
        /// Instantiates a prefab. Disposing the returned handle destroys the instance
        /// and releases the prefab.
        /// </summary>
        UniTask<IAssetHandle<GameObject>> InstantiateAsync(AssetKey key, Transform parent, CancellationToken ct);
    }
}
