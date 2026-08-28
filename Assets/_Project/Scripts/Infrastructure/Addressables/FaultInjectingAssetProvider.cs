using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Object = UnityEngine.Object;

namespace Game.Infrastructure
{
    /// <summary>
    /// Demo aid: decorates a real <see cref="IAssetLoader"/> with an artificial delay and a failure
    /// forced every Nth load, so the loading state and the fallback path are both easy to show
    /// without touching a network cable. Both knobs are off at zero, which is how the game ships.
    ///
    /// The fallback key itself is exempt - sabotaging the texture that is supposed to save the round
    /// would defeat the point of a fallback demo, not illustrate it.
    /// </summary>
    public sealed class FaultInjectingAssetProvider : IAssetLoader
    {
        private readonly IAssetLoader _inner;
        private readonly AssetKey _exemptKey;
        private readonly int _artificialDelayMilliseconds;
        private readonly int _failEveryNthLoad;

        private int _loadCount;

        public FaultInjectingAssetProvider(
            IAssetLoader inner, AssetKey exemptKey, int artificialDelayMilliseconds, int failEveryNthLoad)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _exemptKey = exemptKey;
            _artificialDelayMilliseconds = artificialDelayMilliseconds;
            _failEveryNthLoad = failEveryNthLoad;
        }

        public async UniTask<IAssetHandle<T>> LoadAsync<T>(AssetKey key, CancellationToken ct) where T : Object
        {
            if (!key.Equals(_exemptKey))
            {
                if (_artificialDelayMilliseconds > 0)
                {
                    await UniTask.Delay(_artificialDelayMilliseconds, cancellationToken: ct);
                }

                if (_failEveryNthLoad > 0 && ++_loadCount % _failEveryNthLoad == 0)
                {
                    throw new AssetLoadException(key, new Exception("Artificial failure for demo purposes."));
                }
            }

            return await _inner.LoadAsync<T>(key, ct);
        }
    }
}
