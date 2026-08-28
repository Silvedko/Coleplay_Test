using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>Supplies the texture for each round and owns the fallback substitution policy.</summary>
    public interface IImageSource
    {
        /// <summary>Loads the fallback texture once, at start-up. Repeated calls are no-ops.</summary>
        UniTask PreloadFallbackAsync(CancellationToken ct);

        /// <summary>
        /// Returns the next round texture. A failed download is not an error here: the fallback
        /// texture is returned with <see cref="RoundImage.IsFallback"/> set. Throws
        /// <see cref="System.OperationCanceledException"/> when the round is cancelled.
        /// </summary>
        UniTask<RoundImage> NextAsync(CancellationToken ct);
    }
}
