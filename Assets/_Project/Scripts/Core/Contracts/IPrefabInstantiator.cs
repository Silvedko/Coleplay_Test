using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.Core
{
    /// <summary>Asynchronous prefab instantiation. Implementations must never block the main thread.</summary>
    public interface IPrefabInstantiator
    {
        /// <summary>
        /// Instantiates a prefab. Disposing the returned handle destroys the instance
        /// and releases the prefab.
        /// </summary>
        UniTask<IAssetHandle<GameObject>> InstantiateAsync(AssetKey key, Transform parent, CancellationToken ct);
    }
}
