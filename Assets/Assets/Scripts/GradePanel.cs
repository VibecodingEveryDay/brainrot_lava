using UnityEngine;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if Localization_yg
using YG;
#endif

/// <summary>
/// Панель улучшения для размещённого брейнрота.
/// При клике на панель увеличивает уровень брейнрота и списывает стоимость улучшения с баланса.
/// </summary>
public class GradePanel : MonoBehaviour
{
    [Header("Настройки панели")]
    [Tooltip("ID панели для связи с PlacementPanel (должен совпадать с panelID в PlacementPanel)")]
    [SerializeField] private int id = 0;
    
    [Tooltip("Радиус обнаружения игрока (панель скрывается, если игрок дальше этого расстояния)")]
    [SerializeField] private float range = 5f;
    
    [Tooltip("Использовать горизонтальное расстояние (игнорировать высоту Y)")]
    [SerializeField] private bool useHorizontalDistance = true;
    
    [Tooltip("Временно отключить автоматическое скрытие панели (для тестирования)")]
    [SerializeField] private bool disableAutoHide = false;
    
    [Header("UI")]
    [Tooltip("TextMeshPro компонент для отображения уровня (автоматически находится в дочернем объекте 'Level')")]
    [SerializeField] private TextMeshPro levelText;
    
    [Header("Настройки обнаружения игрока")]
    [Tooltip("Transform игрока (перетащите из иерархии, или будет найден автоматически по тегу 'Player')")]
    [SerializeField] private Transform playerTransform;
    
    [Header("Анимация")]
    [Tooltip("Длительность анимации улучшения (в секундах)")]
    [SerializeField] private float animationDuration = 0.2f;
    
    [Header("Эффекты")]
    [Tooltip("Префаб эффекта для отображения при улучшении brainrot")]
    [SerializeField] private GameObject upgradeEffectPrefab;
    
    [Tooltip("Масштаб эффекта")]
    [SerializeField] private Vector3 effectScale = Vector3.one;
    
    [Tooltip("Смещение позиции эффекта относительно позиции brainrot (X, Y, Z)")]
    [SerializeField] private Vector3 effectPosOffset = Vector3.zero;
    
    [Header("Отладка")]
    [Tooltip("Показывать отладочные сообщения в консоли")]
    [SerializeField] private bool debug = false;
    
    // Ссылка на связанную PlacementPanel
    private PlacementPanel linkedPlacementPanel;
    
    // Ссылка на размещённый brainrot объект
    private BrainrotObject placedBrainrot;
    
    // Кэш для всех Renderer компонентов (для визуального скрытия)
    private Renderer[] renderers;
    
    // Кэш для всех Collider компонентов (для отключения взаимодействия)
    private Collider[] colliders;
    
    // Флаг для отслеживания предыдущего состояния видимости (для логирования)
    private bool wasVisible = true;
    
    // Флаг для отслеживания, идет ли анимация улучшения
    private bool isAnimating = false;
    
    private void Awake()
    {
        // Автоматически находим TextMeshPro компонент в дочернем объекте "Level"
        if (levelText == null)
        {
            Transform levelTransform = transform.Find("Level");
            if (levelTransform != null)
            {
                levelText = levelTransform.GetComponent<TextMeshPro>();
                if (levelText == null && debug)
                {
                    Debug.LogWarning($"[GradePanel] TextMeshPro компонент не найден на объекте 'Level' в {gameObject.name}");
                }
            }
            else if (debug)
            {
                Debug.LogWarning($"[GradePanel] Объект 'Level' не найден в дочерних объектах {gameObject.name}");
            }
        }
        
        // Пытаемся найти игрока автоматически, если не назначен
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else if (debug)
            {
                Debug.LogWarning($"[GradePanel] Игрок не найден по тегу 'Player'! Назначьте playerTransform в инспекторе.");
            }
        }
        
