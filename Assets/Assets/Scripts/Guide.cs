using UnityEngine;

/// <summary>
/// Скрипт для управления направляющей линией к брейнроту
/// </summary>
public class Guide : MonoBehaviour
{
    [Header("Префабы")]
    [Tooltip("Префаб GuidanceLine")]
    [SerializeField] private GameObject guidanceLinePrefab;
    
    [Header("Цели")]
    [Tooltip("Transform кнопки спавна брейнрота (не используется для линии: линия ведёт только к брейнротам не в placement).")]
    [SerializeField] private Transform spawnBrainrotButtonTransform;
    
    [Header("Оптимизация")]
    [Tooltip("Интервал обновления цели (секунды). Больше = меньше нагрузка на CPU")]
    [SerializeField] private float updateInterval = 0.25f;
    
    private GameObject guidanceLineInstance;
    private GuidanceLine.GuidanceLine guidanceLineScript; // Asset Store GuidanceLine (GuidanceLine1.cs)
    private GameObject tempTargetObject; // Временный объект для позиции панели
    private PlayerCarryController playerCarryController;
    private bool forceUpdateTarget = false; // Флаг для принудительного обновления цели
    private float updateTimer = 0f; // Таймер для оптимизации обновления
    
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
        if (guidanceLineScript == null) return;
        
        Transform target = FindNearestBrainrot();
        
        // Ведём линию только если на карте есть брейнрот не в placement; на кнопку спавна не ведём
        if (target != null)
        {
            SetLineEnabled(true);
            guidanceLineScript.endPoint = target;
        }
        else
        {
            SetLineEnabled(false);
        }
    }
    
    void FindPlayerCarryController()
    {
        if (playerCarryController == null)
        {
            playerCarryController = FindFirstObjectByType<PlayerCarryController>();
        }
    }
    
    void Update()
    {
        if (guidanceLineScript == null) return;
        
        // Если требуется принудительное обновление — выполняем сразу
        if (forceUpdateTarget)
        {
            forceUpdateTarget = false;
            ForceResetToButton();
            updateTimer = 0f; // Сбрасываем таймер после принудительного обновления
            return;
        }
        
        // Оптимизация: обновляем цель не каждый кадр, а по таймеру
        updateTimer += Time.deltaTime;
        if (updateTimer >= updateInterval)
        {
            updateTimer = 0f;
            UpdateGuidanceLineTarget();
        }
    }
    
    void CreateGuidanceLine()
    {
        if (guidanceLinePrefab == null)
        {
            Debug.LogError("[Guide] Префаб GuidanceLine не назначен!");
            return;
        }
        
        // Создаем экземпляр направляющей линии (Asset Store CurvedLine prefab)
        guidanceLineInstance = Instantiate(guidanceLinePrefab);
        guidanceLineScript = guidanceLineInstance.GetComponent<GuidanceLine.GuidanceLine>();
        
        if (guidanceLineScript == null)
        {
            Debug.LogError("[Guide] У префаба GuidanceLine нет компонента GuidanceLine!");
            return;
        }
        
        // Назначаем startPoint = игрок, endPoint будет устанавливаться в UpdateGuidanceLineTarget
        guidanceLineScript.startPoint = GetPlayerTransform();
        if (guidanceLineScript.startPoint == null)
            Debug.LogWarning("[Guide] Player не найден — GuidanceLine.startPoint не назначен.");
        
        Debug.Log("[Guide] Направляющая линия создана (Asset Store GuidanceLine)");
    }
    
    void UpdateGuidanceLineTarget()
    {
        if (guidanceLineScript == null)
            return;
        
        // Обновляем startPoint, если игрок ещё не был назначен
        if (guidanceLineScript.startPoint == null)
            guidanceLineScript.startPoint = GetPlayerTransform();
        
        // Стандартное поведение
        // Проверяем, есть ли брейнрот в руках
        bool hasBrainrotInHands = false;
        
        if (playerCarryController == null)
        {
            FindPlayerCarryController();
        }
        
        if (playerCarryController != null)
        {
            BrainrotObject carriedObject = playerCarryController.GetCurrentCarriedObject();
            hasBrainrotInHands = (carriedObject != null);
        }
        
        // Если в руках есть брейнрот — ведём к ближайшему пустому placement, иначе к любому placement
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
                if (guidanceLineScript.endPoint != tempTargetObject.transform)
                    guidanceLineScript.endPoint = tempTargetObject.transform;
            }
            else
            {
                SetLineEnabled(false);
            }
        }
        else
        {
            // В руках нет брейнрота: ведём только на ближайшего брейнрота не в placement; если таких нет — линию не показываем
            Transform target = FindNearestBrainrot();
            
            if (target != null)
            {
                SetLineEnabled(true);
                if (guidanceLineScript.endPoint != target)
                    guidanceLineScript.endPoint = target;
            }
            else
            {
                SetLineEnabled(false);
            }
        }
    }
    
    void SetLineEnabled(bool enabled)
    {
        if (guidanceLineInstance == null) return;
        LineRenderer lineRenderer = guidanceLineInstance.GetComponent<LineRenderer>();
        if (lineRenderer != null)
            lineRenderer.enabled = enabled;
        if (guidanceLineScript != null)
            guidanceLineScript.enabled = enabled;
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
        // Отписываемся от событий
        UnsubscribeFromBattleEvents();
        
        // Удаляем guidanceline только при уничтожении Guide
        if (guidanceLineInstance != null)
        {
            Destroy(guidanceLineInstance);
            guidanceLineInstance = null;
            guidanceLineScript = null;
        }
        
        // Удаляем временный объект для панели, если он есть
        if (tempTargetObject != null)
        {
            Destroy(tempTargetObject);
            tempTargetObject = null;
        }
    }
}
