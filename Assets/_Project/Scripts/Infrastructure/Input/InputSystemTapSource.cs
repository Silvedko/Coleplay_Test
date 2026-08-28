using System;
using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Infrastructure
{
    /// <summary>
    /// Taps and clicks through the Input System. Bound to the generic Pointer device, so the same
    /// code answers a mouse on Windows and a finger on a touchscreen.
    /// </summary>
    public sealed class InputSystemTapSource : ITapSource
    {
        private readonly InputAction _press;
        private readonly InputAction _position;

        private bool _disposed;

        public InputSystemTapSource()
        {
            _press = new InputAction("Tap", InputActionType.Button, "<Pointer>/press");
            _position = new InputAction("PointerPosition", InputActionType.Value, "<Pointer>/position");

            _press.performed += OnPressPerformed;
        }

        public event Action<Vector2> Tapped;

        public void Enable()
        {
            if (_disposed)
            {
                return;
            }

            // Position first: the press callback reads it straight away.
            _position.Enable();
            _press.Enable();
        }

        public void Disable()
        {
            if (_disposed)
            {
                return;
            }

            _press.Disable();
            _position.Disable();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            _press.performed -= OnPressPerformed;
            _press.Dispose();
            _position.Dispose();

            Tapped = null;
        }

        private void OnPressPerformed(InputAction.CallbackContext context)
        {
            Tapped?.Invoke(_position.ReadValue<Vector2>());
        }
    }
}
