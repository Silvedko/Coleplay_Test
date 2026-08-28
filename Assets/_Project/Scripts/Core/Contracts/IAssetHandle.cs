using System;

namespace Game.Core
{
    /// <summary>
    /// A loaded asset together with its ownership. Disposing releases it in the underlying
    /// loading subsystem (for Addressables that means Addressables.Release).
    /// Handles that are owned elsewhere - the shared fallback texture, for instance -
    /// may implement Dispose as a no-op.
    /// </summary>
    public interface IAssetHandle<out T> : IDisposable
    {
        T Value { get; }
    }
}
