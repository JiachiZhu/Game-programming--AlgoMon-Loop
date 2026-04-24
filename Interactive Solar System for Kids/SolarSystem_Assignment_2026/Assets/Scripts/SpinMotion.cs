using UnityEngine;

public class SpinMotion : MonoBehaviour
{
    [Header("Spin Settings")]
    [SerializeField] private Vector3 spinAxis = Vector3.up;
    [SerializeField] private float spinSpeedDegreesPerSecond = 30f;
    [SerializeField] private Space spinSpace = Space.Self;

    private void Update()
    {
        transform.Rotate(spinAxis.normalized, spinSpeedDegreesPerSecond * Time.deltaTime, spinSpace);
    }
}
