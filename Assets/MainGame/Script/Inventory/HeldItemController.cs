using UnityEngine;

namespace FpsHorrorKit
{
    public sealed class HeldItemController : MonoBehaviour
    {
        public static HeldItemController Instance { get; private set; }

        [SerializeField] private Transform itemHoldPoint;
        [SerializeField] private string[] leftHandRootNames = { "LeftHandProp", "LeftHand" };
        [SerializeField] private Vector3 fallbackLocalPosition = new Vector3(0.02f, 0.04f, 0.02f);
        [SerializeField] private Vector3 fallbackLocalEuler = new Vector3(0f, 90f, 0f);
        [SerializeField] private Vector3 heldLocalScale = new Vector3(0.18f, 0.18f, 0.18f);

        private GameObject currentVisual;

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
            EnsureHoldPoint();
        }

        public void Equip(ItemData item)
        {
            Clear();
            if (item == null || item.heldPrefab == null)
                return;

            EnsureHoldPoint();
            if (itemHoldPoint == null)
                return;

            currentVisual = Instantiate(item.heldPrefab, itemHoldPoint);
            currentVisual.transform.localPosition = Vector3.zero;
            currentVisual.transform.localRotation = Quaternion.identity;
            currentVisual.transform.localScale = heldLocalScale;

            foreach (var collider in currentVisual.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach (var pickup in currentVisual.GetComponentsInChildren<ItemPickup>(true))
                pickup.enabled = false;
            foreach (var pickup in currentVisual.GetComponentsInChildren<MusicSheetPickup>(true))
                pickup.enabled = false;
        }

        public void Clear()
        {
            if (currentVisual != null)
                Destroy(currentVisual);
            currentVisual = null;
        }

        public void HideCurrentVisual()
        {
            if (currentVisual != null)
                currentVisual.SetActive(false);
        }

        private void EnsureHoldPoint()
        {
            if (itemHoldPoint != null)
                return;

            Transform parent = FindLeftHandRoot();
            if (parent == null)
            {
                var camera = Camera.main;
                parent = camera != null ? camera.transform : transform;
            }

            var existing = parent.Find("ItemHoldPoint");
            if (existing != null)
            {
                itemHoldPoint = existing;
                return;
            }

            var holdPoint = new GameObject("ItemHoldPoint").transform;
            holdPoint.SetParent(parent, false);
            holdPoint.localPosition = fallbackLocalPosition;
            holdPoint.localEulerAngles = fallbackLocalEuler;
            itemHoldPoint = holdPoint;
        }

        private Transform FindLeftHandRoot()
        {
            if (leftHandRootNames != null)
            {
                foreach (var rootName in leftHandRootNames)
                {
                    var found = FindSceneTransform(rootName);
                    if (found != null)
                        return found;
                }
            }

            return null;
        }

        private static Transform FindSceneTransform(string objectName)
        {
            if (string.IsNullOrWhiteSpace(objectName))
                return null;

            foreach (var transform in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (transform.name == objectName && transform.gameObject.scene.IsValid())
                    return transform;
            }

            return null;
        }
    }
}
