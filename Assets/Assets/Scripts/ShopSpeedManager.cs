using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;

/// <summary>
/// Менеджер для управления покупками улучшений скорости в магазине
/// Управляет отображением скорости и покупками через Bar1, Bar2, Bar3
/// </summary>
public class ShopSpeedManager : MonoBehaviour
{
    private const int MAX_LEVEL = 60; // Максимальный уровень скорости
    
    [Header("Speed Upgrade Settings")]
    [Tooltip("Стартовый уровень скорости игрока (устанавливается при первом запуске, если текущий уровень = 0)")]
    [SerializeField] private int startingSpeedLevel = 10;
    
    [Tooltip("Множитель увеличения скорости за уровень (отображается в UI для информации)")]
    [SerializeField] private float speedByLevelScaler = 1f;
    
    [Header("Price Settings")]
    [Tooltip("Цены за уровни от 0 до 60. Если цена не указана (-1), будет интерполирована между ближайшими указанными ценами")]
    [SerializeField] private long[] levelPrices;
    
    [Header("References")]
    [Tooltip("Transform, содержащий все Bar объекты (Bar1, Bar2, Bar3)")]
    [SerializeField] private Transform speedBarsContainer;
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения в консоли")]
    [SerializeField] private bool debug = false;
    
    [System.Serializable]
    public class SpeedBar
    {
        public string barName;
        public Transform barTransform;
        public TextMeshProUGUI speedText1; // Текущий уровень
        public TextMeshProUGUI speedText2; // Будущий уровень
        public TextMeshProUGUI priceText; // Цена
        public Button button;
        public int speedAmount; // Количество уровней, добавляемых при покупке (1, 5 или 10)
    }
    
    // Список баров (всегда 3: Bar1, Bar2, Bar3)
    private List<SpeedBar> speedBars = new List<SpeedBar>();
    
    private GameStorage gameStorage;
    private ThirdPersonController playerController;
    
    // Для отслеживания изменений баланса
    private double lastBalance = -1;
    private float balanceCheckInterval = 0.1f; // Проверяем баланс каждые 0.1 секунды
    private float balanceCheckTimer = 0f;
    
    private void Reset()
    {
        levelPrices = new long[61];
        for (int i = 0; i < 61; i++)
            levelPrices[i] = -1;
    }
    