        // Проверяем наличие коллайдера для обработки кликов
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            col = GetComponentInChildren<Collider>();
            if (col == null && debug)
            {
                Debug.LogWarning($"[GradePanel] На объекте {gameObject.name} нет коллайдера! Клики не будут работать. Добавьте Collider для обработки кликов.");
            }
        }
        
        // Кэшируем все Renderer компоненты для визуального скрытия
        renderers = GetComponentsInChildren<Renderer>(true);
        
        // Кэшируем все Collider компоненты для отключения взаимодействия
        colliders = GetComponentsInChildren<Collider>(true);
    }
    
    private void Start()
    {
        // Находим связанную PlacementPanel по ID
        FindLinkedPlacementPanel();
        
        if (linkedPlacementPanel == null && debug)
        {
            Debug.LogWarning($"[GradePanel] PlacementPanel с ID {id} НЕ найдена! Проверьте, что ID совпадает с panelID в PlacementPanel.");
        }
        
        // ВАЖНО: Не скрываем панель сразу в Start, даём Update() возможность проверить условия
        // Панель должна быть видима по умолчанию, если она активна в иерархии
    }
    
    private void Update()
    {
        // Обновляем ссылку на размещённый brainrot объект
        UpdatePlacedBrainrot();
        
        // Проверяем видимость панели (расстояние до игрока и наличие брейнрота)
        UpdatePanelVisibility();
        
        // Обновляем текст уровня (всегда, так как объект активен)
        UpdateLevelText();
        
        // Обрабатываем клик мыши через Raycast (всегда, так как объект активен)
        HandleMouseClick();
    }
    
    /// <summary>
    /// Находит связанную PlacementPanel по ID
    /// </summary>
    private void FindLinkedPlacementPanel()
    {
        linkedPlacementPanel = PlacementPanel.GetPanelByID(id);
        if (linkedPlacementPanel == null)
        {
            if (debug)
            {
                Debug.LogWarning($"[GradePanel] PlacementPanel с ID {id} не найдена!");
            }
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
                if (debug && Time.frameCount % 120 == 0) // Логируем каждые 2 секунды
                {
                    Debug.LogWarning($"[GradePanel] PlacementPanel с ID {id} не найдена! Проверьте, что ID совпадает с panelID в PlacementPanel.");
                }
                return;
            }
        }
        
        // Получаем размещённый brainrot из связанной панели
        placedBrainrot = linkedPlacementPanel.GetPlacedBrainrot();
    }
    
    /// <summary>
    /// Получает мировую позицию для проверки расстояния (использует позицию размещения из PlacementPanel)
    /// </summary>
    private Vector3 GetDetectionPosition()
    {
        // Приоритет: используем позицию размещения из связанной PlacementPanel
        if (linkedPlacementPanel != null)
        {
            return linkedPlacementPanel.GetPlacementPosition();
        }
        
        // Если PlacementPanel не найдена, используем позицию самой панели
        // Пытаемся использовать центр коллайдера (всегда возвращает мировые координаты)
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            col = GetComponentInChildren<Collider>();
        }
        
        if (col != null)
        {
            // bounds.center всегда возвращает мировую позицию
            return col.bounds.center;
        }
        
        // Если коллайдера нет, используем transform.position (тоже мировые координаты)
        return transform.position;
    }
    
    /// <summary>
    /// Обновляет видимость панели в зависимости от расстояния до игрока и наличия брейнрота
    /// </summary>
    private void UpdatePanelVisibility()
    {
        // Проверяем наличие размещённого брейнрота
        bool hasBrainrot = placedBrainrot != null;
        
        // Получаем позицию для обнаружения (из PlacementPanel или самой панели)
        Vector3 detectionPosition = GetDetectionPosition();
        
        // Проверяем расстояние до игрока (используя мировые координаты)
        bool playerInRange = false;
        float distance = float.MaxValue;
        
        if (playerTransform != null)
        {
            // playerTransform.position всегда возвращает мировые координаты
            Vector3 playerWorldPosition = playerTransform.position;
            
            if (useHorizontalDistance)
            {
                // Используем только горизонтальное расстояние (игнорируем высоту Y)
                Vector2 detectionPos2D = new Vector2(detectionPosition.x, detectionPosition.z);
                Vector2 playerPos2D = new Vector2(playerWorldPosition.x, playerWorldPosition.z);
                distance = Vector2.Distance(detectionPos2D, playerPos2D);
            }
            else
            {
                // Используем полное 3D расстояние
                distance = Vector3.Distance(detectionPosition, playerWorldPosition);
            }
            
            playerInRange = distance <= range;
        }
        else
        {
            // Если игрок не найден, пытаемся найти его снова
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                Vector3 playerWorldPosition = playerTransform.position;
                
                if (useHorizontalDistance)
                {
                    Vector2 detectionPos2D = new Vector2(detectionPosition.x, detectionPosition.z);
                    Vector2 playerPos2D = new Vector2(playerWorldPosition.x, playerWorldPosition.z);
                    distance = Vector2.Distance(detectionPos2D, playerPos2D);
                }
                else
                {
                    distance = Vector3.Distance(detectionPosition, playerWorldPosition);
                }
                
                playerInRange = distance <= range;
            }
        }
        
        // Панель должна быть видима только если:
        // 1. Есть размещённый брейнрот
        // 2. Игрок в радиусе
        // ИЛИ если отключено автоматическое скрытие (для тестирования)
        bool shouldBeVisible = disableAutoHide || (hasBrainrot && playerInRange);
        
        // Обновляем видимость панели (визуально, но объект остаётся активным)
        // ВАЖНО: Объект должен оставаться активным, чтобы скрипт продолжал работать
        SetPanelVisibility(shouldBeVisible);
        
        // Логируем изменение видимости только при изменении состояния и если debug включен
        if (wasVisible != shouldBeVisible && Time.frameCount > 1 && debug)
        {
            wasVisible = shouldBeVisible;
            
            if (shouldBeVisible)
            {
                string reason = disableAutoHide ? "автоскрытие отключено" : $"брейнрот размещён, игрок в радиусе {distance:F2}/{range}";
                Debug.Log($"[GradePanel] Панель ПОКАЗАНА (ID: {id}, причина: {reason})");
            }
            else
            {
                string reason = !hasBrainrot ? "нет размещённого брейнрота" : $"игрок вне радиуса (расстояние: {distance:F2}, range: {range})";
                Debug.Log($"[GradePanel] Панель СКРЫТА (ID: {id}, причина: {reason})");
            }
        }
        else if (wasVisible != shouldBeVisible)
        {
            wasVisible = shouldBeVisible;
        }
    }
    
    /// <summary>
    /// Устанавливает визуальную видимость панели (объект остаётся активным)
    /// </summary>
    private void SetPanelVisibility(bool visible)
    {
        // Скрываем/показываем все Renderer компоненты
        if (renderers != null)
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }
        
        // Отключаем/включаем все Collider компоненты (чтобы нельзя было кликнуть когда скрыто)
        if (colliders != null)
        {
            foreach (Collider col in colliders)
            {
                if (col != null)
                {
                    col.enabled = visible;
                }
            }
        }
        
        // Также скрываем/показываем TextMeshPro компонент
        if (levelText != null)
        {
            levelText.enabled = visible;
        }
    }
    
    /// <summary>
    /// Обновляет текст уровня в формате "Ур.1 -> Ур.2" (русский) или "(Lv.1 -> Lv.2)" (английский) или "Макс. ур" если уровень 20
    /// </summary>
    private void UpdateLevelText()
    {
        if (levelText == null) return;
        
        string lang = GetCurrentLanguage();
        string levelPrefix = lang == "ru" ? "Ур" : "Lv";
        string maxLevelText = lang == "ru" ? "Макс. ур" : "Max Level";
        
        if (placedBrainrot == null)
        {
            // Если нет размещённого брейнрота, показываем пустой текст или дефолтное значение
            if (lang == "ru")
            {
                levelText.text = $"{levelPrefix}.0 -> {levelPrefix}.0";
            }
            else
            {
                levelText.text = $"({levelPrefix}.0 -> {levelPrefix}.0)";
            }
            return;
        }
        
        int currentLevel = placedBrainrot.GetLevel();
        
        // Если уровень достиг 20, показываем локализованный текст
        if (currentLevel >= 20)
        {
            levelText.text = maxLevelText;
            return;
        }
        
        int nextLevel = currentLevel + 1;
        
        // Форматируем текст: для русского без скобок, для английского со скобками
        if (lang == "ru")
        {
            levelText.text = $"{levelPrefix}.{currentLevel} -> {levelPrefix}.{nextLevel}";
        }
        else
        {
            levelText.text = $"({levelPrefix}.{currentLevel} -> {levelPrefix}.{nextLevel})";
        }
    }
    
    /// <summary>
    /// Получает текущий язык
    /// </summary>
    private string GetCurrentLanguage()
    {
#if Localization_yg
        if (YG2.lang != null)
        {
            return YG2.lang;
        }
#endif
        return LocalizationManager.GetCurrentLanguage();
    }
    
    /// <summary>
    /// Обрабатывает клик мыши через Raycast
    /// </summary>
    private void HandleMouseClick()
    {
        // Проверяем, была ли нажата левая кнопка мыши
        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }
        
        // ВАЖНО: Проверяем, видима ли панель - если панель скрыта, не обрабатываем клики
        // Это предотвращает конфликты с камерой когда панель скрыта
        if (!IsVisuallyVisible())
        {
            return;
        }
        
        // ВАЖНО: Проверяем, не заблокирован ли курсор (управление камерой)
        // Если курсор заблокирован, не обрабатываем клик, чтобы не мешать управлению камерой
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            return;
        }
        
        // ВАЖНО: Проверяем, не зажата ли ПКМ (правая кнопка мыши) - управление камерой
        // Если ПКМ зажата, не обрабатываем клик
        bool rightMouseButtonPressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            rightMouseButtonPressed = Mouse.current.rightButton.isPressed;
        }
