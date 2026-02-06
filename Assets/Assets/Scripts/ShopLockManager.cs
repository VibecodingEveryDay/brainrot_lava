using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;
#if Localization_yg
using YG;
#endif

/// <summary>
/// Менеджер для управления покупками разблокировки лестниц в магазине
/// Управляет отображением и покупками через Bar1, Bar2
/// </summary>
public class ShopLockManager : MonoBehaviour
{
    [Header("Price Settings")]
    [Tooltip("Цена разблокировки лестницы 1")]
    [SerializeField] private long price1 = 10000;
    
    [Tooltip("Цена разблокировки лестницы 2")]
    [SerializeField] private long price2 = 50000;
    
    [Header("References")]
    [Tooltip("Transform, содержащий все Bar объекты (Bar1, Bar2)")]
    [SerializeField] private Transform lockBarsContainer;
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения в консоли")]
    [SerializeField] private bool debug = false;
    
    [System.Serializable]
    public class LockBar
    {
        public string barName;
        public Transform barTransform;
        public TextMeshProUGUI priceText;
        public Button button;
        public int ladderId; // ID лестницы для разблокировки
        public long price; // Цена разблокировки
    }
    
    // Список баров (Bar1, Bar2)
    private List<LockBar> lockBars = new List<LockBar>();
    
    private GameStorage gameStorage;
    
    // Для отслеживания изменений баланса
    private double lastBalance = -1;
    private float balanceCheckInterval = 0.1f;
    private float balanceCheckTimer = 0f;
    
    private void Awake()
    {
        // Автоматически находим контейнер с барами, если не назначен
        if (lockBarsContainer == null)
        {
            GameObject locksModalContainer = GameObject.Find("LocksModalContainer");
            if (locksModalContainer != null)
            {
                lockBarsContainer = locksModalContainer.transform;
                if (debug)
                {
                    Debug.Log($"[ShopLockManager] LocksModalContainer найден через GameObject.Find: {locksModalContainer.name}");
                }
            }
            else
            {
                Transform parent = transform.parent;
                if (parent != null && (parent.name == "LocksModalContainer" || parent.name.Contains("Lock")))
                {
                    lockBarsContainer = parent;
                }
                else
                {
                    lockBarsContainer = transform;
                }
            }
        }
        
        // Автоматически находим и настраиваем Bar объекты
        if (lockBars.Count == 0)
        {
            SetupLockBars();
        }
    }
    
    private void Start()
    {
        gameStorage = GameStorage.Instance;
        
        if (gameStorage == null)
        {
            Debug.LogError("[ShopLockManager] GameStorage.Instance не найден!");
        }
        
        // Обновляем UI
        UpdateAllLockBars();
        
        // Инициализируем lastBalance для отслеживания изменений
        if (gameStorage != null)
        {
            lastBalance = gameStorage.GetBalanceDouble();
        }
    }
    
    private void OnEnable()
    {
        // Обновляем UI при активации
        if (gameStorage != null)
        {
            UpdateAllLockBars();
        }
    }
    
    private void Update()
    {
        // Обновляем UI только если модальное окно активно
        if (lockBarsContainer != null && lockBarsContainer.gameObject.activeInHierarchy)
        {
            balanceCheckTimer += Time.deltaTime;
            if (balanceCheckTimer >= balanceCheckInterval)
            {
                balanceCheckTimer = 0f;
                
                if (gameStorage == null)
                {
                    gameStorage = GameStorage.Instance;
                }
                
                if (gameStorage != null)
                {
                    double currentBalance = gameStorage.GetBalanceDouble();
                    
                    if (lastBalance < 0 || Math.Abs(currentBalance - lastBalance) > 0.0001)
                    {
                        lastBalance = currentBalance;
                        UpdateAllLockBars();
                    }
                }
            }
        }
        else
        {
            balanceCheckTimer = 0f;
        }
    }
    
    /// <summary>
    /// Автоматически настраивает LockBar объекты из иерархии
    /// </summary>
    private void SetupLockBars()
    {
        Transform bar1 = FindChildByName(lockBarsContainer, "Bar1") ?? FindChildByName(lockBarsContainer, "bar1");
        Transform bar2 = FindChildByName(lockBarsContainer, "Bar2") ?? FindChildByName(lockBarsContainer, "bar2");
        
        lockBars.Clear();
        
        // Настраиваем Bar1 (лестница 1)
        if (bar1 != null)
        {
            LockBar bar = CreateLockBar(bar1, "Bar1", 1, price1);
            lockBars.Add(bar);
        }
        
        // Настраиваем Bar2 (лестница 2)
        if (bar2 != null)
        {
            LockBar bar = CreateLockBar(bar2, "Bar2", 2, price2);
            lockBars.Add(bar);
        }
        
        if (lockBars.Count == 0 && debug)
        {
            Debug.LogWarning("[ShopLockManager] Не найдено ни одного Bar объекта (Bar1, Bar2)!");
        }
    }
    
