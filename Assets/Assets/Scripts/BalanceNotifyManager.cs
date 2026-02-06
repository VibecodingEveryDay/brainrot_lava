using UnityEngine;
using System.Collections;

/// <summary>
/// Менеджер уведомления о доходе с EarnPanel.
/// Игрок поочерёдно наступает на панели — общая сумма дохода растёт и показывается в уведомлении.
/// Если игрок не наступал на EarnPanel в течение заданного времени (по умолчанию 3 сек), общий доход обнуляется.
/// </summary>
public class BalanceNotifyManager : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Через сколько секунд без сбора обнулять общий доход")]
    [SerializeField] private float idleResetSeconds = 3f;
    
    [Tooltip("Как часто проверять, нужно ли обнулить доход (сек)")]
    [SerializeField] private float checkInterval = 0.5f;
    
    [Header("References")]
    [Tooltip("BalanceNotify GameObject (если не назначен, будет найден автоматически)")]
    [SerializeField] private GameObject balanceNotify;
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения")]
    [SerializeField] private bool debug = false;
    
    private BalanceNotifyAnimation notifyAnimation;
    private Coroutine checkCoroutine;
    
    /// <summary> Накопленная сумма дохода с момента последнего обнуления (при каждом наступании на EarnPanel растёт). </summary>
    private double accumulatedTotal = 0.0;
    
    /// <summary> Время последнего сбора с любой EarnPanel. </summary>
    private float lastCollectTime = 0f;
    
    private void Awake()
    {
        if (debug)
        {
            Debug.Log($"[BalanceNotifyManager] Awake() вызван на объекте: {gameObject.name}");
        }
        FindBalanceNotify();
    }
    
    private void Start()
    {
        // Убеждаемся, что BalanceNotify найден
        if (balanceNotify == null || notifyAnimation == null)
        {
            if (debug)
            {
                Debug.LogWarning("[BalanceNotifyManager] BalanceNotify или notifyAnimation не найдены, пытаемся найти снова...");
            }
            FindBalanceNotify();
        }
        
        // ВАЖНО: Убеждаемся, что BalanceNotify активен
        if (balanceNotify != null && !balanceNotify.activeInHierarchy)
        {
            balanceNotify.SetActive(true);
        }
        
        accumulatedTotal = 0.0;
        lastCollectTime = 0f;
        
        if (checkCoroutine != null)
            StopCoroutine(checkCoroutine);
        checkCoroutine = StartCoroutine(IdleResetCheckCoroutine());
    }
    
    private void OnEnable()
    {
        if (checkCoroutine != null)
            StopCoroutine(checkCoroutine);
        checkCoroutine = StartCoroutine(IdleResetCheckCoroutine());
    }
    
    private void OnDisable()
    {
        if (checkCoroutine != null)
        {
            StopCoroutine(checkCoroutine);
            checkCoroutine = null;
        }
    }
    
    /// <summary>
    /// Находит BalanceNotify в сцене
    /// </summary>
    private void FindBalanceNotify()
    {
        if (balanceNotify == null)
        {
            balanceNotify = GameObject.Find("BalanceNotify");
            if (balanceNotify == null)
            {
                // Пытаемся найти через поиск по имени с учетом вложенности
                balanceNotify = FindGameObjectInScene("BalanceNotify");
            }
        }
        
        if (balanceNotify != null)
        {
            // ВАЖНО: Убеждаемся, что объект активен для поиска компонентов
            if (!balanceNotify.activeInHierarchy)
            {
                balanceNotify.SetActive(true);
            }
            
            // Находим компонент BalanceNotifyAnimation
            notifyAnimation = balanceNotify.GetComponent<BalanceNotifyAnimation>();
            if (notifyAnimation == null)
            {
                notifyAnimation = balanceNotify.GetComponentInChildren<BalanceNotifyAnimation>(true); // true = включая неактивные
            }
            
            if (notifyAnimation == null)
            {
                Debug.LogError("[BalanceNotifyManager] BalanceNotifyAnimation не найден на объекте BalanceNotify! Проверьте, что компонент BalanceNotifyAnimation добавлен на объект BalanceNotify или его дочерний объект.");
            }
            else if (debug)
            {
                Debug.Log($"[BalanceNotifyManager] BalanceNotify найден: {balanceNotify.name}");
            }
        }
        else
        {
            Debug.LogError("[BalanceNotifyManager] BalanceNotify не найден в сцене! Убедитесь, что объект с именем 'BalanceNotify' существует в иерархии.");
        }
    }
    
    /// <summary>
    /// Корутина: если игрок не наступал на EarnPanel в течение idleResetSeconds — обнуляем общий доход и показываем 0.
    /// </summary>
    private IEnumerator IdleResetCheckCoroutine()
    {
        if (debug)
            Debug.Log("[BalanceNotifyManager] IdleResetCheckCoroutine запущена");
        
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);
            
            if (accumulatedTotal <= 0.0)
                continue;
            
            float idle = Time.time - lastCollectTime;
            if (idle >= idleResetSeconds)
            {
                if (debug)
                    Debug.Log($"[BalanceNotifyManager] Нет сбора {idle:F1}с — обнуляем доход (было {accumulatedTotal})");
                
                accumulatedTotal = 0.0;
                
                if (notifyAnimation == null)
                    FindBalanceNotify();
                if (notifyAnimation != null)
                {
                    try
                    {
                        notifyAnimation.AnimateToAmount(0.0, 0.0);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[BalanceNotifyManager] Ошибка при обнулении уведомления: {e.Message}");
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Вызывается из EarnPanel (и др.) при сборе дохода. Добавляет сумму к общему доходу и сразу обновляет уведомление.
    /// </summary>
    public void UpdateNotificationImmediately(double amount)
    {
        if (amount <= 0.0)
        {
            if (debug)
                Debug.LogWarning($"[BalanceNotifyManager] UpdateNotificationImmediately вызван с невалидной суммой: {amount}, пропускаем");
            return;
        }
        
        accumulatedTotal += amount;
        lastCollectTime = Time.time;
        
        if (notifyAnimation == null)
            FindBalanceNotify();
        if (notifyAnimation != null)
        {
            try
            {
                notifyAnimation.AnimateToAmount(0.0, accumulatedTotal);
                if (debug)
                    Debug.Log($"[BalanceNotifyManager] Сбор +{amount}, общая сумма: {accumulatedTotal}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BalanceNotifyManager] Ошибка при обновлении уведомления: {e.Message}");
            }
        }
    }
    
    
    /// <summary>
    /// Рекурсивно ищет GameObject в сцене по имени
    /// </summary>
    private GameObject FindGameObjectInScene(string name)
    {
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        foreach (GameObject root in rootObjects)
        {
            GameObject found = FindGameObjectInHierarchy(root.transform, name);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Рекурсивно ищет GameObject в иерархии
    /// </summary>
    private GameObject FindGameObjectInHierarchy(Transform parent, string name)
    {
        if (parent.name == name)
        {
            return parent.gameObject;
        }
        
        foreach (Transform child in parent)
        {
            GameObject found = FindGameObjectInHierarchy(child, name);
            if (found != null)
            {
                return found;
            }
        }
        
        return null;
    }
}
