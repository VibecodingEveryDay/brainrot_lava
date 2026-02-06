using UnityEngine;
using System.Collections;
using System.Reflection;

/// <summary>
/// Плоскость для продажи брейнротов.
/// Когда брейнрот размещается на этой плоскости, он продаётся.
/// Игрок получает сумму, равную 20x от заработка в секунду брейнрота.
/// </summary>
public class BrainrotSellPlane : MonoBehaviour
{
    [Header("Настройки продажи")]
    [Tooltip("Множитель продажи (сколько раз умножается доход в секунду)")]
    [SerializeField] private float sellMultiplier = 20f;
    
    [Header("Отладка")]
    [Tooltip("Показывать отладочные сообщения")]
    [SerializeField] private bool debug = false;
    
    // Коллайдер для обнаружения брейнротов (должен быть триггером)
    private Collider sellPlaneCollider;
    
    // Список брейнротов, которые находятся в зоне продажи
    private System.Collections.Generic.List<BrainrotObject> brainrotsInZone = new System.Collections.Generic.List<BrainrotObject>();
    
    // Интервал проверки (в секундах) - проверяем раз в кадр, но можно оптимизировать
    private const float CHECK_INTERVAL = 0.1f;
    private float lastCheckTime = 0f;
    
    private void Awake()
    {
        // Находим коллайдер (должен быть триггером)
        sellPlaneCollider = GetComponent<Collider>();
        if (sellPlaneCollider == null)
        {
            sellPlaneCollider = GetComponentInChildren<Collider>();
        }
        
        if (sellPlaneCollider == null)
        {
            Debug.LogError($"[BrainrotSellPlane] На объекте {gameObject.name} не найден Collider! Добавьте Collider с включенным IsTrigger.");
        }
        else if (!sellPlaneCollider.isTrigger)
        {
            Debug.LogWarning($"[BrainrotSellPlane] Коллайдер на объекте {gameObject.name} не является триггером! Включите IsTrigger в настройках коллайдера.");
        }
    }
    
    private void Start()
    {
        // Проверяем брейнроты, которые уже могут быть в зоне при старте
        StartCoroutine(CheckInitialBrainrotsDelayed());
    }
    
    private void Update()
    {
        // Проверяем брейнроты в зоне периодически
        if (Time.time - lastCheckTime >= CHECK_INTERVAL)
        {
            CheckBrainrotsForSale();
            lastCheckTime = Time.time;
        }
    }
    