    private void Awake()
    {
        // Автоматически находим контейнер с барами, если не назначен
        if (speedBarsContainer == null)
        {
            // Сначала пытаемся найти SpeedModalContainer в сцене
            GameObject speedModalContainer = GameObject.Find("SpeedModalContainer");
            if (speedModalContainer != null)
            {
                speedBarsContainer = speedModalContainer.transform;
                if (debug)
                {
                    Debug.Log($"[ShopSpeedManager] SpeedModalContainer найден через GameObject.Find: {speedModalContainer.name}");
                }
            }
            else
            {
                // Если не найден в сцене, ищем в родительском объекте
                Transform parent = transform.parent;
                if (parent != null && (parent.name == "SpeedModalContainer" || parent.name.Contains("Speed")))
                {
                    speedBarsContainer = parent;
                    if (debug)
                    {
                        Debug.Log($"[ShopSpeedManager] Контейнер найден через родительский объект: {parent.name}");
                    }
                }
                else
                {
                    // Если ничего не найдено, используем текущий transform
                    speedBarsContainer = transform;
                    if (debug)
                    {
                        Debug.Log($"[ShopSpeedManager] Используется текущий transform: {transform.name}");
                    }
                }
            }
        }
        
        if (debug && speedBarsContainer != null)
        {
            Debug.Log($"[ShopSpeedManager] Контейнер для поиска баров: {speedBarsContainer.name}");
            // Выводим список дочерних объектов для отладки
            foreach (Transform child in speedBarsContainer)
            {
                Debug.Log($"[ShopSpeedManager] Дочерний объект найден: {child.name}");
            }
        }
        
        // Инициализируем массив цен, если он не инициализирован
        if (levelPrices == null)
        {
            levelPrices = new long[61];
            // Инициализируем все цены как -1 (не указано)
            for (int i = 0; i < levelPrices.Length; i++)
            {
                levelPrices[i] = -1;
            }
            if (debug)
            {
                Debug.Log("[ShopSpeedManager] Массив levelPrices инициализирован как null");
            }
        }
        else if (levelPrices.Length != 61)
        {
            // Если длина не 61, создаем новый массив и копируем существующие значения
            long[] oldPrices = levelPrices;
            levelPrices = new long[61];
            for (int i = 0; i < levelPrices.Length; i++)
            {
                if (i < oldPrices.Length)
                {
                    levelPrices[i] = oldPrices[i];
                }
                else
                {
                    levelPrices[i] = -1;
                }
            }
            if (debug)
            {
                Debug.Log($"[ShopSpeedManager] Массив levelPrices изменен с длины {oldPrices.Length} на 61");
            }
        }
        
        // Отладочная информация о массиве цен
        if (debug)
        {
            int specifiedPrices = 0;
            for (int i = 0; i < levelPrices.Length; i++)
            {
                if (levelPrices[i] >= 0)
                {
                    specifiedPrices++;
                    Debug.Log($"[ShopSpeedManager] Уровень {i}: цена = {levelPrices[i]}");
                }
            }
            Debug.Log($"[ShopSpeedManager] Всего указано цен: {specifiedPrices} из {levelPrices.Length}");
        }
        
        // Автоматически находим и настраиваем Bar объекты (всегда 3)
        if (speedBars.Count == 0)
        {
            SetupSpeedBars();
        }
    }
    
