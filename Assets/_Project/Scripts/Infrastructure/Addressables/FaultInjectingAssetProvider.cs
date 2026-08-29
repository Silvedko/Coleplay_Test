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
    /// without touching a network cable.
    ///
    /// Both knobs are read from <see cref="IFaultInjectionSettings"/> on every load rather than
    /// captured once, so the settings window can turn them on and off mid-session. When both read
    /// zero this decorator is a straight pass-through - one comparison per load - which is why it is
    /// now always in the pipeline instead of being wired in only when a knob starts non-zero.
    ///
    /// The fallback key itself is exempt - sabotaging the texture that is supposed to save the round
    /// would defeat the point of a fallback demo, not illustrate it.
    /// </summary>
    public sealed class FaultInjectingAssetProvider : IAssetLoader
    {
        private readonly IAssetLoader _inner;
        private readonly AssetKey _exemptKey;
        private readonly IFaultInjectionSettings _settings;

        private int _loadCount;

        public FaultInjectingAssetProvider(IAssetLoader inner, AssetKey exemptKey, IFaultInjectionSettings settings)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _exemptKey = exemptKey;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async UniTask<IAssetHandle<T>> LoadAsync<T>(AssetKey key, CancellationToken ct) where T : Object
        {
            if (!key.Equals(_exemptKey))
            {
                var delayMilliseconds = _settings.ArtificialDelayMilliseconds;
                if (delayMilliseconds > 0)
                {
                    await UniTask.Delay(delayMilliseconds, cancellationToken: ct);
                }

                var failEveryNthLoad = _settings.ForceFailureEveryNthRound;
                if (failEveryNthLoad > 0 && ++_loadCount % failEveryNthLoad == 0)
                {
                    throw new AssetLoadException(key, new Exception("Artificial failure for demo purposes."));
                }
            }

            return await _inner.LoadAsync<T>(key, ct);
        }
    }
}
