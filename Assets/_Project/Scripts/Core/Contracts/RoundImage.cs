using UnityEngine;

namespace Game.Core
{
    /// <summary>Texture for one round plus the flag telling whether the fallback asset was substituted.</summary>
    public readonly struct RoundImage
    {
        public readonly IAssetHandle<Texture2D> Handle;
        public readonly bool IsFallback;

        public RoundImage(IAssetHandle<Texture2D> handle, bool isFallback)
        {
            Handle = handle;
            IsFallback = isFallback;
        }
    }
}
