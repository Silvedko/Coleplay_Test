using System;
using System.Linq;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Tests
{
    public sealed class TargetBoardTests
    {
        private static FakeTargetView[] CreateViews(int count)
        {
            return Enumerable.Range(0, count).Select(_ => new FakeTargetView()).ToArray();
        }

        [Test]
        public void Constructor_WithNoObjects_Throws()
        {
            Assert.Throws<ArgumentException>(() => new TargetBoard(new ITargetView[0], new System.Random(1)));
        }

        [Test]
        public void Constructor_HidesTargetUnderExactlyOneObject()
        {
            var views = CreateViews(5);
            var board = new TargetBoard(views, new System.Random(1));

            Assert.AreEqual(1, views.Count(board.IsTarget));
        }

        [Test]
        public void MoveTarget_NeverKeepsTheSameObject()
        {
            var views = CreateViews(4);
            var board = new TargetBoard(views, new System.Random(42));

            for (var i = 0; i < 200; i++)
            {
                var before = views.Single(board.IsTarget);
                board.MoveTarget();

                Assert.AreNotSame(before, views.Single(board.IsTarget));
            }
        }

        [Test]
        public void MoveTarget_WithSingleObject_KeepsIt()
        {
            var views = CreateViews(1);
            var board = new TargetBoard(views, new System.Random(1));

            board.MoveTarget();

            Assert.IsTrue(board.IsTarget(views[0]));
        }

        [Test]
        public void Dress_AppliesTextureToEveryObject()
        {
            var views = CreateViews(3);
            var board = new TargetBoard(views, new System.Random(1));
            var texture = new Texture2D(1, 1);

            try
            {
                board.Dress(texture);

                Assert.IsTrue(views.All(v => v.Texture == texture));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
