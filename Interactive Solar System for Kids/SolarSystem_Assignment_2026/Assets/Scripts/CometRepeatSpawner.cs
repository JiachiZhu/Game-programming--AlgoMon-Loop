using UnityEngine;

/// <summary>
/// Spawns a prefab on a fixed interval using <see cref="Time.unscaledDeltaTime"/> so
/// <see cref="Time.timeScale"/> = 0 does not stop the timer. Remove the asset
/// <c>Spawner</c> from the same object or you will get double spawns and confusing behaviour.
/// </summary>
public class CometRepeatSpawner : MonoBehaviour
{
    [Tooltip("Prefab to clone each interval. Prefer a Project prefab asset, not a scene object.")]
    [SerializeField] private GameObject spawnPrefab;

    [Tooltip("Seconds between each comet. Example: 6 = one comet every 6s (in 18s you only get 3). Lower this if you want more comets. Uses unscaled time.")]
    [SerializeField] private float intervalSeconds = 2.5f;

    [Tooltip("If on, one comet is spawned as soon as Play starts; after that, interval still applies.")]
    [SerializeField] private bool firstCometOnPlay = true;

    [Tooltip("Goes up while playing (enter Play to see it change). This is a counter, not a limit.")]
    [SerializeField] private int totalSpawned;

    private float timer;

    private void Start()
    {
        totalSpawned = 0;
        timer = 0f;

        if (firstCometOnPlay && spawnPrefab != null)
        {
            Instantiate(spawnPrefab, transform.position, transform.rotation, null);
            totalSpawned = 1;
        }
    }

    private void OnEnable()
    {
        timer = 0f;
    }

    private void Update()
    {
        if (spawnPrefab == null)
        {
            return;
        }

        float wait = Mathf.Max(0.05f, intervalSeconds);
        timer += Time.unscaledDeltaTime;

        if (timer < wait)
        {
            return;
        }

        timer = 0f;
        Instantiate(spawnPrefab, transform.position, transform.rotation, null);
        totalSpawned++;
    }
}
