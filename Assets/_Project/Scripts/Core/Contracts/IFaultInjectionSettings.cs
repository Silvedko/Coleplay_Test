namespace Game.Core
{
    /// <summary>
    /// The two knobs the fault-injection loader decorator reads. They are re-read once per load, so
    /// a change made in the settings window at runtime takes effect on the very next round without
    /// rebuilding the loading pipeline.
    /// </summary>
    public interface IFaultInjectionSettings
    {
        /// <summary>Pads every load so the loading state stays long enough to see. Zero in normal play.</summary>
        int ArtificialDelayMilliseconds { get; }

        /// <summary>Forces a failure every Nth load to exercise the fallback path. Zero disables it.</summary>
        int ForceFailureEveryNthRound { get; }
    }
}
