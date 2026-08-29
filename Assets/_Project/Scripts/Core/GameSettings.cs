using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The runtime-tunable slice of the game configuration. GameConfig still authors the immutable
    /// structural values - which prefabs, which Addressables label - while every knob a player is
    /// allowed to turn while the game is running lives here, behind one <see cref="Changed"/>
    /// notification that both the loading pipeline and the settings window listen to.
    ///
    /// Every setter clamps its value, so a caller - a dragged slider, a future preset - can never
    /// push the game into a nonsense state. Values are session-only: nothing here is persisted, so a
    /// fresh launch always starts from the seeds the composition root copies in from GameConfig.
    /// </summary>
    public sealed class GameSettings : IFaultInjectionSettings
    {
        /// <summary>Slider bounds. Nothing outside this type enforces them; they only keep a runtime control sane.</summary>
        public const int MaxArtificialDelayMilliseconds = 3000;

        public const int MaxForceFailureEveryNthRound = 10;

        public const float MinMissVisibleSeconds = 0.1f;

        public const float MaxMissVisibleSeconds = 5f;

        private int _artificialDelayMilliseconds;
        private int _forceFailureEveryNthRound;
        private float _missVisibleSeconds = 1f;

        /// <summary>Raised after any value actually changes - synchronously, on the caller's thread.</summary>
        public event Action Changed;

        public int ArtificialDelayMilliseconds
        {
            get => _artificialDelayMilliseconds;
            set => Assign(ref _artificialDelayMilliseconds, Mathf.Clamp(value, 0, MaxArtificialDelayMilliseconds));
        }

        public int ForceFailureEveryNthRound
        {
            get => _forceFailureEveryNthRound;
            set => Assign(ref _forceFailureEveryNthRound, Mathf.Clamp(value, 0, MaxForceFailureEveryNthRound));
        }

        public float MissVisibleSeconds
        {
            get => _missVisibleSeconds;
            set => Assign(ref _missVisibleSeconds, Mathf.Clamp(value, MinMissVisibleSeconds, MaxMissVisibleSeconds));
        }

        private void Assign<T>(ref T field, T value) where T : IEquatable<T>
        {
            if (field.Equals(value))
            {
                return;
            }

            field = value;
            Changed?.Invoke();
        }
    }
}
