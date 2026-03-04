using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Спавнит брейнроты на плоскости один раз при старте сцены.
/// Вешается на пустой объект; плоскость задаётся параметрами (X, Z), редкости — списком шансов с нормализацией до 100%.
/// </summary>
public class PlaneBrSpawner : MonoBehaviour
{
    [System.Serializable]
    public struct RarityChanceEntry
    {
        [Tooltip("Редкость: Common, Rare, Exclusive, Epic, Mythic, Legendary, Secret")]
        public string rarity;
        [Tooltip("0 = 0%, положительное = шанс в %, отрицательное = вес для распределения остатка до 100%")]
        public float chance;
    }

    [Header("Плоскость спавна")]
    [Tooltip("Размер плоскости по локальной оси X")]
    [SerializeField] private float spawnPlaneSizeX = 10f;
    [Tooltip("Размер плоскости по локальной оси Z")]
    [SerializeField] private float spawnPlaneSizeZ = 10f;
    [Tooltip("Смещение Y позиции спавна относительно объекта")]
    [SerializeField] private float spawnHeightOffset = 0f;

    [Header("Количество и расстояние")]
    [Tooltip("Минимальное количество брейнротов")]
    [SerializeField] private int minCount = 1;
    [Tooltip("Максимальное количество брейнротов")]
    [SerializeField] private int maxCount = 5;
    [Tooltip("Минимальное расстояние между брейнротами по XZ; ≤0 — не проверять")]
    [SerializeField] private float minOffset = 0f;
    [Tooltip("Максимум попыток найти точку с учётом minOffset для одного объекта")]
    [SerializeField] private int maxPlacementAttempts = 100;

    [Header("Базовый доход (baseIncome)")]
    [Tooltip("Минимальный baseIncome заспавненного брейнрота")]
    [SerializeField] private long baseIncomeMin = 1L;
    [Tooltip("Максимальный baseIncome заспавненного брейнрота")]
    [SerializeField] private long baseIncomeMax = 100L;

    [Header("Шансы редкостей (0 = 0%, >0 = %, <0 = вес остатка до 100%)")]
    [Tooltip("Common")]
    [SerializeField] private float chanceCommon = -1f;
    [Tooltip("Rare")]
    [SerializeField] private float chanceRare = -1f;
    [Tooltip("Exclusive")]
    [SerializeField] private float chanceExclusive = 0f;
    [Tooltip("Epic")]
    [SerializeField] private float chanceEpic = 0f;
    [Tooltip("Mythic")]
    [SerializeField] private float chanceMythic = 0f;
    [Tooltip("Legendary")]
    [SerializeField] private float chanceLegendary = 0f;
    [Tooltip("Secret")]
    [SerializeField] private float chanceSecret = 0f;

    private List<Vector3> _spawnedPositions = new List<Vector3>();
    private List<GameObject> _spawnedInstances = new List<GameObject>();
    private List<GameObject> _prefabs = new List<GameObject>();
    private List<string> _normalizedRarities = new List<string>();
    private List<float> _normalizedCumulative = new List<float>();

    private const string BrainrotsResourcePath = "game/Brainrots";
    private static List<GameObject> _cachedPrefabs;

    private void Start()
    {
        LoadPrefabs();
        if (_prefabs.Count == 0)
        {
            Debug.LogWarning("[PlaneBrSpawner] Нет префабов с BrainrotObject в Resources/" + BrainrotsResourcePath, this);
            return;
        }

        BuildNormalizedRarityChances();
        if (_normalizedRarities.Count == 0)
        {
            Debug.LogWarning("[PlaneBrSpawner] Нет валидных шансов редкостей — используем Common по умолчанию.", this);
            _normalizedRarities.Add("Common");
            _normalizedCumulative.Add(1f);
        }

        int count = Mathf.Clamp(Random.Range(minCount, maxCount + 1), 0, 1000);
        _spawnedPositions.Clear();

        for (int i = 0; i < count; i++)
        {
            if (!TrySpawnOne(out _))
            {
                Debug.LogWarning($"[PlaneBrSpawner] Не удалось разместить брейнрот {i + 1}/{count} после {maxPlacementAttempts} попыток (minOffset={minOffset}).", this);
            }
        }
    }

