using UnityEngine;

/// <summary>
/// Renders the scene from the reflected position of the gameplay camera and
/// writes that image onto the mirror surface.
/// </summary>
public sealed class MirrorReflectionCamera : MonoBehaviour
{
    [SerializeField] private Transform mirrorSurface;
    [SerializeField] private Renderer mirrorRenderer;
    [SerializeField] private Camera sourceCamera;
    [SerializeField] private Camera reflectionCamera;
    [SerializeField] private int textureSize = 1024;
    [SerializeField] private int mirrorLayer = 31;
    [SerializeField] private bool renderEveryFrame = true;
    [SerializeField] private bool keepReflectionCameraFixed = true;
    [SerializeField, Min(0f)] private float fixedCameraOffsetFromMirror = 0.08f;
    [SerializeField, Min(1f)] private float fixedCheckFieldOfView = 80f;

    private RenderTexture reflectionTexture;
    private Texture2D bloodTexture;
    private Material mirrorMaterial;
    private Material bloodMaterial;
    private Transform surfaceVisual;
    private Transform bloodOverlay;
    private Material frameMaterial;
    private bool reflectionCameraCreatedAtRuntime;
    private bool fixedCameraPoseCaptured;
    private Vector3 fixedCameraLocalPosition;
    private Quaternion fixedCameraLocalRotation;

    public Camera SourceCamera
    {
        get
        {
            ResolveSourceCamera();
            return sourceCamera;
        }
    }

    public Camera ReflectionCamera
    {
        get
        {
            EnsureCamera();
            return reflectionCamera;
        }
    }

    public Camera RefreshReflection(Camera sourceOverride)
    {
        if (sourceOverride != null && sourceOverride != reflectionCamera)
            sourceCamera = sourceOverride;

        ResolveSourceCamera();
        EnsureCamera();
        if (reflectionCamera == null)
            return null;

        if (keepReflectionCameraFixed)
            ApplyFixedCameraPose();
        else if (sourceCamera != null)
            UpdateReflectionCamera(sourceCamera);

        return reflectionCamera;
    }

    private void Awake()
    {
        EnsureSurface();
        EnsureBloodOverlay();
        EnsureFrame();
        ResolveSourceCamera();
        EnsureCamera();
    }

    private void LateUpdate()
    {
        ResolveSourceCamera();
        if (!renderEveryFrame || reflectionCamera == null)
            return;

        if (keepReflectionCameraFixed)
            ApplyFixedCameraPose();
        else if (sourceCamera != null)
            UpdateReflectionCamera(sourceCamera);
        else
            return;

        reflectionCamera.targetTexture = reflectionTexture;
        ApplyReflectionTextureToMaterial();
    }

    public void SetReflectionVisible(bool visible)
    {
        if (surfaceVisual != null)
            surfaceVisual.gameObject.SetActive(visible);

        if (bloodOverlay != null)
            bloodOverlay.gameObject.SetActive(false);
    }

    public void SetBloodStained()
    {
        if (surfaceVisual != null)
            surfaceVisual.gameObject.SetActive(false);

        if (bloodOverlay != null)
            bloodOverlay.gameObject.SetActive(true);
    }

    private void EnsureSurface()
    {
        if (mirrorSurface == null)
            mirrorSurface = transform.Find("MirrorSurface");

        if (mirrorSurface == null)
        {
            var surfaceObject = new GameObject("MirrorSurface");
            surfaceObject.transform.SetParent(transform, false);
            surfaceObject.transform.localPosition = new Vector3(0f, 2.1f, 0f);
            surfaceObject.transform.localRotation = Quaternion.identity;
            surfaceObject.transform.localScale = new Vector3(2f, 3.3f, 1f);
            mirrorSurface = surfaceObject.transform;
        }

        surfaceVisual = mirrorSurface.Find("SurfaceVisual");
        if (surfaceVisual == null)
        {
            var visualObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            visualObject.name = "SurfaceVisual";
            visualObject.transform.SetParent(mirrorSurface, false);
            visualObject.transform.localPosition = Vector3.zero;
            visualObject.transform.localRotation = Quaternion.identity;
            visualObject.transform.localScale = Vector3.one;
            var collider = visualObject.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
            surfaceVisual = visualObject.transform;
        }

        surfaceVisual.gameObject.layer = mirrorLayer;
        mirrorRenderer = surfaceVisual.GetComponent<Renderer>();
        if (mirrorRenderer == null)
            return;

        var shader = Shader.Find("HDRP/Unlit") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
        if (shader == null)
            return;

        mirrorMaterial = new Material(shader)
        {
            name = "RuntimeMirrorMaterial"
        };
        mirrorMaterial.SetColor("_BaseColor", new Color(0.25f, 0.35f, 0.42f, 1f));
        mirrorMaterial.SetColor("_Color", new Color(0.25f, 0.35f, 0.42f, 1f));
        mirrorRenderer.sharedMaterial = mirrorMaterial;
    }

