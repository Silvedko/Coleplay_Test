using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Infrastructure;
using Game.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Composition
{
    /// <summary>
    /// The composition root. Every service is built here, in one readable place, and torn down in
    /// reverse order when the object dies - consumers first, then the provider that owns their handles.
    ///
    /// The boot sequence runs on a cancellation token tied to this component, so leaving play mode
    /// mid-load unwinds quietly instead of touching destroyed objects.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private GameConfig _config;
        [SerializeField] private HudView _hud;
        [SerializeField] private Camera _camera;

        private readonly List<IDisposable> _disposables = new List<IDisposable>();

        private CancellationTokenSource _lifetime;
        private ITapSource _tap;
        private GameSession _session;
        private SettingsView _settingsView;

        private void Awake()
        {
            ValidateInspectorFields();
            _lifetime = new CancellationTokenSource();
        }

        private void Start()
        {
            BootAsync(_lifetime.Token).Forget();
        }

        private void OnDestroy()
        {
            _lifetime?.Cancel();

            if (_tap != null)
            {
                _tap.Tapped -= OnPointerTapped;
            }

            if (_settingsView != null)
            {
                _settingsView.OpenChanged -= OnSettingsOpen;
            }

            // Reverse order: the session releases its texture through the provider,
            // so the provider has to outlive it.
            for (var i = _disposables.Count - 1; i >= 0; i--)
            {
                _disposables[i].Dispose();
            }

            _disposables.Clear();

            _lifetime?.Dispose();
            _lifetime = null;
        }

        private async UniTaskVoid BootAsync(CancellationToken ct)
        {
            try
            {
                // Seeded from the authored config; the settings window mutates this copy at runtime
                // and both the loader decorator and the HUD read it live.
                var settings = new GameSettings
                {
                    ArtificialDelayMilliseconds = _config.ArtificialDelayMilliseconds,
                    ForceFailureEveryNthRound = _config.ForceFailureEveryNthRound,
                    MissVisibleSeconds = _hud.MissVisibleSeconds,
                };

                var assets = Track(new AddressablesAssetProvider());
                var spawner = Track(new TargetSpawner(assets, _config.TargetPrefabKeys));
                var images = Track(new ImageRepository(BuildImagePicker(), BuildImageLoader(assets, settings), _config.FallbackKey));
                var hitTester = new CameraRaycastHitTester(_camera);
                var score = new ScoreCounter();

                _tap = Track(new InputSystemTapSource());
                _session = Track(new GameSession(images, hitTester, _hud, score));

                _hud.SetScore(0);
                _hud.SetStatus(GameStatus.Loading);

                // Both are loaded once per session and released when this component is destroyed.
                var instance = await spawner.SpawnAsync(null, ct);
                instance.transform.Rotate(Vector3.up, 90);
                await images.PreloadFallbackAsync(ct);

                var target = instance.GetComponentInChildren<TargetView>();
                if (target == null)
                {
                    throw new InvalidOperationException(
                        "The target prefab '" + instance.name + "' has no TargetView component.");
                }

                hitTester.Target = target.Collider;
                _session.Attach(target);

                _tap.Tapped += OnPointerTapped;
                _tap.Enable();

                // The window prefab carries its own SettingsView; instantiate it like any other
                // Addressable and hand it the live settings. Released with the other handles on teardown.
                var settingsInstance = Track(await assets.InstantiateAsync(_config.SettingsScreenKey, null, ct));
                _settingsView = settingsInstance.Value.GetComponentInChildren<SettingsView>(true);
                if (_settingsView == null)
                {
                    throw new InvalidOperationException(
                        "The settings window prefab has no SettingsView component.");
                }

                _settingsView.Initialize(settings);
                _settingsView.OpenChanged += OnSettingsOpen;
                _hud.BindSettings(settings);

                _session.StartNextRound();
            }
            catch (OperationCanceledException)
            {
                // Play mode ended while the game was still booting.
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private T Track<T>(T disposable) where T : IDisposable
        {
            _disposables.Add(disposable);
            return disposable;
        }

        private IImagePicker BuildImagePicker()
        {
            return new AddressableImagePicker(_config.ImagesLabel);
        }

        /// <summary>
        /// Always wraps the real loader now that the demo knobs are runtime-editable: the decorator
        /// reads them live from <paramref name="settings"/> and is a straight pass-through while both
        /// sit at zero.
        /// </summary>
        private IAssetLoader BuildImageLoader(IAssetLoader inner, IFaultInjectionSettings settings)
        {
            return new FaultInjectingAssetProvider(inner, _config.FallbackKey, settings);
        }

        /// <summary>
        /// The Input System tap source reads the pointer straight from the device, bypassing uGUI.
        /// While the settings window is open the source is muted outright (see <see cref="OnSettingsOpen"/>);
        /// this guard only has to stop the tap that opens it - a press on the top-right button - from
        /// also landing on the game as a miss.
        /// </summary>
        private void OnPointerTapped(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem != null && eventSystem.IsPointerOverGameObject())
            {
                return;
            }

            _session.OnTapped(screenPosition);
        }

        /// <summary>
        /// Kill game input while the window is up: uGUI raycasts alone cannot stop the Input System
        /// tap source, and polling <c>IsPointerOverGameObject</c> from an input callback is a frame
        /// behind. Muting the source is exact and frame-independent.
        /// </summary>
        private void OnSettingsOpen(bool open)
        {
            if (_tap == null)
            {
                return;
            }

            if (open)
            {
                _tap.Disable();
            }
            else
            {
                _tap.Enable();
            }
        }

        /// <summary>Fails with a readable message instead of a null reference somewhere downstream.</summary>
        private void ValidateInspectorFields()
        {
            if (_config == null)
            {
                throw new InvalidOperationException(name + ": GameConfig is not assigned.");
            }

            if (_hud == null)
            {
                throw new InvalidOperationException(name + ": HudView is not assigned.");
            }

            if (_camera == null)
            {
                throw new InvalidOperationException(name + ": the game camera is not assigned.");
            }
        }
    }
}
