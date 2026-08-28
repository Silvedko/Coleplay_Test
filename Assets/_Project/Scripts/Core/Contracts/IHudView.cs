namespace Game.Core
{
    /// <summary>Status label and score label.</summary>
    public interface IHudView
    {
        void SetStatus(GameStatus status);

        void SetScore(int score);

        /// <summary>
        /// Transient "you missed" feedback. Deliberately separate from <see cref="SetStatus"/>
        /// so that a miss during a load does not wipe the "loading" indicator.
        /// </summary>
        void ShowMiss();
    }
}
