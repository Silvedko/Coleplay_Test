namespace Game.Core
{
    /// <summary>Loads the image for each round and puts it on the board.</summary>
    public interface IRoundLoop
    {
        /// <summary>True from the start of a round until its image is shown or its failure is handled.</summary>
        bool IsLoading { get; }

        /// <summary>Cancels the round in flight, if any, and starts a new one.</summary>
        void StartNext();
    }
}
