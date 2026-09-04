using System.Collections;
using UnityEngine;

namespace MainGame.P2
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class P2KnockPlank : MonoBehaviour
    {
        [SerializeField] private int plankIndex;
        [SerializeField] private string noteLabel;
        [SerializeField] private bool hollow;
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private Color normalTint = Color.white;
        [SerializeField] private Color focusedTint = new(1f, 0.78f, 0.28f, 1f);

        private MaterialPropertyBlock propertyBlock;
        private bool poseStored;
        private Vector3 originalLocalPosition;
        private Quaternion originalLocalRotation;
        private Collider plankCollider;

        public int PlankIndex => plankIndex;
        public string NoteLabel => noteLabel;
        public bool IsHollow => hollow;

        public void Configure(int index, string label, bool isHollow)
        {
            plankIndex = index;
            noteLabel = label;
            hollow = isHollow;
            ResolveReferences();
            StorePose();
            SetFocused(false);
        }

        private void Awake()
        {
            ResolveReferences();
            StorePose();
            SetFocused(false);
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        public void SetFocused(bool focused)
        {
            ResolveReferences();
            if (targetRenderer == null)
                return;

            propertyBlock ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            var tint = focused ? focusedTint : normalTint;
            propertyBlock.SetColor("_BaseColor", tint);
            propertyBlock.SetColor("_Color", tint);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        public void SetColliderEnabled(bool enabled)
        {
            ResolveReferences();
            if (plankCollider != null)
                plankCollider.enabled = enabled;
        }

        public IEnumerator PopOff(Vector3 localOffset, Vector3 localEulerOffset, float seconds)
        {
            StorePose();
            SetColliderEnabled(false);

            var startPosition = transform.localPosition;
            var startRotation = transform.localRotation;
            var targetPosition = originalLocalPosition + localOffset;
            var targetRotation = originalLocalRotation * Quaternion.Euler(localEulerOffset);

            float timer = 0f;
            seconds = Mathf.Max(0.01f, seconds);
            while (timer < seconds)
            {
                timer += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(timer / seconds));
                transform.localPosition = Vector3.Lerp(startPosition, targetPosition, t);
                transform.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
                yield return null;
            }

            transform.localPosition = targetPosition;
            transform.localRotation = targetRotation;
        }

        public void ResetPose()
        {
            StorePose();
            transform.localPosition = originalLocalPosition;
            transform.localRotation = originalLocalRotation;
            SetColliderEnabled(true);
            SetFocused(false);
        }

        private void ResolveReferences()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponentInChildren<Renderer>();
            if (plankCollider == null)
                plankCollider = GetComponent<Collider>();
        }

        private void StorePose()
        {
            if (poseStored)
                return;

            originalLocalPosition = transform.localPosition;
            originalLocalRotation = transform.localRotation;
            poseStored = true;
        }
    }
}