    private void LoadPrefabs()
    {
        _prefabs.Clear();
        if (_cachedPrefabs != null && _cachedPrefabs.Count > 0)
        {
            _prefabs.AddRange(_cachedPrefabs);
            return;
        }
        GameObject[] all = Resources.LoadAll<GameObject>(BrainrotsResourcePath);
        foreach (GameObject go in all)
        {
            if (go != null && go.GetComponent<BrainrotObject>() != null)
                _prefabs.Add(go);
        }
        _cachedPrefabs = new List<GameObject>(_prefabs);
    }

    /// <summary>
    /// Нормализует шансы: 0 остаётся 0, положительные — как есть, отрицательные распределяют остаток до 100%.
    /// </summary>
    private void BuildNormalizedRarityChances()
    {
        _normalizedRarities.Clear();
        _normalizedCumulative.Clear();

        var entries = new List<RarityChanceEntry>
        {
            new RarityChanceEntry { rarity = "Common", chance = chanceCommon },
            new RarityChanceEntry { rarity = "Rare", chance = chanceRare },
            new RarityChanceEntry { rarity = "Exclusive", chance = chanceExclusive },
            new RarityChanceEntry { rarity = "Epic", chance = chanceEpic },
            new RarityChanceEntry { rarity = "Mythic", chance = chanceMythic },
            new RarityChanceEntry { rarity = "Legendary", chance = chanceLegendary },
            new RarityChanceEntry { rarity = "Secret", chance = chanceSecret }
        };

        float sumPositive = 0f;
        var negativeIndices = new List<int>();
        var negativeWeights = new List<float>();

        for (int i = 0; i < entries.Count; i++)
        {
            float v = entries[i].chance;
            if (v > 0f)
                sumPositive += v;
            else if (v < 0f)
            {
                negativeIndices.Add(i);
                negativeWeights.Add(-v);
            }
        }

        float remainder = 100f - sumPositive;
        if (remainder < 0f)
            remainder = 0f;

        float negSum = 0f;
        foreach (float w in negativeWeights)
            negSum += w;

        var finalChances = new List<float>();
        for (int i = 0; i < entries.Count; i++)
        {
            float v = entries[i].chance;
            float pct;
            if (v > 0f)
                pct = v;
            else if (v < 0f)
            {
                int idx = negativeIndices.IndexOf(i);
                float w = idx >= 0 ? negativeWeights[idx] : 0f;
                pct = negSum > 0f ? (w / negSum) * remainder : (negativeWeights.Count > 0 ? remainder / negativeWeights.Count : 0f);
            }
            else
                pct = 0f;

            if (pct > 0f && !string.IsNullOrEmpty(entries[i].rarity))
            {
                _normalizedRarities.Add(entries[i].rarity);
                finalChances.Add(pct);
            }
        }

        float total = 0f;
        foreach (float p in finalChances)
            total += p;
        if (total <= 0f)
            return;

        float cum = 0f;
        for (int i = 0; i < finalChances.Count; i++)
        {
            cum += finalChances[i] / total;
            _normalizedCumulative.Add(cum);
        }
    }

    private string PickRarity()
    {
        if (_normalizedRarities.Count == 0)
            return "Common";
        float r = Random.value;
        for (int i = 0; i < _normalizedCumulative.Count; i++)
        {
            if (r <= _normalizedCumulative[i])
                return _normalizedRarities[i];
        }
        return _normalizedRarities[_normalizedRarities.Count - 1];
    }

