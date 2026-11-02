using System;
using System.Collections.Generic;
using Game.Core;
using Game.Infrastructure;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Game.Composition
{
    /// <summary>
    /// The only place where Addressables references are authored by hand. Everything below the
    /// composition root sees plain <see cref="AssetKey"/> values, which is what keeps Core free
    /// of any Addressables types.
    /// </summary>
    [CreateAssetMenu(fileName = "GameConfig", menuName = "Game/Game Config")]
    public sealed class GameConfig : ScriptableObject
    {
        [Header("Board")]
        [Tooltip("Every spawned object picks one of these at random, so the board mixes both shapes.")]
        [SerializeField] private AssetReferenceGameObject[] _targetPrefabs;

        [Tooltip("How many objects the board holds. Exactly one of them is the hidden target.")]
        [SerializeField, Min(1)] private int _maxTargets = 6;

        [Header("Round images")]
        [SerializeField] private AssetLabelReference _imagesLabel;
        [Tooltip("Shown when a round image fails to load, and on the board until the first target is found.")]
        [SerializeField] private AssetReferenceTexture2D _fallbackTexture;

        [Header("Settings window")]
        [Tooltip("Instantiated once at boot; its SettingsView is bound to the runtime GameSettings.")]
        [SerializeField] private AssetReferenceGameObject _settingsScreenPrefab;

        [Header("Demo aids (leave at zero for normal play)")]
        [Tooltip("Pads every load so the loading state is long enough to see.")]
        [SerializeField, Min(0)] private int _artificialDelayMilliseconds;

        [Tooltip("Requests a missing address every Nth round to demonstrate the fallback path.")]
        [SerializeField, Min(0)] private int _forceFailureEveryNthRound;

        private IReadOnlyList<AssetKey> _targetPrefabKeys;

        public IReadOnlyList<AssetKey> TargetPrefabKeys
        {
            get
            {
                if (_targetPrefabKeys != null)
                {
                    return _targetPrefabKeys;
                }

                var keys = new List<AssetKey>();

                if (_targetPrefabs != null)
                {
                    foreach (var reference in _targetPrefabs)
                    {
                        if (reference != null && reference.RuntimeKeyIsValid())
                        {
                            keys.Add(new AssetKey(reference.RuntimeKey));
                        }
                    }
                }

                if (keys.Count == 0)
                {
                    throw new InvalidOperationException(
                        name + ": assign at least one Addressable target prefab.");
                }

                _targetPrefabKeys = keys;
                return _targetPrefabKeys;
            }
        }

        public string ImagesLabel
        {
            get
            {
                if (_imagesLabel == null || string.IsNullOrEmpty(_imagesLabel.labelString))
                {
                    throw new InvalidOperationException(name + ": the round images label is not set.");
                }

                return _imagesLabel.labelString;
            }
        }

        public AssetKey FallbackKey
        {
            get
            {
                if (_fallbackTexture == null || !_fallbackTexture.RuntimeKeyIsValid())
                {
                    throw new InvalidOperationException(name + ": the fallback texture is not assigned.");
                }

                return new AssetKey(_fallbackTexture.RuntimeKey);
            }
        }

        /// <summary>How many objects the board holds. Exactly one of them is the hidden target.</summary>
        public int MaxTargets => _maxTargets;

        public AssetKey SettingsScreenKey
        {
            get
            {
                if (_settingsScreenPrefab == null || !_settingsScreenPrefab.RuntimeKeyIsValid())
                {
                    throw new InvalidOperationException(name + ": the settings window prefab is not assigned.");
                }

                return new AssetKey(_settingsScreenPrefab.RuntimeKey);
            }
        }

        /// <summary>Demo aid: pads every load so the loading state is visible. Zero in normal play.</summary>
        public int ArtificialDelayMilliseconds => _artificialDelayMilliseconds;

        /// <summary>Demo aid: fails every Nth round image load to demonstrate the fallback path. Zero disables it.</summary>
        public int ForceFailureEveryNthRound => _forceFailureEveryNthRound;
    }
}
