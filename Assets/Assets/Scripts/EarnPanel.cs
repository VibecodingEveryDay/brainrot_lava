using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Панель для накопления дохода от размещённого brainrot объекта.
/// Каждую секунду прибавляет доход от brainrot на PlacementPanel с тем же ID.
/// При наступлении игрока собирает накопленный баланс и добавляет его в GameStorage.
/// </summary>
public class EarnPanel : MonoBehaviour
{
    [Header("Настройки панели")]
    [Tooltip("ID панели для связи с PlacementPanel (должен совпадать с panelID в PlacementPanel)")]
    [SerializeField] private int panelID = 0;
    
    [Header("UI")]
    [Tooltip("TextMeshPro компонент для отображения накопленного баланса")]
    [SerializeField] private TextMeshPro moneyText;
    
    [Header("Effects")]
    [Tooltip("Префаб эффекта, который будет спавниться при сборе дохода")]
    [SerializeField] private GameObject collectEffectPrefab;
    
    [Tooltip("Эффект «золотого» накопления: показывается, когда накоплено ≥ 20 сек дохода. Префаб: BoldVFXPackDemo/Content/URP/Prefabs/FX_Shimmering_Yellow или Built-in")]
    [SerializeField] private GameObject shimmerEffectPrefab;
    
    [Tooltip("Горизонтальный масштаб эффекта shimmer (FX_Shimmering_Yellow) по X и Z")]
    [SerializeField] private float shimmerEffectScale = 2.5f;
    [Tooltip("Вертикальный масштаб эффекта shimmer (FX_Shimmering_Yellow) по Y")]
    [SerializeField] private float shimmerEffectScaleY = 2.5f;
    [Tooltip("Смещение эффекта shimmer по оси Y относительно центра панели")]
    [SerializeField] private float shimmerEffectOffsetY = 1f;

    [Header("Sound")]
    [Tooltip("Звук, который проигрывается, когда игрок наступает на панель")]
    [SerializeField] private AudioClip playerStepOnPanelClip;
    [Range(0f, 1f)]
    [Tooltip("Громкость звука наступания на панель")]
    [SerializeField] private float playerStepOnPanelVolume = 1f;
    
    [Header("Настройки обнаружения игрока")]
    [Tooltip("Transform игрока (перетащите из иерархии)")]
    [SerializeField] private Transform playerTransform;
    
    [Tooltip("Радиус обнаружения игрока (игрок считается на панели, если находится в этом радиусе)")]
    [SerializeField] private float detectionRadius = 2f;
    [Tooltip("Половина высоты вертикального диапазона обнаружения игрока по оси Y (для разделения панелей по этажам)")]
    [SerializeField] private float detectionHalfHeight = 1.5f;
    
    [Header("Визуальная обратная связь кнопки")]
    [Tooltip("Transform 3D кнопки для смещения по Y (если не назначен, используется текущий объект)")]
    [SerializeField] private Transform buttonTransform;
    
    [Tooltip("MeshRenderer 3D кнопки (если не назначен, ищется на текущем или дочернем объекте)")]
    [SerializeField] private Renderer buttonRenderer;
    
    [Tooltip("Цвет кнопки когда игрок НЕ на панели")]
    [SerializeField] private Color normalColor = new Color(0.2f, 0.8f, 0.2f); // Зелёный
    
    [Tooltip("Цвет кнопки когда игрок на панели")]
    [SerializeField] private Color pressedColor = Color.yellow;
    
    [Tooltip("Смещение кнопки вниз по Y при наступлении")]
    [SerializeField] private float pressedYOffset = -0.3f;
    
    [Tooltip("Скорость анимации перехода")]
    [SerializeField] private float visualTransitionSpeed = 10f;
    
    [Header("Отладка")]
    [Tooltip("Показывать отладочные сообщения в консоли")]
    [SerializeField] private bool debug = false;
    
    // Накопленный баланс панели
    private double accumulatedBalance = 0.0;
    
    // Ссылка на связанную PlacementPanel
    private PlacementPanel linkedPlacementPanel;
    
    // Ссылка на размещённый brainrot объект
    private BrainrotObject placedBrainrot;
    
