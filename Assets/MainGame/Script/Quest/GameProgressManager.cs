using System;
using UnityEngine;

namespace FpsHorrorKit
{
    public enum GameProgress
    {
        Start,
        EnteredVilla,
        ExploringVilla,
        CollectingMusicSheets,
        PianoUnlocked,
        PianoCompleted
    }

    public sealed class GameProgressManager : MonoBehaviour
    {
        public static GameProgressManager Instance { get; private set; }

        [SerializeField] private GameProgress currentProgress = GameProgress.Start;

        public event Action<GameProgress> OnProgressChanged;
        public event Action OnPianoQuestUnlocked;
        public event Action OnPianoCompleted;

        public GameProgress CurrentProgress => currentProgress;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void SetProgress(GameProgress progress)
        {
            if (currentProgress == progress)
                return;

            currentProgress = progress;
            OnProgressChanged?.Invoke(currentProgress);
        }

        public void NotifyMusicSheetCollected()
        {
            if (currentProgress < GameProgress.CollectingMusicSheets)
                SetProgress(GameProgress.CollectingMusicSheets);
        }

        public void UnlockPianoQuest()
        {
            if (currentProgress < GameProgress.PianoUnlocked)
                SetProgress(GameProgress.PianoUnlocked);
            OnPianoQuestUnlocked?.Invoke();
        }

        public void CompletePiano()
        {
            SetProgress(GameProgress.PianoCompleted);
            OnPianoCompleted?.Invoke();
        }
    }
}