    /// <summary>
    /// Проверяет брейнроты, которые уже могут быть в зоне при старте (с задержкой, чтобы все объекты успели инициализироваться)
    /// </summary>
    private IEnumerator CheckInitialBrainrotsDelayed()
    {
        yield return new WaitForSeconds(0.5f);
        
        if (sellPlaneCollider != null && sellPlaneCollider.isTrigger)
        {
            // Находим все BrainrotObject в сцене
            BrainrotObject[] allBrainrots = FindObjectsByType<BrainrotObject>(FindObjectsSortMode.None);
            
            foreach (BrainrotObject brainrot in allBrainrots)
            {
                if (brainrot != null && brainrot.gameObject != null)
                {
                    // Проверяем, пересекается ли брейнрот с зоной продажи (по bounds, а не только pivot — иначе при опущенном putOffsetY pivot может быть ниже плоскости)
                    if (GetBrainrotBounds(brainrot, out Bounds brainrotBounds) && sellPlaneCollider.bounds.Intersects(brainrotBounds))
                    {
                        // Добавляем в список, если ещё не добавлен
                        if (!brainrotsInZone.Contains(brainrot))
                        {
                            brainrotsInZone.Add(brainrot);
                            
                            if (debug)
                            {
                                Debug.Log($"[BrainrotSellPlane] Брейнрот '{brainrot.GetObjectName()}' найден в зоне продажи при старте");
                            }
                        }
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Проверяет брейнроты в зоне и продаёт те, которые размещены
    /// </summary>
    private void CheckBrainrotsForSale()
    {
        // 1) Обрабатываем брейнротов, попавших в зону через триггер
        var brainrotsToCheck = new System.Collections.Generic.List<BrainrotObject>(brainrotsInZone);
        
        foreach (BrainrotObject brainrot in brainrotsToCheck)
        {
            if (brainrot == null || brainrot.gameObject == null)
            {
                brainrotsInZone.Remove(brainrot);
                continue;
            }
            
            if (brainrot.IsPlaced() && !brainrot.IsCarried())
            {
                SellBrainrot(brainrot);
            }
        }
        
        // 2) Дополнительно: ищем все размещённые брейнроты, чья позиция внутри границ плоскости
        // (на случай если OnTriggerEnter не сработал — слои, kinematic Rigidbody и т.д.)
        if (sellPlaneCollider == null || !sellPlaneCollider.enabled) return;
        
        Bounds bounds = sellPlaneCollider.bounds;
        BrainrotObject[] allBrainrots = FindObjectsByType<BrainrotObject>(FindObjectsSortMode.None);
        
        foreach (BrainrotObject brainrot in allBrainrots)
        {
            if (brainrot == null || brainrot.gameObject == null) continue;
            if (!brainrot.IsPlaced() || brainrot.IsCarried()) continue;
            if (!GetBrainrotBounds(brainrot, out Bounds brainrotBounds) || !bounds.Intersects(brainrotBounds)) continue;
            // Уже в списке — обработаем в следующем цикле или уже обработан
            if (brainrotsInZone.Contains(brainrot)) continue;
            
            // В зоне продажи по пересечению bounds — продаём
            brainrotsInZone.Add(brainrot);
            SellBrainrot(brainrot);
        }
    }
    
    /// <summary>
    /// Возвращает мировые bounds брейнрота (по рендерерам или коллайдерам). Истина, если bounds удалось вычислить.
    /// </summary>
    private bool GetBrainrotBounds(BrainrotObject brainrot, out Bounds bounds)
    {
        bounds = new Bounds();
        if (brainrot == null || brainrot.gameObject == null) return false;
        
        Renderer[] renderers = brainrot.GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                if (renderers[i] != null) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }
        
        Collider col = brainrot.GetComponentInChildren<Collider>();
        if (col != null)
        {
            bounds = col.bounds;
            return true;
        }
        
        bounds = new Bounds(brainrot.transform.position, Vector3.zero);
        return true;
    }
    
    /// <summary>
    /// Вызывается, когда объект входит в триггер
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        BrainrotObject brainrot = other.GetComponent<BrainrotObject>();
        if (brainrot == null)
        {
            brainrot = other.GetComponentInParent<BrainrotObject>();
        }
        
        if (brainrot != null)
        {
            // Добавляем в список брейнротов в зоне
            if (!brainrotsInZone.Contains(brainrot))
            {
                brainrotsInZone.Add(brainrot);
                
                if (debug)
                {
                    Debug.Log($"[BrainrotSellPlane] Брейнрот '{brainrot.GetObjectName()}' вошёл в зону продажи");
                }
            }
        }
    }
    
    /// <summary>
    /// Вызывается, когда объект выходит из триггера
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        BrainrotObject brainrot = other.GetComponent<BrainrotObject>();
        if (brainrot == null)
        {
            brainrot = other.GetComponentInParent<BrainrotObject>();
        }
        
        if (brainrot != null)
        {
            // Удаляем из списка брейнротов в зоне
            brainrotsInZone.Remove(brainrot);
            
            if (debug)
            {
                Debug.Log($"[BrainrotSellPlane] Брейнрот '{brainrot.GetObjectName()}' вышел из зоны продажи");
            }
        }
    }
    
    /// <summary>
    /// Продаёт брейнрот: добавляет деньги игроку, удаляет брейнрот
    /// </summary>
    private void SellBrainrot(BrainrotObject brainrot)
    {
        if (brainrot == null || brainrot.gameObject == null)
        {
            return;
        }
        
        // Получаем доход в секунду брейнрота
        double incomePerSecond = brainrot.GetFinalIncome();
        
        // Вычисляем цену продажи (20x от дохода в секунду)
        double sellPrice = incomePerSecond * sellMultiplier;
        
        if (sellPrice <= 0)
        {
            if (debug)
            {
                Debug.LogWarning($"[BrainrotSellPlane] Цена продажи равна нулю или отрицательна для брейнрота '{brainrot.GetObjectName()}'");
            }
            return;
        }
        
        string brainrotName = brainrot.GetObjectName();
        
        if (debug)
        {
            Debug.Log($"[BrainrotSellPlane] Продаём брейнрот '{brainrotName}': доход в секунду = {incomePerSecond}, цена продажи = {sellPrice} (x{sellMultiplier})");
        }
        
        // Добавляем деньги игроку через GameStorage
        if (GameStorage.Instance != null)
        {
            GameStorage.Instance.AddBalanceDouble(sellPrice);
            
            if (debug)
            {
                string balanceFormatted = GameStorage.Instance.FormatBalance();
                Debug.Log($"[BrainrotSellPlane] Деньги добавлены игроку. Новый баланс: {balanceFormatted}");
            }
        }
        else
        {
            Debug.LogError("[BrainrotSellPlane] GameStorage.Instance не найден! Деньги не добавлены.");
        }
        
        // ВАЖНО: Обновляем уведомление о балансе через BalanceNotifyManager
        BalanceNotifyManager notifyManager = FindFirstObjectByType<BalanceNotifyManager>();
        if (notifyManager != null)
        {
            notifyManager.UpdateNotificationImmediately(sellPrice);
            
            if (debug)
            {
                Debug.Log($"[BrainrotSellPlane] Уведомление о балансе обновлено с суммой: {sellPrice}");
            }
        }
        else
        {
            if (debug)
            {
                Debug.LogWarning("[BrainrotSellPlane] BalanceNotifyManager не найден! Уведомление не показано.");
            }
        }
        
        // Находим PlacementPanel, на котором размещён брейнрот (если есть)
        PlacementPanel linkedPlacementPanel = FindPlacementPanelWithBrainrot(brainrot);
        
        // Удаляем брейнрот из GameStorage
        if (GameStorage.Instance != null)
        {
            // Удаляем из списка всех брейнротов
            GameStorage.Instance.RemoveBrainrotByName(brainrotName);
            
            // Удаляем из списка размещенных брейнротов (если есть PlacementPanel)
            if (linkedPlacementPanel != null)
            {
                int panelID = linkedPlacementPanel.GetPanelID();
                GameStorage.Instance.RemovePlacedBrainrot(panelID);
                
                if (debug)
                {
                    Debug.Log($"[BrainrotSellPlane] Брейнрот удалён из PlacementPanel с ID: {panelID}");
                }
            }
        }
        
        // Очищаем ссылку в PlacementPanel (если есть)
        if (linkedPlacementPanel != null)
        {
            // Используем рефлексию для доступа к приватному полю placedBrainrot
            var placementPanelType = typeof(PlacementPanel);
            var placedBrainrotField = placementPanelType.GetField("placedBrainrot", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (placedBrainrotField != null)
            {
                placedBrainrotField.SetValue(linkedPlacementPanel, null);
                
                if (debug)
                {
                    Debug.Log($"[BrainrotSellPlane] Ссылка на брейнрот очищена в PlacementPanel");
                }
            }
        }
        
        // Удаляем объект из сцены
        Destroy(brainrot.gameObject);
        
        // Удаляем из списка брейнротов в зоне
        brainrotsInZone.Remove(brainrot);
        
        if (debug)
        {
            Debug.Log($"[BrainrotSellPlane] Брейнрот '{brainrotName}' продан и удалён из мира");
        }
    }
    
    /// <summary>
    /// Находит PlacementPanel, на котором размещён указанный брейнрот
    /// </summary>
    private PlacementPanel FindPlacementPanelWithBrainrot(BrainrotObject brainrot)
    {
        if (brainrot == null)
        {
            return null;
        }
        
        // Находим все PlacementPanel в сцене
        PlacementPanel[] allPanels = FindObjectsByType<PlacementPanel>(FindObjectsSortMode.None);
        
        foreach (PlacementPanel panel in allPanels)
        {
            if (panel == null)
            {
                continue;
            }
            
            // Используем рефлексию для получения placedBrainrot из PlacementPanel
            var placementPanelType = typeof(PlacementPanel);
            var placedBrainrotField = placementPanelType.GetField("placedBrainrot", 
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (placedBrainrotField != null)
            {
                BrainrotObject panelBrainrot = placedBrainrotField.GetValue(panel) as BrainrotObject;
                
                if (panelBrainrot == brainrot)
                {
                    return panel;
                }
            }
        }
        
        return null;
    }
}
