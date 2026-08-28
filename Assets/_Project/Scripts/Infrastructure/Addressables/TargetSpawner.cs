using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

namespace Game.Infrastructure
{
    /// <summary>
    /// Spawns the one object the game is played with. Sphere or cube is decided by a coin flip
    /// before the load starts, so exactly one prefab is ever brought into memory.
    /// </summary>
    public sealed class TargetSpawner : IDisposable
    {
        private readonly IPrefabInstantiator _assets;
        private readonly IReadOnlyList<AssetKey> _prefabKeys;
        private readonly System.Random _random = new System.Random();

        private IAssetHandle<GameObject> _instance;
        private bool _disposed;

        public TargetSpawner(IPrefabInstantiator assets, IReadOnlyList<AssetKey> prefabKeys)
        {
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _prefabKeys = prefabKeys ?? throw new ArgumentNullException(nameof(prefabKeys));

            if (_prefabKeys.Count == 0)
            {
                throw new ArgumentException("At least one target prefab is required.", nameof(prefabKeys));
            }
        }

        public async UniTask<GameObject> SpawnAsync(Transform parent, CancellationToken ct)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TargetSpawner));
            }

            if (_instance != null)
            {
                throw new InvalidOperationException("The target object has already been spawned.");
            }

            var key = _prefabKeys[_random.Next(_prefabKeys.Count)];
            _instance = await _assets.InstantiateAsync(key, parent, ct);

            return _instance.Value;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _instance?.Dispose();
            _instance = null;
        }
    }
}
