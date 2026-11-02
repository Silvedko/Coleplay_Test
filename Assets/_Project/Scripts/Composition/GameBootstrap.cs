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

        [Tooltip("Parent for every spawned board object.")]
        [SerializeField] private Transform _boardRoot;

        [Tooltip("Screen-space canvas the settings window is instantiated under.")]
        [SerializeField] private RectTransform _uiRoot;

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

            // Reverse order: the round loop releases its texture through the provider,
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

                var random = new System.Random();
                var assets = Track(new AddressablesAssetProvider());
                var layout = new ViewportGridLayout(_camera, random);
                var spawner = Track(new TargetBoardSpawner(assets, _config.TargetPrefabKeys, layout, random));
                var images = Track(new ImageRepository(BuildImagePicker(), BuildImageLoader(assets, settings), _config.FallbackKey));
                var hitTester = new CameraRaycastHitTester(_camera);
                var score = new ScoreCounter();

                _tap = Track(new InputSystemTapSource());

                _hud.SetScore(0);
                _hud.SetStatus(GameStatus.Loading);

                // The board objects and the fallback texture live until this component is destroyed.
                // The board opens on the fallback: it is local, so the first frame never waits on the network.
                var targets = await spawner.SpawnAsync(_config.MaxTargets, _boardRoot, ct);
                var fallbackTexture = await images.PreloadFallbackAsync(ct);

                // Composed only after the board exists, so no object needs a second initialisation step.
                var board = new TargetBoard(targets, random);
                board.Dress(fallbackTexture);

                var rounds = Track(new RoundLoop(images, _hud, board.Dress));
                _session = new GameSession(hitTester, board, rounds, _hud, score);

                _hud.SetStatus(GameStatus.Ready);

                _tap.Tapped += OnPointerTapped;
                _tap.Enable();

                // Released with the other handles on teardown.
                var settingsInstance = Track(await assets.InstantiateAsync(_config.SettingsScreenKey, _uiRoot, ct));
                _settingsView = PrefabContract.RequireOnRoot<SettingsView>(settingsInstance.Value);

                _settingsView.Initialize(settings);
                _settingsView.OpenChanged += OnSettingsOpen;
                _hud.BindSettings(settings);

                // No round is started here: the first image load happens when the player finds the target.
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

            if (_boardRoot == null)
            {
                throw new InvalidOperationException(name + ": the board root is not assigned.");
            }

            if (_uiRoot == null)
            {
                throw new InvalidOperationException(name + ": the UI root canvas is not assigned.");
            }
        }
    }
}
