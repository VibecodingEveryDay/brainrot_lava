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
    
    [Header("Guidance Dots")]
    [Tooltip("Transform родителя объектов-точек (точки могут быть прямыми детьми или вложенными). Имена: N_M (N — номер плоскости, M — 1=начало плоскости, 2=конец). Линия строится через эти чекпоинты.")]
    [SerializeField] private Transform guidanceDotsRoot;
    [Tooltip("Если |Y игрока - Y цели| меньше этого значения, игнорируем GuidanceDots и ведём напрямую (2 точки). 0 = всегда через точки.")]
    [SerializeField] private float guidanceDotsMaxYDifference = 50f;
    
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
    
    
    [Tooltip("Высота над точкой траектории для raycast вниз (не слишком большая, чтобы не цеплять потолок)")]
    [SerializeField] private float raycastHeight = 4f;
    
    [Tooltip("Максимальная дистанция raycast вниз")]
    [SerializeField] private float raycastMaxDistance = 50f;
    
    [Tooltip("Слой для raycast поверхности (-1 = всё)")]
    [SerializeField] private LayerMask surfaceLayerMask = -1;
    
    [Header("Ступеньки")]
    [Tooltip("Порог по Y между точками, при котором считаем участок ступеньками")]
    [SerializeField] private float stairsYThreshold = 0.08f;
    
    [Tooltip("Добавлять к Y точек на ступеньках, чтобы линия шла чуть выше ступенек")]
    [SerializeField] private float groundStairsOffsetY = 0.15f;
    
    [Tooltip("Минимальный normal.y поверхности, чтобы считать её «ходовой» (верх ступеньки, не пол под ней)")]
    [SerializeField] private float walkableNormalY = 0.5f;
    
    [Tooltip("Максимум метров вниз от луча: не брать поверхность глубже (чтобы не цеплять пол под лестницей)")]
    [SerializeField] private float raycastMaxDrop = 8f;
    
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
    private BrainrotObject[] _cachedBrainrots;
    private PlacementPanel[] _cachedPanels;
    private float _guideCacheTime = -999f;
    private const float GUIDE_CACHE_LIFETIME = 1.5f;
    
    private struct GuidanceDotEntry
    {
        public int plane;
        public int type; // 1 = начало плоскости, 2 = конец плоскости
        public Transform transform;
    }
    private List<GuidanceDotEntry> _guidanceDotsCache = new List<GuidanceDotEntry>();
    private bool _guidanceDotsCacheValid;
    
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
                UpdateSurfaceWaypoints(start, end);
            }
        }
    }
    
    void RefreshGuidanceDotsCache()
    {
        _guidanceDotsCache.Clear();
        _guidanceDotsCacheValid = false;
        if (guidanceDotsRoot == null) return;
        Transform[] all = guidanceDotsRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t == guidanceDotsRoot) continue;
            string name = t.name;
            string[] parts = name.Split('_');
            if (parts.Length < 2) continue;
            if (!int.TryParse(parts[0].Trim(), out int plane) || !int.TryParse(parts[1].Trim(), out int type)) continue;
            if (type != 1 && type != 2) continue;
            _guidanceDotsCache.Add(new GuidanceDotEntry { plane = plane, type = type, transform = t });
        }
        _guidanceDotsCache.Sort((a, b) => a.plane != b.plane ? a.plane.CompareTo(b.plane) : a.type.CompareTo(b.type));
        _guidanceDotsCacheValid = true;
        if (_guidanceDotsCache.Count > 0)
            Debug.Log($"[Guide] GuidanceDots: загружено {_guidanceDotsCache.Count} точек (родитель: {guidanceDotsRoot.name})");
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
    /// Строит путь через чекпоинты GuidanceDots: start → точки от плоскости старта до плоскости цели → end.
    /// </summary>
    List<Vector3> BuildPathViaGuidanceDots(Vector3 start, Vector3 end)
    {
        if (!_guidanceDotsCacheValid || _guidanceDotsCache.Count == 0)
            RefreshGuidanceDotsCache();
        if (_guidanceDotsCache.Count == 0)
            return null;
        if (guidanceDotsMaxYDifference > 0f && Mathf.Abs(start.y - end.y) <= guidanceDotsMaxYDifference)
        {
            return new List<Vector3> { start, end };
        }
        int startPlane = -1, startType = -1, endPlane = -1, endType = -1;
        float bestStartSq = float.MaxValue, bestEndSq = float.MaxValue;
        float minDistDotToTargetSq = float.MaxValue;
        for (int i = 0; i < _guidanceDotsCache.Count; i++)
        {
            var e = _guidanceDotsCache[i];
            if (e.transform == null) continue;
            Vector3 dotPos = e.transform.position;
            float dStartSq = (dotPos - start).sqrMagnitude;
            float dEndSq = (dotPos - end).sqrMagnitude;
            if (dStartSq < bestStartSq) { bestStartSq = dStartSq; startPlane = e.plane; startType = e.type; }
            if (dEndSq < bestEndSq) { bestEndSq = dEndSq; endPlane = e.plane; endType = e.type; }
            if (dEndSq < minDistDotToTargetSq) minDistDotToTargetSq = dEndSq;
        }
        if (startPlane < 0 || endPlane < 0) return null;
        float distPlayerToTargetSq = (end - start).sqrMagnitude;
        if (distPlayerToTargetSq <= minDistDotToTargetSq)
        {
            var two = new List<Vector3> { start, end };
            return two;
        }
        var seq = BuildOrderedSequence(startPlane, startType, endPlane, endType);
        List<Vector3> positions = new List<Vector3>();
        positions.Add(start);
        foreach (var key in seq)
        {
            var e = FindDot(key.plane, key.type);
            if (e.transform != null)
                positions.Add(e.transform.position);
        }
        positions.Add(end);
        return positions;
    }
    
    /// <summary>
    /// Цепочка: от ближайшей к игроку точки до ближайшей к цели.
    /// Вниз (6→1): 6_2→6_1→5_2→5_1→…→1_2→1_1. Вверх (1→6): 1_2→2_1→2_2→…→6_1 (без 6_2 если цель у 6_1).
    /// </summary>
    List<(int plane, int type)> BuildOrderedSequence(int startPlane, int startType, int endPlane, int endType)
    {
        var list = new List<(int plane, int type)>();
        if (startPlane == endPlane)
        {
            // Одна и та же плоскость:
            // 1_1 -> 1_2  =>  [1_1, 1_2]
            // 1_2 -> 1_1  =>  [1_2, 1_1]
            // 1_1 -> 1_1  =>  [1_1]
            // 1_2 -> 1_2  =>  [1_2]
            if (startType == 1 && endType == 2)
            {
                list.Add((endPlane, 1));
                list.Add((endPlane, 2));
            }
            else if (startType == 2 && endType == 1)
            {
                list.Add((endPlane, 2));
                list.Add((endPlane, 1));
            }
            else if (startType == endType)
            {
                list.Add((endPlane, endType));
            }
            return list;
        }
        if (startPlane > endPlane)
        {
            if (startType == 2)
                list.Add((startPlane, 2));
            list.Add((startPlane, 1));
            for (int p = startPlane - 1; p >= endPlane; p--)
            {
                list.Add((p, 2));
                list.Add((p, 1));
            }
        }
        else
        {
            list.Add((startPlane, 2));
            for (int p = startPlane + 1; p <= endPlane - 1; p++)
            {
                list.Add((p, 1));
                list.Add((p, 2));
            }
            list.Add((endPlane, 1));
            if (endType == 2) list.Add((endPlane, 2));
        }
        return list;
    }
    
    GuidanceDotEntry FindDot(int plane, int type)
    {
        for (int i = 0; i < _guidanceDotsCache.Count; i++)
        {
            var e = _guidanceDotsCache[i];
            if (e.plane == plane && e.type == type) return e;
        }
        return default;
    }
    
    /// <summary>
    /// Строит точки по поверхности между start и end (переработка с нуля).
    /// Цель выше: start → промежуточная (Y = end.y) → end (последний отрезок горизонтальный).
    /// Цель ниже: start → промежуточная (end.x, start.y, end.z) → end (сначала плоскость, потом спуск).
    /// </summary>
    void UpdateSurfaceWaypoints(Vector3 start, Vector3 end)
    {
        if (lineRenderer == null)
            return;
        
        float totalDist = Vector3.Distance(start, end);
        if (totalDist < 0.001f)
        {
            lineRenderer.positionCount = 2;
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
            return;
        }
        
        if (guidanceDotsRoot != null)
        {
            List<Vector3> path = BuildPathViaGuidanceDots(start, end);
            if (path != null && path.Count > 0)
            {
                ApplyLinePositions(path);
                return;
            }
        }
        
        List<Vector3> positions = new List<Vector3>();
        positions.Add(start);
        
        const float ySameThreshold = 0.1f;
        bool targetAbove = end.y > start.y + ySameThreshold;
        bool targetBelow = end.y < start.y - ySameThreshold;
        
        if (targetAbove)
        {
            Vector3 intermediate = FindIntermediateAtTargetHeight(start, end);
            positions.Add(intermediate);
            positions.Add(end);
        }
        else if (targetBelow)
        {
            Vector3 intermediate = GetSurfaceAbove(end, start.y);
            positions.Add(intermediate);
            positions.Add(end);
        }
        else
        {
            positions.Add(end);
        }
        
        ApplyStairsOffset(positions);
        ApplyLinePositions(positions);
    }
    
    /// <summary>
    /// Точка над целью на высоте baseY, спроецированная на поверхность (край обрыва/лестницы).
    /// </summary>
    Vector3 GetSurfaceAbove(Vector3 end, float baseY)
    {
        Vector3 above = new Vector3(end.x, baseY, end.z);
        Vector3 rayOrigin = above + Vector3.up * raycastHeight;
        Vector3 surface = GetSurfacePosition(rayOrigin, null);
        return new Vector3(surface.x, surface.y, surface.z);
    }
    
    /// <summary>
    /// Ищет вдоль отрезка start→end точку «вершины подъёма»: первый локальный максимум поверхности (конец лестницы).
    /// После подъёма при первом спаде Y возвращаем вершину + offset, чтобы линия не пронзала ступени и не уходила на платформу.
    /// </summary>
    Vector3 FindIntermediateAtTargetHeight(Vector3 start, Vector3 end)
    {
        const int steps = 48;
        const float dropThreshold = 0.06f; // спад выше порога = прошли вершину
        const float maxAboveTarget = 0.1f;
        float bestSurfaceY = float.MinValue;
        Vector3 bestSurface = new Vector3(end.x, end.y, end.z);
        float prevSurfaceY = float.MinValue;
        for (int i = 1; i <= steps; i++)
        {
            float t = (float)i / (steps + 1);
            Vector3 onSegment = Vector3.Lerp(start, end, t);
            Vector3 rayOrigin = onSegment + Vector3.up * raycastHeight;
            Vector3 surface = GetSurfacePosition(rayOrigin, null);
            float sy = surface.y;
            // Не учитываем поверхность выше цели (платформа за лестницей)
            if (sy > end.y + maxAboveTarget)
                continue;
            if (sy > bestSurfaceY)
            {
                bestSurfaceY = sy;
                bestSurface = surface;
            }
            // Первый спад после подъёма — мы прошли вершину лестницы
            if (prevSurfaceY > float.MinValue && bestSurfaceY > start.y + dropThreshold && sy < bestSurfaceY - dropThreshold)
            {
                Vector3 top = bestSurface;
                top.y += groundStairsOffsetY;
                return top;
            }
            prevSurfaceY = sy;
        }
        // Не нашли спад — возвращаем лучшую точку не выше цели, с offset
        Vector3 fallback = bestSurface;
        fallback.y += groundStairsOffsetY;
        return fallback;
    }
    
    /// <summary>
    /// Записывает список позиций в LineRenderer.
    /// </summary>
    void ApplyLinePositions(List<Vector3> positions)
    {
        if (lineRenderer == null || positions == null || positions.Count == 0) return;
        lineRenderer.positionCount = positions.Count;
        for (int i = 0; i < positions.Count; i++)
            lineRenderer.SetPosition(i, positions[i]);
    }
    
    /// <summary>
    /// Записывает позиции [start, ...waypoints..., end] в LineRenderer (TexturePanLine). Оставлено для совместимости.
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
    /// Raycast вниз для поиска поверхности.
    /// Без preferMaxY: берём самую НИЗКУЮ ходовую поверхность (пол/ступеньки), чтобы не цеплять потолок и не строить треугольник через верх.
    /// С preferMaxY (под потолком): пол не выше лимита.
    /// </summary>
    Vector3 GetSurfacePosition(Vector3 fromAbove, float? preferMaxY = null)
    {
        RaycastHit[] rawHits = Physics.RaycastAll(fromAbove, Vector3.down, raycastMaxDistance, surfaceLayerMask);
        var filtered = new List<RaycastHit>();
        foreach (var h in rawHits)
        {
            if (h.collider != null && !h.collider.gameObject.CompareTag("Ball"))
                filtered.Add(h);
        }
        RaycastHit[] hits = filtered.ToArray();
        if (hits.Length == 0)
            return new Vector3(fromAbove.x, fromAbove.y - raycastHeight, fromAbove.z);
        if (!preferMaxY.HasValue)
        {
            float rayOriginY = fromAbove.y;
            float minAllowedY = rayOriginY - raycastMaxDrop;
            // Ходовая = нормаль вверх. Берём самую ВЫСОКУЮ ходовую под лучом в пределах raycastMaxDrop (верх ступеньки, не пол под лестницей)
            RaycastHit? walkable = null;
            float bestY = float.MinValue;
            foreach (var h in hits)
            {
                if (h.normal.y >= walkableNormalY && h.point.y < rayOriginY - 0.05f && h.point.y >= minAllowedY && h.point.y > bestY)
                {
                    bestY = h.point.y;
                    walkable = h;
                }
            }
            if (walkable.HasValue)
                return walkable.Value.point;
            // Без ограничения по глубине — самая высокая ходовая под лучом
            walkable = null;
            bestY = float.MinValue;
            foreach (var h in hits)
            {
                if (h.normal.y >= walkableNormalY && h.point.y < rayOriginY - 0.05f && h.point.y > bestY)
                {
                    bestY = h.point.y;
                    walkable = h;
                }
            }
            if (walkable.HasValue)
                return walkable.Value.point;
            // Fallback: самый высокий hit в пределах maxDrop (не уходить в подпол)
            RaycastHit? fallback = null;
            bestY = float.MinValue;
            foreach (var h in hits)
            {
                if (h.point.y >= minAllowedY && h.point.y > bestY)
                {
                    bestY = h.point.y;
                    fallback = h;
                }
            }
            if (fallback.HasValue)
                return fallback.Value.point;
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            return hits[0].point;
        }
        // Под потолком (дом): берём пол не выше preferMaxY + 0.5f, из них — самый высокий (пол под ногами)
        float capBestY = float.MinValue;
        RaycastHit? best = null;
        foreach (var h in hits)
        {
            if (h.point.y <= preferMaxY.Value + 0.5f && h.point.y > capBestY && h.normal.y >= walkableNormalY)
            {
                capBestY = h.point.y;
                best = h;
            }
        }
        if (best.HasValue) return best.Value.point;
        // Fallback без требования по normal
        foreach (var h in hits)
        {
            if (h.point.y <= preferMaxY.Value + 0.5f && h.point.y > capBestY)
            {
                capBestY = h.point.y;
                best = h;
            }
        }
        if (best.HasValue) return best.Value.point;
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        return hits[0].point;
    }
    
    /// <summary>
    /// Определяет участки по ступенькам (когда Y соседних точек заметно отличается) и добавляет groundStairsOffsetY к этим точкам.
    /// </summary>
    void ApplyStairsOffset(Vector3 start, Vector3 end, List<Transform> waypoints)
    {
        if (waypoints == null || waypoints.Count == 0 || groundStairsOffsetY <= 0f) return;

        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;
            float y = waypoints[i].position.y;
            float prevY = i == 0 ? start.y : waypoints[i - 1].position.y;
            float nextY = i == waypoints.Count - 1 ? end.y : waypoints[i + 1].position.y;
            bool onStairs = Mathf.Abs(y - prevY) > stairsYThreshold || Mathf.Abs(y - nextY) > stairsYThreshold;
            if (onStairs)
            {
                Vector3 p = waypoints[i].position;
                p.y += groundStairsOffsetY;
                waypoints[i].position = p;
            }
        }
    }
    
    /// <summary>
    /// Смещение по Y для точек на ступеньках (in-place по списку позиций).
    /// </summary>
    void ApplyStairsOffset(List<Vector3> positions)
    {
        if (positions == null || positions.Count < 3 || groundStairsOffsetY <= 0f) return;
        for (int i = 1; i < positions.Count - 1; i++)
        {
            float y = positions[i].y;
            float prevY = positions[i - 1].y;
            float nextY = positions[i + 1].y;
            bool onStairs = Mathf.Abs(y - prevY) > stairsYThreshold || Mathf.Abs(y - nextY) > stairsYThreshold;
            if (onStairs)
            {
                Vector3 p = positions[i];
                p.y += groundStairsOffsetY;
                positions[i] = p;
            }
        }
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
    
    void RefreshGuideCacheIfNeeded()
    {
        if (Time.time - _guideCacheTime >= GUIDE_CACHE_LIFETIME)
        {
            _cachedBrainrots = FindObjectsByType<BrainrotObject>(FindObjectsSortMode.None);
            _cachedPanels = FindObjectsByType<PlacementPanel>(FindObjectsSortMode.None);
            _guideCacheTime = Time.time;
        }
    }

    /// <summary>
    /// Сбросить кэш брейнротов (вызывать после респавна, чтобы не обращаться к уничтоженным объектам).
    /// </summary>
    public void InvalidateBrainrotCache()
    {
        _guideCacheTime = -999f;
    }

    /// <summary>
    /// Сбросить кэш у всех Guide на сцене (вызвать после респавна брейнротов).
    /// </summary>
    public static void InvalidateAllGuidesCache()
    {
        Guide[] guides = FindObjectsByType<Guide>(FindObjectsSortMode.None);
        for (int i = 0; i < guides.Length; i++)
        {
            if (guides[i] != null)
                guides[i].InvalidateBrainrotCache();
        }
    }

    Transform FindNearestBrainrot()
    {
        RefreshGuideCacheIfNeeded();
        BrainrotObject[] allBrainrots = _cachedBrainrots;
        if (allBrainrots == null || allBrainrots.Length == 0)
            return null;

        Transform playerTransform = GetPlayerTransform();
        if (playerTransform == null)
            return null;
        
        float minDistance = float.MaxValue;
        BrainrotObject closest = null;
        foreach (BrainrotObject brainrot in allBrainrots)
        {
            if (brainrot == null)
                continue;
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
        RefreshGuideCacheIfNeeded();
        PlacementPanel[] allPanels = _cachedPanels;
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
        RefreshGuideCacheIfNeeded();
        PlacementPanel[] allPanels = _cachedPanels;
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
