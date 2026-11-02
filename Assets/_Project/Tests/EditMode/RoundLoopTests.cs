using System;
using System.Text.RegularExpressions;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public sealed class RoundLoopTests
    {
        private FakeImageSource _images;
        private FakeHud _hud;
        private int _drawCount;
        private RoundLoop _rounds;

        [SetUp]
        public void SetUp()
        {
            _images = new FakeImageSource();
            _hud = new FakeHud();
            _drawCount = 0;
            _rounds = new RoundLoop(_images, _hud, _ => _drawCount++);
        }

        [TearDown]
        public void TearDown()
        {
            _rounds.Dispose();
        }

        [Test]
        public void LoadedImage_IsDrawnAndUnlocksBoard()
        {
            _rounds.StartNext();
            Assert.IsTrue(_rounds.IsLoading);
            Assert.AreEqual(GameStatus.Loading, _hud.Status);

            _images.Pending[0].TrySetResult(new RoundImage(new FakeHandle(), isFallback: false));

            Assert.AreEqual(1, _drawCount);
            Assert.IsFalse(_rounds.IsLoading);
            Assert.AreEqual(GameStatus.Ready, _hud.Status);
        }

        [Test]
        public void FallbackImage_ReportsFallbackStatus()
        {
            _rounds.StartNext();
            _images.Pending[0].TrySetResult(new RoundImage(new FakeHandle(), isFallback: true));

            Assert.AreEqual(GameStatus.Fallback, _hud.Status);
        }

        [Test]
        public void NextImage_ReleasesPreviousHandle()
        {
            var first = new FakeHandle();
            var second = new FakeHandle();

            _rounds.StartNext();
            _images.Pending[0].TrySetResult(new RoundImage(first, false));
            _rounds.StartNext();
            _images.Pending[1].TrySetResult(new RoundImage(second, false));

            Assert.IsTrue(first.Disposed);
            Assert.IsFalse(second.Disposed);
        }

        [Test]
        public void ReplacedRound_IsNotDrawnAndItsHandleIsReleased()
        {
            var stale = new FakeHandle();

            _rounds.StartNext();
            _rounds.StartNext();
            _images.Pending[1].TrySetResult(new RoundImage(new FakeHandle(), false));
            _images.Pending[0].TrySetResult(new RoundImage(stale, false));

            Assert.AreEqual(1, _drawCount);
            Assert.IsTrue(stale.Disposed);
            Assert.IsFalse(_rounds.IsLoading);
        }

        [Test]
        public void ReplacedRound_DoesNotUnlockBoard()
        {
            _rounds.StartNext();
            _rounds.StartNext();
            _images.Pending[0].TrySetResult(new RoundImage(new FakeHandle(), false));

            Assert.IsTrue(_rounds.IsLoading);
        }

        [Test]
        public void FailedLoad_KeepsGamePlayable()
        {
            LogAssert.Expect(LogType.Exception, new Regex("boom"));

            _rounds.StartNext();
            _images.Pending[0].TrySetException(new InvalidOperationException("boom"));

            Assert.AreEqual(0, _drawCount);
            Assert.IsFalse(_rounds.IsLoading);
            Assert.AreEqual(GameStatus.Ready, _hud.Status);
        }

        [Test]
        public void Dispose_ReleasesCurrentHandle()
        {
            var handle = new FakeHandle();

            _rounds.StartNext();
            _images.Pending[0].TrySetResult(new RoundImage(handle, false));
            _rounds.Dispose();

            Assert.IsTrue(handle.Disposed);
        }
    }
}
