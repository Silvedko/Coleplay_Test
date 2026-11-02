using System;
using Game.Core;
using UnityEngine;

namespace Game.Infrastructure
{
    /// <summary>
    /// Reports which object a tap landed on by raycasting the interactable layer.
    ///
    /// The view is fetched through the interface rather than a concrete component, so Infrastructure
    /// never has to reference Presentation - Unity resolves GetComponentInParent against interfaces
    /// just as well as against classes.
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

        public bool TryGetTarget(Vector2 screenPosition, out ITargetView target)
        {
            target = null;

            if (_camera == null)
            {
                return false;
            }

            var ray = _camera.ScreenPointToRay(screenPosition);

            if (!Physics.Raycast(ray, out var hit, MaxDistance, _layerMask, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // InParent, not on the collider itself: the prefab is free to keep the collider on a child.
            target = hit.collider.GetComponentInParent<ITargetView>();
            return target != null;
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