    private void Start()
    {
        // Получаем ссылки на необходимые компоненты
        gameStorage = GameStorage.Instance;
        playerController = FindFirstObjectByType<ThirdPersonController>();
        
        if (gameStorage == null)
        {
            Debug.LogError("[ShopSpeedManager] GameStorage.Instance не найден!");
        }
        
        if (playerController == null)
        {
            Debug.LogError("[ShopSpeedManager] ThirdPersonController не найден в сцене!");
        }
        
        // Устанавливаем стартовый уровень скорости, если текущий уровень = 0
        if (gameStorage != null)
        {
            int currentLevel = gameStorage.GetPlayerSpeedLevel();
            if (currentLevel == 0 && startingSpeedLevel > 0)
            {
                gameStorage.SetPlayerSpeedLevel(startingSpeedLevel);
                gameStorage.Save();
                if (debug)
                {
                    Debug.Log($"[ShopSpeedManager] Установлен стартовый уровень скорости: {startingSpeedLevel} (текущий уровень был: 0)");
                }
                
                // Обновляем скорость игрока
                if (playerController != null)
                {
                    playerController.RefreshSpeedFromLevel();
                }
            }
        }
        
        // Обновляем UI
        UpdateAllSpeedBars();
        
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
            UpdateAllSpeedBars();
        }
    }
    
    private void Update()
    {
        // Обновляем UI только если модальное окно активно
        if (speedBarsContainer != null && speedBarsContainer.gameObject.activeInHierarchy)
        {
            // Проверяем баланс с интервалом для оптимизации
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
                    
                    // Если баланс изменился, обновляем UI
                    if (lastBalance < 0 || Math.Abs(currentBalance - lastBalance) > 0.0001)
                    {
                        lastBalance = currentBalance;
                        UpdateAllSpeedBars();
                        
                        if (debug)
                        {
                            Debug.Log($"[ShopSpeedManager] Баланс изменился до {currentBalance}, обновляю UI");
                        }
                    }
                }
            }
        }
        else
        {
            // Если модальное окно неактивно, сбрасываем таймер
            balanceCheckTimer = 0f;
        }
    }
    
    /// <summary>
    /// Автоматически настраивает SpeedBar объекты из иерархии
    /// </summary>
    private void SetupSpeedBars()
    {
        // Ищем Bar1, Bar2, Bar3 в дочерних объектах (поиск нечувствителен к регистру)
        Transform bar1 = FindChildByName(speedBarsContainer, "Bar1") ?? FindChildByName(speedBarsContainer, "bar1");
        Transform bar2 = FindChildByName(speedBarsContainer, "Bar2") ?? FindChildByName(speedBarsContainer, "bar2");
        Transform bar3 = FindChildByName(speedBarsContainer, "Bar3") ?? FindChildByName(speedBarsContainer, "bar3");
        
        speedBars.Clear();
        
        // Настраиваем Bar1 (всегда добавляет +1 к уровню)
        if (bar1 != null)
        {
            SpeedBar bar = CreateSpeedBar(bar1, "Bar1", 1);
            speedBars.Add(bar);
        }
        
        // Настраиваем Bar2 (всегда добавляет +5 к уровню)
        if (bar2 != null)
        {
            SpeedBar bar = CreateSpeedBar(bar2, "Bar2", 5);
            speedBars.Add(bar);
        }
        
        // Настраиваем Bar3 (всегда добавляет +10 к уровню)
        if (bar3 != null)
        {
            SpeedBar bar = CreateSpeedBar(bar3, "Bar3", 10);
            speedBars.Add(bar);
        }
        
        if (speedBars.Count == 0 && debug)
        {
            Debug.LogWarning("[ShopSpeedManager] Не найдено ни одного Bar объекта (Bar1, Bar2, Bar3)!");
        }
    }
    
    /// <summary>
    /// Создает SpeedBar из Transform
    /// </summary>
    private SpeedBar CreateSpeedBar(Transform barTransform, string name, int speedAmount)
    {
        SpeedBar bar = new SpeedBar
        {
            barName = name,
            barTransform = barTransform,
            speedAmount = speedAmount
        };
        
        // Ищем SpeedText1, SpeedText2 и Price
        bar.speedText1 = FindChildComponent<TextMeshProUGUI>(barTransform, "SpeedText1");
        bar.speedText2 = FindChildComponent<TextMeshProUGUI>(barTransform, "SpeedText2");
        bar.priceText = FindChildComponent<TextMeshProUGUI>(barTransform, "Price") ?? 
                        FindChildComponent<TextMeshProUGUI>(barTransform, "price");
        
        // Отладочная информация о найденных компонентах
        if (debug)
        {
            Debug.Log($"[ShopSpeedManager] Bar '{name}' - SpeedText1: {(bar.speedText1 != null ? bar.speedText1.name : "НЕ НАЙДЕН")}, " +
                      $"SpeedText2: {(bar.speedText2 != null ? bar.speedText2.name : "НЕ НАЙДЕН")}, " +
                      $"Price: {(bar.priceText != null ? bar.priceText.name : "НЕ НАЙДЕН")}");
        }
        
        // Ищем Button
        bar.button = FindChildComponent<Button>(barTransform, "Button");
        if (bar.button != null)
        {
            // Добавляем обработчик клика
            bar.button.onClick.RemoveAllListeners();
            int speedAmountCopy = speedAmount; // Копируем для замыкания
            bar.button.onClick.AddListener(() => OnBuySpeedButtonClicked(speedAmountCopy));
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
    /// Обновляет все SpeedBar UI
    /// </summary>
    public void UpdateAllSpeedBars()
    {
        // Убеждаемся, что gameStorage инициализирован
        if (gameStorage == null)
        {
            gameStorage = GameStorage.Instance;
        }
        
        if (gameStorage == null)
        {
            if (debug)
            {
                Debug.LogWarning("[ShopSpeedManager] GameStorage недоступен, не могу обновить SpeedBars");
            }
            return;
        }
        
        int currentLevel = gameStorage.GetPlayerSpeedLevel();
        float currentSpeed = GetCurrentSpeed();
        
        if (debug)
        {
            Debug.Log($"[ShopSpeedManager] Обновление всех SpeedBars - Уровень игрока: {currentLevel}, Текущая скорость: {currentSpeed}");
        }
        
        foreach (SpeedBar bar in speedBars)
        {
            UpdateSpeedBar(bar, currentLevel, currentSpeed);
        }
    }
    
    /// <summary>
    /// Обновляет UI конкретного SpeedBar
    /// </summary>
    private void UpdateSpeedBar(SpeedBar bar, int currentLevel, float currentSpeed)
    {
        // Проверяем, достигнут ли максимальный уровень
        bool isMaxLevel = currentLevel >= MAX_LEVEL;
        
        // Вычисляем будущий уровень с учетом ограничения в 60
        int futureLevel = currentLevel + bar.speedAmount;
        int actualLevelsToAdd = bar.speedAmount;
        
        // Если будущий уровень превышает максимум, ограничиваем до 60
        if (futureLevel > MAX_LEVEL)
        {
            futureLevel = MAX_LEVEL;
            actualLevelsToAdd = MAX_LEVEL - currentLevel;
        }
        
        // Вычисляем цену покупки (только за реально доступные уровни)
        long price = 0;
        if (!isMaxLevel && actualLevelsToAdd > 0)
        {
            price = CalculatePriceForLevels(currentLevel, actualLevelsToAdd);
        }
        
        // Обновляем SpeedText1 (текущий уровень)
        if (bar.speedText1 != null)
        {
            bar.speedText1.text = currentLevel.ToString();
            if (debug)
            {
                Debug.Log($"[ShopSpeedManager] Bar '{bar.barName}' - Уровень: {currentLevel}, SpeedText1 обновлен: {bar.speedText1.text}");
            }
        }
        else if (debug)
        {
            Debug.LogWarning($"[ShopSpeedManager] SpeedText1 не найден для Bar '{bar.barName}'!");
        }
        
        // Обновляем SpeedText2 (будущий уровень)
        if (bar.speedText2 != null)
        {
            bar.speedText2.text = futureLevel.ToString();
            if (debug)
            {
                Debug.Log($"[ShopSpeedManager] Bar '{bar.barName}' - Уровень: {currentLevel}, SpeedText2 обновлен: {bar.speedText2.text} (будущий уровень: {futureLevel})");
            }
        }
        else if (debug)
        {
            Debug.LogWarning($"[ShopSpeedManager] SpeedText2 не найден для Bar '{bar.barName}'!");
        }
        
        // Обновляем PriceText (цена покупки) - форматируем через GameStorage
        if (bar.priceText != null)
        {
            if (isMaxLevel)
            {
                bar.priceText.text = "Макс. ур";
                if (debug)
                {
                    Debug.Log($"[ShopSpeedManager] Bar '{bar.barName}' - Максимальный уровень достигнут, PriceText: 'Макс. ур'");
                }
            }
            else
            {
                string formattedPrice = gameStorage != null ? gameStorage.FormatBalance((double)price) : price.ToString();
                bar.priceText.text = formattedPrice;
                
                // Проверяем баланс и меняем цвет цены
                if (gameStorage != null && price > 0)
                {
                    double balance = gameStorage.GetBalanceDouble();
                    if (balance < (double)price)
                    {
                        // Не хватает баланса - красный цвет
                        bar.priceText.color = Color.red;
                    }
                    else
                    {
                        // Хватает баланса - белый цвет
                        bar.priceText.color = Color.white;
                    }
                }
                else if (price == 0)
                {
                    // Если цена 0, устанавливаем белый цвет
                    bar.priceText.color = Color.white;
                }
                
                if (debug)
                {
                    double balance = gameStorage != null ? gameStorage.GetBalanceDouble() : 0;
                    Debug.Log($"[ShopSpeedManager] Bar '{bar.barName}' - Уровень: {currentLevel}, PriceText обновлен: {bar.priceText.text} (raw price: {price}, balance: {balance}, color: {bar.priceText.color})");
                }
            }
        }
        else if (debug)
        {
            Debug.LogWarning($"[ShopSpeedManager] PriceText не найден для Bar '{bar.barName}'!");
        }
        
        // Обновляем активность кнопки (достаточно ли денег и не достигнут ли максимум)
        if (bar.button != null)
        {
            if (isMaxLevel)
            {
                bar.button.interactable = false;
            }
            else if (gameStorage != null && price > 0)
            {
                double balance = gameStorage.GetBalanceDouble();
                bool canAfford = balance >= (double)price;
                bar.button.interactable = canAfford && actualLevelsToAdd > 0;
                
                if (debug)
                {
                    Debug.Log($"[ShopSpeedManager] Bar '{bar.barName}' - Кнопка: interactable={bar.button.interactable}, balance={balance}, price={price}, canAfford={canAfford}, actualLevelsToAdd={actualLevelsToAdd}");
                }
            }
            else
            {
                bar.button.interactable = false;
            }
        }
    }
    
    /// <summary>
    /// Форматирует скорость, убирая .0 для целых чисел
    /// </summary>
    private string FormatSpeed(float speed)
    {
        // Проверяем, является ли число целым
        if (speed == Mathf.Floor(speed))
        {
            // Целое число - без десятичных знаков
            return ((int)speed).ToString();
        }
        else
        {
            // Дробное число - с десятичными знаками (убираем лишние нули)
            return speed.ToString("0.##");
        }
    }
    
    /// <summary>
    /// Получает цену за уровень из массива, с интерполяцией если цена не указана
    /// </summary>
    private long GetPriceForLevel(int level)
    {
        // Проверяем, что массив инициализирован
        if (levelPrices == null || levelPrices.Length == 0)
        {
            if (debug)
            {
                Debug.LogError($"[ShopSpeedManager] Массив levelPrices не инициализирован!");
            }
            return 0;
        }
        
        // Проверяем границы массива
        if (level < 0) level = 0;
        if (level >= levelPrices.Length) level = levelPrices.Length - 1;
        
        if (debug)
        {
            Debug.Log($"[ShopSpeedManager] GetPriceForLevel: запрос уровня {level}, массив длины {levelPrices.Length}, значение в массиве: {levelPrices[level]}");
        }
        
        // Если цена указана (не -1), возвращаем её
        if (levelPrices[level] >= 0)
        {
            return levelPrices[level];
        }
        
        // Если цена не указана, ищем ближайшие указанные цены для интерполяции
        int lowerLevel = -1;
        int upperLevel = -1;
        
        // Ищем ближайший нижний уровень с указанной ценой
        for (int i = level - 1; i >= 0; i--)
        {
            if (levelPrices[i] >= 0)
            {
                lowerLevel = i;
                break;
            }
        }
        
        // Ищем ближайший верхний уровень с указанной ценой
        for (int i = level + 1; i < levelPrices.Length; i++)
        {
            if (levelPrices[i] >= 0)
            {
                upperLevel = i;
                break;
            }
        }
        
        if (debug)
        {
            Debug.Log($"[ShopSpeedManager] GetPriceForLevel: для уровня {level} найдены: lowerLevel={lowerLevel}, upperLevel={upperLevel}");
        }
        
        // Если нашли оба уровня, интерполируем линейно
        if (lowerLevel >= 0 && upperLevel >= 0)
        {
            long lowerPrice = levelPrices[lowerLevel];
            long upperPrice = levelPrices[upperLevel];
            
            // Линейная интерполяция с использованием double для избежания переполнения
            double t = (double)(level - lowerLevel) / (double)(upperLevel - lowerLevel);
            double priceDiff = (double)upperPrice - (double)lowerPrice;
            double interpolatedPriceDouble = (double)lowerPrice + priceDiff * t;
            long interpolatedPrice = (long)System.Math.Round(interpolatedPriceDouble);
            
            if (debug)
            {
                Debug.Log($"[ShopSpeedManager] Интерполяция для уровня {level}: между уровнем {lowerLevel} (цена {lowerPrice}) и уровнем {upperLevel} (цена {upperPrice}), t={t}, результат: {interpolatedPrice}");
            }
            
            return interpolatedPrice;
        }
        // Если нашли только нижний уровень
        else if (lowerLevel >= 0)
        {
            if (debug)
            {
                Debug.Log($"[ShopSpeedManager] Используем цену нижнего уровня {lowerLevel}: {levelPrices[lowerLevel]}");
            }
            return levelPrices[lowerLevel];
        }
        // Если нашли только верхний уровень
        else if (upperLevel >= 0)
        {
            if (debug)
            {
                Debug.Log($"[ShopSpeedManager] Используем цену верхнего уровня {upperLevel}: {levelPrices[upperLevel]}");
            }
            return levelPrices[upperLevel];
        }
        
        // Если ничего не найдено, возвращаем 0 и выводим предупреждение
        Debug.LogWarning($"[ShopSpeedManager] Не найдено цен для уровня {level}. Массив цен пуст или не заполнен в Inspector! Проверьте, что в массиве Level Prices указаны цены хотя бы для некоторых уровней (например, уровень 0, 1, 5, 10).");
        return 0;
    }
    
    /// <summary>
    /// Вычисляет общую цену покупки за несколько уровней
    /// Суммирует цену за каждый уровень от currentLevel до currentLevel + levelsToAdd - 1
    /// </summary>
    private long CalculatePriceForLevels(int currentLevel, int levelsToAdd)
    {
        long totalPrice = 0;
        
        if (debug)
        {
            Debug.Log($"[ShopSpeedManager] CalculatePriceForLevels: currentLevel={currentLevel}, levelsToAdd={levelsToAdd}");
        }
        
        // Суммируем цену за каждый уровень
        for (int i = 0; i < levelsToAdd; i++)
        {
            int levelToCalculate = currentLevel + i;
            long priceForLevel = GetPriceForLevel(levelToCalculate);
            totalPrice += priceForLevel;
            
            if (debug)
            {
                Debug.Log($"[ShopSpeedManager] Уровень {levelToCalculate}: цена за уровень = {priceForLevel}, сумма = {totalPrice}");
            }
        }
        
        return totalPrice;
    }
    
    /// <summary>
    /// Получает текущую скорость игрока
    /// </summary>
    private float GetCurrentSpeed()
    {
        // Пытаемся получить скорость из ThirdPersonController (более точное значение)
        if (playerController != null)
        {
            return playerController.GetMoveSpeed();
        }
        
        // Если ThirdPersonController недоступен, вычисляем на основе уровня
        if (gameStorage == null) return 0f;
        
        int currentLevel = gameStorage.GetPlayerSpeedLevel();
        return CalculateSpeed(currentLevel);
    }
    
    /// <summary>
    /// Вычисляет скорость на основе уровня
    /// </summary>
    private float CalculateSpeed(int level)
    {
        // Используем ThirdPersonController для расчета скорости, если доступен
        if (playerController != null)
        {
            return playerController.CalculateSpeedFromLevel(level);
        }
        
        // Если ThirdPersonController недоступен, используем speedByLevelScaler
        return level * speedByLevelScaler;
    }
    
    
    /// <summary>
    /// Обработчик клика по кнопке покупки скорости
    /// </summary>
    private void OnBuySpeedButtonClicked(int speedAmount)
    {
        if (gameStorage == null)
        {
            Debug.LogError("[ShopSpeedManager] GameStorage недоступен!");
            return;
        }
        
        int currentLevel = gameStorage.GetPlayerSpeedLevel();
        
        // Проверяем, не достигнут ли максимальный уровень
        if (currentLevel >= MAX_LEVEL)
        {
            if (debug)
            {
                Debug.LogWarning($"[ShopSpeedManager] Максимальный уровень ({MAX_LEVEL}) уже достигнут!");
            }
            return;
        }
        
        // Вычисляем реальное количество уровней для покупки (с учетом максимума)
        int actualLevelsToAdd = speedAmount;
        int futureLevel = currentLevel + speedAmount;
        if (futureLevel > MAX_LEVEL)
        {
            actualLevelsToAdd = MAX_LEVEL - currentLevel;
            if (debug)
            {
                Debug.Log($"[ShopSpeedManager] Будущий уровень ({futureLevel}) превышает максимум ({MAX_LEVEL}). Покупаем только до {MAX_LEVEL} уровня ({actualLevelsToAdd} уровней)");
            }
        }
        
        // Вычисляем цену только за реально доступные уровни
        long price = CalculatePriceForLevels(currentLevel, actualLevelsToAdd);
        
        // Проверяем баланс
        double balance = gameStorage.GetBalanceDouble();
        if (balance < (double)price)
        {
            if (debug)
            {
                Debug.LogWarning($"[ShopSpeedManager] Недостаточно средств для покупки! Требуется: {price}, есть: {balance}");
            }
            return;
        }
        
        // Вычитаем деньги - используем метод GameStorage для корректной конвертации
        // Используем SubtractBalanceLong для работы с long значениями
        bool purchaseSuccess = gameStorage.SubtractBalanceLong(price);
        
        if (purchaseSuccess)
        {
            // Увеличиваем уровень скорости только на доступное количество
            gameStorage.IncreasePlayerSpeedLevel(actualLevelsToAdd);
            
            // Сохраняем прогресс
            gameStorage.Save();
            
            // Обновляем скорость игрока
            UpdatePlayerSpeed();
            
            // Обновляем UI
            UpdateAllSpeedBars();
            
            if (debug)
            {
                int newLevel = gameStorage.GetPlayerSpeedLevel();
                int oldLevel = newLevel - actualLevelsToAdd;
                Debug.Log($"[ShopSpeedManager] Скорость увеличена на {actualLevelsToAdd}! Уровень изменен с {oldLevel} на {newLevel}");
            }
        }
        else
        {
            Debug.LogError($"[ShopSpeedManager] Не удалось вычесть деньги из баланса!");
        }
    }
    
    /// <summary>
    /// Конвертирует double цену в формат value + scaler (аналогично балансу)
    /// </summary>
    private (int value, string scaler) ConvertDoubleToPrice(double price)
    {
        if (price <= 0)
        {
            return (0, "");
        }
        
        // Триллионы (10^12)
        if (price >= 1000000000000.0)
        {
            double trillions = price / 1000000000000.0;
            return ((int)trillions, "T");
        }
        // Миллиарды (10^9)
        else if (price >= 1000000000.0)
        {
            double billions = price / 1000000000.0;
            return ((int)billions, "B");
        }
        // Миллионы (10^6)
        else if (price >= 1000000.0)
        {
            double millions = price / 1000000.0;
            return ((int)millions, "M");
        }
        // Тысячи (10^3)
        else if (price >= 1000.0)
        {
            double thousands = price / 1000.0;
            return ((int)thousands, "K");
        }
        else
        {
            return ((int)price, "");
        }
    }
    
    /// <summary>
    /// Обновляет скорость игрока в ThirdPersonController
    /// </summary>
    private void UpdatePlayerSpeed()
    {
        if (playerController == null || gameStorage == null) return;
        
        // Используем метод RefreshSpeedFromLevel для пересчета скорости на основе уровня
        playerController.RefreshSpeedFromLevel();
    }
}
