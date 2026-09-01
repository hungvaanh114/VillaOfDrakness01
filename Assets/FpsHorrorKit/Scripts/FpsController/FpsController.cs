namespace FpsHorrorKit
{
    using UnityEngine;
    using UnityEngine.InputSystem;
    using Unity.Cinemachine;

    [RequireComponent(typeof(CharacterController))]
    public class FpsController : MonoBehaviour
    {
        public bool isCutScene = true;
        [Header("Movement Settings")]
        public float walkSpeed = 4.0f;
        public float sprintSpeed = 7.0f;
        public float rotationSpeed = 1.0f;
        public float accelerationRate = 10.0f;
        public float decelerationRate = 10f;
        public float cutSceneTurnSpeed = 180f;

        [Header("Jump Settings")]
        public float jumpHeight = 2f;
        public float gravity = -20f;
        public float jumpCooldown = 0.2f;

        [Header("Grounded Settings")]
        public float groundedOffset = .85f;
        public float groundedRadius = 0.3f;
        public LayerMask groundLayers;

        [Header("Camera Settings")]
        public CinemachineCamera virtualCamera;
        public float maxCameraPitch = 70f;
        public float minCameraPitch = -70f;

        [Header("Headbob Settings")]
        public CinemachineBasicMultiChannelPerlin headBob;
        public float headBobAcceleration = 10f;
        public float idleBobAmp = .5f;
        public float idleBobFreq = 1f;
        public float walkBobAmp = 3f;
        public float walkBobFreq = 1f;
        public float sprintBobAmp = 4f;
        public float sprintBobFreq = 3f;

        [Header("Footstep Audio")]
        [SerializeField] private float walkStepInterval = 0.55f;
        [SerializeField] private float sprintStepInterval = 0.36f;
        [SerializeField] private float footstepRayDistance = 1.8f;
        [SerializeField] private LayerMask footstepSurfaceMask = ~0;
        [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.72f;

        [Header("Interact Settings")]
        public bool isInteracting = false;

        private CharacterController characterController;
        private FpsAssetsInputs _input;

        private Vector3 velocity;
        private bool isGrounded;
        private float jumpCooldownTimer;
        private float cameraPitch;
        private float footstepTimer;
        private float cutSceneFootstepTimer;

        [Header("Flashlight Aim Settings")]
        public Transform flashlightPivot;
        public Transform flashlightLookTarget;
        public Transform rightHandTarget;
        public Transform rightHandGrip;
        public Transform followTarget;
        public Light flashlightLight;
        public float flashlightLookDistance = 10f;
        public float flashlightAimSmooth = 15f;
        public bool showFlashlightRay = true;
        public float flashlightRayDistance = 50f;
        public float flashlightRayStartOffset = 0.1f;
        public LayerMask flashlightRayMask = ~0;
        public float flashlightPivotYAmount = 0.2f;
        public float flashlightPivotBackAmount = 0.2f;

        [Header("Detached Character Attachments")]
        public Transform detachedHairRoot;
        public Transform detachedHairFollowTarget;
        public Vector3 detachedHairLocalPosition = new Vector3(0f, -1.5624f, -0.0209f);
        public Vector3 detachedHairLocalEulerAngles;

        [Header("Animation Settings")]
        public Animator playerAnimator;
        public float animationDampTime = 0.2f;

        private float flashlightPivotStartY;
        private float flashlightPivotStartZ;
        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            var playerInput = GetComponent<PlayerInput>();
            _input = GetComponent<FpsAssetsInputs>();
        }

        private void Start()
        {
            if (virtualCamera == null)
            {
                Debug.LogError("Cinemachine Virtual Camera is not assigned.");
            }

            if (flashlightPivot != null)
            {
                flashlightPivotStartY = flashlightPivot.localPosition.y;
                flashlightPivotStartZ = flashlightPivot.localPosition.z;
            }

            EnsureFlashlightLightReference();
        }

        private void Update()
        {
            if (isCutScene) return;
            CaptureJumpInputFallback();
            if (!CanUseCharacterController())
                return;

            GroundedCheck();
            HandleMovement();
            HandleFootsteps();
            HandleGravity();
            HandleJumping();
            HandleAnimation();
        }

        private bool CanUseCharacterController()
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            return characterController != null
                && characterController.enabled
                && characterController.gameObject.activeInHierarchy
                && gameObject.activeInHierarchy;
        }

        private void LateUpdate()
        {
            if (!isCutScene)
                HandleRotation();

            HandleFlashlightAim();
            UpdateRightHandTarget();
            UpdateDetachedHair();
        }

        private void UpdateRightHandTarget()
        {
            if (rightHandTarget != null && rightHandGrip != null)
            {
                rightHandTarget.position = rightHandGrip.position;
                rightHandTarget.rotation = rightHandGrip.rotation;
            }
        }

        private void UpdateDetachedHair()
        {
            if (detachedHairRoot == null)
                detachedHairRoot = FindChildByName(transform, "npc_haircut_a_02");
            if (detachedHairFollowTarget == null && playerAnimator != null)
                detachedHairFollowTarget = FindChildByName(playerAnimator.transform, "Neck");

            if (detachedHairRoot == null || detachedHairFollowTarget == null)
                return;

            detachedHairRoot.SetPositionAndRotation(
                detachedHairFollowTarget.TransformPoint(detachedHairLocalPosition),
                detachedHairFollowTarget.rotation * Quaternion.Euler(detachedHairLocalEulerAngles));
        }

        private void HandleMovement()
        {
            if (isInteracting)
            {
                _input.move = Vector2.zero;
                velocity = Vector3.zero;

                if (headBob != null)
                {
                    headBob.AmplitudeGain = idleBobAmp;
                    headBob.FrequencyGain = idleBobFreq;
                }

                return;
            }

            HeadBob();

            Vector2 input = _input.move;
            Vector3 moveDirection = transform.right * input.x + transform.forward * input.y;

            float targetSpeed = _input.sprint ? sprintSpeed : walkSpeed;

            if (moveDirection != Vector3.zero)
            {
                velocity.x = Mathf.Lerp(velocity.x, targetSpeed * moveDirection.x, Time.deltaTime * accelerationRate);
                velocity.z = Mathf.Lerp(velocity.z, targetSpeed * moveDirection.z, Time.deltaTime * accelerationRate);
            }
            else
            {
                velocity.x = Mathf.Lerp(velocity.x, 0, Time.deltaTime * decelerationRate);
                velocity.z = Mathf.Lerp(velocity.z, 0, Time.deltaTime * decelerationRate);
            }

            characterController.Move(new Vector3(velocity.x, 0, velocity.z) * Time.deltaTime);
        }

        private void HandleFootsteps()
        {
            if (_input == null || isInteracting || !isGrounded)
            {
                footstepTimer = 0f;
                return;
            }

            bool hasMoveInput = _input.move.sqrMagnitude > 0.01f;
            bool isActuallyMoving = new Vector2(velocity.x, velocity.z).sqrMagnitude > 0.05f;
            if (!hasMoveInput || !isActuallyMoving)
            {
                footstepTimer = 0f;
                return;
            }

            float interval = _input.sprint ? sprintStepInterval : walkStepInterval;
            footstepTimer += Time.deltaTime;
            if (footstepTimer < interval)
                return;

            footstepTimer = 0f;
            PlayFootstepForCurrentSurface(_input.sprint);
        }

        private void PlayFootstepForCurrentSurface(bool isSprinting)
        {
            var audioManager = global::AudioManager.Instance;
            if (audioManager == null)
                return;

            float volume = footstepVolume * (isSprinting ? 1f : 0.82f);
            if (IsStandingOnWood())
                audioManager.PlayWoodFootstep(volume);
            else
                audioManager.PlayGroundFootstep(volume);
        }

        private bool IsStandingOnWood()
        {
            if (!TryGetCurrentSurfaceHit(out RaycastHit hit))
                return false;

            if (hit.collider is TerrainCollider)
                return false;

            return ContainsWoodKeyword(hit.collider.name)
                || ContainsWoodKeyword(GetHierarchyName(hit.collider.transform))
                || ContainsWoodKeyword(GetRendererMaterialNames(hit.collider));
        }

        private bool TryGetCurrentSurfaceHit(out RaycastHit surfaceHit)
        {
            Vector3 origin = transform.position + Vector3.up * 0.25f;
            int mask = footstepSurfaceMask.value == 0 ? Physics.DefaultRaycastLayers : footstepSurfaceMask.value;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, footstepRayDistance, mask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                    continue;

                var hitController = hit.collider.GetComponentInParent<FpsController>();
                if (hitController == this)
                    continue;

                surfaceHit = hit;
                return true;
            }

            surfaceHit = default;
            return false;
        }

        private static bool ContainsWoodKeyword(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            value = value.ToLowerInvariant();
            return value.Contains("wood")
                || value.Contains("floor")
                || value.Contains("plank")
                || value.Contains("house")
                || value.Contains("villa");
        }

        private static string GetHierarchyName(Transform target)
        {
            string path = string.Empty;
            int depth = 0;

            while (target != null && depth < 8)
            {
                path = string.IsNullOrEmpty(path) ? target.name : target.name + "/" + path;
                target = target.parent;
                depth++;
            }

            return path;
        }

        private static string GetRendererMaterialNames(Collider collider)
        {
            var renderer = collider.GetComponentInParent<Renderer>();
            if (renderer == null)
                return string.Empty;

            string names = string.Empty;
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null)
                    continue;

                names += material.name + " ";
            }

            return names;
        }

        public void MoveCutScene(Vector3 worldDirection, float speed, bool faceMoveDirection = true, float turnSpeed = -1f)
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            GroundedCheck();

            var horizontalDirection = worldDirection;
            horizontalDirection.y = 0f;
            if (horizontalDirection.sqrMagnitude > 1f)
                horizontalDirection.Normalize();

            if (isGrounded && velocity.y < 0f)
                velocity.y = -2f;

            velocity.y += gravity * Time.deltaTime;
            characterController.Move((horizontalDirection * speed + Vector3.up * velocity.y) * Time.deltaTime);

            if (faceMoveDirection)
                RotateCutSceneTowards(horizontalDirection, turnSpeed > 0f ? turnSpeed : cutSceneTurnSpeed);

            HandleCutSceneFootsteps(horizontalDirection.sqrMagnitude > 0.01f, speed);
            UpdateCutSceneAnimation(horizontalDirection.sqrMagnitude > 0.01f);
        }

        private void HandleCutSceneFootsteps(bool isMoving, float speed)
        {
            if (!isGrounded)
            {
                cutSceneFootstepTimer = 0f;
                return;
            }

            if (!isMoving)
            {
                cutSceneFootstepTimer = 0f;
                return;
            }

            bool isSprinting = speed > walkSpeed * 1.05f;
            float interval = isSprinting ? sprintStepInterval : walkStepInterval;
            cutSceneFootstepTimer += Time.deltaTime;
            if (cutSceneFootstepTimer < interval)
                return;

            cutSceneFootstepTimer = 0f;
            PlayFootstepForCurrentSurface(isSprinting);
        }

        public bool RotateCutSceneTowards(Vector3 worldDirection, float turnSpeed)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude <= 0.001f)
                return true;

            var targetRotation = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, Mathf.Max(1f, turnSpeed) * Time.deltaTime);
            return Quaternion.Angle(transform.rotation, targetRotation) <= 0.25f;
        }

        public void SetCutSceneCameraPitch(float pitch)
        {
            cameraPitch = Mathf.Clamp(pitch, minCameraPitch, maxCameraPitch);
        }

        public void StopCutSceneMovement()
        {
            velocity = Vector3.zero;
            jumpCooldownTimer = 0f;
            cutSceneFootstepTimer = 0f;
            footstepTimer = 0f;
            if (_input != null)
                _input.ClearGameplayInput();
            UpdateCutSceneAnimation(false);
        }

        public void ForceIdleState(bool freezeHeadBob = true)
        {
            velocity = Vector3.zero;
            jumpCooldownTimer = 0f;
            cutSceneFootstepTimer = 0f;
            footstepTimer = 0f;

            if (_input != null)
                _input.ClearGameplayInput();

            if (playerAnimator != null)
            {
                playerAnimator.SetBool("isRun", false);
                playerAnimator.SetFloat("speed", 0f);
            }

            if (freezeHeadBob && headBob != null)
            {
                headBob.AmplitudeGain = 0f;
                headBob.FrequencyGain = 0f;
            }
        }

        public void TeleportCutScene(Transform point)
        {
            if (point == null)
                return;

            TeleportCutScene(point.position, point.rotation);
        }

        public void TeleportCutScene(Vector3 position, Quaternion rotation)
        {
            if (characterController == null)
                characterController = GetComponent<CharacterController>();

            var wasEnabled = characterController != null && characterController.enabled;
            if (characterController != null)
                characterController.enabled = false;

            transform.SetPositionAndRotation(position, rotation);
            velocity = Vector3.zero;

            if (characterController != null)
                characterController.enabled = wasEnabled;
        }

        private void HandleAnimation()
        {
            if (playerAnimator == null)
                return;

            bool isMoving = _input.move.sqrMagnitude > 0.01f;

            playerAnimator.SetBool("isRun", isMoving);

            float targetAnimationSpeed = 0f;

            if (isMoving)
            {
                targetAnimationSpeed = _input.sprint ? 1f : 0f;
            }

            playerAnimator.SetFloat("speed", targetAnimationSpeed, animationDampTime, Time.deltaTime);
        }

        private void UpdateCutSceneAnimation(bool isMoving)
        {
            if (playerAnimator == null)
                return;

            playerAnimator.SetBool("isRun", isMoving);
            playerAnimator.SetFloat("speed", isMoving ? 0.35f : 0f, animationDampTime, Time.deltaTime);
        }

        private void HandleRotation()
        {
            if (isInteracting)
                return;

            Vector2 lookInput = _input.look;

            cameraPitch += lookInput.y * rotationSpeed;
            cameraPitch = Mathf.Clamp(cameraPitch, minCameraPitch, maxCameraPitch);

            transform.Rotate(Vector3.up * lookInput.x * rotationSpeed);
        }

        private void HandleFlashlightAim()
        {
            if (followTarget == null)
                return;

            EnsureFlashlightLightReference();

            Quaternion aimRotation = Quaternion.Euler(cameraPitch, transform.eulerAngles.y, 0f);

            Vector3 rayDirection = aimRotation * Vector3.forward;
            Vector3 rayOrigin = followTarget.position + rayDirection * flashlightRayStartOffset;

            // Điểm cố định để Cinemachine nhìn theo.
            if (flashlightLookTarget != null)
            {
                flashlightLookTarget.position = rayOrigin + rayDirection * flashlightLookDistance;
            }

            // Spot Light LUÔN phát từ FollowTarget.
            if (flashlightLight != null)
            {
                flashlightLight.transform.position = rayOrigin;
                flashlightLight.transform.rotation = aimRotation;
            }

            Vector3 hitPoint = rayOrigin + rayDirection * flashlightRayDistance;

            if (TryGetFlashlightHit(rayOrigin, rayDirection, out RaycastHit hit))
            {
                hitPoint = hit.point;

                if (showFlashlightRay)
                    Debug.DrawLine(rayOrigin, hitPoint, Color.green);
            }
            else
            {
                if (showFlashlightRay)
                    Debug.DrawRay(rayOrigin, rayDirection * flashlightRayDistance, Color.red);
            }

            // Model đèn pin chỉ xoay về điểm mà tia sáng đang chạm.
            if (flashlightPivot != null)
            {
                Vector3 pivotPosition = flashlightPivot.localPosition;
                pivotPosition.y = flashlightPivotStartY + rayDirection.y * flashlightPivotYAmount;
                pivotPosition.z = flashlightPivotStartZ - Mathf.Abs(rayDirection.y) * flashlightPivotBackAmount;
                flashlightPivot.localPosition = pivotPosition;

                Vector3 direction = hitPoint - flashlightPivot.position;

                if (direction.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
                    flashlightPivot.rotation = Quaternion.Slerp(flashlightPivot.rotation, targetRotation, flashlightAimSmooth * Time.deltaTime);
                }

                if (showFlashlightRay) Debug.DrawLine(flashlightPivot.position, hitPoint, Color.yellow);
            }
        }

        private bool TryGetFlashlightHit(Vector3 rayOrigin, Vector3 rayDirection, out RaycastHit closestHit)
        {
            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, rayDirection, flashlightRayDistance, flashlightRayMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

            foreach (RaycastHit hit in hits)
            {
                if (ShouldIgnoreFlashlightAimHit(hit.collider))
                    continue;

                closestHit = hit;
                return true;
            }

            closestHit = default;
            return false;
        }

        private static bool ShouldIgnoreFlashlightAimHit(Collider hitCollider)
        {
            if (hitCollider == null)
                return true;

            return hitCollider.GetComponentInParent<ItemPickup>() != null
                || hitCollider.GetComponentInParent<MusicSheetPickup>() != null;
        }

        private void EnsureFlashlightLightReference()
        {
            if (followTarget == null)
                return;

            if (flashlightLight != null && flashlightLight.name != "Spot Light_1")
                return;

            var spotLight = followTarget.Find("Spot Light");
            if (spotLight != null)
                flashlightLight = spotLight.GetComponent<Light>();
        }

        private void GroundedCheck()
        {
            Vector3 spherePosition = new Vector3(transform.position.x, transform.position.y - groundedOffset, transform.position.z);
            isGrounded = (characterController != null && characterController.isGrounded)
                || Physics.CheckSphere(spherePosition, groundedRadius, groundLayers, QueryTriggerInteraction.Ignore);
        }

        private void HandleGravity()
        {
            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f;
            }

            velocity.y += gravity * Time.deltaTime;
            characterController.Move(Vector3.up * velocity.y * Time.deltaTime);
        }

        private void HandleJumping()
        {
            if (jumpCooldownTimer > 0)
            {
                jumpCooldownTimer -= Time.deltaTime;
            }

            if (isGrounded)
            {
                if (_input.jump && jumpCooldownTimer <= 0)
                {
                    velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                    jumpCooldownTimer = jumpCooldown;
                    _input.jump = false;
                }
            }
            else
            {
                _input.jump = false;
            }
        }

        private void CaptureJumpInputFallback()
        {
            if (global::GameController.IsGameplayInputLocked())
                return;

            if (_input == null || Keyboard.current == null)
                return;

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
                _input.jump = true;
        }

        private void HeadBob()
        {
            if (headBob == null)
                return;

            float moveMagnitude = _input.move.magnitude;
            float targetAmp = moveMagnitude > 0 ? (_input.sprint ? sprintBobAmp : walkBobAmp) : idleBobAmp;
            float targetFreq = moveMagnitude > 0 ? (_input.sprint ? sprintBobFreq : walkBobFreq) : idleBobFreq;

            headBob.AmplitudeGain = Mathf.Lerp(headBob.AmplitudeGain, targetAmp, Time.deltaTime * headBobAcceleration);
            headBob.FrequencyGain = Mathf.Lerp(headBob.FrequencyGain, targetFreq, Time.deltaTime * headBobAcceleration);
        }

        private void OnDrawGizmosSelected()
        {
            Color transparentGreen = new Color(0.0f, 1.0f, 0.0f, 0.35f);
            Color transparentRed = new Color(1.0f, 0.0f, 0.0f, 0.35f);

            if (isGrounded) Gizmos.color = transparentGreen;
            else Gizmos.color = transparentRed;

            Gizmos.DrawSphere(new Vector3(transform.position.x, transform.position.y - groundedOffset, transform.position.z), groundedRadius);
        }

        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null)
                return null;

            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName)
                    return child;
            }

            return null;
        }


    }
}