    private void EnsureBloodOverlay()
    {
        if (mirrorSurface == null || bloodOverlay != null)
            return;

        var bloodObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bloodObject.name = "BloodStainOverlay";
        bloodObject.transform.SetParent(mirrorSurface, false);
        bloodObject.transform.localPosition = new Vector3(0f, 0f, -0.006f);
        bloodObject.transform.localRotation = Quaternion.identity;
        bloodObject.transform.localScale = Vector3.one;
        bloodObject.layer = mirrorLayer;

        var collider = bloodObject.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);

        var renderer = bloodObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            bloodMaterial = CreateBloodMaterial();
            renderer.sharedMaterial = bloodMaterial;
        }

        bloodOverlay = bloodObject.transform;
        bloodOverlay.gameObject.SetActive(false);
    }

    private Material CreateBloodMaterial()
    {
        var shader = Shader.Find("HDRP/Unlit") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Standard");
        if (shader == null)
            return null;

        bloodTexture = CreateBloodTexture(256, 384);
        var material = new Material(shader)
        {
            name = "RuntimeMirrorBloodMaterial"
        };
        material.mainTexture = bloodTexture;
        material.SetColor("_BaseColor", Color.white);
        material.SetColor("_Color", Color.white);

        SetTexture("_BaseColorMap");
        SetTexture("_UnlitColorMap");
        SetTexture("_MainTex");

        return material;

        void SetTexture(string propertyName)
        {
            if (!material.HasProperty(propertyName))
                return;

            material.SetTexture(propertyName, bloodTexture);
            material.SetTextureScale(propertyName, new Vector2(1f, -1f));
            material.SetTextureOffset(propertyName, new Vector2(0f, 1f));
        }
    }

    private static Texture2D CreateBloodTexture(int width, int height)
    {
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true)
        {
            name = "RuntimeMirrorBloodTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        var pixels = new Color[width * height];
        var blobs = new[]
        {
            new Vector2(0.28f, 0.72f),
            new Vector2(0.60f, 0.57f),
            new Vector2(0.42f, 0.34f),
            new Vector2(0.76f, 0.24f),
            new Vector2(0.16f, 0.18f)
        };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width;
                float v = (y + 0.5f) / height;
                float stain = 0f;

                for (int i = 0; i < blobs.Length; i++)
                {
                    Vector2 delta = new Vector2(u - blobs[i].x, (v - blobs[i].y) * 0.72f);
                    float distance = delta.magnitude;
                    stain = Mathf.Max(stain, Mathf.Exp(-distance * distance * 42f));
                }

                float verticalRun = Mathf.Exp(-Mathf.Pow((u - 0.58f) * 8f, 2f))
                    * Mathf.SmoothStep(0f, 1f, 1f - v) * 0.72f;
                float diagonalRun = Mathf.Exp(-Mathf.Pow((v - (0.92f - u * 0.72f)) * 10f, 2f)) * 0.5f;
                stain = Mathf.Clamp01(stain + verticalRun + diagonalRun);

                Color darkBlood = new Color(0.035f, 0.002f, 0.002f, 1f);
                Color wetBlood = new Color(0.48f, 0.012f, 0.008f, 1f);
                pixels[y * width + x] = Color.Lerp(darkBlood, wetBlood, stain);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void EnsureCamera()
    {
        if (reflectionCamera == null)
        {
            var cameraTransform = transform.Find("ReflectionCamera");
            if (cameraTransform != null)
                reflectionCamera = cameraTransform.GetComponent<Camera>();
        }

        if (reflectionCamera == null)
        {
            var cameraObject = new GameObject("ReflectionCamera");
            cameraObject.transform.SetParent(transform, false);
            reflectionCamera = cameraObject.AddComponent<Camera>();
            reflectionCameraCreatedAtRuntime = true;
        }

        reflectionCamera.enabled = true;
        reflectionCamera.allowHDR = true;
        reflectionCamera.allowMSAA = false;
        reflectionCamera.tag = "Untagged";
        reflectionCamera.cullingMask &= ~(1 << mirrorLayer);
        reflectionCamera.clearFlags = CameraClearFlags.Skybox;

        if (reflectionCamera.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>() == null)
            reflectionCamera.gameObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();

        var listener = reflectionCamera.GetComponent<AudioListener>();
        if (listener != null)
            Destroy(listener);

        EnsureReflectionTexture();
        reflectionCamera.targetTexture = reflectionTexture;
        ApplyReflectionTextureToMaterial();

        if (keepReflectionCameraFixed)
            ApplyFixedCameraPose();
    }

    private void EnsureReflectionTexture()
    {
        int safeSize = Mathf.Clamp(textureSize, 256, 2048);
        if (reflectionTexture != null
            && reflectionTexture.width == safeSize
            && reflectionTexture.height == safeSize)
            return;

        if (reflectionTexture != null)
        {
            reflectionTexture.Release();
            Destroy(reflectionTexture);
        }

        reflectionTexture = new RenderTexture(safeSize, safeSize, 24, RenderTextureFormat.ARGB32)
        {
            name = "MirrorReflectionTexture",
            useMipMap = false,
            autoGenerateMips = false,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        reflectionTexture.Create();
    }

    private void ApplyReflectionTextureToMaterial()
    {
        if (reflectionTexture == null || mirrorMaterial == null)
            return;

        mirrorMaterial.mainTexture = reflectionTexture;
        SetTexture("_BaseColorMap");
        SetTexture("_UnlitColorMap");
        SetTexture("_MainTex");

        void SetTexture(string propertyName)
        {
            if (!mirrorMaterial.HasProperty(propertyName))
                return;

            mirrorMaterial.SetTexture(propertyName, reflectionTexture);
            mirrorMaterial.SetTextureScale(propertyName, new Vector2(1f, -1f));
            mirrorMaterial.SetTextureOffset(propertyName, new Vector2(0f, 1f));
        }
    }

    private void ResolveSourceCamera()
    {
        if (sourceCamera != null && sourceCamera != reflectionCamera)
            return;

        if (sourceCamera == reflectionCamera)
            sourceCamera = null;

        if (sourceCamera == null)
        {
            var sourceTransform = transform.Find("SourceCamera");
            if (sourceTransform != null)
                sourceCamera = sourceTransform.GetComponent<Camera>();
        }

        if (sourceCamera != null && sourceCamera != reflectionCamera)
            return;

        var cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Camera fallbackCamera = null;
        Camera fallbackMainCamera = null;
        for (int i = 0; i < cameras.Length; i++)
        {
            var candidate = cameras[i];
            if (candidate == null || candidate == reflectionCamera)
                continue;

            if (candidate.CompareTag("MainCamera"))
            {
                if (fallbackMainCamera == null)
                    fallbackMainCamera = candidate;
                continue;
            }

            if (fallbackCamera == null)
                fallbackCamera = candidate;
        }

        sourceCamera = fallbackCamera ?? fallbackMainCamera;
    }

    private void EnsureFrame()
    {
        if (transform.Find("FrameTop") != null)
            return;

        var shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
        if (shader == null)
            return;

        frameMaterial = new Material(shader)
        {
            name = "RuntimeMirrorFrameMaterial",
            color = new Color(0.025f, 0.018f, 0.012f, 1f)
        };
        frameMaterial.SetColor("_BaseColor", new Color(0.025f, 0.018f, 0.012f, 1f));
        frameMaterial.SetColor("_Color", new Color(0.025f, 0.018f, 0.012f, 1f));

        CreateFramePiece("FrameTop", new Vector3(0f, 3.85f, 0f), new Vector3(2.45f, 0.22f, 0.18f));
        CreateFramePiece("FrameBottom", new Vector3(0f, 0.35f, 0f), new Vector3(2.45f, 0.22f, 0.18f));
        CreateFramePiece("FrameLeft", new Vector3(-1.12f, 2.1f, 0f), new Vector3(0.22f, 3.7f, 0.18f));
        CreateFramePiece("FrameRight", new Vector3(1.12f, 2.1f, 0f), new Vector3(0.22f, 3.7f, 0.18f));
    }

    private void CreateFramePiece(string objectName, Vector3 localPosition, Vector3 localScale)
    {
        var piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.name = objectName;
        piece.transform.SetParent(transform, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localRotation = Quaternion.identity;
        piece.transform.localScale = localScale;

        var renderer = piece.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = frameMaterial;

        var collider = piece.GetComponent<Collider>();
        if (collider != null)
            Destroy(collider);
    }

    private void UpdateReflectionCamera(Camera sourceCamera)
    {
        if (keepReflectionCameraFixed)
        {
            ApplyFixedCameraPose();
            return;
        }

        Vector3 planePosition = mirrorSurface != null ? mirrorSurface.position : transform.position;
        Vector3 planeNormal = mirrorSurface != null ? mirrorSurface.forward : transform.forward;
        planeNormal.Normalize();

        reflectionCamera.transform.position = ReflectPoint(sourceCamera.transform.position, planePosition, planeNormal);

        Vector3 reflectedForward = Vector3.Reflect(sourceCamera.transform.forward, planeNormal);
        Vector3 reflectedUp = Vector3.Reflect(sourceCamera.transform.up, planeNormal);
        if (reflectedForward.sqrMagnitude > 0.001f && reflectedUp.sqrMagnitude > 0.001f)
            reflectionCamera.transform.rotation = Quaternion.LookRotation(reflectedForward, reflectedUp);

        reflectionCamera.fieldOfView = sourceCamera.fieldOfView;
        reflectionCamera.nearClipPlane = sourceCamera.nearClipPlane;
        reflectionCamera.farClipPlane = sourceCamera.farClipPlane;
        reflectionCamera.aspect = sourceCamera.aspect;
    }

    private void ApplyFixedCameraPose()
    {
        CaptureFixedCameraPose();
        if (!fixedCameraPoseCaptured || reflectionCamera == null)
            return;

        reflectionCamera.transform.localPosition = fixedCameraLocalPosition;
        reflectionCamera.transform.localRotation = fixedCameraLocalRotation;
    }

    private void CaptureFixedCameraPose()
    {
        if (fixedCameraPoseCaptured || reflectionCamera == null)
            return;

        if (reflectionCameraCreatedAtRuntime)
            PlaceRuntimeFixedCamera();

        fixedCameraLocalPosition = reflectionCamera.transform.localPosition;
        fixedCameraLocalRotation = reflectionCamera.transform.localRotation;
        fixedCameraPoseCaptured = true;
    }

    private void PlaceRuntimeFixedCamera()
    {
        Transform reference = mirrorSurface != null ? mirrorSurface : transform;
        Vector3 forward = reference.forward;
        if (forward.sqrMagnitude < 0.001f)
            forward = transform.forward;

        forward.Normalize();
        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, forward);
        if (up.sqrMagnitude < 0.001f)
            up = Vector3.ProjectOnPlane(transform.up, forward);
        if (up.sqrMagnitude < 0.001f)
            up = Vector3.right;

        reflectionCamera.transform.position = reference.position + forward * fixedCameraOffsetFromMirror;
        reflectionCamera.transform.rotation = Quaternion.LookRotation(forward, up.normalized);
        reflectionCamera.fieldOfView = fixedCheckFieldOfView;
        reflectionCamera.nearClipPlane = 0.01f;
    }

    private static Vector3 ReflectPoint(Vector3 point, Vector3 planePosition, Vector3 planeNormal)
    {
        float distance = Vector3.Dot(point - planePosition, planeNormal);
        return point - 2f * distance * planeNormal;
    }

    private void OnDestroy()
    {
        if (reflectionTexture != null)
        {
            reflectionTexture.Release();
            Destroy(reflectionTexture);
        }

        if (mirrorMaterial != null)
            Destroy(mirrorMaterial);

        if (bloodMaterial != null)
            Destroy(bloodMaterial);

        if (bloodTexture != null)
            Destroy(bloodTexture);

        if (frameMaterial != null)
            Destroy(frameMaterial);

        if (reflectionCamera != null)
            Destroy(reflectionCamera.gameObject);
    }
}
