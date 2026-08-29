using System;
using System.Collections.Generic;
using UnityEngine;

namespace FpsHorrorKit
{
    public sealed class MusicSheetManager : MonoBehaviour
    {
        public static MusicSheetManager Instance { get; private set; }

        [SerializeField] private List<MusicSheetData> sheets = new();
        [SerializeField] private int requiredMusicSheetCount = 5;

        private readonly HashSet<string> collected = new();

        public event Action<MusicSheetData> OnMusicSheetCollected;
        public event Action OnMusicSheetCompleted;

        public IReadOnlyList<MusicSheetData> Sheets => sheets;
        public int RequiredMusicSheetCount => requiredMusicSheetCount;
        public int CollectedMusicSheetCount => collected.Count;
        public bool MusicSheetCompleted { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public bool Collect(MusicSheetData sheet)
        {
            if (sheet == null || string.IsNullOrWhiteSpace(sheet.musicSheetID) || collected.Contains(sheet.musicSheetID))
                return false;

            collected.Add(sheet.musicSheetID);
            OnMusicSheetCollected?.Invoke(sheet);
            AudioManager.Instance?.RequestGameplayAmbienceMoment();
            GameProgressManager.Instance?.NotifyMusicSheetCollected();

            if (!MusicSheetCompleted && CollectedMusicSheetCount >= requiredMusicSheetCount)
            {
                MusicSheetCompleted = true;
                OnMusicSheetCompleted?.Invoke();
                GameProgressManager.Instance?.UnlockPianoQuest();
                InteractMessageScript.Instance?.ShowMessage("Nhiệm vụ mới: Đánh đàn piano.");
            }

            return true;
        }

        public bool IsCollected(MusicSheetData sheet)
        {
            return sheet != null && collected.Contains(sheet.musicSheetID);
        }

        public bool IsCollected(string sheetID)
        {
            return collected.Contains(sheetID);
        }
    }
}
