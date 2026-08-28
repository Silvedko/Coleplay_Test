using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>Raw taps and clicks, in screen space.</summary>
    public interface ITapSource : IDisposable
    {
        event Action<Vector2> Tapped;

        void Enable();

        void Disable();
    }
}
