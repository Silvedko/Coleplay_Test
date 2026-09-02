using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The rules of the game. Every object on the board looks the same and one of them is the hidden
    /// target; finding it is the whole game. A correct tap scores, moves the target to another object
    /// and starts a round, whose image then lands on every object at once - so the board the player
    /// searches next wears the picture they just won.
    ///
    /// A tap on any other object is a miss: it gives feedback on that object and changes nothing, the
    /// target included, or the hidden object could never be found.
    /// </summary>
    public sealed class GameSession
    {
        private readonly ITargetHitTester _hitTester;
        private readonly TargetBoard _board;
        private readonly RoundLoop _rounds;
        private readonly IHudView _hud;
        private readonly ScoreCounter _score;

        public GameSession(
            ITargetHitTester hitTester, TargetBoard board, RoundLoop rounds, IHudView hud, ScoreCounter score)
        {
            _hitTester = hitTester ?? throw new ArgumentNullException(nameof(hitTester));
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _rounds = rounds ?? throw new ArgumentNullException(nameof(rounds));
            _hud = hud ?? throw new ArgumentNullException(nameof(hud));
            _score = score ?? throw new ArgumentNullException(nameof(score));
        }

        /// <summary>
        /// One round is one search. While the next image is on its way the board is out of play: the
        /// target has already moved, so a tap accepted now would be scored against a picture the
        /// player has not been shown yet - and enough fast taps would keep cancelling the load the
        /// board is waiting for. The status label already says why nothing is happening.
        /// </summary>
        public void OnTapped(Vector2 screenPosition)
        {
            if (_rounds.IsLoading)
            {
                return;
            }

            if (!_hitTester.TryGetTarget(screenPosition, out var tapped))
            {
                _hud.ShowMiss();
                return;
            }

            if (!_board.IsTarget(tapped))
            {
                tapped.PlayErrorFeedback();
                _hud.ShowMiss();
                return;
            }

            _hud.SetScore(_score.Increment());

            // Re-hidden right away rather than when the image lands: the next search is then ready
            // the instant the board is back in play, and this stays the only place the target moves.
            _board.MoveTarget();
            _rounds.StartNext();
        }
    }
}
