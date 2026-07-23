using UnityEngine;

/// <summary>
/// Smoothly follows a target (the player), keeping the camera view inside
/// the level bounds so the void beyond the level edges never shows. Purely
/// visual — no gameplay logic, safe to remove or retune without touching
/// any other system.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0f, 2f, -10f);
    [SerializeField] private float smoothTime = 0.15f;

    [Header("View Bounds")]
    [Tooltip("World-space rect the camera VIEW (not just its center) stays inside.")]
    [SerializeField] private Vector2 boundsMin = new Vector2(-1000f, -1000f);
    [SerializeField] private Vector2 boundsMax = new Vector2(1000f, 1000f);

    private Camera cam;
    private Vector3 velocity;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        SnapToTarget(); // play opens correctly framed, clamp included
    }

    private void LateUpdate()
    {
        if (target == null) return;
        Vector3 desired = ClampToBounds(target.position + offset);
        transform.position = ClampToBounds(
            Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime));
    }

    /// <summary>Jump straight to the (clamped) follow position (e.g. after a respawn teleport).</summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        velocity = Vector3.zero;
        transform.position = ClampToBounds(target.position + offset);
    }

    /// <summary>
    /// Keeps the whole orthographic view inside the bounds rect: half the
    /// view extends each way from the camera center, so the center is
    /// clamped to the rect shrunk by the view's half-extents.
    /// </summary>
    private Vector3 ClampToBounds(Vector3 position)
    {
        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        position.x = ClampAxis(position.x, boundsMin.x + halfWidth, boundsMax.x - halfWidth);
        position.y = ClampAxis(position.y, boundsMin.y + halfHeight, boundsMax.y - halfHeight);
        return position;
    }

    // When the view is larger than the bounds on an axis, center on it instead.
    private static float ClampAxis(float value, float min, float max)
    {
        return min > max ? (min + max) * 0.5f : Mathf.Clamp(value, min, max);
    }
}
