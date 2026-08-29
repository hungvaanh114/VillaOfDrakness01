using System;
using System.Collections.Generic;
using UnityEngine;

namespace FpsHorrorKit
{
    public sealed class PianoPuzzle : MonoBehaviour
    {
        public static PianoPuzzle Instance { get; private set; }

        [SerializeField] private string[] requiredMelody = { "E", "C", "F", "D", "G" };

        private readonly List<string> inputMelody = new();
        private bool completed;

        public event Action<string> OnNotePlayed;
        public event Action OnPianoCompleted;
        public event Action OnPianoFailed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public void PlayNote(string note)
        {
            if (completed || string.IsNullOrWhiteSpace(note))
                return;

            inputMelody.Add(note);
            OnNotePlayed?.Invoke(note);

            int index = inputMelody.Count - 1;
            if (index >= requiredMelody.Length || inputMelody[index] != requiredMelody[index])
            {
                inputMelody.Clear();
                AudioManager.Instance?.PlayPianoWrong();
                OnPianoFailed?.Invoke();
                InteractMessageScript.Instance?.ShowMessage("Hình như mình đánh sai bản nhạc rồi thì phải, để thử lại.");
                return;
            }

            if (inputMelody.Count == requiredMelody.Length)
                Complete();
        }

        public void ResetPuzzle()
        {
            inputMelody.Clear();
        }

        private void Complete()
        {
            if (completed)
                return;

            completed = true;
            inputMelody.Clear();
            GameProgressManager.Instance?.CompletePiano();
            OnPianoCompleted?.Invoke();
        }
    }
}
