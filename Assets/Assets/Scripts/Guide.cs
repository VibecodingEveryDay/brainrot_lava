using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Скрипт для управления направляющей линией к брейнроту.
/// Генерирует промежуточные точки по поверхности (raycast), чтобы линия следовала рельефу, а не уходила в текстуру.
/// Использует префаб TexturePanLine (LineRenderer без отдельного скрипта).
/// </summary>
[DefaultExecutionOrder(-100)]
public class Guide : MonoBehaviour
{
    [Header("Префабы")]
    [Tooltip("Префаб линии (TexturePanLine: Assets/YAH/TexturePanningAsset/Prefabs/TexturePanLine.prefab)")]
    [SerializeField] private GameObject linePrefab;
    
    [Header("Цели")]
    [Tooltip("Transform кнопки спавна брейнрота (не используется для линии: линия ведёт только к брейнротам не в placement).")]
    [SerializeField] private Transform spawnBrainrotButtonTransform;
    
    [Header("Оптимизация")]
    [Tooltip("Интервал обновления цели (секунды). Больше = меньше нагрузка на CPU")]
    [SerializeField] private float updateInterval = 0.25f;
    
    [Header("Поверхность")]
    [Tooltip("Максимум промежуточных точек (при большой дистанции до цели)")]
    [SerializeField] private int surfaceWaypointCountMax = 12;
    
    [Tooltip("Минимум промежуточных точек (при близкой дистанции до цели)")]
    [SerializeField] private int surfaceWaypointCountMin = 1;
    
    [Tooltip("Дистанция, при которой точек максимум")]
    [SerializeField] private float waypointDistanceFar = 30f;
    
    [Tooltip("Дистанция, при которой точек минимум (ближе — не меньше)")]
    [SerializeField] private float waypointDistanceNear = 3f;
    
    
    [Tooltip("Высота над траекторией для raycast вниз (поиск поверхности)")]
    [SerializeField] private float raycastHeight = 20f;
    
    [Tooltip("Максимальная дистанция raycast вниз")]
    [SerializeField] private float raycastMaxDistance = 50f;
    
    [Tooltip("Слой для raycast поверхности (-1 = всё)")]
    [SerializeField] private LayerMask surfaceLayerMask = -1;
    
    private GameObject lineInstance;
    private LineRenderer lineRenderer;
    private Transform currentEndPoint; // Текущая цель линии (для отрисовки и waypoints)
    private bool lineEnabled;
    private GameObject tempTargetObject; // Временный объект для позиции панели
    private GameObject waypointsContainer; // Контейнер для промежуточных точек
    private List<Transform> waypointTransforms = new List<Transform>();
    private PlayerCarryController playerCarryController;
    private bool forceUpdateTarget = false; // Флаг для принудительного обновления цели
    private float updateTimer = 0f;
    
    void Start()
    {
        // Находим PlayerCarryController для проверки состояния переноски
        FindPlayerCarryController();
        
        // Создаем направляющую линию сразу при старте (независимо от баланса)
        CreateGuidanceLine();
        
        // Подписываемся на события BattleManager
        SubscribeToBattleEvents();
    }
    
    void OnEnable()
    {
        SubscribeToBattleEvents();
    }
    
    void OnDisable()
    {
        UnsubscribeFromBattleEvents();
    }
    
    void SubscribeToBattleEvents()
    {
        // BattleManager удалён из проекта
    }
    
    void UnsubscribeFromBattleEvents()
    {
        // BattleManager удалён из проекта
    }
    
    /// <summary>
    /// Принудительно сбрасывает цель линии: к ближайшему брейнроту не в placement (если есть).
    /// </summary>
    void ForceResetToButton()
    {
        if (lineRenderer == null) return;
        
        Transform target = FindNearestBrainrot();
        
        if (target != null)
        {
            SetLineEnabled(true);
            currentEndPoint = target;
            UpdateSurfaceWaypoints(GetPlayerTransform()?.position ?? Vector3.zero, target.position);
        }
        else
        {
            SetLineEnabled(false);
        }
    }

