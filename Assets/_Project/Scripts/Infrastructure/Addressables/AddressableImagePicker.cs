using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Game.Infrastructure
{
    /// <summary>
    /// Resolves an Addressables label into concrete addresses once, then hands out a fresh one each
    /// round. Selection is uniform among every address except the one shown last, so the same image
    /// never appears twice in a row. Knows nothing about loading a texture or falling back on failure
    /// - that is <see cref="ImageRepository"/>'s job.
    /// </summary>
    public sealed class AddressableImagePicker : IImagePicker
    {
        private readonly string _label;
        private readonly System.Random _random = new System.Random();

        private string[] _addresses;
        private int _lastIndex = -1;

        public AddressableImagePicker(string label)
        {
            _label = string.IsNullOrEmpty(label)
                ? throw new System.ArgumentException("Label must not be empty.", nameof(label))
                : label;
        }

        public async UniTask<AssetKey> NextKeyAsync(CancellationToken ct)
        {
            await EnsureCatalogAsync(ct);
            return NextKey();
        }

        /// <summary>Resolves the label into concrete addresses once, on the first round.</summary>
        private async UniTask EnsureCatalogAsync(CancellationToken ct)
        {
            if (_addresses != null)
            {
                return;
            }

            var handle = Addressables.LoadResourceLocationsAsync(_label, typeof(Texture2D));

            try
            {
                var locations = await handle.ToUniTask(cancellationToken: ct, autoReleaseWhenCanceled: true);

                if (locations == null || locations.Count == 0)
                {
                    throw new AssetLoadException(new AssetKey(_label), null);
                }

                _addresses = new string[locations.Count];
                for (var i = 0; i < locations.Count; i++)
                {
                    _addresses[i] = locations[i].PrimaryKey;
                }
            }
            finally
            {
                // In the cancelled case UniTask has already released it, hence the validity check.
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

        /// <summary>Uniform pick among every texture except the one shown last.</summary>
        private AssetKey NextKey()
        {
            int index;
            if (_addresses.Length > 1 && _lastIndex >= 0)
            {
                index = _random.Next(_addresses.Length - 1);
                if (index >= _lastIndex)
                {
                    index++;
                }
            }
            else
            {
                index = _random.Next(_addresses.Length);
            }

            _lastIndex = index;
            return new AssetKey(_addresses[index]);
        }
    }
}
