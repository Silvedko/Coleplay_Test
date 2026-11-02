using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// The tappable object. Textures and tint go through a MaterialPropertyBlock: touching
    /// renderer.material would clone the material and leak one copy per play session.
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class TargetView : MonoBehaviour, ITargetView
    {
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private MeshRenderer _renderer;
        [SerializeField] private Color _errorColor = new Color(0.85f, 0.19f, 0.15f);
        [SerializeField] private float _errorFlashSeconds = 0.35f;

        private MaterialPropertyBlock _block;
        private Color _baseColor = Color.white;
        private CancellationTokenSource _flashCts;

        public void SetTexture(Texture2D texture)
        {
            _renderer.GetPropertyBlock(_block);
            _block.SetTexture(MainTexId, texture);
            _renderer.SetPropertyBlock(_block);
        }

        public void PlayErrorFeedback()
        {
            RestartFlashToken();
            FlashAsync(_flashCts.Token).Forget();
        }

        private void Awake()
        {
            if (_renderer == null)
            {
                throw new InvalidOperationException(name + ": TargetView._renderer is not assigned.");
            }

            _block = new MaterialPropertyBlock();

            var material = _renderer.sharedMaterial;
            if (material != null && material.HasProperty(ColorId))
            {
                _baseColor = material.GetColor(ColorId);
            }
        }

        /// <summary>Editor-only: fills the reference when the component is added to a prefab.</summary>
        private void Reset()
        {
            _renderer = GetComponent<MeshRenderer>();
        }

        private void OnDestroy()
        {
            _flashCts?.Cancel();
            _flashCts?.Dispose();
            _flashCts = null;
        }

        private async UniTaskVoid FlashAsync(CancellationToken ct)
        {
            var elapsed = 0f;

            try
            {
                while (elapsed < _errorFlashSeconds)
                {
                    SetTint(Color.Lerp(_errorColor, _baseColor, elapsed / _errorFlashSeconds));
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    elapsed += Time.deltaTime;
                }
            }
            catch (OperationCanceledException)
            {
                // Either the object is gone or a newer flash took over - in both cases
                // resetting the tint here would be wrong.
                return;
            }

            SetTint(_baseColor);
        }

        private void RestartFlashToken()
        {
            _flashCts?.Cancel();
            _flashCts?.Dispose();
            _flashCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
        }

        private void SetTint(Color color)
        {
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(ColorId, color);
            _renderer.SetPropertyBlock(_block);
        }
    }
}