    void FindPlayerCarryController()
    {
        if (playerCarryController == null)
            playerCarryController = FindFirstObjectByType<PlayerCarryController>();
    }
    
    bool HasBrainrotInHands()
    {
        if (playerCarryController == null) FindPlayerCarryController();
        return playerCarryController != null && playerCarryController.GetCurrentCarriedObject() != null;
    }
    
    void Update()
    {
        if (lineRenderer == null) return;
        
        if (forceUpdateTarget)
        {
            forceUpdateTarget = false;
            ForceResetToButton();
            updateTimer = 0f;
            return;
        }
        
        updateTimer += Time.deltaTime;
        if (updateTimer >= updateInterval)
        {
            updateTimer = 0f;
            UpdateGuidanceLineTarget();
        }
        
        if (lineEnabled && currentEndPoint != null)
        {
            Transform startTr = GetPlayerTransform();
            if (startTr != null)
            {
                Vector3 start = startTr.position;
                Vector3 end = currentEndPoint.position;
                bool directToPlacement = HasBrainrotInHands() && tempTargetObject != null && currentEndPoint == tempTargetObject.transform;
                UpdateSurfaceWaypoints(start, end, directToPlacement ? 0 : (int?)null, directToPlacement);
            }
        }
    }
    
    void CreateGuidanceLine()
    {
        if (linePrefab == null)
        {
            Debug.LogError("[Guide] Префаб линии (TexturePanLine) не назначен! Укажите Assets/YAH/TexturePanningAsset/Prefabs/TexturePanLine.prefab");
            return;
        }
        
        lineInstance = Instantiate(linePrefab);
        lineRenderer = lineInstance.GetComponent<LineRenderer>();
        
        if (lineRenderer == null)
        {
            Debug.LogError("[Guide] У префаба линии нет компонента LineRenderer!");
            return;
        }
        
        lineRenderer.useWorldSpace = true;
        currentEndPoint = null;
        lineEnabled = false;
        Debug.Log("[Guide] Направляющая линия создана (TexturePanLine)");
    }
    
    void UpdateGuidanceLineTarget()
    {
        if (lineRenderer == null)
            return;
        
        bool hasBrainrotInHands = false;
        if (playerCarryController == null)
            FindPlayerCarryController();
        if (playerCarryController != null)
            hasBrainrotInHands = playerCarryController.GetCurrentCarriedObject() != null;
        
        if (hasBrainrotInHands)
        {
            PlacementPanel targetPanel = FindNearestEmptyPlacement();
            if (targetPanel == null)
                targetPanel = FindNearestPlacement();
            
            if (targetPanel != null)
            {
                if (tempTargetObject == null)
                    tempTargetObject = new GameObject("Guide_TempTarget");
                tempTargetObject.transform.position = targetPanel.GetPlacementPosition();
                SetLineEnabled(true);
                if (currentEndPoint != tempTargetObject.transform)
                    currentEndPoint = tempTargetObject.transform;
                UpdateSurfaceWaypoints(GetPlayerTransform()?.position ?? Vector3.zero, tempTargetObject.transform.position);
            }
            else
            {
                SetLineEnabled(false);
            }
        }
        else
        {
            Transform target = FindNearestBrainrot();
            if (target != null)
            {
                SetLineEnabled(true);
                if (currentEndPoint != target)
                    currentEndPoint = target;
                UpdateSurfaceWaypoints(GetPlayerTransform()?.position ?? Vector3.zero, target.position);
            }
            else
            {
                SetLineEnabled(false);
            }
        }
    }
    
