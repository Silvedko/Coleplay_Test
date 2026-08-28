using UnityEngine;

namespace Game.Core
{
    /// <summary>Answers whether a screen-space point lands on the target object.</summary>
    public interface ITargetHitTester
    {
        bool IsHit(Vector2 screenPosition);
    }
}
