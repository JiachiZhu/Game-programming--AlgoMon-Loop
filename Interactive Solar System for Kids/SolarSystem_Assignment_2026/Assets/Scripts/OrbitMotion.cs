using UnityEngine;

public class OrbitMotion : MonoBehaviour
{
    [Header("Orbit Settings")]
    [SerializeField] private Transform orbitCenter;
    [SerializeField] private Vector3 orbitAxis = Vector3.up;
    [SerializeField] private float orbitSpeedDegreesPerSecond = 15f;

    private void Update()
    {
        if (orbitCenter == null)
        {
            return;
        }

        transform.RotateAround(
            orbitCenter.position,
            orbitAxis.normalized,
            orbitSpeedDegreesPerSecond * Time.deltaTime);
    }
}
