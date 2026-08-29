namespace FpsHorrorKit
{
    using System.Collections;
    using TMPro;
    using UnityEngine;
    using UnityEngine.UI;

    public class InteractMessageScript : MonoBehaviour
    {
        private static InteractMessageScript instance;

        public static InteractMessageScript Instance
        {
            get
            {
                if (instance == null)
                    instance = FindFirstObjectByType<InteractMessageScript>(FindObjectsInactive.Include);
                if (instance == null)
                    instance = CreateRuntimeInstance();
                return instance;
            }
            private set => instance = value;
        }

        [SerializeField] private CanvasGroup interactMessageCanvasGroup;
        [SerializeField] private TextMeshProUGUI interatMessageText;
        [SerializeField] private float displayTime = 5f;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            ResolveReferences();
            HideMessage();
        }

        public void PauseMessageForSeconds(float seconds, System.Func<bool> shouldResume = null)
        {
            ResolveReferences();
            if (interatMessageText == null || interactMessageCanvasGroup == null || !interactMessageCanvasGroup.gameObject.activeSelf)
                return;

            string message = interatMessageText.text;
            if (string.IsNullOrWhiteSpace(message))
                return;

            StopAllCoroutines();
            StartCoroutine(PauseMessageRoutine(message, Mathf.Max(0f, seconds), shouldResume));
        }

        public void ShowMessage(string message, float displayTime = -1f)
        {
            ResolveReferences();
            if (interatMessageText == null || interactMessageCanvasGroup == null)
                return;

            interatMessageText.text = message;
            StopAllCoroutines();
            float effectiveDisplayTime = displayTime > 0f ? displayTime : this.displayTime;
            StartCoroutine(ShowInfoMessage(Mathf.Max(0.1f, effectiveDisplayTime)));
        }

        public void ClearMessage()
        {
            ResolveReferences();
            StopAllCoroutines();
            HideMessage();
        }

        private void ResolveReferences()
        {
            if (interactMessageCanvasGroup == null)
                interactMessageCanvasGroup = GetComponentInChildren<CanvasGroup>(true);
            if (interatMessageText == null)
                interatMessageText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        private void HideMessage()
        {
            if (interatMessageText != null)
                interatMessageText.text = "";

            if (interactMessageCanvasGroup != null)
            {
                interactMessageCanvasGroup.alpha = 0f;
                interactMessageCanvasGroup.gameObject.SetActive(false);
            }
        }

        private IEnumerator PauseMessageRoutine(string message, float seconds, System.Func<bool> shouldResume)
        {
            HideMessage();

            if (seconds > 0f)
                yield return new WaitForSeconds(seconds);

            if (shouldResume != null && !shouldResume())
                yield break;

            ShowMessage(message);
        }

        private IEnumerator ShowInfoMessage(float displayDuration)
        {
            float duration = 0f;
            interactMessageCanvasGroup.alpha = 0f;
            interactMessageCanvasGroup.gameObject.SetActive(true);

            while (duration <= 1f)
            {
                duration += Time.deltaTime / (displayDuration / 2f);
                interactMessageCanvasGroup.alpha = duration;
                yield return null;
            }

            while (duration >= 0f)
            {
                duration -= Time.deltaTime / (displayDuration / 2f);
                interactMessageCanvasGroup.alpha = duration;
                yield return null;
            }

            HideMessage();
        }

        private static InteractMessageScript CreateRuntimeInstance()
        {
            var canvasObject = new GameObject("RuntimeInteractMessageSystem", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 130;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var panelObject = new GameObject("InteractMessagePanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            panelObject.transform.SetParent(canvasObject.transform, false);
            var panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 58f);
            panelRect.sizeDelta = new Vector2(980f, 118f);

            var panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.01f, 0.035f, 0.055f, 0.78f);
            panelImage.raycastTarget = false;

            var textObject = new GameObject("InteractMessageText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(panelObject.transform, false);
            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(34f, 16f);
            textRect.offsetMax = new Vector2(-34f, -16f);

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = "";
            text.fontSize = 30f;
            text.color = new Color(0.9f, 0.96f, 1f, 1f);
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;

            var script = canvasObject.AddComponent<InteractMessageScript>();
            script.interactMessageCanvasGroup = panelObject.GetComponent<CanvasGroup>();
            script.interatMessageText = text;
            script.HideMessage();
            return script;
        }
    }
}