    /// <summary>
    /// Генерирует промежуточные точки по поверхности между start и end.
    /// «Шагает» по земле: каждая точка ищется raycast'ом вниз с учётом предыдущей точки,
    /// чтобы путь следовал рельефу и BoxCollider'ам земли, а не шёл по прямой через верх.
    /// </summary>
    void UpdateSurfaceWaypoints(Vector3 start, Vector3 end, int? waypointCountOverride = null, bool directLine = false)
    {
        if (lineRenderer == null)
            return;
        
        int count;
        if (waypointCountOverride.HasValue)
            count = Mathf.Clamp(waypointCountOverride.Value, 0, surfaceWaypointCountMax);
        else
        {
            float distance = Vector3.Distance(start, end);
            count = Mathf.RoundToInt(Mathf.Lerp(
                surfaceWaypointCountMin,
                surfaceWaypointCountMax,
                Mathf.InverseLerp(waypointDistanceNear, waypointDistanceFar, distance)
            ));
            count = Mathf.Clamp(count, 0, surfaceWaypointCountMax);
        }
        
        if (count <= 0 && !directLine)
        {
            ApplyLinePositions(start, end, null);
            return;
        }
        
        if (directLine)
            count = 1;
        
        if (waypointsContainer == null)
        {
            waypointsContainer = new GameObject("Guide_Waypoints");
            waypointsContainer.transform.SetParent(lineInstance != null ? lineInstance.transform : transform);
        }
        
        while (waypointTransforms.Count > count)
        {
            int last = waypointTransforms.Count - 1;
            if (waypointTransforms[last] != null) Destroy(waypointTransforms[last].gameObject);
            waypointTransforms.RemoveAt(last);
        }
        while (waypointTransforms.Count < count)
        {
            var wp = new GameObject($"Guide_Waypoint_{waypointTransforms.Count + 1}");
            wp.transform.SetParent(waypointsContainer.transform);
            waypointTransforms.Add(wp.transform);
        }
        
        Vector3 currentGround = GetSurfacePosition(new Vector3(start.x, start.y + raycastHeight, start.z), start.y);
        float totalXZ = Vector2.Distance(new Vector2(start.x, start.z), new Vector2(end.x, end.z));
        
        if (totalXZ < 0.001f && !directLine)
        {
            ApplyLinePositions(start, end, null);
            return;
        }
        
        if (directLine)
        {
            waypointTransforms[0].position = Vector3.Lerp(start, end, 0.5f);
        }
        else
        {
            float heightCap = Mathf.Min(start.y, end.y);
            for (int i = 1; i <= count; i++)
            {
                float t = (float)i / (count + 1);
                Vector2 nextXZ = Vector2.Lerp(new Vector2(start.x, start.z), new Vector2(end.x, end.z), t);
                float rayStartY = Mathf.Max(currentGround.y, heightCap) + raycastHeight;
                Vector3 rayStart = new Vector3(nextXZ.x, rayStartY, nextXZ.y);
                Vector3 surfacePos = GetSurfacePosition(rayStart, heightCap);
                currentGround = surfacePos;
                waypointTransforms[i - 1].position = surfacePos;
            }
        }
        
        ApplyLinePositions(start, end, waypointTransforms);
    }
    
    /// <summary>
    /// Записывает позиции [start, ...waypoints..., end] в LineRenderer (TexturePanLine).
    /// </summary>
    void ApplyLinePositions(Vector3 start, Vector3 end, List<Transform> waypoints)
    {
        if (lineRenderer == null) return;
        int n = (waypoints != null ? waypoints.Count : 0);
        int total = 2 + n;
        lineRenderer.positionCount = total;
        lineRenderer.SetPosition(0, start);
        for (int i = 0; i < n; i++)
            lineRenderer.SetPosition(1 + i, waypoints[i].position);
        lineRenderer.SetPosition(total - 1, end);
    }
    