    // Корутина для обновления дохода
    private Coroutine incomeCoroutine;
    
    // Флаг, находится ли игрок на панели
    private bool isPlayerOnPanel = false;
    
    // Время последнего сохранения дохода (для оптимизации)
    private float lastSaveTime = 0f;
    
    // Интервал сохранения дохода (в секундах)
    private const float SAVE_INTERVAL = 5f;
    
    // Кэш для оптимизации - обновляем текст только при изменении
    private string lastFormattedBalance = "";
    private double lastAccumulatedBalance = -1;
    
    // Визуальная обратная связь
    private Vector3 originalButtonWorldPos;
    private Vector3 originalTextWorldPos;
    private Material buttonMaterial;
    private float currentColorBlend = 0f;
    private AudioSource audioSource;
    private static float lastStepSoundTime = -999f;
    private const float STEP_SOUND_MIN_INTERVAL = 0.05f;
    
    // Эффект накопления (FX_Shimmering_Yellow) — один экземпляр, включается/выключается
    private GameObject shimmerEffectInstance;
    private float _visualUpdateTimer;
    private const float VISUAL_UPDATE_INTERVAL = 0.08f;

    private void Awake()
    {
        // Автоматически находим TextMeshPro компонент, если не назначен
        if (moneyText == null)
        {
            moneyText = GetComponentInChildren<TextMeshPro>();
            if (debug)
            {
                Debug.Log($"[EarnPanel] TextMeshPro компонент {(moneyText != null ? "найден" : "НЕ найден")} на {gameObject.name}");
            }
        }
        
        // Пытаемся найти игрока автоматически, если не назначен
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                if (debug)
                {
                    Debug.Log($"[EarnPanel] Игрок найден автоматически: {player.name}");
                }
            }
            else
            {
                if (debug)
                {
                    Debug.LogWarning($"[EarnPanel] Игрок не найден! Назначьте playerTransform в инспекторе.");
                }
            }
        }
        
        // Инициализация визуальной обратной связи
        if (buttonRenderer == null)
        {
            buttonRenderer = GetComponent<Renderer>();
            if (buttonRenderer == null)
            {
                buttonRenderer = GetComponentInChildren<Renderer>();
            }
        }
        if (buttonRenderer != null && buttonRenderer.sharedMaterial != null)
        {
            buttonMaterial = buttonRenderer.material; // Создаём instance материала
        }
        Transform buttonT = buttonTransform != null ? buttonTransform : transform;
        originalButtonWorldPos = buttonT.position;
        if (moneyText != null)
            originalTextWorldPos = moneyText.transform.position;

        // Аудиоисточник для звука наступания
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }
    }
    
    private void Start()
    {
        // Находим связанную PlacementPanel по ID
        FindLinkedPlacementPanel();
        
        // Загружаем сохраненный доход из GameStorage с задержкой
        // (чтобы PlacementPanel успел загрузить размещенные брейнроты)
        StartCoroutine(LoadSavedBalanceDelayed());
        
        // Запускаем корутину для обновления дохода
        if (incomeCoroutine == null)
        {
            incomeCoroutine = StartCoroutine(UpdateIncomeCoroutine());
        }
    }
    
    /// <summary>
    /// Загружает сохраненный доход с задержкой, чтобы PlacementPanel успел загрузить брейнроты
    /// </summary>
    private IEnumerator LoadSavedBalanceDelayed()
    {
        // Ждем несколько кадров, чтобы PlacementPanel успел загрузить размещенные брейнроты
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(0.1f); // Небольшая дополнительная задержка
        
        LoadSavedBalance();
    }
    
    /// <summary>
    /// Загружает сохраненный накопленный доход из GameStorage
    /// </summary>
    private void LoadSavedBalance()
    {
        if (GameStorage.Instance != null)
        {
            double savedBalance = GameStorage.Instance.GetEarnPanelBalance(panelID);
            if (savedBalance > 0.0)
            {
                accumulatedBalance = savedBalance;
                lastAccumulatedBalance = savedBalance;
                UpdateMoneyText();
                
                if (debug)
                {
                    Debug.Log($"[EarnPanel] Загружен сохраненный доход: {savedBalance} для панели {panelID}");
                }
            }
        }
    }
    
    private void OnEnable()
    {
        // Перезапускаем корутину при включении
        if (incomeCoroutine == null)
        {
            incomeCoroutine = StartCoroutine(UpdateIncomeCoroutine());
        }
    }
    
    private void OnDisable()
    {
        // Останавливаем корутину при выключении
        if (incomeCoroutine != null)
        {
            StopCoroutine(incomeCoroutine);
            incomeCoroutine = null;
        }
    }
    
    private void Update()
    {
        // Обновляем размещённый brainrot объект
        UpdatePlacedBrainrot();
        
        // Проверяем, находится ли игрок на панели
        CheckPlayerOnPanel();
        
        if (!isPlayerOnPanel && Mathf.Abs((float)(accumulatedBalance - lastAccumulatedBalance)) > 0.0001f)
            UpdateMoneyText();

        _visualUpdateTimer += Time.deltaTime;
        if (_visualUpdateTimer >= VISUAL_UPDATE_INTERVAL)
        {
            _visualUpdateTimer = 0f;
            UpdateButtonVisual();
            UpdateShimmerEffect();
        }
    }
    
    /// <summary>
    /// Показывает эффект FX_Shimmering_Yellow, когда накопленный доход ≥ 20× заработка в секунду (за 20 сек).
    /// </summary>
    private void UpdateShimmerEffect()
    {
        if (shimmerEffectPrefab == null) return;
        
        double incomePerSecond = 0.0;
        if (placedBrainrot != null && placedBrainrot.IsPlaced() && !placedBrainrot.IsCarried())
            incomePerSecond = placedBrainrot.GetFinalIncome();
        
        double threshold = incomePerSecond * 20.0;
        bool shouldShow = incomePerSecond > 0.0 && accumulatedBalance >= threshold;
        
        if (shouldShow)
        {
            if (shimmerEffectInstance == null)
            {
                shimmerEffectInstance = Instantiate(shimmerEffectPrefab, transform.position, Quaternion.identity);
                shimmerEffectInstance.transform.SetParent(transform);
                shimmerEffectInstance.transform.localPosition = new Vector3(0f, shimmerEffectOffsetY, 0f);
            }
            // Компенсируем масштаб родителя, чтобы мировой масштаб эффекта был равномерным (2.5, 2.5, 2.5)
            Vector3 parentScale = transform.lossyScale;
            float sx = Mathf.Abs(parentScale.x) > 0.001f ? shimmerEffectScale / parentScale.x : shimmerEffectScale;
            float syScale = shimmerEffectScaleY > 0f ? shimmerEffectScaleY : shimmerEffectScale;
            float sy = Mathf.Abs(parentScale.y) > 0.001f ? syScale / parentScale.y : syScale;
            float sz = Mathf.Abs(parentScale.z) > 0.001f ? shimmerEffectScale / parentScale.z : shimmerEffectScale;
            shimmerEffectInstance.transform.localScale = new Vector3(sx, sy, sz);
            if (!shimmerEffectInstance.activeInHierarchy)
                shimmerEffectInstance.SetActive(true);
        }
        else
        {
            if (shimmerEffectInstance != null && shimmerEffectInstance.activeInHierarchy)
                shimmerEffectInstance.SetActive(false);
        }
    }
    
    /// <summary>
    /// Обновляет визуальное состояние кнопки (цвет и смещение по Y)
    /// </summary>
    private void UpdateButtonVisual()
    {
        if (buttonMaterial == null) return;
        
        // Плавный переход blend (0 = нормальное состояние, 1 = нажатое)
        float targetBlend = isPlayerOnPanel ? 1f : 0f;
        currentColorBlend = Mathf.MoveTowards(currentColorBlend, targetBlend, Time.deltaTime * visualTransitionSpeed);
        
        // Применяем цвет
        Color newColor = Color.Lerp(normalColor, pressedColor, currentColorBlend);
        if (buttonMaterial.HasProperty("_BaseColor"))
        {
            buttonMaterial.SetColor("_BaseColor", newColor);
        }
        else if (buttonMaterial.HasProperty("_Color"))
        {
            buttonMaterial.SetColor("_Color", newColor);
        }
        
        // Плавное смещение по Y — только EarnPanel (меш) и Text, Cube не трогаем
        Transform buttonT = buttonTransform != null ? buttonTransform : transform;
        float offset = isPlayerOnPanel ? Mathf.Abs(pressedYOffset) : 0f;
        Vector3 targetButtonPos = originalButtonWorldPos + Vector3.down * offset;
        buttonT.position = Vector3.Lerp(buttonT.position, targetButtonPos, Time.deltaTime * visualTransitionSpeed);
        if (moneyText != null)
        {
            Vector3 targetTextPos = originalTextWorldPos + Vector3.down * offset;
            moneyText.transform.position = Vector3.Lerp(moneyText.transform.position, targetTextPos, Time.deltaTime * visualTransitionSpeed);
        }
    }
    
    /// <summary>
    /// Проверяет, находится ли игрок на панели (в радиусе обнаружения)
    /// </summary>
    private void CheckPlayerOnPanel()
    {
        if (playerTransform == null)
        {
            isPlayerOnPanel = false;
            return;
        }
        
        // Вычисляем расстояние от центра панели до игрока
        Vector3 panelPosition = transform.position;
        Vector3 playerPosition = playerTransform.position;
        
        // 1) Горизонтальное расстояние (X,Z)
        Vector2 panelPos2D = new Vector2(panelPosition.x, panelPosition.z);
        Vector2 playerPos2D = new Vector2(playerPosition.x, playerPosition.z);
        float distance = Vector2.Distance(panelPos2D, playerPos2D);
        
        // 2) Вертикальное расстояние (Y) для разделения панелей по этажам
        float verticalDelta = Mathf.Abs(playerPosition.y - panelPosition.y);
        
        bool wasOnPanel = isPlayerOnPanel;
        // Если detectionHalfHeight <= 0, вертикальная проверка отключена (поведение как раньше).
        bool verticalOk = detectionHalfHeight <= 0.01f || verticalDelta <= detectionHalfHeight;
        // Игрок «на панели», только если он в радиусе по XZ и (опционально) в допустимом диапазоне по высоте
        isPlayerOnPanel = (distance <= detectionRadius) && verticalOk;
        
        if (debug && wasOnPanel != isPlayerOnPanel)
        {
            Debug.Log($"[EarnPanel] Игрок {(isPlayerOnPanel ? "на" : "не на")} панели. DistXZ={distance:F2}/{detectionRadius}, ΔY={verticalDelta:F2}/{detectionHalfHeight}");
        }
        
        // Если игрок только что наступил на панель, обнуляем баланс (один раз)
        if (!wasOnPanel && isPlayerOnPanel)
        {
            if (debug)
            {
                Debug.Log($"[EarnPanel] Игрок наступил на панель! Расстояние: {distance:F2}, накопленный баланс: {accumulatedBalance}");
            }
            CollectBalance();

            // Проигрываем звук наступания на панель (не чаще одного раза в кадр/небольшой интервал для всех панелей)
            if (playerStepOnPanelClip != null && audioSource != null)
            {
                if (Time.time - lastStepSoundTime >= STEP_SOUND_MIN_INTERVAL)
                {
                    audioSource.PlayOneShot(playerStepOnPanelClip, playerStepOnPanelVolume);
                    lastStepSoundTime = Time.time;
                }
            }
        }
    }
    
    /// <summary>
    /// Находит связанную PlacementPanel по ID
    /// </summary>
    private void FindLinkedPlacementPanel()
    {
        linkedPlacementPanel = PlacementPanel.GetPanelByID(panelID);
        if (linkedPlacementPanel == null)
        {
            Debug.LogWarning($"[EarnPanel] PlacementPanel с ID {panelID} не найдена!");
        }
    }
    
    /// <summary>
    /// Обновляет ссылку на размещённый brainrot объект
    /// </summary>
    private void UpdatePlacedBrainrot()
    {
        if (linkedPlacementPanel == null)
        {
            // Пытаемся найти панель снова
            FindLinkedPlacementPanel();
            if (linkedPlacementPanel == null)
            {
                placedBrainrot = null;
                return;
            }
        }
        
        // Получаем размещённый brainrot из связанной панели
        placedBrainrot = linkedPlacementPanel.GetPlacedBrainrot();
    }
    
    /// <summary>
    /// Корутина для обновления дохода каждую секунду
    /// </summary>
    private IEnumerator UpdateIncomeCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            
            // Не добавляем доход, если игрок на панели (баланс должен быть обнулён)
            if (isPlayerOnPanel) continue;
            
            // Если есть размещённый brainrot, добавляем доход
            if (placedBrainrot != null && placedBrainrot.IsPlaced() && !placedBrainrot.IsCarried())
            {
                // Получаем финальный доход (уже включает редкость и уровень)
                double finalIncome = placedBrainrot.GetFinalIncome();
                accumulatedBalance += finalIncome;
                
                // Сохраняем накопленный доход в GameStorage периодически (каждые 5 секунд)
                // Это оптимизирует производительность, не сохраняя каждый кадр
                if (GameStorage.Instance != null && Time.time - lastSaveTime >= SAVE_INTERVAL)
                {
                    GameStorage.Instance.SaveEarnPanelBalance(panelID, accumulatedBalance);
                    lastSaveTime = Time.time;
                }
            }
        }
    }
    
    /// <summary>
    /// Обновляет текст баланса в формате 1.89B (без скобок)
    /// </summary>
    private void UpdateMoneyText()
    {
        if (moneyText == null) return;
        
        // Кэшируем значение для оптимизации
        lastAccumulatedBalance = accumulatedBalance;
        
        if (accumulatedBalance <= 0)
        {
            if (lastFormattedBalance != "0")
            {
                moneyText.text = "0$";
                lastFormattedBalance = "0";
            }
            return;
        }
        
        // Форматируем баланс в нужный формат
        string formattedBalance = FormatBalance(accumulatedBalance);
        
        // Формат: число + буква, например 1.89B
        // Ограничиваем длину текста
        // Максимум 8 символов (например, "999.99T" = 7 символов)
        if (formattedBalance.Length > 8)
        {
            formattedBalance = formattedBalance.Substring(0, 8);
        }
        
        // Обновляем текст только если он изменился (оптимизация)
        if (formattedBalance != lastFormattedBalance)
        {
            moneyText.text = formattedBalance + "$";
            lastFormattedBalance = formattedBalance;
        }
    }
    
    /// <summary>
    /// Форматирует баланс в читаемый формат (1.89B, 5.2M и т.д.)
    /// Возвращает строку без скобок, максимум 8 символов
    /// Целые числа отображаются без десятичных знаков
    /// </summary>
    private string FormatBalance(double balance)
    {
        // Нониллионы (10^30)
        if (balance >= 1000000000000000000000000000000.0)
        {
            double nonillions = balance / 1000000000000000000000000000000.0;
            return FormatBalanceValue(nonillions, "NO");
        }
        // Октиллионы (10^27)
        else if (balance >= 1000000000000000000000000000.0)
        {
            double octillions = balance / 1000000000000000000000000000.0;
            return FormatBalanceValue(octillions, "OC");
        }
        // Септиллионы (10^24)
        else if (balance >= 1000000000000000000000000.0)
        {
            double septillions = balance / 1000000000000000000000000.0;
            return FormatBalanceValue(septillions, "SP");
        }
        // Секстиллионы (10^21)
        else if (balance >= 1000000000000000000000.0)
        {
            double sextillions = balance / 1000000000000000000000.0;
            return FormatBalanceValue(sextillions, "SX");
        }
        // Квинтиллионы (10^18)
        else if (balance >= 1000000000000000000.0)
        {
            double quintillions = balance / 1000000000000000000.0;
            return FormatBalanceValue(quintillions, "QI");
        }
        // Квадриллионы (10^15)
        else if (balance >= 1000000000000000.0)
        {
            double quadrillions = balance / 1000000000000000.0;
            return FormatBalanceValue(quadrillions, "QA");
        }
        // Триллионы (10^12)
        else if (balance >= 1000000000000.0)
        {
            double trillions = balance / 1000000000000.0;
            return FormatBalanceValue(trillions, "T");
        }
        // Миллиарды (10^9)
        else if (balance >= 1000000000.0)
        {
            double billions = balance / 1000000000.0;
            return FormatBalanceValue(billions, "B");
        }
        // Миллионы (10^6)
        else if (balance >= 1000000.0)
        {
            double millions = balance / 1000000.0;
            return FormatBalanceValue(millions, "M");
        }
        // Тысячи (10^3)
        else if (balance >= 1000.0)
        {
            double thousands = balance / 1000.0;
            return FormatBalanceValue(thousands, "K");
        }
        else
        {
            // Меньше тысячи - показываем как целое число
            string formatted = ((long)balance).ToString();
            // Ограничиваем до 8 символов
            if (formatted.Length > 8) formatted = formatted.Substring(0, 8);
            return formatted;
        }
    }
    
    /// <summary>
    /// Вспомогательный метод для форматирования значения баланса. Максимум один знак после запятой.
    /// </summary>
    private string FormatBalanceValue(double value, string suffix)
    {
        value = System.Math.Round(value, 1);
        string formatted = value == System.Math.Floor(value)
            ? $"{(long)value}{suffix}"
            : $"{value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}{suffix}";
        if (formatted.Length > 8) formatted = formatted.Substring(0, 8);
        return formatted;
    }
    
    
    /// <summary>
    /// Собирает накопленный баланс и добавляет его в GameStorage
    /// Всегда обнуляет счётчик при вызове
    /// </summary>
    public void CollectBalance()
    {
        // ВАЖНО: Защита от двойного вызова - если баланс уже обнулен, не обрабатываем повторно
        if (accumulatedBalance <= 0.0)
        {
            if (debug)
            {
                Debug.Log($"[EarnPanel] CollectBalance вызван, но баланс уже обнулен (accumulatedBalance={accumulatedBalance}), пропускаем");
            }
            return;
        }
        
        // Сохраняем значение ДО обнуления
        double balanceToAdd = accumulatedBalance;
        
        // ВАЖНО: Сразу обнуляем баланс, чтобы предотвратить повторный вызов
        accumulatedBalance = 0.0;
        lastAccumulatedBalance = 0.0;
        
        // Сохраняем обнуленный баланс в GameStorage
        if (GameStorage.Instance != null)
        {
            GameStorage.Instance.ClearEarnPanelBalance(panelID);
        }
        
        // Немедленно обновляем текст, чтобы показать 0
        if (moneyText != null)
        {
            moneyText.text = "0$";
            lastFormattedBalance = "0";
        }
        
        // Добавляем баланс в GameStorage только если он больше 0
        if (balanceToAdd > 0)
        {
            // Проверяем, что GameStorage доступен
            if (GameStorage.Instance != null)
            {
                string balanceBeforeFormatted = null;
                if (debug)
                {
                    balanceBeforeFormatted = GameStorage.Instance.FormatBalance();
                }
                
                // Используем AddBalanceDouble для корректной обработки значений с множителями
                // Используем сохраненное значение balanceToAdd
                GameStorage.Instance.AddBalanceDouble(balanceToAdd);
                
                if (debug)
                {
                    string balanceAfterFormatted = GameStorage.Instance.FormatBalance();
                    string balanceToAddFormatted = FormatBalance(balanceToAdd);
                    Debug.Log($"[EarnPanel] Собран баланс: {balanceToAddFormatted} (raw: {balanceToAdd:F2}). Баланс игрока: {balanceBeforeFormatted} -> {balanceAfterFormatted}");
                }
            }
            
            // ВАЖНО: Обновляем уведомление о балансе сразу после сбора
            // Это гарантирует, что уведомление покажет собранную сумму до того, как она обнулится
            Debug.Log($"[EarnPanel] Ищу BalanceNotifyManager для обновления уведомления с суммой: {balanceToAdd}");
            BalanceNotifyManager notifyManager = FindFirstObjectByType<BalanceNotifyManager>();
            if (notifyManager == null)
            {
                Debug.LogWarning("[EarnPanel] BalanceNotifyManager не найден через FindFirstObjectByType, пытаюсь найти через GameObject.Find...");
                // Пытаемся найти через поиск по имени
                GameObject managerObj = GameObject.Find("BalanceNotifyManager");
                if (managerObj != null)
                {
                    Debug.Log($"[EarnPanel] Найден GameObject 'BalanceNotifyManager': {managerObj.name}, активен: {managerObj.activeInHierarchy}");
                    notifyManager = managerObj.GetComponent<BalanceNotifyManager>();
                    if (notifyManager == null)
                    {
                        Debug.LogError("[EarnPanel] BalanceNotifyManager компонент не найден на GameObject 'BalanceNotifyManager'!");
                    }
                }
                else
                {
                    Debug.LogError("[EarnPanel] GameObject 'BalanceNotifyManager' не найден в сцене!");
                }
            }
            else
            {
                Debug.Log($"[EarnPanel] BalanceNotifyManager найден через FindFirstObjectByType: {notifyManager.gameObject.name}, активен: {notifyManager.gameObject.activeInHierarchy}");
            }
            
            if (notifyManager != null)
            {
                Debug.Log($"[EarnPanel] BalanceNotifyManager найден, вызываю UpdateNotificationImmediately с суммой: {balanceToAdd} (ID панели: {panelID})");
                try
                {
                    notifyManager.UpdateNotificationImmediately(balanceToAdd);
                    Debug.Log($"[EarnPanel] UpdateNotificationImmediately вызван успешно для суммы: {balanceToAdd} (ID панели: {panelID})");
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[EarnPanel] Ошибка при вызове UpdateNotificationImmediately: {e.Message}\n{e.StackTrace}");
                }
            }
            else
            {
                Debug.LogError("[EarnPanel] BalanceNotifyManager не найден! Уведомление не будет обновлено. Убедитесь, что BalanceNotifyManager существует в сцене.");
            }
            
            // Спавним эффект при сборе дохода
            SpawnCollectEffect();
        }
    }
    
    /// <summary>
    /// Получить ID панели
    /// </summary>
    public int GetPanelID()
    {
        return panelID;
    }
    
    /// <summary>
    /// Установить ID панели
    /// </summary>
    public void SetPanelID(int id)
    {
        panelID = id;
        FindLinkedPlacementPanel();
    }
    
    /// <summary>
    /// Получить текущий накопленный баланс
    /// </summary>
    public double GetAccumulatedBalance()
    {
        return accumulatedBalance;
    }
    
    /// <summary>
    /// Установить накопленный баланс (для тестирования или загрузки сохранений)
    /// </summary>
    public void SetAccumulatedBalance(double balance)
    {
        accumulatedBalance = balance;
    }
    
    /// <summary>
    /// Спавнит эффект сбора дохода: полупрозрачный блок вокруг EarnPanel, анимация 500ms.
    /// Дополнительно может спавнить collectEffectPrefab (частицы), если назначен.
    /// </summary>
    private void SpawnCollectEffect()
    {
        // Создаём эффект "Effect" — полупрозрачный блок, анимация 500ms
        Transform buttonT = buttonTransform != null ? buttonTransform : transform;
        GameObject effectObj = new GameObject("Effect");
        effectObj.transform.SetParent(buttonT.parent);
        EarnPanelCollectEffect effect = effectObj.AddComponent<EarnPanelCollectEffect>();
        effect.Init(buttonT);

        // Опционально: префаб частиц (если назначен)
        if (collectEffectPrefab != null)
        {
            Vector3 spawnPosition = transform.position;
            GameObject effectInstance = Instantiate(collectEffectPrefab, spawnPosition, Quaternion.identity);
            effectInstance.transform.localScale = Vector3.one * 2f;

            ParticleSystem particles = effectInstance.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                ParticleSystem.MainModule main = particles.main;
                float duration = main.duration;
                float maxLifetime = main.startLifetime.mode == ParticleSystemCurveMode.Constant
                    ? main.startLifetime.constant
                    : (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants
                        ? main.startLifetime.constantMax
                        : 2f);
                Destroy(effectInstance, duration + maxLifetime + 1f);
            }
            else
            {
                Destroy(effectInstance, 5f);
            }
        }
    }
}
