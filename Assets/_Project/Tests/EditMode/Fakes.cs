using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

namespace Game.Tests
{
    internal sealed class FakeTargetView : ITargetView
    {
        public Texture2D Texture { get; private set; }
        public int ErrorFeedbackCount { get; private set; }

        public void SetTexture(Texture2D texture) => Texture = texture;

        public void PlayErrorFeedback() => ErrorFeedbackCount++;
    }

    internal sealed class FakeHud : IHudView
    {
        public GameStatus Status { get; private set; }
        public int Score { get; private set; }
        public int MissCount { get; private set; }

        public void SetStatus(GameStatus status) => Status = status;

        public void SetScore(int score) => Score = score;

        public void ShowMiss() => MissCount++;
    }

    internal sealed class FakeHitTester : ITargetHitTester
    {
        public ITargetView Result { get; set; }

        public bool TryGetTarget(Vector2 screenPosition, out ITargetView target)
        {
            target = Result;
            return target != null;
        }
    }

    internal sealed class FakeBoard : ITargetBoard
    {
        public ITargetView Target { get; set; }
        public int MoveCount { get; private set; }

        public bool IsTarget(ITargetView view) => ReferenceEquals(view, Target);

        public void Dress(Texture2D texture)
        {
        }

        public void MoveTarget() => MoveCount++;
    }

    internal sealed class FakeRounds : IRoundLoop
    {
        public bool IsLoading { get; set; }
        public int StartCount { get; private set; }

        public void StartNext() => StartCount++;
    }

    internal sealed class FakeHandle : IAssetHandle<Texture2D>
    {
        public Texture2D Value => null;
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    /// <summary>Each NextAsync call gets its own completion source, completed by the test.</summary>
    internal sealed class FakeImageSource : IImageSource
    {
        public readonly List<UniTaskCompletionSource<RoundImage>> Pending =
            new List<UniTaskCompletionSource<RoundImage>>();

        public UniTask<RoundImage> NextAsync(CancellationToken ct)
        {
            var source = new UniTaskCompletionSource<RoundImage>();
            Pending.Add(source);
            return source.Task;
        }
    }
}
