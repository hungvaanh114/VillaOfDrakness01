using System;
using System.Collections.Generic;
using UnityEngine;

namespace FpsHorrorKit
{
    public sealed class JournalManager : MonoBehaviour
    {
        public static JournalManager Instance { get; private set; }

        [SerializeField] private List<JournalEntryData> startingEntries = new();
        private readonly List<JournalEntryData> entries = new();

        public event Action OnJournalChanged;
        public IReadOnlyList<JournalEntryData> Entries => entries;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            foreach (var entry in startingEntries)
                AddEntry(entry);
        }

        public void AddEntry(JournalEntryData entry)
        {
            if (entry == null || entries.Contains(entry))
                return;

            entries.Add(entry);
            OnJournalChanged?.Invoke();
        }
    }
}