    /// <summary>
    /// Создает LockBar из Transform
    /// </summary>
    private LockBar CreateLockBar(Transform barTransform, string name, int ladderId, long price)
    {
        LockBar bar = new LockBar
        {
            barName = name,
            barTransform = barTransform,
            ladderId = ladderId,
            price = price
        };
        
        // Ищем Price
        bar.priceText = FindChildComponent<TextMeshProUGUI>(barTransform, "Price") ?? 
                        FindChildComponent<TextMeshProUGUI>(barTransform, "price");
        
        // Ищем Button
        bar.button = FindChildComponent<Button>(barTransform, "Button");
        if (bar.button != null)
        {
            bar.button.onClick.RemoveAllListeners();
            int ladderIdCopy = ladderId;
            long priceCopy = price;
            bar.button.onClick.AddListener(() => OnBuyLockButtonClicked(ladderIdCopy, priceCopy));
        }
        
        if (debug)
        {
            Debug.Log($"[ShopLockManager] Bar '{name}' настроен: ladderId={ladderId}, price={price}");
        }
        
        return bar;
    }
    
    /// <summary>
    /// Рекурсивно ищет дочерний объект по имени
    /// </summary>
    private Transform FindChildByName(Transform parent, string name)
    {
        if (parent == null) return null;
        
        foreach (Transform child in parent)
        {
            if (child.name == name)
            {
                return child;
            }
            
            Transform found = FindChildByName(child, name);
            if (found != null)
            {
                return found;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Рекурсивно ищет компонент в дочерних объектах
    /// </summary>
    private T FindChildComponent<T>(Transform parent, string name) where T : Component
    {
        Transform child = FindChildByName(parent, name);
        if (child != null)
        {
            return child.GetComponent<T>();
        }
        return null;
    }
    
    /// <summary>
    /// Обновляет все LockBar UI
    /// </summary>
    public void UpdateAllLockBars()
    {
        if (gameStorage == null)
        {
            gameStorage = GameStorage.Instance;
        }
        
        if (gameStorage == null)
        {
            return;
        }
        
        foreach (LockBar bar in lockBars)
        {
            UpdateLockBar(bar);
        }
    }
    
    /// <summary>
    /// Обновляет UI конкретного LockBar
    /// </summary>
    private void UpdateLockBar(LockBar bar)
    {
        // Проверяем, разблокирована ли лестница
        bool isUnlocked = gameStorage.IsLadderUnlocked(bar.ladderId);
        
        // Получаем текущий язык
        string lang = GetCurrentLanguage();
        
        // Обновляем PriceText
        if (bar.priceText != null)
        {
            if (isUnlocked)
            {
                bar.priceText.text = (lang == "ru") ? "Куплено" : "Purchased";
                bar.priceText.color = Color.green;
            }
            else
            {
                string formattedPrice = gameStorage.FormatBalance((double)bar.price);
                bar.priceText.text = formattedPrice;
                
                // Проверяем баланс и меняем цвет цены
                double balance = gameStorage.GetBalanceDouble();
                if (balance < (double)bar.price)
                {
                    bar.priceText.color = Color.red;
                }
                else
                {
                    bar.priceText.color = Color.white;
                }
            }
        }
        
        // Обновляем активность кнопки
        if (bar.button != null)
        {
            if (isUnlocked)
            {
                bar.button.interactable = false;
            }
            else
            {
                double balance = gameStorage.GetBalanceDouble();
                bar.button.interactable = balance >= (double)bar.price;
            }
        }
    }
    
    /// <summary>
    /// Обработчик клика по кнопке покупки разблокировки
    /// </summary>
    private void OnBuyLockButtonClicked(int ladderId, long price)
    {
        if (gameStorage == null)
        {
            Debug.LogError("[ShopLockManager] GameStorage недоступен!");
            return;
        }
        
        // Проверяем, не разблокирована ли уже лестница
        if (gameStorage.IsLadderUnlocked(ladderId))
        {
            if (debug)
            {
                Debug.LogWarning($"[ShopLockManager] Лестница {ladderId} уже разблокирована!");
            }
            return;
        }
        
        // Проверяем баланс
        double balance = gameStorage.GetBalanceDouble();
        if (balance < (double)price)
        {
            if (debug)
            {
                Debug.LogWarning($"[ShopLockManager] Недостаточно средств для покупки! Требуется: {price}, есть: {balance}");
            }
            return;
        }
        
        // Вычитаем деньги
        bool purchaseSuccess = gameStorage.SubtractBalanceLong(price);
        
        if (purchaseSuccess)
        {
            // Разблокируем лестницу
            gameStorage.UnlockLadder(ladderId);
            
            // Обновляем все лестницы в сцене
            UpdateAllLaddersInScene();
            
            // Обновляем UI
            UpdateAllLockBars();
            
            if (debug)
            {
                Debug.Log($"[ShopLockManager] Лестница {ladderId} разблокирована!");
            }
        }
        else
        {
            Debug.LogError($"[ShopLockManager] Не удалось вычесть деньги из баланса!");
        }
    }
    
    /// <summary>
    /// Обновляет статус всех лестниц в сцене
    /// </summary>
    private void UpdateAllLaddersInScene()
    {
        Ladder[] allLadders = FindObjectsByType<Ladder>(FindObjectsSortMode.None);
        foreach (Ladder ladder in allLadders)
        {
            if (ladder != null)
            {
                ladder.UpdateUnlockStatus();
            }
        }
    }
    
    /// <summary>
    /// Получить текущий язык
    /// </summary>
    private string GetCurrentLanguage()
    {
#if Localization_yg
        if (YG2.lang != null)
        {
            return YG2.lang;
        }
#endif
        return "ru";
    }
}
