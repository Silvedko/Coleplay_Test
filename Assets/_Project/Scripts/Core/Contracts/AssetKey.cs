using System;

namespace Game.Core
{
    /// <summary>
    /// Opaque asset identifier passed from the composition root down to the loading layer.
    /// Core never knows whether the value is an Addressables address, a label or an AssetReference.
    /// </summary>
    public readonly struct AssetKey : IEquatable<AssetKey>
    {
        public readonly object Value;

        public AssetKey(object value)
        {
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }
        
        public bool Equals(AssetKey other) => Equals(Value, other.Value);

        public override bool Equals(object obj) => obj is AssetKey other && Equals(other);

        public override int GetHashCode() => Value != null ? Value.GetHashCode() : 0;

        public override string ToString() => Value != null ? Value.ToString() : "<empty key>";
    }
}
