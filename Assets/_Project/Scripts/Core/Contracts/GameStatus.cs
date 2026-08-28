namespace Game.Core
{
    /// <summary>State of the current round, shown by the status label.</summary>
    public enum GameStatus
    {
        /// <summary>A texture is being loaded.</summary>
        Loading = 0,

        /// <summary>The texture is on the object, waiting for a tap.</summary>
        Ready = 1,

        /// <summary>The load failed and the fallback texture is on the object.</summary>
        Fallback = 2,
    }
}
