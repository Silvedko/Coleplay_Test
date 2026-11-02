using System;
using Game.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Game.Presentation
{
    /// <summary>
    /// Binds a hand-authored settings-window prefab to <see cref="GameSettings"/>. It builds nothing:
    /// every widget is a serialized reference set on the prefab.
    ///
    /// The window is a two-way view - a slider writes the model, and the model's
    /// <see cref="GameSettings.Changed"/> event writes the sliders and readouts back - so "Reset"
    /// needs no special-casing. Slider ranges are pushed from <see cref="GameSettings"/> in
    /// <see cref="Initialize"/>, so the prefab does not have to get the min/max exactly right.
    /// </summary>
    public sealed class SettingsView : MonoBehaviour
    {
        [Header("Root")]
        [Tooltip("The subtree shown while the window is open and hidden while it is closed - the full-screen " +
                 "backdrop plus the panel, not this object.")]
        [SerializeField] private GameObject _window;

        [Tooltip("Full-screen raycast-blocking Image behind the panel. Clicking it closes the window and it " +
                 "is what stops taps reaching the game while the window is open.")]
        [SerializeField] private Button _backdropButton;

        [Header("Buttons")]
        [Tooltip("The always-visible button, top-right of the screen, that opens the window.")]
        [SerializeField] private Button _openButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _resetButton;

        [Header("Load delay (ms)")]
        [SerializeField] private Slider _delaySlider;
        [SerializeField] private TMP_Text _delayValue;

        [Header("Fail every Nth round")]
        [SerializeField] private Slider _failSlider;
        [SerializeField] private TMP_Text _failValue;

        [Header("Miss text duration (s)")]
        [SerializeField] private Slider _missSlider;
        [SerializeField] private TMP_Text _missValue;

        private GameSettings _settings;
        private bool _built;
        private bool _syncing;

        private int _defaultDelay;
        private int _defaultFail;
        private float _defaultMiss;

        /// <summary>Raised with <c>true</c> when the window opens and <c>false</c> when it closes.</summary>
        public event Action<bool> OpenChanged;

        public bool IsOpen => _window != null && _window.activeSelf;

        /// <summary>Binds the window to <paramref name="settings"/>. Called once, by the composition root.</summary>
        public void Initialize(GameSettings settings)
        {
            if (_built)
            {
                return;
            }

            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            Require(_window, nameof(_window));
            Require(_openButton, nameof(_openButton));
            Require(_delaySlider, nameof(_delaySlider));
            Require(_failSlider, nameof(_failSlider));
            Require(_missSlider, nameof(_missSlider));

            _built = true;

            _defaultDelay = settings.ArtificialDelayMilliseconds;
            _defaultFail = settings.ForceFailureEveryNthRound;
            _defaultMiss = settings.MissVisibleSeconds;

            BindSlider(_delaySlider, 0f, GameSettings.MaxArtificialDelayMilliseconds, true,
                v => _settings.ArtificialDelayMilliseconds = Mathf.RoundToInt(v));
            BindSlider(_failSlider, 0f, GameSettings.MaxForceFailureEveryNthRound, true,
                v => _settings.ForceFailureEveryNthRound = Mathf.RoundToInt(v));
            BindSlider(_missSlider, GameSettings.MinMissVisibleSeconds, GameSettings.MaxMissVisibleSeconds, false,
                v => _settings.MissVisibleSeconds = v);

            _openButton.onClick.AddListener(Toggle);
            if (_closeButton != null)
            {
                _closeButton.onClick.AddListener(Close);
            }

            if (_backdropButton != null)
            {
                _backdropButton.onClick.AddListener(Close);
            }

            if (_resetButton != null)
            {
                _resetButton.onClick.AddListener(ResetToDefaults);
            }

            _settings.Changed += SyncFromSettings;
            SyncFromSettings();

            _window.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_settings != null)
            {
                _settings.Changed -= SyncFromSettings;
            }
        }

        private void Toggle() => SetOpen(!IsOpen);

        private void Close() => SetOpen(false);

        private void SetOpen(bool open)
        {
            if (_window == null || _window.activeSelf == open)
            {
                return;
            }

            _window.SetActive(open);
            OpenChanged?.Invoke(open);
        }

        private void BindSlider(Slider slider, float min, float max, bool wholeNumbers, UnityAction<float> onChanged)
        {
            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = wholeNumbers;
            slider.onValueChanged.AddListener(v =>
            {
                if (_syncing)
                {
                    return;
                }

                onChanged(v);
                RefreshReadouts();
            });
        }

        private void SyncFromSettings()
        {
            _syncing = true;

            _delaySlider.value = _settings.ArtificialDelayMilliseconds;
            _failSlider.value = _settings.ForceFailureEveryNthRound;
            _missSlider.value = _settings.MissVisibleSeconds;
            RefreshReadouts();

            _syncing = false;
        }

        private void RefreshReadouts()
        {
            if (_delayValue != null)
            {
                _delayValue.text = _settings.ArtificialDelayMilliseconds + " ms";
            }

            if (_failValue != null)
            {
                _failValue.text = _settings.ForceFailureEveryNthRound == 0
                    ? "off"
                    : _settings.ForceFailureEveryNthRound.ToString();
            }

            if (_missValue != null)
            {
                _missValue.text = _settings.MissVisibleSeconds.ToString("0.0") + " s";
            }
        }

        private void ResetToDefaults()
        {
            _settings.ArtificialDelayMilliseconds = _defaultDelay;
            _settings.ForceFailureEveryNthRound = _defaultFail;
            _settings.MissVisibleSeconds = _defaultMiss;
        }

        private void Require(UnityEngine.Object field, string fieldName)
        {
            if (field == null)
            {
                throw new InvalidOperationException(name + ": SettingsView." + fieldName + " is not assigned.");
            }
        }
    }
}
