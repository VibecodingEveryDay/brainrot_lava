using UnityEngine;

/// <summary>
/// Спавнит ботов с заданным интервалом (случайное значение между min и max в секундах).
/// </summary>
public class BotSpawner : MonoBehaviour
{
    [Header("Интервал спавна (секунды)")]
    [SerializeField] private float spawnIntervalMin = 10f;
    [SerializeField] private float spawnIntervalMax = 20f;
    
    [Header("Префаб и точка спавна")]
    [SerializeField] private GameObject botPrefab;
    [Tooltip("Если не назначен, используется transform этого объекта")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Случайная позиция по X при спавне: X = Random(min, max). Если min == max, используется X точки спавна.")]
    [SerializeField] private float spawnXMin = 0f;
    [SerializeField] private float spawnXMax = 0f;
    
    [Header("Debug")]
    [Tooltip("Если включено: спавнится 1 бот сразу при старте и больше не спавнится")]
    [SerializeField] private bool devSpawnOnlyOne = false;
    
    private float nextSpawnTime;
    private bool devOneSpawned;
    
    private void Start()
    {
        if (devSpawnOnlyOne && botPrefab != null)
        {
            Vector3 pos = GetSpawnPosition();
            Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
            Instantiate(botPrefab, pos, rot);
            devOneSpawned = true;
            return;
        }
        nextSpawnTime = Time.time + Random.Range(spawnIntervalMin, spawnIntervalMax);
    }
    
    private void Update()
    {
        if (botPrefab == null) return;
        if (devSpawnOnlyOne && devOneSpawned) return;
        if (Time.time < nextSpawnTime) return;
        
        Vector3 pos = GetSpawnPosition();
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
        Instantiate(botPrefab, pos, rot);
        nextSpawnTime = Time.time + Random.Range(spawnIntervalMin, spawnIntervalMax);
    }
    
    private Vector3 GetSpawnPosition()
    {
        Vector3 basePos = spawnPoint != null ? spawnPoint.position : transform.position;
        if (spawnXMin != spawnXMax)
            basePos.x = Random.Range(spawnXMin, spawnXMax);
        return basePos;
    }
}
