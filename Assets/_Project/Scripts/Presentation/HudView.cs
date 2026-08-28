using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core;
using TMPro;
using UnityEngine;

namespace Game.Presentation
{
    /// <summary>Status line, score line and the transient miss line.</summary>
    public sealed class HudView : MonoBehaviour, IHudView
    {
        private const string LoadingText = "Loading image...";
        private const string ReadyText = "Tap the object!";
        private const string FallbackText = "Download failed - showing the fallback image";
        private const string MissText = "Missed!";
        private const string ScoreFormat = "Score: {0}";

        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private TMP_Text _scoreLabel;
        [SerializeField] private TMP_Text _missLabel;
        [SerializeField] private float _missVisibleSeconds = 1f;

        private CancellationTokenSource _missCts;

        public void SetStatus(GameStatus status)
        {
            _statusLabel.text = TextFor(status);

            // The round moved on, so whatever the player was told about the previous tap is stale.
            ClearMiss();
        }

        public void SetScore(int score)
        {
            _scoreLabel.text = string.Format(ScoreFormat, score);
        }

        public void ShowMiss()
        {
            ClearMiss();

            _missCts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
            ShowMissAsync(_missCts.Token).Forget();
        }

        private void Awake()
        {
            _missLabel.text = string.Empty;
        }

        private void OnDestroy()
        {
            _missCts?.Cancel();
            _missCts?.Dispose();
            _missCts = null;
        }

        private async UniTaskVoid ShowMissAsync(CancellationToken ct)
        {
            _missLabel.text = MissText;

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_missVisibleSeconds), cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                // A newer miss owns the label now, or the HUD is gone.
                return;
            }

            _missLabel.text = string.Empty;
        }

        /// <summary>Stops a pending hide and wipes the line right away.</summary>
        private void ClearMiss()
        {
            _missCts?.Cancel();
            _missCts?.Dispose();
            _missCts = null;

            _missLabel.text = string.Empty;
        }

        private static string TextFor(GameStatus status)
        {
            switch (status)
            {
                case GameStatus.Loading:
                    return LoadingText;
                case GameStatus.Ready:
                    return ReadyText;
                case GameStatus.Fallback:
                    return FallbackText;
                default:
                    return string.Empty;
            }
        }
    }
}
