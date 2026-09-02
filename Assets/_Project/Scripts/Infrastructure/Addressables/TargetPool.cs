using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Infrastructure
{
    /// <summary>
    /// Builds the board once and owns it for the whole session. Every object is instantiated at boot
    /// with its shape decided by a coin flip, and then reused round after round - nothing is
    /// instantiated or destroyed while the game runs, because a round only changes which object is the
    /// hidden target and what texture all of them wear.
    ///
    /// Positions come from the camera's viewport rather than from world coordinates, so the board is
    /// always inside the frame and never behind the camera, wherever the scene happens to put it.
    /// </summary>
    public sealed class TargetPool : IDisposable
    {
        /// <summary>Keeps the outermost objects clear of the screen edges.</summary>
        private const float Margin = 0.15f;

        /// <summary>Share of a grid cell an object may wander from its slot, so the board is not a spreadsheet.</summary>
        private const float JitterFraction = 0.15f;

        private readonly IPrefabInstantiator _assets;
        private readonly IReadOnlyList<AssetKey> _prefabKeys;
        private readonly Camera _camera;
        private readonly System.Random _random = new System.Random();

        private readonly List<IAssetHandle<GameObject>> _instances = new List<IAssetHandle<GameObject>>();
        private readonly List<ITargetView> _targets = new List<ITargetView>();

        private Transform _root;
        private bool _ownsRoot;
        private bool _disposed;

        public TargetPool(IPrefabInstantiator assets, IReadOnlyList<AssetKey> prefabKeys, Camera camera)
        {
            _assets = assets ?? throw new ArgumentNullException(nameof(assets));
            _prefabKeys = prefabKeys ?? throw new ArgumentNullException(nameof(prefabKeys));
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));

            if (_prefabKeys.Count == 0)
            {
                throw new ArgumentException("At least one target prefab is required.", nameof(prefabKeys));
            }
        }

        public async UniTask<IReadOnlyList<ITargetView>> SpawnAsync(
            int count, float distance, Transform parent, CancellationToken ct)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TargetPool));
            }

            if (_targets.Count > 0)
            {
                throw new InvalidOperationException("The board has already been spawned.");
            }

            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "At least one object is required.");
            }

            // Everything the pool spawns lives under one transform, so the hierarchy shows a board
            // rather than a heap of loose objects at the scene root. The scene is expected to author
            // that transform; the pool makes its own only when it was handed none.
            _ownsRoot = parent == null;
            _root = _ownsRoot ? new GameObject("Pool").transform : parent;

            for (var i = 0; i < count; i++)
            {
                var key = _prefabKeys[_random.Next(_prefabKeys.Count)];
                var handle = await _assets.InstantiateAsync(key, _root, ct);

                // Tracked before anything else can throw, so a failure halfway through still releases
                // whatever is already on the scene when the pool is disposed.
                _instances.Add(handle);

                var instance = handle.Value;
                instance.transform.SetPositionAndRotation(
                    PositionFor(i, count, distance),
                    Quaternion.Euler(0f, (float)_random.NextDouble() * 360f, 0f));

                var view = instance.GetComponentInChildren<ITargetView>();
                if (view == null)
                {
                    throw new InvalidOperationException(
                        "The target prefab '" + instance.name + "' has no ITargetView component.");
                }

                _targets.Add(view);
            }

            return _targets;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Handles first: releasing an instance is what destroys it properly, and tearing the root
            // down beforehand would pull the objects out from under Addressables.
            for (var i = _instances.Count - 1; i >= 0; i--)
            {
                _instances[i].Dispose();
            }

            _instances.Clear();
            _targets.Clear();

            if (_ownsRoot && _root != null)
            {
                Object.Destroy(_root.gameObject);
            }

            _root = null;
        }

        /// <summary>
        /// Lays the objects out on a grid sized to the count. The camera decides the horizontal and
        /// vertical spread - that is what keeps the board inside the frame - but the depth it returns
        /// is thrown away and every object sits flat on Z = 0.
        ///
        /// Consequence worth knowing: the spread is framed against the plane at <paramref name="distance"/>,
        /// so that value should match the camera's own distance to Z = 0. Set it shorter and the board
        /// bunches up in the middle; set it longer and the outer objects drift past the frame edges.
        /// </summary>
        private Vector3 PositionFor(int index, int count, float distance)
        {
            var columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            var rows = Mathf.CeilToInt(count / (float)columns);

            var x = Axis(index % columns, columns);
            var y = Axis(index / columns, rows);

            var position = _camera.ViewportToWorldPoint(new Vector3(x, y, distance));
            position.z = 0f;

            return position;
        }

        /// <summary>One viewport axis: the slot's centre plus a nudge that never leaves the safe area.</summary>
        private float Axis(int index, int steps)
        {
            var t = steps <= 1 ? 0.5f : index / (float)(steps - 1);
            var centre = Mathf.Lerp(Margin, 1f - Margin, t);

            var cell = (1f - 2f * Margin) / steps;
            var jitter = ((float)_random.NextDouble() * 2f - 1f) * cell * JitterFraction;

            return Mathf.Clamp(centre + jitter, Margin * 0.5f, 1f - Margin * 0.5f);
        }
    }
}
