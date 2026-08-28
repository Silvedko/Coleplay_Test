using System;
using Game.Core;
using UnityEngine;

namespace Game.Infrastructure
{
    /// <summary>
    /// Decides whether a tap landed on the target object.
    ///
    /// Two paths, in this order:
    /// 1. The target collider is known - ask it directly. No scene query, no layer configuration,
    ///    and nothing else on the scene can be mistaken for the target.
    /// 2. It is not known yet - raycast the interactable layer instead, so a tap is still answered
    ///    correctly during the window before the object has been spawned and handed over.
    /// </summary>
    public sealed class CameraRaycastHitTester : ITargetHitTester
    {
        /// <summary>Layer the target prefabs live on.</summary>
        public const string DefaultLayerName = "Interactable";

        private const float MaxDistance = 1000f;

        private readonly Camera _camera;
        private readonly int _layerMask;

        public CameraRaycastHitTester(Camera camera)
            : this(camera, ResolveDefaultLayerMask())
        {
        }

        public CameraRaycastHitTester(Camera camera, int layerMask)
        {
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _layerMask = layerMask;
        }

        /// <summary>Set once the target object has been spawned.</summary>
        public Collider Target { get; set; }

        public bool IsHit(Vector2 screenPosition)
        {
            if (_camera == null)
            {
                return false;
            }

            var ray = _camera.ScreenPointToRay(screenPosition);

            if (Target != null)
            {
                return Target.Raycast(ray, out _, MaxDistance);
            }

            return Physics.Raycast(ray, out _, MaxDistance, _layerMask, QueryTriggerInteraction.Ignore);
        }

        private static int ResolveDefaultLayerMask()
        {
            var layer = LayerMask.NameToLayer(DefaultLayerName);

            if (layer < 0)
            {
                Debug.LogWarning("Layer '" + DefaultLayerName
                                 + "' does not exist, so the hit tester falls back to every layer.");
                return Physics.DefaultRaycastLayers;
            }

            return 1 << layer;
        }
    }
}
