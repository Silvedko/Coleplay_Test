using UnityEngine;

namespace Game.Core
{
    /// <summary>A set of identical-looking objects, one of which is the hidden target.</summary>
    public interface ITargetBoard
    {
        bool IsTarget(ITargetView view);

        /// <summary>Applies one texture to every object, the hidden target included.</summary>
        void Dress(Texture2D texture);

        /// <summary>Hides the target under a different object than the current one.</summary>
        void MoveTarget();
    }
}
