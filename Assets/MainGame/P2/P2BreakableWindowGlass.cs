using System.Collections.Generic;
using UnityEngine;

namespace MainGame.P2
{
    public sealed class P2BreakableWindowGlass : MonoBehaviour
    {
        private static readonly List<P2BreakableWindowGlass> RegisteredGlass = new List<P2BreakableWindowGlass>();
        private static Material runtimeShardMaterial;

        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip glassBreakClip;
        [SerializeField] private Material shardMaterial;
        [SerializeField] private bool breakOnCollision;
        [SerializeField] private bool breakOnTriggerEnter;
        [SerializeField] private bool disableCollidersAfterBreak = true;
        [SerializeField, Min(1)] private int shardCount = 18;
        [SerializeField, Min(0f)] private float shardLifetime = 6f;
        [SerializeField, Min(0f)] private float shardImpulse = 2.4f;
        [SerializeField, Min(0f)] private float upwardImpulse = 0.65f;
        [SerializeField, Min(0f)] private float shardTorque = 7f;
        [SerializeField] private Vector2 shardSizeRange = new Vector2(0.055f, 0.18f);

        private bool broken;

        public bool IsBroken => broken;

        private void Awake()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();
        }

        private void OnEnable()
        {
            if (!RegisteredGlass.Contains(this))
                RegisteredGlass.Add(this);
        }

        private void OnDisable()
        {
            RegisteredGlass.Remove(this);
        }

        public void Configure(Renderer rendererToBreak, AudioClip breakClip, Material shardsMaterial)
        {
            targetRenderer = rendererToBreak;
            glassBreakClip = breakClip;
            shardMaterial = shardsMaterial;
        }

        public void BreakGlass()
        {
            BreakGlassInternal(true);
        }

        private void BreakGlassInternal(bool playSound)
        {
            if (broken)
                return;

            broken = true;
            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();

            Bounds bounds = targetRenderer != null
                ? targetRenderer.bounds
                : new Bounds(transform.position, Vector3.one);

            if (playSound)
                PlayBreakSound(bounds.center);
            SpawnShards(bounds);

            if (targetRenderer != null)
                targetRenderer.enabled = false;

            if (disableCollidersAfterBreak)
                SetCollidersEnabled(false);
        }

        public void ResetGlass()
        {
            broken = false;
            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();
            if (targetRenderer != null)
                targetRenderer.enabled = true;
            if (disableCollidersAfterBreak)
                SetCollidersEnabled(true);
        }

        public static int BreakAllHouseGlass(bool playBreakSound = true)
        {
            int brokenCount = 0;
            bool playedSound = false;
            for (int i = RegisteredGlass.Count - 1; i >= 0; i--)
            {
                var glass = RegisteredGlass[i];
                if (glass == null || glass.IsBroken)
                    continue;

                glass.BreakGlassInternal(playBreakSound && !playedSound);
                playedSound = true;
                brokenCount++;
            }

            return brokenCount;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!breakOnCollision)
                return;

            BreakGlass();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!breakOnTriggerEnter)
                return;

            BreakGlass();
        }

        private void PlayBreakSound(Vector3 position)
        {
            if (glassBreakClip == null)
                return;

            if (audioSource != null)
            {
                audioSource.transform.position = position;
                audioSource.PlayOneShot(glassBreakClip);
                return;
            }

            AudioSource.PlayClipAtPoint(glassBreakClip, position, 1f);
        }

        private void SpawnShards(Bounds bounds)
        {
            if (shardCount <= 0)
                return;

            Material material = shardMaterial != null ? shardMaterial : GetRuntimeShardMaterial();
            Transform parent = transform.parent != null ? transform.parent : transform;

            for (int i = 0; i < shardCount; i++)
            {
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = $"P2_WindowGlassShard_{i:00}";
                shard.transform.SetParent(parent, true);
                shard.transform.position = GetRandomPoint(bounds);
                shard.transform.rotation = Random.rotation;

                float size = Random.Range(shardSizeRange.x, shardSizeRange.y);
                shard.transform.localScale = new Vector3(size, size * Random.Range(0.45f, 1.4f), 0.012f);

                var renderer = shard.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.sharedMaterial = material;

                var rigidbody = shard.AddComponent<Rigidbody>();
                rigidbody.mass = 0.035f;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                Vector3 outward = (shard.transform.position - bounds.center).normalized;
                if (outward.sqrMagnitude <= 0.001f)
                    outward = transform.forward;
                outward = (outward + Vector3.up * upwardImpulse + Random.insideUnitSphere * 0.45f).normalized;
                rigidbody.AddForce(outward * shardImpulse, ForceMode.Impulse);
                rigidbody.AddTorque(Random.insideUnitSphere * shardTorque, ForceMode.Impulse);

                if (shardLifetime > 0f)
                    Destroy(shard, shardLifetime);
            }
        }

        private Vector3 GetRandomPoint(Bounds bounds)
        {
            return new Vector3(
                Random.Range(bounds.min.x, bounds.max.x),
                Random.Range(bounds.min.y, bounds.max.y),
                Random.Range(bounds.min.z, bounds.max.z));
        }

        private void SetCollidersEnabled(bool enabled)
        {
            var colliders = GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
                colliders[i].enabled = enabled;
        }

        private static Material GetRuntimeShardMaterial()
        {
            if (runtimeShardMaterial != null)
                return runtimeShardMaterial;

            var shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
            runtimeShardMaterial = new Material(shader)
            {
                name = "RuntimeP2WindowGlassShard",
                color = new Color(0.65f, 0.88f, 1f, 0.45f)
            };
            runtimeShardMaterial.SetColor("_BaseColor", new Color(0.65f, 0.88f, 1f, 0.45f));
            runtimeShardMaterial.SetColor("_Color", new Color(0.65f, 0.88f, 1f, 0.45f));
            return runtimeShardMaterial;
        }
    }
}
