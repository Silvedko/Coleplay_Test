using System;

namespace Game.Core
{
    /// <summary>Raised when an asset cannot be loaded. Cancellation is not an error and is reported separately.</summary>
    public sealed class AssetLoadException : Exception
    {
        public AssetKey Key { get; }

        public AssetLoadException(AssetKey key, Exception innerException)
            : base("Failed to load asset '" + key + "'.", innerException)
        {
            Key = key;
        }
    }
}
