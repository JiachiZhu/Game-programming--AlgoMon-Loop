using System.Collections;
using UnityEngine;

public class CameraFocusController : MonoBehaviour
{
    public static CameraFocusController Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private InfoPanelController infoPanelController;
    [SerializeField] private AudioSource audioSource;

    [Header("Camera Motion")]
    [SerializeField] private float moveDuration = 0.8f;
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private KeyCode returnKey = KeyCode.Escape;
    [SerializeField] private bool keepLookingAtFocusedTarget = true;

    private Vector3 defaultPosition;
    private Quaternion defaultRotation;
    private Coroutine moveRoutine;
    private Transform focusedTarget;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            enabled = false;
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (infoPanelController == null)
        {
            infoPanelController = FindObjectOfType<InfoPanelController>();
        }

        if (audioSource == null)
        {
            if (targetCamera != null)
            {
                audioSource = targetCamera.GetComponent<AudioSource>();
            }

            if (audioSource == null)
            {
                audioSource = FindObjectOfType<AudioSource>();
            }
        }

        if (targetCamera != null)
        {
            defaultPosition = targetCamera.transform.position;
            defaultRotation = targetCamera.transform.rotation;
        }

        if (infoPanelController != null)
        {
            infoPanelController.BindReturnButton(ReturnToMainView);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(returnKey))
        {
            ReturnToMainView();
        }
    }

    private void LateUpdate()
    {
        if (!keepLookingAtFocusedTarget || targetCamera == null || focusedTarget == null)
        {
            return;
        }

        targetCamera.transform.LookAt(focusedTarget.position);
    }

    public void FocusOn(CelestialBodyClickable body)
    {
        if (targetCamera == null || body == null)
        {
            return;
        }

        focusedTarget = body.transform;

        Vector3 destination = body.transform.position + body.CameraWorldOffset;
        Quaternion destinationRotation = Quaternion.LookRotation(body.transform.position - destination, Vector3.up);
        StartMove(destination, destinationRotation);

        if (infoPanelController != null)
        {
            infoPanelController.ShowInfo(body.DisplayName, body.FactText);
        }

        if (audioSource != null && body.ClickAudioClip != null)
        {
            audioSource.PlayOneShot(body.ClickAudioClip);
        }
    }

    public void ReturnToMainView()
    {
        if (targetCamera == null)
        {
            return;
        }

        focusedTarget = null;
        StartMove(defaultPosition, defaultRotation);

        if (infoPanelController != null)
        {
            infoPanelController.HideInfo();
        }
    }

    private void StartMove(Vector3 destinationPosition, Quaternion destinationRotation)
    {
        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
        }

        moveRoutine = StartCoroutine(MoveCameraRoutine(destinationPosition, destinationRotation));
    }

    private IEnumerator MoveCameraRoutine(Vector3 destinationPosition, Quaternion destinationRotation)
    {
        Vector3 startPosition = targetCamera.transform.position;
        Quaternion startRotation = targetCamera.transform.rotation;

        float timer = 0f;

        while (timer < moveDuration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / moveDuration);
            float curvedT = moveCurve.Evaluate(t);

            targetCamera.transform.position = Vector3.Lerp(startPosition, destinationPosition, curvedT);
            targetCamera.transform.rotation = Quaternion.Slerp(startRotation, destinationRotation, curvedT);

            yield return null;
        }

        targetCamera.transform.position = destinationPosition;
        targetCamera.transform.rotation = destinationRotation;
        moveRoutine = null;
    }
}
