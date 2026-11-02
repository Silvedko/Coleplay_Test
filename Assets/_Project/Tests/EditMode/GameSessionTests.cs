using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public sealed class GameSessionTests
    {
        private FakeHitTester _hitTester;
        private FakeBoard _board;
        private FakeRounds _rounds;
        private FakeHud _hud;
        private GameSession _session;

        [SetUp]
        public void SetUp()
        {
            _hitTester = new FakeHitTester();
            _board = new FakeBoard { Target = new FakeTargetView() };
            _rounds = new FakeRounds();
            _hud = new FakeHud();
            _session = new GameSession(_hitTester, _board, _rounds, _hud, new ScoreCounter());
        }

        [Test]
        public void TapOnTarget_ScoresMovesTargetAndStartsRound()
        {
            _hitTester.Result = _board.Target;

            _session.OnTapped(Vector2.zero);

            Assert.AreEqual(1, _hud.Score);
            Assert.AreEqual(1, _board.MoveCount);
            Assert.AreEqual(1, _rounds.StartCount);
        }

        [Test]
        public void TapOnOtherObject_IsMissWithFeedback()
        {
            var other = new FakeTargetView();
            _hitTester.Result = other;

            _session.OnTapped(Vector2.zero);

            Assert.AreEqual(1, other.ErrorFeedbackCount);
            Assert.AreEqual(1, _hud.MissCount);
            Assert.AreEqual(0, _board.MoveCount);
            Assert.AreEqual(0, _rounds.StartCount);
        }

        [Test]
        public void TapOnEmptySpace_IsMiss()
        {
            _session.OnTapped(Vector2.zero);

            Assert.AreEqual(1, _hud.MissCount);
            Assert.AreEqual(0, _rounds.StartCount);
        }

        [Test]
        public void TapWhileLoading_IsIgnored()
        {
            _rounds.IsLoading = true;
            _hitTester.Result = _board.Target;

            _session.OnTapped(Vector2.zero);

            Assert.AreEqual(0, _hud.Score);
            Assert.AreEqual(0, _hud.MissCount);
            Assert.AreEqual(0, _rounds.StartCount);
        }
    }
}
