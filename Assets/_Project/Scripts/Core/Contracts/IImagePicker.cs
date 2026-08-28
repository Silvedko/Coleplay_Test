using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Core
{
    /// <summary>
    /// Chooses which image address comes up next. Knows nothing about loading or fallback -
    /// that is <see cref="IImageSource"/>'s job - only about selection order.
    /// </summary>
    public interface IImagePicker
    {
        UniTask<AssetKey> NextKeyAsync(CancellationToken ct);
    }
}
