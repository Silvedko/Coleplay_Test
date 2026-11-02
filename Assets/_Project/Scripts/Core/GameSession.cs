using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The rules of the game. One object on the board is the hidden target. Tapping it scores,
    /// hides the target under another object and starts the next round. Tapping any other object
    /// is a miss and changes nothing.
    /// </summary>
    public sealed class GameSession
    {
        private readonly ITargetHitTester _hitTester;
        private readonly ITargetBoard _board;
        private readonly IRoundLoop _rounds;
        private readonly IHudView _hud;
        private readonly ScoreCounter _score;

        public GameSession(
            ITargetHitTester hitTester, ITargetBoard board, IRoundLoop rounds, IHudView hud, ScoreCounter score)
        {
            _hitTester = hitTester ?? throw new ArgumentNullException(nameof(hitTester));
            _board = board ?? throw new ArgumentNullException(nameof(board));
            _rounds = rounds ?? throw new ArgumentNullException(nameof(rounds));
            _hud = hud ?? throw new ArgumentNullException(nameof(hud));
            _score = score ?? throw new ArgumentNullException(nameof(score));
        }

        public void OnTapped(Vector2 screenPosition)
        {
            // The target has already moved, but the board still shows the previous image:
            // a tap now would be judged against a picture the player has not seen yet.
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
            BeginNextSearch();
        }

        /// <summary>
        /// The only place a new search starts. Moving the target and starting the round always go
        /// together, so the target never moves without the board getting a new image.
        /// </summary>
        private void BeginNextSearch()
        {
            _board.MoveTarget();
            _rounds.StartNext();
        }
    }
}
