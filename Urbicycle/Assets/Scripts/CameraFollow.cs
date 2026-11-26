using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour
{
    public enum CameraMode
    {
        FirstPerson,
        SecondPerson,
        ThirdPerson
    }

    public CameraMode startingMode = CameraMode.FirstPerson;
    public Key switchModeKey = Key.V;
    public Transform target;

    public float eyeHeight = 0.6f;
    public float eyeForwardOffset = 0.2f;
    public float firstPersonMinPitch = -85f;
    public float firstPersonMaxPitch = 85f;
    public bool hideTargetInFirstPerson = true;

    public float distance = 1f;
    public float pivotHeight = 0.7f;
    public bool autoCenter = true;
    public float minPitch = -20f;
    public float maxPitch = 70f;
    public float secondPersonMinPitch = -89f;
    public float secondPersonMaxPitch = 60f;

    public float mouseSensitivity = 0.12f;
    public bool invertY = false;

    public LayerMask collisionMask = ~0;
    public float collisionRadius = 0.25f;
    public float collisionPadding = 0.1f;
    public float nearClipPlane = 0.03f;
    public float hideCharacterDistance = 1f;
    public float hideCharacterMargin = 0.05f;
    public float hideCharacterBuffer = 0.1f;
    public float characterShowDelay = 0.05f;

    public bool alignTargetRotation = true;
    public float targetYawOffset = 0f;

    public bool InvertMovement => mode == CameraMode.SecondPerson;

    private float yaw;
    private float orbitPitch = 35f;
    private float lookPitch;

    private readonly RaycastHit[] cameraHits = new RaycastHit[32];
    private Renderer[] targetRenderers;
    private bool[] targetRendererStates;
    private bool targetHidden;
    private float showTimer;
    private float characterRadius = 0.3f;
    private Vector3 targetLocalCenter;
    private bool hasTargetCenter;
    private CameraMode mode;

    private void Awake()
    {
        mode = startingMode;
        ClampOrbitPitch();

        Camera cam = GetComponent<Camera>();

        if (cam == null)
            cam = GetComponentInChildren<Camera>();

        if (cam != null)
            cam.nearClipPlane = nearClipPlane;

        if (target == null)
        {
            PlayerMovement player = FindAnyObjectByType<PlayerMovement>();

            if (player != null)
                target = player.transform;
        }

        if (target != null)
        {
            yaw = target.eulerAngles.y - targetYawOffset;
            targetRenderers = target.GetComponentsInChildren<Renderer>(true);
            targetRendererStates = new bool[targetRenderers.Length];

            for (int i = 0; i < targetRenderers.Length; i++)
                targetRendererStates[i] = targetRenderers[i].enabled;

            Bounds bounds = new Bounds(target.position, Vector3.zero);
            bool hasBounds = false;

            for (int i = 0; i < targetRenderers.Length; i++)
            {
                Renderer renderer = targetRenderers[i];

                if (renderer == null || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer))
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (hasBounds)
            {
                characterRadius = Mathf.Max(bounds.extents.x, bounds.extents.z);
                targetLocalCenter = target.InverseTransformPoint(bounds.center);
                hasTargetCenter = true;
            }
            else
            {
                CharacterController controller = target.GetComponent<CharacterController>();

                if (controller != null)
                {
                    Vector3 scale = target.lossyScale;
                    characterRadius = controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                }
            }

            characterRadius = Mathf.Max(0.1f, characterRadius);
        }

        SetTargetHidden(mode == CameraMode.FirstPerson && hideTargetInFirstPerson);
        LockCursor();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            LockCursor();
    }

    private void OnDisable()
    {
        SetTargetHidden(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void SetTargetHidden(bool hidden)
    {
        if (targetHidden == hidden || targetRenderers == null)
            return;

        targetHidden = hidden;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] != null)
                targetRenderers[i].enabled = !hidden && targetRendererStates[i];
        }
    }

    private bool IsTarget(Transform hitTransform)
    {
        return hitTransform == target || hitTransform.IsChildOf(target);
    }

    private Vector3 GetPivot()
    {
        if (autoCenter && hasTargetCenter)
        {
            Vector3 lateralOffset = target.TransformPoint(targetLocalCenter) - target.position;
            lateralOffset.y = 0f;

            return target.position + lateralOffset + Vector3.up * pivotHeight;
        }

        return target.position + Vector3.up * pivotHeight;
    }

    private bool ShouldHideCharacter(float cameraDistance)
    {
        float baseHide = Mathf.Min(hideCharacterDistance, characterRadius + hideCharacterMargin);
        float buffer = targetHidden ? Mathf.Min(hideCharacterBuffer, baseHide * 0.5f) : 0f;

        return cameraDistance < baseHide + buffer;
    }

    private Vector3 ResolveCameraCollision(Vector3 pivot, Vector3 desiredPosition)
    {
        Vector3 direction = desiredPosition - pivot;
        float cameraDistance = direction.magnitude;

        if (cameraDistance < 0.001f)
            return desiredPosition;

        direction /= cameraDistance;

        int hitCount = Physics.SphereCastNonAlloc(
            pivot,
            collisionRadius,
            direction,
            cameraHits,
            cameraDistance,
            collisionMask,
            QueryTriggerInteraction.Ignore);

        float nearest = cameraDistance;

        for (int i = 0; i < hitCount; i++)
        {
            if (IsTarget(cameraHits[i].transform))
                continue;

            if (cameraHits[i].distance < nearest)
                nearest = cameraHits[i].distance;
        }

        if (nearest < cameraDistance)
            return pivot + direction * Mathf.Max(0f, nearest - collisionPadding);

        return desiredPosition;
    }

    public void CycleMode()
    {
        switch (mode)
        {
            case CameraMode.FirstPerson:
                mode = CameraMode.ThirdPerson;
                break;
            case CameraMode.ThirdPerson:
                mode = CameraMode.SecondPerson;
                break;
            default:
                mode = CameraMode.FirstPerson;
                break;
        }

        ClampOrbitPitch();
    }

    private void ClampOrbitPitch()
    {
        if (mode == CameraMode.SecondPerson)
            orbitPitch = Mathf.Clamp(orbitPitch, secondPersonMinPitch, secondPersonMaxPitch);
        else if (mode == CameraMode.ThirdPerson)
            orbitPitch = Mathf.Clamp(orbitPitch, minPitch, maxPitch);
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        if (Keyboard.current != null && Keyboard.current[switchModeKey].wasPressedThisFrame)
            CycleMode();

        if (Cursor.lockState == CursorLockMode.Locked && Mouse.current != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            yaw += delta.x * mouseSensitivity;

            float pitchDelta = (invertY ? delta.y : -delta.y) * mouseSensitivity;

            if (mode == CameraMode.FirstPerson)
            {
                lookPitch = Mathf.Clamp(lookPitch - pitchDelta, firstPersonMinPitch, firstPersonMaxPitch);
            }
            else
            {
                float min = mode == CameraMode.SecondPerson ? secondPersonMinPitch : minPitch;
                float max = mode == CameraMode.SecondPerson ? secondPersonMaxPitch : maxPitch;

                orbitPitch = Mathf.Clamp(orbitPitch + pitchDelta, min, max);
            }
        }

        if (alignTargetRotation)
            target.rotation = Quaternion.Euler(0f, yaw + targetYawOffset, 0f);

        bool hideTarget = false;

        switch (mode)
        {
            case CameraMode.FirstPerson:
            {
                Vector3 head = target.position + Vector3.up * eyeHeight;
                Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                float offset = eyeForwardOffset;

                if (Physics.SphereCast(
                        head,
                        0.05f,
                        forward,
                        out RaycastHit hit,
                        offset + collisionPadding,
                        collisionMask,
                        QueryTriggerInteraction.Ignore) &&
                    !IsTarget(hit.transform))
                {
                    offset = Mathf.Max(0f, hit.distance - collisionPadding);
                }

                transform.position = head + forward * offset;
                transform.rotation = Quaternion.Euler(-lookPitch, yaw, 0f);
                hideTarget = hideTargetInFirstPerson;
                break;
            }
            case CameraMode.SecondPerson:
            {
                Vector3 pivot = GetPivot();
                Quaternion orbit = Quaternion.Euler(orbitPitch, yaw, 0f);
                float offset = Mathf.Max(0.1f, distance);
                Vector3 desiredPosition = pivot + orbit * new Vector3(0f, 0f, offset);

                transform.position = ResolveCameraCollision(pivot, desiredPosition);

                Vector3 lookDirection = pivot - transform.position;

                if (lookDirection.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(lookDirection);

                hideTarget = ShouldHideCharacter(Vector3.Distance(transform.position, pivot));
                break;
            }
            case CameraMode.ThirdPerson:
            {
                Vector3 pivot = GetPivot();
                Quaternion orbit = Quaternion.Euler(orbitPitch, yaw, 0f);
                float offset = Mathf.Max(0.1f, distance);
                Vector3 desiredPosition = pivot + orbit * new Vector3(0f, 0f, -offset);

                transform.position = ResolveCameraCollision(pivot, desiredPosition);

                Vector3 lookDirection = pivot - transform.position;

                if (lookDirection.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(lookDirection);

                hideTarget = ShouldHideCharacter(Vector3.Distance(transform.position, pivot));
                break;
            }
        }

        if (hideTarget)
        {
            showTimer = 0f;
            SetTargetHidden(true);
        }
        else
        {
            showTimer += Time.deltaTime;

            if (showTimer >= characterShowDelay)
                SetTargetHidden(false);
        }
    }
}
