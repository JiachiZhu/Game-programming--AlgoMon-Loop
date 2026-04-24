using UnityEngine;

public class CometOrbitMotion : MonoBehaviour
{
    [Header("Comet Orbit")]
    [SerializeField] private Transform center;
    [SerializeField] private float orbitSpeedDegreesPerSecond = 22f;
    [SerializeField] private float verticalWaveAmplitude = 0.5f;
    [SerializeField] private float verticalWaveFrequency = 1f;

    private Vector3 initialOffset;
    private float startY;

    private void Start()
    {
        if (center != null)
        {
            initialOffset = transform.position - center.position;
        }

        startY = transform.position.y;
    }

    private void Update()
    {
        if (center == null)
        {
            return;
        }

        initialOffset = Quaternion.Euler(0f, orbitSpeedDegreesPerSecond * Time.deltaTime, 0f) * initialOffset;
        Vector3 targetPosition = center.position + initialOffset;
        targetPosition.y = startY + Mathf.Sin(Time.time * verticalWaveFrequency) * verticalWaveAmplitude;
        transform.position = targetPosition;

        Vector3 forwardDir = Vector3.ProjectOnPlane(targetPosition - center.position, Vector3.up).normalized;
        if (forwardDir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(forwardDir, Vector3.up);
        }
    }
}
