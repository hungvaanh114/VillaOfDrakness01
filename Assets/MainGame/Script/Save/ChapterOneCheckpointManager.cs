using System.Collections.Generic;
using UnityEngine;

namespace FpsHorrorKit
{
    public enum ChapterOneCheckpoint
    {
        None = 0,
        Kitchen = 1,
        Piano = 2
    }

    public sealed class ChapterOneCheckpointManager : MonoBehaviour
    {
        private const string SavePrefix = "ChapterOne.";
        private const string IntroCompletedKey = SavePrefix + "IntroCompleted";
        private const string CheckpointKey = SavePrefix + "Checkpoint";

        private static readonly string[] PianoCheckpointKeyIds =
        {
            "Item_Key_Warehouse",
            "WarehouseKey",
            "Item_Key_Salon",
            "SalonKey",
            "Item_Key_Guest",
            "GuestKey",
            "KeyPhongAN"
        };

        [Header("Respawn Points")]
        [SerializeField] private Transform kitchenRespawnPoint;
        [SerializeField] private Transform pianoRespawnPoint;

        [Header("Piano Checkpoint Inventory")]
        [SerializeField] private List<ItemData> pianoCheckpointKeys = new();

        [Header("Fallback Respawn")]
        [SerializeField] private Vector3 kitchenFallbackPosition = new(-29.94f, 1.07f, -6.24f);
        [SerializeField] private Vector3 kitchenFallbackEuler = new(0f, 90f, 0f);
        [SerializeField] private Vector3 pianoFallbackPosition = new(3.01f, 1.15f, 9.2f);
        [SerializeField] private Vector3 pianoFallbackEuler = new(0f, 0f, 0f);

        public static ChapterOneCheckpointManager Instance { get; private set; }
        public static bool HasCompletedIntroCutscenes => PlayerPrefs.GetInt(IntroCompletedKey, 0) == 1;
        public static ChapterOneCheckpoint SavedCheckpoint => (ChapterOneCheckpoint)PlayerPrefs.GetInt(CheckpointKey, 0);
        public static bool HasContinueSave => HasCompletedIntroCutscenes || SavedCheckpoint != ChapterOneCheckpoint.None;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        public static void ClearSavedCheckpoint()
        {
            PlayerPrefs.DeleteKey(IntroCompletedKey);
            PlayerPrefs.DeleteKey(CheckpointKey);
            PlayerPrefs.Save();
        }

        public void MarkWindowCutsceneCompleted()
        {
            PlayerPrefs.SetInt(IntroCompletedKey, 1);
            if (SavedCheckpoint < ChapterOneCheckpoint.Kitchen)
                PlayerPrefs.SetInt(CheckpointKey, (int)ChapterOneCheckpoint.Kitchen);
            PlayerPrefs.Save();
        }

        public void MarkPianoCheckpoint()
        {
            PlayerPrefs.SetInt(IntroCompletedKey, 1);
            PlayerPrefs.SetInt(CheckpointKey, (int)ChapterOneCheckpoint.Piano);
            PlayerPrefs.Save();
        }

        public bool ApplySavedState(global::GameController controller)
        {
            if (!HasCompletedIntroCutscenes)
                return false;

            var checkpoint = SavedCheckpoint;
            if (checkpoint == ChapterOneCheckpoint.None)
                checkpoint = ChapterOneCheckpoint.Kitchen;

            if (controller != null)
            {
                controller.playIntroOnStart = false;
                controller.currentGameState = global::GameController.GameState.Gameplay;
                controller.currentChapterPhase = checkpoint >= ChapterOneCheckpoint.Piano
                    ? global::GameController.ChapterPhase.PianoPuzzle
                    : global::GameController.ChapterPhase.EnterHouse;
            }

            ResetInventoryToFlashlight();
            MusicSheetManager.Instance?.ResetCollectedSilently();
            GameProgressManager.Instance?.SetProgress(checkpoint >= ChapterOneCheckpoint.Piano
                ? GameProgress.PianoUnlocked
                : GameProgress.EnteredVilla);

            if (checkpoint >= ChapterOneCheckpoint.Piano)
                RestorePianoCheckpointInventory();

            MovePlayerToCheckpoint(controller, checkpoint);
            FpsAssetsInputs.Instance?.ClearGameplayInput();
            return true;
        }

        private static void ResetInventoryToFlashlight()
        {
            if (InventoryManager.Instance != null)
                InventoryManager.Instance.ResetToStartingItems();
            else
                ItemUsageSystem.Instance?.GrantFlashlightItem(false);
        }

        private void RestorePianoCheckpointInventory()
        {
            var grantedKeyIds = new HashSet<string>();
            foreach (var item in pianoCheckpointKeys)
            {
                if (item == null || item.itemType != ItemType.Key || !IsPianoCheckpointKey(item))
                    continue;

                string uniqueKeyId = !string.IsNullOrWhiteSpace(item.id) ? item.id : item.keyID;
                if (grantedKeyIds.Add(uniqueKeyId))
                    InventoryManager.Instance?.AddItem(item, 1);
            }

            foreach (var pickup in FindObjectsByType<ItemPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var item = pickup != null ? pickup.ItemData : null;
                if (item == null || item.itemType != ItemType.Key || !IsPianoCheckpointKey(item))
                    continue;

                string uniqueKeyId = !string.IsNullOrWhiteSpace(item.id) ? item.id : item.keyID;
                if (grantedKeyIds.Add(uniqueKeyId))
                    InventoryManager.Instance?.AddItem(item, 1);
                pickup.gameObject.SetActive(false);
            }

            MusicSheetManager.Instance?.RestoreAllCollectedSilently();
            foreach (var pickup in FindObjectsByType<MusicSheetPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (pickup != null && pickup.MusicSheetData != null)
                    pickup.gameObject.SetActive(false);
            }

            GameProgressManager.Instance?.SetProgress(GameProgress.PianoUnlocked);
        }

        private static bool IsPianoCheckpointKey(ItemData item)
        {
            if (item == null)
                return false;

            foreach (var id in PianoCheckpointKeyIds)
            {
                if (item.id == id || item.keyID == id)
                    return true;
            }

            return false;
        }

        private void MovePlayerToCheckpoint(global::GameController controller, ChapterOneCheckpoint checkpoint)
        {
            var player = controller != null && controller.playerController != null
                ? controller.playerController
                : FindFirstObjectByType<FpsController>(FindObjectsInactive.Include);
            if (player == null)
                return;

            var point = checkpoint >= ChapterOneCheckpoint.Piano ? pianoRespawnPoint : kitchenRespawnPoint;
            if (point != null)
            {
                player.TeleportCutScene(point);
                player.StopCutSceneMovement();
                return;
            }

            var position = checkpoint >= ChapterOneCheckpoint.Piano ? pianoFallbackPosition : kitchenFallbackPosition;
            var rotation = Quaternion.Euler(checkpoint >= ChapterOneCheckpoint.Piano ? pianoFallbackEuler : kitchenFallbackEuler);
            var characterController = player.GetComponent<CharacterController>();
            bool wasEnabled = characterController != null && characterController.enabled;
            if (characterController != null)
                characterController.enabled = false;

            player.transform.SetPositionAndRotation(position, rotation);
            player.StopCutSceneMovement();

            if (characterController != null)
                characterController.enabled = wasEnabled;
        }
    }
}
