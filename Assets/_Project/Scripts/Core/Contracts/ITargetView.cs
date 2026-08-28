using UnityEngine;

namespace Game.Core
{
    /// <summary>The tappable object on the scene.</summary>
    public interface ITargetView
    {
        void SetTexture(Texture2D texture);

        /// <summary>Negative feedback for a missed tap - the object flashes red.</summary>
        void PlayErrorFeedback();
    }
}