#else
        rightMouseButtonPressed = Input.GetMouseButton(1); // 1 = правая кнопка мыши
#endif
        
        if (rightMouseButtonPressed)
        {
            return;
        }
        
        // Получаем камеру (главную камеру или камеру из сцены)
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindFirstObjectByType<Camera>();
        }
        
        if (mainCamera == null)
        {
            if (debug)
            {
                Debug.LogWarning("[GradePanel] Камера не найдена для обработки клика!");
            }
            return;
        }
        
        // Создаём луч из позиции мыши
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        // Проверяем, попал ли луч в коллайдер этого объекта или его дочерних объектов
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            col = GetComponentInChildren<Collider>();
        }
        
        if (col == null)
        {
            return;
        }
        
        // Проверяем пересечение луча с коллайдером
        if (col.Raycast(ray, out hit, Mathf.Infinity))
        {
            // ВАЖНО: При быстрых кликах проверяем, не идет ли уже обработка
            // Это предотвращает множественные вызовы ProcessUpgrade при быстрых кликах
            // и помогает избежать блокировки камеры
            // ВАЖНО: Если анимация идет, НЕ обрабатываем клик, чтобы камера могла работать
            if (isAnimating)
            {
                // Анимация уже идет, не обрабатываем клик повторно
                // Это позволяет камере работать нормально при быстрых кликах
                if (debug)
                {
                    Debug.Log("[GradePanel] Анимация уже идет, пропускаем клик (камера не блокируется)");
                }
                return;
            }
            
            // Клик попал в панель - обрабатываем улучшение
            // ВАЖНО: Обрабатываем клик только если панель может его обработать
            // Это предотвращает блокировку камеры при быстрых кликах
            if (CanProcessClick())
            {
                ProcessUpgrade();
            }
            else
            {
                if (debug)
                {
                    Debug.Log("[GradePanel] Панель не может обработать клик, пропускаем (камера не блокируется)");
                }
            }
        }
    }
    
    /// <summary>
    /// Обрабатывает улучшение брейнрота
    /// </summary>
    private void ProcessUpgrade()
    {
        // Проверяем, что есть размещённый брейнрот
        if (placedBrainrot == null)
        {
            if (debug)
            {
                Debug.Log("[GradePanel] Нет размещённого брейнрота для улучшения!");
            }
            return;
        }
        
        // Получаем текущий уровень брейнрота
        int currentLevel = placedBrainrot.GetLevel();
        
        // Проверяем, не достиг ли брейнрот максимального уровня
        if (currentLevel >= 20)
        {
            if (debug)
            {
                Debug.Log("[GradePanel] Брейнрот уже достиг максимального уровня (20)!");
            }
            return;
        }
        
        // Получаем финальный доход (уже включает редкость и уровень)
        double finalIncome = placedBrainrot.GetFinalIncome();
        
        // Вычисляем стоимость улучшения: финальный доход * 20
        double upgradeCost = finalIncome * 20.0;
        
        // Проверяем баланс игрока
        if (GameStorage.Instance == null)
        {
            Debug.LogError("[GradePanel] GameStorage.Instance не найден!");
            return;
        }
        
        // Проверяем, достаточно ли баланса
        double currentBalance = GameStorage.Instance.GetBalanceDouble();
        if (currentBalance < upgradeCost)
        {
            if (debug)
            {
                Debug.LogWarning($"[GradePanel] Недостаточно средств для улучшения! Требуется: {upgradeCost}, есть: {currentBalance}");
            }
            return;
        }
        
        // Пытаемся списать баланс - используем SubtractBalanceDouble для корректной работы с double
        bool success = GameStorage.Instance.SubtractBalanceDouble(upgradeCost);
        
        if (success)
        {
            // Увеличиваем уровень брейнрота
            placedBrainrot.SetLevel(currentLevel + 1);
            
            // Сохраняем обновленный уровень размещенного брейнрота в GameStorage со всеми параметрами
            if (GameStorage.Instance != null && linkedPlacementPanel != null)
            {
                GameStorage.Instance.SavePlacedBrainrot(
                    linkedPlacementPanel.GetPanelID(), 
                    placedBrainrot.GetObjectName(), 
                    placedBrainrot.GetLevel(),
                    placedBrainrot.GetRarity(),
                    placedBrainrot.GetBaseIncome()
                );
            }
            
            // Запускаем анимацию улучшения
            StartUpgradeAnimation();
            
            // Создаём эффект улучшения
            SpawnUpgradeEffect();
            
            if (debug)
            {
                string costFormatted = GameStorage.Instance.FormatBalance(upgradeCost);
                Debug.Log($"[GradePanel] Брейнрот улучшен с уровня {currentLevel} до {currentLevel + 1}. Стоимость: {costFormatted}");
            }
        }
        else
        {
            if (debug)
            {
                string costFormatted = GameStorage.Instance.FormatBalance(upgradeCost);
                string balanceFormatted = GameStorage.Instance.FormatBalance();
                Debug.LogWarning($"[GradePanel] Недостаточно средств для улучшения! Требуется: {costFormatted}, Баланс: {balanceFormatted}");
            }
        }
    }
    
    
    /// <summary>
    /// Конвертирует double в баланс (value + scaler)
    /// Использует ту же логику, что и GameStorage
    /// </summary>
    private (int value, string scaler) ConvertDoubleToBalance(double balance)
    {
        if (balance <= 0)
        {
            return (0, "");
        }
        
        // Нониллионы (10^30)
        if (balance >= 1000000000000000000000000000000.0)
        {
            double nonillions = balance / 1000000000000000000000000000000.0;
            return ((int)nonillions, "NO");
        }
        // Октиллионы (10^27)
        else if (balance >= 1000000000000000000000000000.0)
        {
            double octillions = balance / 1000000000000000000000000000.0;
            return ((int)octillions, "OC");
        }
        // Септиллионы (10^24)
        else if (balance >= 1000000000000000000000000.0)
        {
            double septillions = balance / 1000000000000000000000000.0;
            return ((int)septillions, "SP");
        }
        // Секстиллионы (10^21)
        else if (balance >= 1000000000000000000000.0)
        {
            double sextillions = balance / 1000000000000000000000.0;
            return ((int)sextillions, "SX");
        }
        // Квинтиллионы (10^18)
        else if (balance >= 1000000000000000000.0)
        {
            double quintillions = balance / 1000000000000000000.0;
            return ((int)quintillions, "QI");
        }
        // Квадриллионы (10^15)
        else if (balance >= 1000000000000000.0)
        {
            double quadrillions = balance / 1000000000000000.0;
            return ((int)quadrillions, "QA");
        }
        // Триллионы (10^12)
        else if (balance >= 1000000000000.0)
        {
            double trillions = balance / 1000000000000.0;
            return ((int)trillions, "T");
        }
        // Миллиарды (10^9)
        else if (balance >= 1000000000.0)
        {
            double billions = balance / 1000000000.0;
            return ((int)billions, "B");
        }
        // Миллионы (10^6)
        else if (balance >= 1000000.0)
        {
            double millions = balance / 1000000.0;
            return ((int)millions, "M");
        }
        // Тысячи (10^3)
        else if (balance >= 1000.0)
        {
            double thousands = balance / 1000.0;
            return ((int)thousands, "K");
        }
        else
        {
            // Меньше тысячи - возвращаем как int
            return ((int)balance, "");
        }
    }
    
    /// <summary>
    /// Форматирует баланс для отображения (вспомогательный метод для логов)
    /// </summary>
    private string FormatBalance(int value, string scaler)
    {
        if (string.IsNullOrEmpty(scaler))
        {
            return value.ToString();
        }
        return $"{value}{scaler}";
    }
    
    /// <summary>
    /// Получить ID панели
    /// </summary>
    public int GetID()
    {
        return id;
    }
    
    /// <summary>
    /// Установить ID панели
    /// </summary>
    public void SetID(int newID)
    {
        id = newID;
        FindLinkedPlacementPanel();
    }
    
    /// <summary>
    /// Получить информацию о том, должна ли панель быть видима
    /// Используется другими скриптами (например, TrashPlacement) для синхронизации видимости
    /// </summary>
    public bool ShouldBeVisible()
    {
        // Проверяем наличие размещённого брейнрота
        bool hasBrainrot = placedBrainrot != null;
        
        // Если автоскрытие отключено, панель всегда видима
        if (disableAutoHide)
        {
            return hasBrainrot; // Но всё равно проверяем наличие брейнрота
        }
        
        // Если нет брейнрота, панель не видима
        if (!hasBrainrot)
        {
            return false;
        }
        
        // Проверяем расстояние до игрока
        Vector3 detectionPosition = GetDetectionPosition();
        bool playerInRange = false;
        
        if (playerTransform != null)
        {
            Vector3 playerWorldPosition = playerTransform.position;
            float distance;
            
            if (useHorizontalDistance)
            {
                Vector2 detectionPos2D = new Vector2(detectionPosition.x, detectionPosition.z);
                Vector2 playerPos2D = new Vector2(playerWorldPosition.x, playerWorldPosition.z);
                distance = Vector2.Distance(detectionPos2D, playerPos2D);
            }
            else
            {
                distance = Vector3.Distance(detectionPosition, playerWorldPosition);
            }
            
            playerInRange = distance <= range;
        }
        else
        {
            // Если игрок не найден, пытаемся найти его
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                Vector3 playerWorldPosition = playerTransform.position;
                float distance;
                
                if (useHorizontalDistance)
                {
                    Vector2 detectionPos2D = new Vector2(detectionPosition.x, detectionPosition.z);
                    Vector2 playerPos2D = new Vector2(playerWorldPosition.x, playerWorldPosition.z);
                    distance = Vector2.Distance(detectionPos2D, playerPos2D);
                }
                else
                {
                    distance = Vector3.Distance(detectionPosition, playerWorldPosition);
                }
                
                playerInRange = distance <= range;
            }
        }
        
        return hasBrainrot && playerInRange;
    }
    
    /// <summary>
    /// Проверяет, видима ли панель визуально (через Renderer компоненты)
    /// </summary>
    public bool IsVisuallyVisible()
    {
        if (renderers == null || renderers.Length == 0)
        {
            return gameObject.activeSelf; // Если нет Renderer, используем активность объекта
        }
        
        // Проверяем, включен ли хотя бы один Renderer
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null && renderer.enabled)
            {
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Проверяет, может ли панель обработать клик в данный момент
    /// Используется ThirdPersonCamera для определения, нужно ли блокировать камеру
    /// </summary>
    public bool CanProcessClick()
    {
        // Панель может обработать клик только если:
        // 1. Она видима
        // 2. Не идет анимация (чтобы не блокировать камеру при быстрых кликах)
        return IsVisuallyVisible() && !isAnimating;
    }
    
    /// <summary>
    /// Запускает анимацию улучшения brainrot
    /// </summary>
    private void StartUpgradeAnimation()
    {
        // Проверяем, не идет ли уже анимация
        if (isAnimating)
        {
            if (debug)
            {
                Debug.Log("[GradePanel] Анимация уже идет, пропускаем новый запуск");
            }
            return;
        }
        
        // Проверяем, что есть размещённый brainrot
        if (placedBrainrot == null)
        {
            return;
        }
        
        // Запускаем корутину анимации
        StartCoroutine(UpgradeAnimationCoroutine());
    }
    
    /// <summary>
    /// Корутина анимации улучшения: поднятие на 1.5y, увеличение на 10%, затем возврат
    /// </summary>
    private System.Collections.IEnumerator UpgradeAnimationCoroutine()
    {
        isAnimating = true;
        
        Transform brainrotTransform = placedBrainrot.transform;
        Vector3 startPosition = brainrotTransform.position;
        Vector3 startScale = brainrotTransform.localScale;
        
        // Целевые значения
        Vector3 targetPosition = startPosition + Vector3.up * 1.5f;
        Vector3 targetScale = startScale * 1.1f;
        
        float elapsedTime = 0f;
        float halfDuration = animationDuration / 2f; // Половина длительности для подъема, половина для возврата
        
        // Фаза 1: Поднятие и увеличение (первая половина анимации)
        while (elapsedTime < halfDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / halfDuration;
            
            // Используем плавную кривую для более естественного движения
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            
            brainrotTransform.position = Vector3.Lerp(startPosition, targetPosition, smoothT);
            brainrotTransform.localScale = Vector3.Lerp(startScale, targetScale, smoothT);
            
            yield return null;
        }
        
        // Убеждаемся, что достигли целевых значений
        brainrotTransform.position = targetPosition;
        brainrotTransform.localScale = targetScale;
        
        // Фаза 2: Возврат обратно (вторая половина анимации)
        elapsedTime = 0f;
        while (elapsedTime < halfDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / halfDuration;
            
            // Используем плавную кривую для более естественного движения
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            
            brainrotTransform.position = Vector3.Lerp(targetPosition, startPosition, smoothT);
            brainrotTransform.localScale = Vector3.Lerp(targetScale, startScale, smoothT);
            
            yield return null;
        }
        
        // Убеждаемся, что вернулись к исходным значениям
        brainrotTransform.position = startPosition;
        brainrotTransform.localScale = startScale;
        
        isAnimating = false;
    }
    
    /// <summary>
    /// Создаёт эффект улучшения на позиции brainrot с учетом настроек
    /// </summary>
    private void SpawnUpgradeEffect()
    {
        // Проверяем, что есть размещённый brainrot
        if (placedBrainrot == null)
        {
            return;
        }
        
        // Если префаб не назначен, ничего не делаем
        if (upgradeEffectPrefab == null)
        {
            if (debug)
            {
                Debug.Log("[GradePanel] Префаб эффекта не назначен, эффект не создан");
            }
            return;
        }
        
        // Получаем позицию brainrot
        Vector3 brainrotPosition = placedBrainrot.transform.position;
        
        // Вычисляем позицию эффекта с учетом смещения
        Vector3 effectPosition = brainrotPosition + effectPosOffset;
        
        // Создаём экземпляр эффекта
        GameObject effectInstance = Instantiate(upgradeEffectPrefab, effectPosition, Quaternion.identity);
        
        // Применяем масштаб эффекта
        effectInstance.transform.localScale = effectScale;
        
        // Автоматически уничтожаем эффект после завершения (если это ParticleSystem)
        ParticleSystem particles = effectInstance.GetComponent<ParticleSystem>();
        if (particles != null)
        {
            // Получаем длительность эффекта
            ParticleSystem.MainModule main = particles.main;
            float duration = main.duration;
            
            // Получаем максимальное время жизни частиц
            float maxLifetime = 0f;
            if (main.startLifetime.mode == ParticleSystemCurveMode.Constant)
            {
                maxLifetime = main.startLifetime.constant;
            }
            else if (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants)
            {
                maxLifetime = main.startLifetime.constantMax;
            }
            else
            {
                // Для кривых используем максимальное значение по умолчанию
                maxLifetime = 2f;
            }
            
            // Уничтожаем объект после завершения эффекта
            Destroy(effectInstance, duration + maxLifetime + 1f); // +1 секунда для безопасности
        }
        else
        {
            // Если нет ParticleSystem, уничтожаем через 5 секунд
            Destroy(effectInstance, 5f);
        }
        
        if (debug)
        {
            Debug.Log($"[GradePanel] Создан эффект улучшения на позиции {effectPosition} с масштабом {effectScale}");
        }
    }
}
