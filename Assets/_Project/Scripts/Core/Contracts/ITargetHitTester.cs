using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Resolves a screen-space point into the object it landed on. Every object on the board looks
    /// the same to the player, so knowing that *something* was hit is not enough - the session has to
    /// know which one, to tell the hidden target from the rest.
    /// </summary>
    public interface ITargetHitTester
    {
        bool TryGetTarget(Vector2 screenPosition, out ITargetView target);
    }
}