    /// <summary>
    /// Raycast вниз для поиска поверхности. preferMaxY — искать пол у этого уровня (для домов: не цеплять потолок).
    /// </summary>
    Vector3 GetSurfacePosition(Vector3 fromAbove, float? preferMaxY = null)
    {
        RaycastHit[] hits = Physics.RaycastAll(fromAbove, Vector3.down, raycastMaxDistance, surfaceLayerMask);
        if (hits.Length == 0)
            return new Vector3(fromAbove.x, fromAbove.y - raycastHeight, fromAbove.z);
        if (!preferMaxY.HasValue)
        {
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            return hits[0].point;
        }
        // Внутри меша (дом): берём пол на уровне игрока, а не потолок
        float bestY = float.MinValue;
        RaycastHit? best = null;
        foreach (var h in hits)
        {
            if (h.point.y <= preferMaxY.Value + 0.5f && h.point.y > bestY)
            {
                bestY = h.point.y;
                best = h;
            }
        }
        if (best.HasValue) return best.Value.point;
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        return hits[0].point;
    }
    
    void SetLineEnabled(bool enabled)
    {
        lineEnabled = enabled;
        if (lineInstance == null) return;
        if (lineRenderer != null)
            lineRenderer.enabled = enabled;
        if (!enabled)
            currentEndPoint = null;
    }
    
    Transform FindNearestBrainrot()
    {
        BrainrotObject[] allBrainrots = FindObjectsByType<BrainrotObject>(FindObjectsSortMode.None);
        if (allBrainrots == null || allBrainrots.Length == 0)
            return null;
        
        Transform playerTransform = GetPlayerTransform();
        if (playerTransform == null)
            return null;
        
        float minDistance = float.MaxValue;
        BrainrotObject closest = null;
        foreach (BrainrotObject brainrot in allBrainrots)
        {
            if (brainrot.IsCarried() || brainrot.IsPlaced())
                continue;
            float distance = Vector3.Distance(playerTransform.position, brainrot.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = brainrot;
            }
        }
        return closest?.transform;
    }
    
    
    Transform GetPlayerTransform()
    {
        if (playerCarryController != null)
            return playerCarryController.GetPlayerTransform();
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform : null;
    }
    
    PlacementPanel FindNearestEmptyPlacement()
    {
        // Находим все панели размещения в сцене
        PlacementPanel[] allPanels = FindObjectsByType<PlacementPanel>(FindObjectsSortMode.None);
        
        if (allPanels == null || allPanels.Length == 0)
        {
            return null;
        }
        
        Transform playerTransform = GetPlayerTransform();
        if (playerTransform == null)
            return null;
        
        float minDistance = float.MaxValue;
        PlacementPanel closest = null;
        foreach (PlacementPanel panel in allPanels)
        {
            if (panel == null)
                continue;
            if (panel.GetPlacedBrainrot() != null)
                continue;
            float distance = Vector3.Distance(playerTransform.position, panel.GetPlacementPosition());
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = panel;
            }
        }
        return closest;
    }
    
    /// <summary>Ближайшая панель размещения (пустая или занятая).</summary>
    PlacementPanel FindNearestPlacement()
    {
        PlacementPanel[] allPanels = FindObjectsByType<PlacementPanel>(FindObjectsSortMode.None);
        if (allPanels == null || allPanels.Length == 0)
            return null;
        
        Transform playerTransform = GetPlayerTransform();
        if (playerTransform == null)
            return null;
        
        float minDistance = float.MaxValue;
        PlacementPanel closest = null;
        foreach (PlacementPanel panel in allPanels)
        {
            if (panel == null)
                continue;
            float distance = Vector3.Distance(playerTransform.position, panel.GetPlacementPosition());
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = panel;
            }
        }
        return closest;
    }
    
    void OnDestroy()
    {
        UnsubscribeFromBattleEvents();
        
        if (lineInstance != null)
        {
            Destroy(lineInstance);
            lineInstance = null;
            lineRenderer = null;
        }
        currentEndPoint = null;
        
        if (tempTargetObject != null)
        {
            Destroy(tempTargetObject);
            tempTargetObject = null;
        }
        
        if (waypointsContainer != null)
        {
            Destroy(waypointsContainer);
            waypointsContainer = null;
        }
    }
}
