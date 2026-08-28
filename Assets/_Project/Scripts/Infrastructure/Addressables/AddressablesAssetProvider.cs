using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Object = UnityEngine.Object;

namespace Game.Infrastructure
{
    /// <summary>
    /// Addressables behind the <see cref="IAssetLoader"/> and <see cref="IPrefabInstantiator"/>
    /// contracts. One class backs both because both are the same Addressables handle bookkeeping;
    /// each consumer still depends only on the one interface it actually calls.
    ///
    /// Cancellation deserves a word. Addressables cannot abort a load that has already started -
    /// there is no such API - so cancelling a round means "stop waiting for the result and release
    /// the handle", not "stop the download". UniTask does exactly that when asked with
    /// autoReleaseWhenCanceled, which is why a cancelled round leaks nothing.
    ///
    /// Every handle the provider hands out is tracked until it is released, so disposing the
    /// provider cleans up whatever the game still holds.
    /// </summary>
    public sealed class AddressablesAssetProvider : IAssetLoader, IPrefabInstantiator, IDisposable
    {
        private readonly List<AsyncOperationHandle> _assets = new List<AsyncOperationHandle>();
        private readonly List<AsyncOperationHandle<GameObject>> _instances = new List<AsyncOperationHandle<GameObject>>();

        private bool _disposed;

        public async UniTask<IAssetHandle<T>> LoadAsync<T>(AssetKey key, CancellationToken ct)
            where T : Object
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();

            var handle = Addressables.LoadAssetAsync<T>(key.Value);
            _assets.Add(handle);

            try
            {
                await handle.ToUniTask(cancellationToken: ct, autoReleaseWhenCanceled: true);
            }
            catch (OperationCanceledException)
            {
                _assets.Remove(handle);
                throw;
            }
            catch (Exception exception)
            {
                // UniTask rethrows the raw Addressables failure - a missing key, an unreachable remote
                // bundle - so the status check below never runs on a failed load. The rest of the game
                // only knows AssetLoadException, and translating here is what keeps the fallback
                // path reachable instead of blowing up the round.
                ReleaseAsset(handle);
                throw new AssetLoadException(key, exception);
            }

            // Safety net for a failure that somehow arrives without an exception.
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                var error = handle.OperationException;
                ReleaseAsset(handle);
                throw new AssetLoadException(key, error);
            }

            if (_disposed)
            {
                // Teardown won the race. Treat it as a cancellation: nobody is waiting any more.
                ReleaseAsset(handle);
                throw new OperationCanceledException();
            }

            return new AssetHandle<T>(this, handle);
        }

        public async UniTask<IAssetHandle<GameObject>> InstantiateAsync(
            AssetKey key, Transform parent, CancellationToken ct)
        {
            ThrowIfDisposed();
            ct.ThrowIfCancellationRequested();

            var handle = Addressables.InstantiateAsync(key.Value, parent);
            _instances.Add(handle);

            try
            {
                await handle.ToUniTask(cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                // Instances need ReleaseInstance, so this one is not delegated to UniTask.
                ReleaseInstance(handle);
                throw;
            }
            catch (Exception exception)
            {
                // Same translation as in LoadAsync: callers contract on AssetLoadException.
                ReleaseInstance(handle);
                throw new AssetLoadException(key, exception);
            }

            // Safety net for a failure that somehow arrives without an exception.
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                var error = handle.OperationException;
                ReleaseInstance(handle);
                throw new AssetLoadException(key, error);
            }

            if (_disposed)
            {
                ReleaseInstance(handle);
                throw new OperationCanceledException();
            }

            return new InstanceHandle(this, handle);
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
                var handle = _instances[i];
                if (handle.IsValid())
                {
                    Addressables.ReleaseInstance(handle);
                }
            }

            for (var i = _assets.Count - 1; i >= 0; i--)
            {
                var handle = _assets[i];
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }

            _instances.Clear();
            _assets.Clear();
        }

        private void ReleaseAsset(AsyncOperationHandle handle)
        {
            _assets.Remove(handle);

            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }

        private void ReleaseInstance(AsyncOperationHandle<GameObject> handle)
        {
            _instances.Remove(handle);

            if (handle.IsValid())
            {
                Addressables.ReleaseInstance(handle);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AddressablesAssetProvider));
            }
        }

        /// <summary>A loaded asset. Disposing releases it exactly once.</summary>
        private sealed class AssetHandle<T> : IAssetHandle<T> where T : Object
        {
            private readonly AddressablesAssetProvider _owner;
            private readonly AsyncOperationHandle<T> _handle;
            private bool _released;

            public AssetHandle(AddressablesAssetProvider owner, AsyncOperationHandle<T> handle)
            {
                _owner = owner;
                _handle = handle;
            }

            public T Value => _handle.Result;

            public void Dispose()
            {
                if (_released)
                {
                    return;
                }

                _released = true;
                _owner.ReleaseAsset(_handle);
            }
        }

        /// <summary>A spawned prefab instance. Disposing destroys it and releases the prefab.</summary>
        private sealed class InstanceHandle : IAssetHandle<GameObject>
        {
            private readonly AddressablesAssetProvider _owner;
            private readonly AsyncOperationHandle<GameObject> _handle;
            private bool _released;

            public InstanceHandle(AddressablesAssetProvider owner, AsyncOperationHandle<GameObject> handle)
            {
                _owner = owner;
                _handle = handle;
            }

            public GameObject Value => _handle.Result;

            public void Dispose()
            {
                if (_released)
                {
                    return;
                }

                _released = true;
                _owner.ReleaseInstance(_handle);
            }
        }
    }
}
