using System;
using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// Lays the board out as a jittered grid inside the camera's view, on the plane Z = 0. Each
    /// viewport point is projected onto that plane, so the grid fills the frame for any camera
    /// distance or projection.
    /// </summary>
    public sealed class ViewportGridLayout : IBoardLayout
    {
        /// <summary>Keeps the outermost objects clear of the screen edges (viewport units).</summary>
        private const float Margin = 0.15f;

        /// <summary>How far an object may drift from its grid slot, as a share of the cell size.</summary>
        private const float JitterFraction = 0.15f;

        private static readonly Plane BoardPlane = new Plane(Vector3.forward, Vector3.zero);

        private readonly Camera _camera;
        private readonly System.Random _random;

        public ViewportGridLayout(Camera camera, System.Random random)
        {
            _camera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        public IReadOnlyList<Pose> Arrange(int count)
        {
            if (count < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "At least one object is required.");
            }

            var columns = Mathf.CeilToInt(Mathf.Sqrt(count));
            var rows = Mathf.CeilToInt(count / (float)columns);

            var poses = new Pose[count];
            for (var i = 0; i < count; i++)
            {
                var x = Axis(i % columns, columns);
                var y = Axis(i / columns, rows);
                var rotation = Quaternion.Euler(0f, (float)_random.NextDouble() * 360f, 0f);

                poses[i] = new Pose(ProjectOnBoard(x, y), rotation);
            }

            return poses;
        }

        private Vector3 ProjectOnBoard(float viewportX, float viewportY)
        {
            var ray = _camera.ViewportPointToRay(new Vector3(viewportX, viewportY, 0f));

            if (!BoardPlane.Raycast(ray, out var distance))
            {
                throw new InvalidOperationException("The camera does not look at the board plane (Z = 0).");
            }

            return ray.GetPoint(distance);
        }

        /// <summary>One viewport axis: the slot's centre plus a random nudge that stays on screen.</summary>
        private float Axis(int index, int steps)
        {
            var t = steps <= 1 ? 0.5f : index / (float)(steps - 1);
            var centre = Mathf.Lerp(Margin, 1f - Margin, t);

            var cell = (1f - 2f * Margin) / steps;
            var jitter = ((float)_random.NextDouble() * 2f - 1f) * cell * JitterFraction;

            return Mathf.Clamp(centre + jitter, Margin * 0.5f, 1f - Margin * 0.5f);
        }
    }
}
