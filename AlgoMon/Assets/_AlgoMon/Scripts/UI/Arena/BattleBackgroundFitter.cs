using UnityEngine;

/// <summary>
/// Keeps the world-space battle background covering the active camera view.
/// Any replacement sprite dropped on the Background object will be scaled to
/// cover the viewport without stretching its aspect ratio.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
// Defense note: BattleBackgroundFitter is a Unity component attached to a scene object for this feature.
public class BattleBackgroundFitter : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField, Min(0f)] private float extraPadding = 0.04f;
    [SerializeField] private bool followCameraCenter = true;
    [SerializeField] private bool fitEveryFrame;

    // Defense note: Unity lifecycle hook that runs the awake step for this component.
    private void Awake()
    {
        ResolveReferences();
        FitToCamera();
    }

    // Defense note: Unity lifecycle hook that runs the on enable step for this component.
    private void OnEnable()
    {
        ResolveReferences();
        FitToCamera();
    }

    // Defense note: Unity lifecycle hook that runs the late update step for this component.
    private void LateUpdate()
    {
        if (fitEveryFrame)
            FitToCamera();
    }

    [ContextMenu("Fit To Camera")]
    // Defense note: Runs the fit to camera helper used by this script.
    public void FitToCamera()
    {
        ResolveReferences();
        if (targetCamera == null || spriteRenderer == null || spriteRenderer.sprite == null)
            return;

        if (followCameraCenter)
        {
            Vector3 cameraPosition = targetCamera.transform.position;
            transform.position = new Vector3(cameraPosition.x, cameraPosition.y, transform.position.z);
        }

        float viewHeight;
        float viewWidth;
        if (targetCamera.orthographic)
        {
            viewHeight = targetCamera.orthographicSize * 2f;
            viewWidth = viewHeight * targetCamera.aspect;
        }
        else
        {
            float distance = Mathf.Abs(transform.position.z - targetCamera.transform.position.z);
            viewHeight = 2f * distance * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            viewWidth = viewHeight * targetCamera.aspect;
        }

        Vector2 spriteSize = spriteRenderer.sprite.bounds.size;
        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
            return;

        float scale = Mathf.Max(viewWidth / spriteSize.x, viewHeight / spriteSize.y) + extraPadding;
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    // Defense note: Resolves the references step and updates dependent state.
    private void ResolveReferences()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }
}
