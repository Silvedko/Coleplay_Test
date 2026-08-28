using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using Game.Infrastructure;
using Game.Presentation;
using UnityEngine;

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

            if (_tap != null && _session != null)
            {
                _tap.Tapped -= _session.OnTapped;
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
                var assets = Track(new AddressablesAssetProvider());
                var spawner = Track(new TargetSpawner(assets, _config.TargetPrefabKeys));
                var images = Track(new ImageRepository(BuildImagePicker(), BuildImageLoader(assets), _config.FallbackKey));
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

                _tap.Tapped += _session.OnTapped;
                _tap.Enable();

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
        /// Wraps the real loader with the demo fault only when a demo knob is actually turned on, so
        /// normal play never pays for a decorator it does not use.
        /// </summary>
        private IAssetLoader BuildImageLoader(IAssetLoader inner)
        {
            if (_config.ArtificialDelayMilliseconds <= 0 && _config.ForceFailureEveryNthRound <= 0)
            {
                return inner;
            }

            return new FaultInjectingAssetProvider(inner, _config.FallbackKey, _config.ArtificialDelayMilliseconds, _config.ForceFailureEveryNthRound);
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
