namespace Game.Core
{
    /// <summary>Number of correct taps in the current session.</summary>
    public sealed class ScoreCounter
    {
        private int _value;

        public int Value => _value;

        public int Increment() => ++_value;

        public void Reset() => _value = 0;
    }
}
