using System.Threading;
using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;

namespace Game.Core
{
    /// <summary>
    /// Asynchronous loading of a single asset. Implementations must never block the main thread.
    /// </summary>
    public interface IAssetLoader
    {
        /// <summary>
        /// Loads a single asset.
        /// Throws <see cref="AssetLoadException"/> when the asset cannot be loaded and
        /// <see cref="System.OperationCanceledException"/> when the token is cancelled.
        /// In both cases the underlying handle is released by the implementation.
        /// </summary>
        UniTask<IAssetHandle<T>> LoadAsync<T>(AssetKey key, CancellationToken ct) where T : Object;
    }
}
