using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>
    /// Supplies the texture for each round and owns the fallback substitution policy.
    ///
    /// RoundLoop cancels a round's token and disposes its CancellationTokenSource back-to-back,
    /// with nothing awaited in between. An implementation must therefore observe cancellation by
    /// polling ct.IsCancellationRequested - safe even after the source is disposed - rather than by
    /// registering a callback through ct.Register, which can throw ObjectDisposedException once the
    /// source is gone.
    /// </summary>
    public interface IImageSource
    {
        /// <summary>
        /// Returns the next round texture. A failed download is not an error here: the fallback
        /// texture is returned with <see cref="RoundImage.IsFallback"/> set. Throws
        /// <see cref="System.OperationCanceledException"/> when the round is cancelled.
        /// </summary>
        UniTask<RoundImage> NextAsync(CancellationToken ct);
    }
}
