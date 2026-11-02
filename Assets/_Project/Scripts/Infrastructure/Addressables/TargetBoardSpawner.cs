using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

namespace Game.Infrastructure
{
    /// <summary>
    /// Instantiates the board's objects once at boot and owns them until teardown. Each object gets a
    /// random prefab; where it stands is decided by <see cref="IBoardLayout"/>.
    /// </summary>
    public sealed class TargetBoardSpawner : IDisposable
    {
        private readonly IPrefabInstantiator _assets;
        private readonly IReadOnlyList<AssetKey> _prefabKeys;
        private readonly IBoardLayout _layout;
        private readonly System.Random _random;

        private readonly List<IAssetHandle<GameObject>> _instances = new List<IAssetHandle<GameObject>>();
        private readonly List<ITargetView> _targets = new List<ITargetView>();

        private bool _spawned;
        private bool _disposed;

        public TargetBoardSpawner(
            IPrefabInstantiator assets, IReadOnlyList<AssetKey> prefabKeys, IBoardLayout layout, System.Random random)
        {
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _prefabKeys = prefabKeys ?? throw new ArgumentNullException(nameof(prefabKeys));
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
            _random = random ?? throw new ArgumentNullException(nameof(random));

            if (_prefabKeys.Count == 0)
            {
                throw new ArgumentException("At least one target prefab is required.", nameof(prefabKeys));
            }
        }

        public async UniTask<IReadOnlyList<ITargetView>> SpawnAsync(int count, Transform parent, CancellationToken ct)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TargetBoardSpawner));
            }

            if (_spawned)
            {
                throw new InvalidOperationException("The board has already been spawned.");
            }

            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "At least one object is required.");
            }

            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            _spawned = true;

            var poses = _layout.Arrange(count);
            if (poses.Count != count)
            {
                throw new InvalidOperationException(
                    "The layout returned " + poses.Count + " poses for " + count + " objects.");
            }

            // All instantiations run in parallel; each one registers its handle as soon as it lands,
            // so a failure in one still leaves the others releasable on Dispose.
            var spawns = new UniTask<ITargetView>[count];
            for (var i = 0; i < count; i++)
            {
                var key = _prefabKeys[_random.Next(_prefabKeys.Count)];
                spawns[i] = SpawnOneAsync(key, poses[i], parent, ct);
            }

            _targets.AddRange(await UniTask.WhenAll(spawns));
            return _targets;
        }

        private async UniTask<ITargetView> SpawnOneAsync(AssetKey key, Pose pose, Transform parent, CancellationToken ct)
        {
            var handle = await _assets.InstantiateAsync(key, parent, ct);

            if (_disposed)
            {
                handle.Dispose();
                throw new ObjectDisposedException(nameof(TargetBoardSpawner));
            }

            _instances.Add(handle);

            var instance = handle.Value;
            instance.transform.SetPositionAndRotation(pose.position, pose.rotation);

            return PrefabContract.RequireOnRoot<ITargetView>(instance);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            for (var i = _instances.Count - 1; i >= 0; i--)
            {
                _instances[i].Dispose();
            }

            _instances.Clear();
            _targets.Clear();
        }
    }
}