    private Vector3 GetRandomPointOnPlane()
    {
        float x = Random.Range(-spawnPlaneSizeX * 0.5f, spawnPlaneSizeX * 0.5f);
        float z = Random.Range(-spawnPlaneSizeZ * 0.5f, spawnPlaneSizeZ * 0.5f);
        Vector3 local = new Vector3(x, 0f, z);
        Vector3 world = transform.TransformPoint(local);
        world.y = transform.position.y + spawnHeightOffset;
        return world;
    }

    private static float DistanceXZ(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private bool IsPointValidForMinOffset(Vector3 point)
    {
        if (minOffset <= 0f)
            return true;
        foreach (Vector3 other in _spawnedPositions)
        {
            if (DistanceXZ(point, other) < minOffset)
                return false;
        }
        return true;
    }

    /// <summary>
    /// Пытается заспавнить один брейнрот. Возвращает true при успехе.
    /// </summary>
    private bool TrySpawnOne(out Vector3 position)
    {
        position = default;

        Vector3 pos = default;
        bool found = false;
        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            pos = GetRandomPointOnPlane();
            if (IsPointValidForMinOffset(pos))
            {
                found = true;
                break;
            }
        }

        if (!found)
            return false;

        string rarity = PickRarity();
        GameObject prefab = _prefabs[Random.Range(0, _prefabs.Count)];
        Quaternion rot = transform.rotation * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        GameObject instance = Instantiate(prefab, pos, rot);

        BrainrotObject br = instance.GetComponent<BrainrotObject>();
        if (br != null)
        {
            br.SetRarity(rarity);
            long range = baseIncomeMax - baseIncomeMin + 1;
            long income = range <= 0 ? baseIncomeMin : baseIncomeMin + (long)(Random.value * range);
            if (income > baseIncomeMax) income = baseIncomeMax;
            br.SetBaseIncome(income);
        }

        _spawnedPositions.Add(pos);
        _spawnedInstances.Add(instance);
        position = pos;
        return true;
    }

    /// <summary>
    /// Уничтожает текущих заспавненных брейнротов и спавнит новых (то же количество и логика, что в Start).
    /// </summary>
    public void RespawnAll()
    {
        for (int i = 0; i < _spawnedInstances.Count; i++)
        {
            GameObject go = _spawnedInstances[i];
            if (go == null) continue;
            BrainrotObject br = go.GetComponent<BrainrotObject>();
            if (br != null && br.IsCarried())
                continue;
            Destroy(go);
        }
        _spawnedInstances.Clear();
        _spawnedPositions.Clear();

        if (_prefabs.Count == 0)
        {
            LoadPrefabs();
            if (_prefabs.Count == 0) return;
        }
        if (_normalizedRarities.Count == 0)
        {
            BuildNormalizedRarityChances();
            if (_normalizedRarities.Count == 0)
            {
                _normalizedRarities.Add("Common");
                _normalizedCumulative.Add(1f);
            }
        }

        int count = Mathf.Clamp(Random.Range(minCount, maxCount + 1), 0, 1000);
        for (int i = 0; i < count; i++)
            TrySpawnOne(out _);
    }

    /// <summary>
    /// Респавнит брейнротов во всех PlaneBrSpawner на сцене.
    /// </summary>
    public static void RespawnAllSpawnersInScene()
    {
        PlaneBrSpawner[] spawners = FindObjectsByType<PlaneBrSpawner>(FindObjectsSortMode.None);
        for (int i = 0; i < spawners.Length; i++)
        {
            if (spawners[i] != null)
                spawners[i].RespawnAll();
        }
        Guide.InvalidateAllGuidesCache();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 c = transform.position;
        Vector3 r = transform.right * (spawnPlaneSizeX * 0.5f);
        Vector3 f = transform.forward * (spawnPlaneSizeZ * 0.5f);
        Gizmos.color = new Color(1f, 0f, 0f, 0.5f);
        Gizmos.DrawLine(c - r - f, c + r - f);
        Gizmos.DrawLine(c + r - f, c + r + f);
        Gizmos.DrawLine(c + r + f, c - r + f);
        Gizmos.DrawLine(c - r + f, c - r - f);
    }
#endif
}
