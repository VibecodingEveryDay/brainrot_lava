using UnityEngine;
using UnityEngine.Events;
using TMPro;
#if Localization_yg
using YG;
#endif

/// <summary>
/// Объект, который можно взять и разместить.
/// Наследуется от InteractableObject для использования системы взаимодействия с UI подсказкой.
/// </summary>
public class BrainrotObject : InteractableObject
{
    [Header("Brainrot Object Settings")]
    [TextArea(1, 3)]
    [Tooltip("Имя объекта (можно использовать любые символы и регистр)")]
    [SerializeField] private string objectName = "Brainrot Object";
    
    [Tooltip("Редкость объекта (всегда используйте английские значения: Common, Rare, Exclusive, Epic, Mythic, Legendary, Secret)")]
    [SerializeField] private string rarity = "Common";
    
    [Tooltip("Базовый доход от объекта (число, до 2000000000000000)")]
    [SerializeField] private long baseIncome = 0;
    
    [Tooltip("Уровень объекта")]
    [SerializeField] private int level = 1;
    
    [Tooltip("Масштаб объекта внутри клетки (Cell)")]
    [SerializeField] private float cellScale = 1f;
    
    
    [Header("UI Префаб с данными")]
    [Tooltip("Префаб с TextMeshPro компонентами для отображения данных (Name, Rarity, Income, Level)")]
    [SerializeField] private GameObject infoPrefab;
    
    [Tooltip("Смещение префаба относительно объекта")]
    [SerializeField] private Vector3 infoPrefabOffset = new Vector3(0f, 2f, 0f);
    
    [Tooltip("Показывать префаб только когда объект размещён")]
    [SerializeField] private bool showOnlyWhenPlaced = true;
    
    [Header("Настройки переноски")]
    [Tooltip("Смещение объекта при переноске по оси X (влево/вправо относительно игрока)")]
    [SerializeField] private float carryOffsetX = 0f;
    
    [Tooltip("Смещение объекта при переноске по оси Y (вверх/вниз относительно игрока)")]
    [SerializeField] private float carryOffsetY = 0f;
    
    [Tooltip("Смещение объекта при переноске по оси Z (вперед/назад относительно игрока)")]
    [SerializeField] private float carryOffsetZ = 0f;
    
    [Tooltip("Поворот объекта при переноске по оси Y (в градусах)")]
    [SerializeField] private float carryRotationY = 0f;
    
    [Header("Настройки размещения")]
    [Tooltip("Смещение объекта при размещении по оси X (влево/вправо относительно игрока)")]
    [SerializeField] private float putOffsetX = 0f;
    
    [Tooltip("Смещение объекта при размещении по оси Y (вверх/вниз относительно земли)")]
    [SerializeField] private float putOffsetY = 0f;
    
    [Tooltip("Смещение объекта при размещении по оси Z (вперед/назад относительно игрока)")]
    [SerializeField] private float putOffsetZ = 1.5f;
    
    [Tooltip("Масштаб объекта при размещении (если 0, используется текущий масштаб)")]
    [SerializeField] private Vector3 placementScale = Vector3.zero;
    
    [Tooltip("Дополнительное смещение при размещении по оси X")]
    [SerializeField] private float placementOffsetX = 0f;
    
    [Tooltip("Дополнительное смещение при размещении по оси Z")]
    [SerializeField] private float placementOffsetZ = 0f;
    
    [Tooltip("Поворот объекта при размещении по оси Y (в градусах)")]
    [SerializeField] private float placementRotationY = 0f;
    
    [Tooltip("Поворот объекта при спавне по оси Y (в градусах)")]
    [SerializeField] private float spawnRotationY = 0f;
    
    [Tooltip("Инвертировать поворот брейнрота по Y на 180 градусов при создании босса")]
    [SerializeField] private bool invertBossRotation = false;
    
    [Header("Events")]
    [SerializeField] private UnityEvent onTake;
    [SerializeField] private UnityEvent onPut;
    
    // Состояние объекта
    private bool isCarried = false;
    private bool isPlaced = false;
    private PlayerCarryController playerCarryController;
    
    // Кэшированные компоненты для оптимизации
    private Rigidbody cachedRigidbody;
    private Collider[] cachedColliders;
    private bool componentsCached = false;
    
    // Оптимизация: флаг для отслеживания состояния UI
    private bool uiHiddenByCarry = false;
    
    /// <summary> True, если объект только что взят с PlacementPanel (чтобы не вызывать Put() в том же кадре). </summary>
    private bool justTakenFromPanel = false;
    
    // Экземпляр префаба с данными
    private GameObject infoPrefabInstance;
    private TextMeshPro nameText;
    private TextMeshPro rarityText;
    private TextMeshPro incomeText;
    private TextMeshPro levelText;
    
    protected override void Awake()
    {
        base.Awake(); // Инициализация InteractableObject (interactionRangeSqr и др.) для работы Put/взаимодействия
        // Находим PlayerCarryController
        FindPlayerCarryController();
        
        // Кэшируем компоненты один раз
        CacheComponents();
        
        // НЕ устанавливаем параметры автоматически - они должны быть установлены в инспекторе
        // Параметр takerange используется только как справочное значение
        // Пользователь должен вручную установить interactionRange и interactionTime в инспекторе
        // или синхронизировать их с takerange через OnValidate (только в редакторе)
    }
    
    /// <summary>
    /// Кэширует компоненты для оптимизации
    /// </summary>
    private void CacheComponents()
    {
        if (componentsCached) return;
        
        cachedRigidbody = GetComponent<Rigidbody>();
        cachedColliders = GetComponentsInChildren<Collider>();
        componentsCached = true;
    }
    
    private void Start()
    {
        // Убеждаемся, что PlayerCarryController найден
        if (playerCarryController == null)
        {
            FindPlayerCarryController();
        }
        
        // Инициализируем префаб с данными, если он назначен
        if (infoPrefab != null)
        {
            InitializeInfoPrefab();
        }
    }
    
    private void OnEnable()
    {
#if Localization_yg
        // Подписываемся на изменение языка для обновления текста редкости
        YG2.onSwitchLang += OnLanguageChanged;
        // Обновляем текст при включении, если префаб уже инициализирован
        if (infoPrefabInstance != null)
        {
            UpdateInfoPrefabTexts();
        }
#endif
    }
    
    private void OnDisable()
    {
#if Localization_yg
        // Отписываемся от события изменения языка
        YG2.onSwitchLang -= OnLanguageChanged;
#endif
    }
    
#if Localization_yg
    /// <summary>
    /// Обработчик изменения языка
    /// </summary>
    private void OnLanguageChanged(string lang)
    {
        // Обновляем тексты при изменении языка
        if (infoPrefabInstance != null)
        {
            UpdateInfoPrefabTexts();
        }
    }
#endif
    
    protected override void Update()
    {
        // Обновляем видимость префаба с данными
        UpdateInfoPrefabVisibility();
        
        // Если объект размещён, решаем нужно ли показывать UI взаимодействия
        if (isPlaced)
        {
            // Если объект размещён на панели (placement) — не рендерим префаб взаимодействия,
            // но оставляем логику взаимодействия через PlacementPanel
            bool isPlacedOnPanel = PlacementPanel.IsBrainrotPlacedOnPanel(this);
            if (isPlacedOnPanel)
            {
                // ВАЖНО: Инициализируем playerTransform, чтобы при взятии объекта из панели
                // HandleInput() мог работать (playerTransform используется в блоке isCarried)
                if (playerTransform == null)
                {
                    GameObject player = GameObject.FindGameObjectWithTag("Player");
                    if (player != null)
                    {
                        playerTransform = player.transform;
                    }
                }
                
                if (HasUI())
                {
                    HideUI();
                }
                return;
            }
            
            // Объект размещён на земле - вызываем базовый Update для показа UI и возможности взаимодействия
            base.Update();
            return;
        }
        
        // Если объект взят, обрабатываем ввод для возможности положить объект
        // Размещение на панели происходит через interaction с PlacementPanel
        // Размещение на земле происходит через interaction с самим объектом (когда нет активной панели)
        if (isCarried)
        {
            // Скрываем UI один раз при взятии объекта
            if (!uiHiddenByCarry && HasUI())
            {
                HideUI();
                uiHiddenByCarry = true;
            }
            
            // Обрабатываем ввод для возможности положить объект на землю (если нет активной панели)
            // Но пропускаем обновление UI и проверку расстояния для оптимизации
            if (playerTransform != null)
            {
                // Устанавливаем isPlayerInRange = true чтобы HandleInput работал
                bool wasInRange = isPlayerInRange;
                isPlayerInRange = true;
                HandleInput();
                isPlayerInRange = wasInRange; // Восстанавливаем значение
            }
            return;
        }
        
        // Сбрасываем флаг когда объект не взят
        uiHiddenByCarry = false;
        
        // ВАЖНО: Если объект размещен на панели, НЕ вызываем base.Update()
        // Это предотвращает обработку взаимодействия через BrainrotObject
        // Взаимодействие должно обрабатываться только через PlacementPanel
        if (isPlaced)
        {
            bool isPlacedOnPanel = PlacementPanel.IsBrainrotPlacedOnPanel(this);
            if (isPlacedOnPanel)
            {
                // Объект размещен на панели - не обрабатываем взаимодействие
                return;
            }
        }
        
        // Вызываем базовый Update для обработки взаимодействия и UI
        base.Update();
    }
    
    
    /// <summary>
    /// Находит PlayerCarryController в сцене
    /// </summary>
    private void FindPlayerCarryController()
    {
        if (playerCarryController == null)
        {
            playerCarryController = FindFirstObjectByType<PlayerCarryController>();
        }
    }
    
    /// <summary>
    /// Переопределяем CompleteInteraction для обработки Take/Put/Fight
    /// </summary>
    protected override void CompleteInteraction()
    {
        // ВАЖНО: Если объект размещен на панели, НЕ обрабатываем взаимодействие
        // Взаимодействие должно обрабатываться только через PlacementPanel
        if (isPlaced)
        {
            bool isPlacedOnPanel = PlacementPanel.IsBrainrotPlacedOnPanel(this);
            if (isPlacedOnPanel)
            {
                ResetInteraction();
                return;
            }
        }
        
        if (isCarried)
        {
            // ВАЖНО: Если объект только что взят с панели — не ставить на пол (взаимодействие уже обработано панелью)
            if (justTakenFromPanel)
            {
                justTakenFromPanel = false;
                ResetInteraction();
                return;
            }
            
            // Если объект уже взят, проверяем, есть ли активная панель
            PlacementPanel activePanel = PlacementPanel.GetActivePanel();
            if (activePanel != null)
            {
                // ВАЖНО: Проверяем, может ли объект быть размещен на этой панели
                // Если на панели уже есть размещённый объект, или это не тот же объект, который был взят,
                // то размещаем на земле
                bool canPlaceOnPanel = activePanel.CanPlaceBrainrot(this);
                if (canPlaceOnPanel)
                {
                    // Если можно разместить на панели, не размещаем на земле
                // Размещение должно происходить только через interaction с PlacementPanel
                ResetInteraction();
                }
                else
                {
                    // Если нельзя разместить на панели (например, там уже есть другой объект),
                    // размещаем на земле
                    Put();
                }
            }
            else
            {
                // Если нет активной панели, размещаем на земле
                Put();
                // После Put НЕ вызываем base.CompleteInteraction() - это установит interactionCompleted = true
                // Вместо этого просто сбрасываем состояние, чтобы UI мог появиться снова
                // UI уже скрыт в методе Put() через ResetInteraction()
            }
        }
        else if (isPlaced)
        {
            // ВАЖНО: Проверяем, размещен ли объект на панели (placement)
            bool isPlacedOnPanel = PlacementPanel.IsBrainrotPlacedOnPanel(this);
            
            if (isPlacedOnPanel)
            {
                // ВАЖНО: Если объект размещен на панели, взаимодействие должно обрабатываться через PlacementPanel
                ResetInteraction();
                return;
            }
            // Размещён на земле — берём в руки
            Take();
            ResetInteraction();
        }
        else
        {
            // Не взят и не размещён — берём в руки
            Take();
            ResetInteraction();
        }
    }
    
    /// <summary>
    /// Переопределяем OnInteractionComplete для дополнительной логики
    /// </summary>
    protected override void OnInteractionComplete()
    {
        // Базовая логика уже обработана в CompleteInteraction()
    }
    
    /// <summary>
    /// Взять объект в руки
    /// </summary>
    public void Take()
    {
        if (playerCarryController == null)
        {
            FindPlayerCarryController();
            if (playerCarryController == null)
            {
                Debug.LogWarning($"[BrainrotObject] {objectName}: PlayerCarryController не найден!");
                return;
            }
        }
        
        // Проверяем, может ли игрок взять объект
        if (!playerCarryController.CanCarry())
        {
            Debug.Log($"[BrainrotObject] {objectName}: Игрок уже несет другой объект!");
            return;
        }
        
        // Убеждаемся, что компоненты кэшированы
        if (!componentsCached)
        {
            CacheComponents();
        }
        
        // Устанавливаем состояние
        isCarried = true;
        isPlaced = false;
        
        // Отключаем физику объекта (если есть) - используем кэшированный компонент
        if (cachedRigidbody != null)
        {
            cachedRigidbody.isKinematic = true;
        }
        
        // Отключаем коллайдеры (чтобы объект не мешал движению) - используем кэшированный массив
        if (cachedColliders != null)
        {
            for (int i = 0; i < cachedColliders.Length; i++)
            {
                if (cachedColliders[i] != null)
                {
                    cachedColliders[i].enabled = false;
                }
            }
        }
        
        // Передаем объект в PlayerCarryController
        playerCarryController.CarryObject(this);
        
        // Скрываем UI подсказку когда объект взят
        HideUI();
        uiHiddenByCarry = true;
        
        // Вызываем событие
        onTake.Invoke();
        
        Debug.Log($"[BrainrotObject] {objectName}: Объект взят игроком");
    }
    
    /// <summary>
    /// Положить объект на землю
    /// </summary>
    public void Put()
    {
        // Размещение на панели происходит только через interaction с PlacementPanel
        // Здесь всегда размещаем на землю
        PutOnGround();
    }
    
    /// <summary>
    /// Положить объект на землю (внутренний метод)
    /// </summary>
    private void PutOnGround()
    {
        if (playerCarryController == null)
        {
            Debug.LogWarning($"[BrainrotObject] {objectName}: PlayerCarryController не найден!");
            return;
        }
        
        // Получаем позицию игрока и направление вперед
        Transform playerTransform = playerCarryController.GetPlayerTransform();
        if (playerTransform == null)
        {
            Debug.LogWarning($"[BrainrotObject] {objectName}: Transform игрока не найден!");
            return;
        }
        
        // Вычисляем позицию перед игроком с учетом смещений
        Vector3 forwardDirection = playerTransform.forward;
        Vector3 rightDirection = playerTransform.right;
        Vector3 putPosition = playerTransform.position + 
                             forwardDirection * (putOffsetZ + placementOffsetZ) + 
                             rightDirection * (putOffsetX + placementOffsetX);
        
        // Используем Raycast для определения точки на земле
        RaycastHit hit;
        float groundY = putPosition.y;
        if (Physics.Raycast(putPosition + Vector3.up * 2f, Vector3.down, out hit, 5f))
        {
            putPosition = hit.point;
            groundY = hit.point.y;
        }
        else
        {
            putPosition.y = playerTransform.position.y;
        }
        
        putPosition.y += putOffsetY;
        
        PutAtPosition(putPosition, Quaternion.Euler(0f, placementRotationY, 0f));
        
        // Опускаем объект по фактическим bounds, чтобы низ касался земли (groundY)
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                if (renderers[i] != null) bounds.Encapsulate(renderers[i].bounds);
            float sink = bounds.min.y - groundY;
            if (sink > 0.001f)
                transform.position -= Vector3.up * sink;
        }
    }
    
    /// <summary>
    /// Размещает объект на заданной позиции с заданным поворотом
    /// </summary>
    public void PutAtPosition(Vector3 position, Quaternion rotation)
    {
        // Убеждаемся, что компоненты кэшированы
        if (!componentsCached)
        {
            CacheComponents();
        }
        
        // Устанавливаем позицию объекта
        transform.position = position;
        
        // Устанавливаем поворот объекта (используем переданный поворот)
        transform.rotation = rotation;
        
        // Устанавливаем масштаб объекта при размещении (если задан)
        if (placementScale != Vector3.zero)
        {
            transform.localScale = placementScale;
        }
        
        // Включаем физику обратно - используем кэшированный компонент
        if (cachedRigidbody != null)
        {
            cachedRigidbody.isKinematic = false;
        }
        
        // Включаем коллайдеры обратно - используем кэшированный массив
        if (cachedColliders != null)
        {
            for (int i = 0; i < cachedColliders.Length; i++)
            {
                if (cachedColliders[i] != null)
                {
                    cachedColliders[i].enabled = true;
                }
            }
        }
        
        // ВАЖНО: Проверяем, размещён ли объект на панели, ПЕРЕД изменением состояния
        // Это нужно, чтобы правильно определить, где размещается объект
        // Проверяем через ссылку на панели, так как состояние может еще не обновиться
        bool isPlacedOnPanel = PlacementPanel.IsBrainrotPlacedOnPanel(this);
        
        // Устанавливаем состояние ПЕРЕД освобождением из рук
        isCarried = false;
        isPlaced = true;
        
        // ВАЖНО: Освобождаем объект из PlayerCarryController только если он еще в руках
        // Если объект размещается на панели, DropObject уже вызван в PlaceOnPanel
        // Но мы все равно проверяем, чтобы быть уверенными
        if (playerCarryController != null)
        {
            // Проверяем, есть ли объект в руках через PlayerCarryController
            BrainrotObject carriedObject = playerCarryController.GetCurrentCarriedObject();
            if (carriedObject == this)
            {
                // Объект все еще в руках - освобождаем
            playerCarryController.DropObject();
        }
        }
        
        // ВАЖНО: Если проверка выше не сработала (из-за того, что ссылка еще не была установлена),
        // проверяем еще раз после установки состояния
        if (!isPlacedOnPanel)
        {
            isPlacedOnPanel = PlacementPanel.IsBrainrotPlacedOnPanel(this);
        }
        
        // Если объект НЕ размещён на панели, сбрасываем состояние взаимодействия для показа UI
        // Если объект размещён на панели, состояние взаимодействия уже должно быть правильным
        // (объект не должен обрабатывать взаимодействие через base.Update())
        if (!isPlacedOnPanel)
        {
            // Сбрасываем состояние взаимодействия только если объект размещён на земле
            ResetInteraction();
            uiHiddenByCarry = false; // Сбрасываем флаг для повторного показа UI
        }
        else
        {
            // Если объект размещён на панели, скрываем UI и блокируем взаимодействие
            HideUI();
            uiHiddenByCarry = false;
            // НЕ вызываем ResetInteraction() - это позволит Update() правильно определить состояние
            // ВАЖНО: interactionCompleted остаётся в текущем состоянии (скорее всего false)
            // чтобы не блокировать будущие взаимодействия через панель
            
            // ВАЖНО: Убеждаемся, что объект активен и виден при размещении на панели
            if (gameObject != null)
            {
                gameObject.SetActive(true);
                // Убеждаемся, что все дочерние объекты (моделька) активны
                SetAllChildrenActive(transform, true);
            }
        }
        
        // Вызываем событие
        onPut.Invoke();
        
        Debug.Log($"[BrainrotObject] {objectName}: Объект размещен на позиции {position}, размещен на панели: {isPlacedOnPanel}, активен: {gameObject.activeSelf}");
    }
    
    /// <summary>
    /// Рекурсивно активирует/деактивирует все дочерние объекты
    /// </summary>
    private void SetAllChildrenActive(Transform parent, bool active)
    {
        if (parent == null) return;
        
        parent.gameObject.SetActive(active);
        
        foreach (Transform child in parent)
        {
            if (child != null)
            {
                SetAllChildrenActive(child, active);
            }
        }
    }
    
    /// <summary>
    /// Получить имя объекта (оригинальное английское имя, используется для внутренних целей)
    /// </summary>
    public string GetObjectName()
    {
        return objectName;
    }
    
    /// <summary>
    /// Получить локализованное имя объекта для отображения
    /// </summary>
    public string GetLocalizedName()
    {
        return GetLocalizedBrainrotName();
    }
    
    /// <summary>
    /// Получить редкость объекта
    /// </summary>
    public string GetRarity()
    {
        return rarity;
    }
    
    /// <summary>
    /// Установить редкость объекта
    /// </summary>
    public void SetRarity(string newRarity)
    {
        rarity = newRarity;
        UpdateInfoPrefabTexts();
    }
    
    /// <summary>
    /// Получить базовый доход от объекта (для обратной совместимости)
    /// </summary>
    public long GetIncome()
    {
        return baseIncome;
    }
    
    /// <summary>
    /// Получить базовый доход от объекта
    /// </summary>
    public long GetBaseIncome()
    {
        return baseIncome;
    }
    
    /// <summary>
    /// Установить базовый доход от объекта
    /// </summary>
    public void SetBaseIncome(long newBaseIncome)
    {
        baseIncome = newBaseIncome;
        UpdateInfoPrefabTexts();
    }
    
    /// <summary>
    /// Установить доход от объекта (для обратной совместимости)
    /// </summary>
    public void SetIncome(long newIncome)
    {
        baseIncome = newIncome;
        UpdateInfoPrefabTexts();
    }
    
    /// <summary>
    /// Форматирует финальный доход в формате "число + скейлер + /S" (или "/с" для русского)
    /// Использует логику из GameStorage.FormatBalance() для форматирования с скейлерами (K, M, B, T и т.д.)
    /// </summary>
    private string FormatIncome(double finalIncome)
    {
        // Получаем текущий язык для определения суффикса
        string timeSuffix = GetTimeSuffix();
        
        if (finalIncome <= 0)
        {
            return "0" + timeSuffix;
        }
        
        string formatted = FormatIncomeValue(finalIncome);
        
        // Добавляем суффикс времени в конец ("/S" для английского, "/с" для русского)
        return formatted + timeSuffix;
    }
    
    /// <summary>
    /// Получает суффикс времени для дохода в зависимости от языка
    /// </summary>
    private string GetTimeSuffix()
    {
        // Получаем текущий язык из YG2
        string currentLang = "ru"; // По умолчанию русский
#if Localization_yg
        if (YG2.lang != null)
        {
            currentLang = YG2.lang;
        }
#endif
        
        // Если язык русский, возвращаем "/с" (кириллическая буква)
        if (currentLang == "ru")
        {
            return "/с";
        }
        
        // Если язык английский или другой, возвращаем "/S" (латинская буква)
        return "/S";
    }
    
    /// <summary>
    /// Форматирует значение дохода с использованием скейлеров (логика из GameStorage.FormatBalance)
    /// </summary>
    private string FormatIncomeValue(double value)
    {
        // Нониллионы (10^30)
        if (value >= 1000000000000000000000000000000.0)
        {
            double nonillions = value / 1000000000000000000000000000000.0;
            return FormatIncomeValueHelper(nonillions, "NO");
        }
        // Октиллионы (10^27)
        else if (value >= 1000000000000000000000000000.0)
        {
            double octillions = value / 1000000000000000000000000000.0;
            return FormatIncomeValueHelper(octillions, "OC");
        }
        // Септиллионы (10^24)
        else if (value >= 1000000000000000000000000.0)
        {
            double septillions = value / 1000000000000000000000000.0;
            return FormatIncomeValueHelper(septillions, "SP");
        }
        // Секстиллионы (10^21)
        else if (value >= 1000000000000000000000.0)
        {
            double sextillions = value / 1000000000000000000000.0;
            return FormatIncomeValueHelper(sextillions, "SX");
        }
        // Квинтиллионы (10^18)
        else if (value >= 1000000000000000000.0)
        {
            double quintillions = value / 1000000000000000000.0;
            return FormatIncomeValueHelper(quintillions, "QI");
        }
        // Квадриллионы (10^15)
        else if (value >= 1000000000000000.0)
        {
            double quadrillions = value / 1000000000000000.0;
            return FormatIncomeValueHelper(quadrillions, "QA");
        }
        // Триллионы (10^12)
        else if (value >= 1000000000000.0)
        {
            double trillions = value / 1000000000000.0;
            return FormatIncomeValueHelper(trillions, "T");
        }
        // Миллиарды (10^9)
        else if (value >= 1000000000.0)
        {
            double billions = value / 1000000000.0;
            return FormatIncomeValueHelper(billions, "B");
        }
        // Миллионы (10^6)
        else if (value >= 1000000.0)
        {
            double millions = value / 1000000.0;
            return FormatIncomeValueHelper(millions, "M");
        }
        // Тысячи (10^3)
        else if (value >= 1000.0)
        {
            double thousands = value / 1000.0;
            return FormatIncomeValueHelper(thousands, "K");
        }
        else
        {
            // Меньше тысячи - показываем как целое число
            return ((long)value).ToString();
        }
    }
    
    /// <summary>
    /// Вспомогательный метод для форматирования значения дохода с суффиксом
    /// Целые числа отображаются без десятичных знаков
    /// </summary>
    private string FormatIncomeValueHelper(double value, string suffix)
    {
        // Проверяем, является ли число целым
        if (value == Mathf.Floor((float)value))
        {
            // Целое число - без десятичных знаков
            return $"{(long)value}{suffix}";
        }
        else
        {
            // Дробное число - с десятичными знаками (убираем лишние нули)
            string formatted = $"{value:F2}{suffix}".TrimEnd('0').TrimEnd('.');
            return formatted;
        }
    }
    
    /// <summary>
    /// Форматирует level в формате "Lv.число" или "Ур.число" в зависимости от языка
    /// </summary>
    private string FormatLevel(int levelValue)
    {
        // Получаем текущий язык из YG2
        string currentLang = "ru"; // По умолчанию русский
#if Localization_yg
        if (YG2.lang != null)
        {
            currentLang = YG2.lang;
        }
#endif
        
        // Если язык русский, используем "Ур."
        if (currentLang == "ru")
        {
            return $"Ур.{levelValue}";
        }
        
        // Если язык английский или другой, используем "Lv."
        return $"Lv.{levelValue}";
    }
    
    /// <summary>
    /// Проверить, взят ли объект
    /// </summary>
    public bool IsCarried()
    {
        return isCarried;
    }
    
    /// <summary>
    /// Проверить, размещен ли объект
    /// </summary>
    public bool IsPlaced()
    {
        return isPlaced;
    }
    
    /// <summary>
    /// Пометить объект как только что взятый с PlacementPanel (чтобы не ставить на пол при следующем CompleteInteraction).
    /// Вызывается из PlacementPanel.TakePlacedBrainrot() после Take().
    /// </summary>
    public void SetJustTakenFromPanel()
    {
        justTakenFromPanel = true;
    }
    
    /// <summary>
    /// Сбросить состояние объекта (для повторного использования)
    /// </summary>
    public void ResetState()
    {
        isCarried = false;
        isPlaced = false;
        
        // Убеждаемся, что компоненты кэшированы
        if (!componentsCached)
        {
            CacheComponents();
        }
        
        // Включаем коллайдеры - используем кэшированный массив
        if (cachedColliders != null)
        {
            for (int i = 0; i < cachedColliders.Length; i++)
            {
                if (cachedColliders[i] != null)
                {
                    cachedColliders[i].enabled = true;
                }
            }
        }
    }
    
    /// <summary>
    /// Получить смещение по X при переноске
    /// </summary>
    public float GetCarryOffsetX()
    {
        return carryOffsetX;
    }
    
    /// <summary>
    /// Получить смещение по Y при переноске
    /// </summary>
    public float GetCarryOffsetY()
    {
        return carryOffsetY;
    }
    
    /// <summary>
    /// Получить смещение по Z при переноске
    /// </summary>
    public float GetCarryOffsetZ()
    {
        return carryOffsetZ;
    }
    
    /// <summary>
    /// Получить поворот по Y при переноске
    /// </summary>
    public float GetCarryRotationY()
    {
        return carryRotationY;
    }
    
    /// <summary>
    /// Установить смещение по X при переноске
    /// </summary>
    public void SetCarryOffsetX(float offsetX)
    {
        carryOffsetX = offsetX;
    }
    
    /// <summary>
    /// Установить смещение по Y при переноске
    /// </summary>
    public void SetCarryOffsetY(float offsetY)
    {
        carryOffsetY = offsetY;
    }
    
    /// <summary>
    /// Установить смещение по Z при переноске
    /// </summary>
    public void SetCarryOffsetZ(float offsetZ)
    {
        carryOffsetZ = offsetZ;
    }
    
    /// <summary>
    /// Установить поворот по Y при переноске
    /// </summary>
    public void SetCarryRotationY(float rotationY)
    {
        carryRotationY = rotationY;
    }
    
    /// <summary>
    /// Получить смещение по X при размещении
    /// </summary>
    public float GetPutOffsetX()
    {
        return putOffsetX;
    }
    
    /// <summary>
    /// Получить смещение по Y при размещении
    /// </summary>
    public float GetPutOffsetY()
    {
        return putOffsetY;
    }
    
    /// <summary>
    /// Получить смещение по Z при размещении
    /// </summary>
    public float GetPutOffsetZ()
    {
        return putOffsetZ;
    }
    
    /// <summary>
    /// Установить смещение по X при размещении
    /// </summary>
    public void SetPutOffsetX(float offsetX)
    {
        putOffsetX = offsetX;
    }
    
    /// <summary>
    /// Установить смещение по Y при размещении
    /// </summary>
    public void SetPutOffsetY(float offsetY)
    {
        putOffsetY = offsetY;
    }
    
    /// <summary>
    /// Установить смещение по Z при размещении
    /// </summary>
    public void SetPutOffsetZ(float offsetZ)
    {
        putOffsetZ = offsetZ;
    }
    
    /// <summary>
    /// Получить масштаб при размещении
    /// </summary>
    public Vector3 GetPlacementScale()
    {
        return placementScale;
    }
    
    /// <summary>
    /// Установить масштаб при размещении
    /// </summary>
    public void SetPlacementScale(Vector3 scale)
    {
        placementScale = scale;
    }
    
    /// <summary>
    /// Получить дополнительное смещение по X при размещении
    /// </summary>
    public float GetPlacementOffsetX()
    {
        return placementOffsetX;
    }
    
    /// <summary>
    /// Установить дополнительное смещение по X при размещении
    /// </summary>
    public void SetPlacementOffsetX(float offsetX)
    {
        placementOffsetX = offsetX;
    }
    
    /// <summary>
    /// Получить дополнительное смещение по Z при размещении
    /// </summary>
    public float GetPlacementOffsetZ()
    {
        return placementOffsetZ;
    }
    
    /// <summary>
    /// Установить дополнительное смещение по Z при размещении
    /// </summary>
    public void SetPlacementOffsetZ(float offsetZ)
    {
        placementOffsetZ = offsetZ;
    }
    
    /// <summary>
    /// Получить поворот по Y при размещении
    /// </summary>
    public float GetPlacementRotationY()
    {
        return placementRotationY;
    }
    
    /// <summary>
    /// Установить поворот по Y при размещении
    /// </summary>
    public void SetPlacementRotationY(float rotationY)
    {
        placementRotationY = rotationY;
    }
    
    /// <summary>
    /// Получить поворот по Y при спавне (из префаба)
    /// </summary>
    public float GetSpawnRotationY()
    {
        return spawnRotationY;
    }
    
    /// <summary>
    /// Установить поворот по Y при спавне
    /// </summary>
    public void SetSpawnRotationY(float rotationY)
    {
        spawnRotationY = rotationY;
    }
    
    /// <summary>
    /// Получить уровень объекта
    /// </summary>
    public int GetLevel()
    {
        return level;
    }
    
    /// <summary>
    /// Установить уровень объекта
    /// </summary>
    public void SetLevel(int newLevel)
    {
        level = newLevel;
        UpdateInfoPrefabTexts();
    }
    
    /// <summary>
    /// Получить масштаб объекта внутри клетки (Cell)
    /// </summary>
    public float GetCellScale()
    {
        return cellScale;
    }
    
    /// <summary>
    /// Установить масштаб объекта внутри клетки (Cell)
    /// </summary>
    public void SetCellScale(float newCellScale)
    {
        cellScale = newCellScale;
    }
    
    /// <summary>
    /// Инициализирует префаб с данными
    /// </summary>
    private void InitializeInfoPrefab()
    {
        if (infoPrefab == null) return;
        
        // Создаём экземпляр префаба
        infoPrefabInstance = Instantiate(infoPrefab, transform);
        infoPrefabInstance.transform.localPosition = infoPrefabOffset;
        infoPrefabInstance.transform.localRotation = Quaternion.identity;
        
        // Добавляем простой скрипт для поворота текста к камере
        // Используем компонент InfoPrefabBillboard вместо стандартного BillboardUI
        InfoPrefabBillboard billboard = infoPrefabInstance.GetComponent<InfoPrefabBillboard>();
        if (billboard == null)
        {
            billboard = infoPrefabInstance.AddComponent<InfoPrefabBillboard>();
        }
        
        // Находим компоненты TextMeshPro по именам дочерних объектов
        FindTextComponents();
        
        // Обновляем тексты
        UpdateInfoPrefabTexts();
        
        // Устанавливаем начальную видимость
        UpdateInfoPrefabVisibility();
    }
    
    /// <summary>
    /// Находит компоненты TextMeshPro по именам дочерних объектов
    /// </summary>
    private void FindTextComponents()
    {
        if (infoPrefabInstance == null) return;
        
        // Ищем компоненты TextMeshPro в дочерних объектах по именам
        Transform[] children = infoPrefabInstance.GetComponentsInChildren<Transform>(true);
        
        foreach (Transform child in children)
        {
            TextMeshPro tmp = child.GetComponent<TextMeshPro>();
            if (tmp != null)
            {
                switch (child.name)
                {
                    case "Name":
                        nameText = tmp;
                        break;
                    case "Rarity":
                        rarityText = tmp;
                        break;
                    case "Income":
                        incomeText = tmp;
                        break;
                    case "Level":
                        levelText = tmp;
                        break;
                }
            }
        }
        
        // Проверяем, что все компоненты найдены
        if (nameText == null)
            Debug.LogWarning($"[BrainrotObject] {objectName}: Не найден TextMeshPro компонент 'Name' в префабе {infoPrefab.name}");
        if (rarityText == null)
            Debug.LogWarning($"[BrainrotObject] {objectName}: Не найден TextMeshPro компонент 'Rarity' в префабе {infoPrefab.name}");
        if (incomeText == null)
            Debug.LogWarning($"[BrainrotObject] {objectName}: Не найден TextMeshPro компонент 'Income' в префабе {infoPrefab.name}");
        if (levelText == null)
            Debug.LogWarning($"[BrainrotObject] {objectName}: Не найден TextMeshPro компонент 'Level' в префабе {infoPrefab.name}");
    }
    
    /// <summary>
    /// Обновляет тексты в префабе с данными
    /// </summary>
    private void UpdateInfoPrefabTexts()
    {
        if (nameText != null)
            nameText.text = GetLocalizedBrainrotName();
        
        if (rarityText != null)
        {
            // Получаем локализованный текст редкости
            string localizedRarity = GetLocalizedRarity(rarity);
            // Применяем цвет в зависимости от редкости (используем оригинальное английское значение для определения цвета)
            ApplyRarityColor(rarityText, localizedRarity, rarity);
        }
        
        if (incomeText != null)
        {
            // Форматируем финальный доход в формате "число + скейлер + /S"
            double finalIncome = GetFinalIncome();
            string formattedIncome = FormatIncome(finalIncome);
            incomeText.text = formattedIncome;
        }
        
        if (levelText != null)
        {
            // Форматируем level в формате "Lv.число"
            string formattedLevel = FormatLevel(level);
            levelText.text = formattedLevel;
        }
    }
    
    /// <summary>
    /// Возвращает префаб BR_Info (для отображения в Cell и т.п.).
    /// </summary>
    public GameObject GetInfoPrefab()
    {
        return infoPrefab;
    }
    
    /// <summary>
    /// Возвращает смещение префаба с данными относительно объекта.
    /// </summary>
    public Vector3 GetInfoPrefabOffset()
    {
        return infoPrefabOffset;
    }
    
    /// <summary>
    /// Заполняет экземпляр префаба BR_Info данными этого брейнрота (имя, редкость, доход, уровень).
    /// Вызывается из Cell для отображения info над клеткой.
    /// </summary>
    public void FillInfoPrefabInstance(GameObject infoInstance)
    {
        if (infoInstance == null) return;
        TextMeshPro nameTmp = null, rarityTmp = null, incomeTmp = null, levelTmp = null;
        Transform[] children = infoInstance.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            TextMeshPro tmp = child.GetComponent<TextMeshPro>();
            if (tmp == null) continue;
            switch (child.name)
            {
                case "Name": nameTmp = tmp; break;
                case "Rarity": rarityTmp = tmp; break;
                case "Income": incomeTmp = tmp; break;
                case "Level": levelTmp = tmp; break;
            }
        }
        if (nameTmp != null) nameTmp.text = GetLocalizedBrainrotName();
        if (rarityTmp != null) ApplyRarityColor(rarityTmp, GetLocalizedRarity(rarity), rarity);
        if (incomeTmp != null) incomeTmp.text = FormatIncome(GetFinalIncome());
        if (levelTmp != null) levelTmp.text = FormatLevel(level);
    }
    
    /// <summary>
    /// Получает локализованный текст редкости на основе текущего языка YG2
    /// В параметрах всегда используется английское значение, но отображается локализованный текст
    /// </summary>
    private string GetLocalizedRarity(string englishRarity)
    {
        // Получаем текущий язык из YG2
        string currentLang = "ru"; // По умолчанию русский
#if Localization_yg
        if (YG2.lang != null)
        {
            currentLang = YG2.lang;
        }
#endif
        
        // Приводим английское значение к нижнему регистру для сравнения
        string rarityLower = englishRarity.ToLower();
        
        // Если язык русский, возвращаем русский перевод
        if (currentLang == "ru")
        {
            switch (rarityLower)
            {
                case "common":
                    return "Обычный";
                case "rare":
                    return "Редкий";
                case "exclusive":
                    return "Эксклюзивный";
                case "epic":
                    return "Эпический";
                case "mythic":
                    return "Мифический";
                case "legendary":
                    return "Легендарный";
                case "secret":
                    return "Секретный";
                default:
                    return englishRarity; // Если не найдено, возвращаем оригинал
            }
        }
        
        // Если язык английский или другой, возвращаем английское значение
        // Приводим к правильному регистру (первая буква заглавная)
        if (englishRarity.Length > 0)
        {
            return char.ToUpper(englishRarity[0]) + (englishRarity.Length > 1 ? englishRarity.Substring(1).ToLower() : "");
        }
        
        return englishRarity;
    }
    
    /// <summary>
    /// Получает локализованное имя брейнрота на основе текущего языка YG2
    /// В параметрах всегда используется английское значение, но отображается локализованный текст
    /// </summary>
    private string GetLocalizedBrainrotName()
    {
        // Получаем текущий язык из YG2
        string currentLang = "ru"; // По умолчанию русский
#if Localization_yg
        if (YG2.lang != null)
        {
            currentLang = YG2.lang;
        }
#endif
        
        // Если язык русский, возвращаем русский перевод
        if (currentLang == "ru")
        {
            return GetRussianBrainrotName(objectName);
        }
        
        // Если язык английский или другой, возвращаем оригинальное имя
        return objectName;
    }
    
    /// <summary>
    /// Получает русское название брейнрота по английскому имени
    /// </summary>
    private string GetRussianBrainrotName(string englishName)
    {
        // Словарь русских переводов имен брейнротов
        switch (englishName)
        {
            case "AdminLuckyBlock":
                return "Админский Счастливый Блок";
            case "BallerinaCappuccina":
                return "Балерина Капуччина";
            case "BisonteGiuppitere":
                return "Бизонте Джуппитере";
            case "BlackholeGoat":
                return "Козёл Чёрной Дыры";
            case "ChimpanziniBananini":
                return "Шимпанзини Бананини";
            case "DragonCannelloni":
                return "Дракон Каннеллони";
            case "GorilloWatermelondrillo":
                return "Горилло Арбуздрилло";
            case "GraipussMedussi":
                return "Грайпус Медусси";
            case "JobJobJobSahur":
                return "ДжобДжобДжоб Сахур";
            case "KarkerkarKurkur":
                return "Каркеркар Куркур";
            case "KetupatKepat":
                return "Кетупат Кепат";
            case "Pakrahmatmamat":
                return "Пакрахматмамат";
            case "SpioniroGolubiro":
                return "Спиониро Голубиро";
            case "TorrtuginniDragonfrutini":
                return "Торртугинни Драгонфрутини";
            case "TralaleroTralala":
                return "Тралалеро Тралала";
            case "TungTungTungSahur":
                return "ТунгТунгТунг Сахур";
            default:
                return englishName; // Если не найдено, возвращаем оригинал
        }
    }
    
    /// <summary>
    /// Применяет цвет к тексту редкости в зависимости от значения
    /// </summary>
    private void ApplyRarityColor(TextMeshPro textComponent, string localizedRarityText, string originalEnglishRarity)
    {
        if (textComponent == null) return;
        
        // Используем оригинальное английское значение для определения цвета
        string rarityLower = originalEnglishRarity.ToLower();
        
        // Для Secret/Секретный используем специальный радужный градиент
        if (rarityLower == "secret")
        {
            ApplySecretGradient(textComponent, localizedRarityText);
            return;
        }
        
        // Для остальных редкостей применяем обычный цвет (используем английское значение)
        Color rarityColor = GetRarityColor(rarityLower);
        
        // Используем rich text для применения цвета к локализованному тексту
        string coloredText = $"<color=#{ColorUtility.ToHtmlStringRGB(rarityColor)}>{localizedRarityText}</color>";
        textComponent.text = coloredText;
    }
    
    /// <summary>
    /// Получает цвет для редкости (использует только английские названия)
    /// </summary>
    private Color GetRarityColor(string rarityLower)
    {
        switch (rarityLower)
        {
            case "common":
                return Color.white; // Белый
                
            case "rare":
                return new Color(0f, 0.8f, 0.8f); // Бирюзовый
                
            case "exclusive":
                return new Color(0f, 0.4f, 1f); // Синий
                
            case "epic":
                return new Color(0.6f, 0.2f, 1f); // Фиолетовый
                
            case "mythic":
                return new Color(1f, 0f, 0f); // Красный
                
            case "legendary":
                return new Color(1f, 0.84f, 0f); // Золотой
                
            default:
                return Color.white; // По умолчанию белый
        }
    }
    
    /// <summary>
    /// Получить множитель редкости для расчёта дохода (12 степень)
    /// </summary>
    public long GetRarityMultiplier()
    {
        string rarityLower = rarity.ToLower();
        
        switch (rarityLower)
        {
            case "common":
                return 1L;             // 1^12
            case "rare":
                return 5L;          // 2^12
            case "exclusive":
                return 10L;        // 3^12
            case "epic":
                return 50L;      // 4^12
            case "mythic":
                return 200L;     // 5^12
            case "legendary":
                return 1000L;    // 6^12
            case "secret":
                return 10000L;   // 7^12
            default:
                return 1L; // По умолчанию Common
        }
    }
    
    /// <summary>
    /// Получить финальный доход с учётом редкости и уровня
    /// Формула: baseIncome * rarityMultiplier * (1 + 1.0 * level)
    /// </summary>
    public double GetFinalIncome()
    {
        long rarityMultiplier = GetRarityMultiplier();
        double levelMultiplier = 1.0 + 1.0 * level;
        return baseIncome * rarityMultiplier * levelMultiplier;
    }
    
    /// <summary>
    /// Получить HP босса на основе дохода
    /// HP = финальный доход * множитель (по умолчанию 1.0)
    /// </summary>
    public float GetBossHP(float hpMultiplier = 1.0f)
    {
        double finalIncome = GetFinalIncome();
        return (float)finalIncome * hpMultiplier;
    }
    
    /// <summary>
    /// Применяет радужный градиент для Secret редкости
    /// </summary>
    private void ApplySecretGradient(TextMeshPro textComponent, string localizedRarityText)
    {
        if (textComponent == null) return;
        
        // Создаём радужный эффект, применяя разные цвета к каждому символу
        // Цвета радуги: красный, оранжевый, жёлтый, зелёный, синий, индиго, фиолетовый
        char[] chars = localizedRarityText.ToCharArray();
        System.Text.StringBuilder gradientText = new System.Text.StringBuilder();
        
        Color[] rainbowColors = new Color[]
        {
            new Color(1f, 0f, 0f),      // Красный
            new Color(1f, 0.5f, 0f),    // Оранжевый
            new Color(1f, 1f, 0f),      // Жёлтый
            new Color(0f, 1f, 0f),       // Зелёный
            new Color(0f, 0f, 1f),       // Синий
            new Color(0.29f, 0f, 0.51f), // Индиго
            new Color(0.58f, 0f, 0.83f)  // Фиолетовый
        };
        
        for (int i = 0; i < chars.Length; i++)
        {
            Color charColor = rainbowColors[i % rainbowColors.Length];
            string colorHex = ColorUtility.ToHtmlStringRGB(charColor);
            gradientText.Append($"<color=#{colorHex}>{chars[i]}</color>");
        }
        
        textComponent.text = gradientText.ToString();
    }
    
    /// <summary>
    /// Обновляет видимость префаба с данными в зависимости от состояния объекта
    /// </summary>
    private void UpdateInfoPrefabVisibility()
    {
        if (infoPrefabInstance == null) return;
        
        bool shouldShow = true;
        
        if (showOnlyWhenPlaced)
        {
            // Показывать только когда объект размещён
            shouldShow = isPlaced;
        }
        else
        {
            // Показывать всегда, кроме когда объект в руках
            shouldShow = !isCarried;
        }
        
        infoPrefabInstance.SetActive(shouldShow);
    }
    
    /// <summary>
    /// Принудительно показать или скрыть префаб с данными (BR_Info). Используется для превью в Cell — показывать info, скрывать только interaction.
    /// </summary>
    public void SetInfoVisible(bool visible)
    {
        if (infoPrefabInstance != null)
            infoPrefabInstance.SetActive(visible);
    }
    
    /// <summary>
    /// Обновляет данные в префабе (публичный метод для внешнего вызова)
    /// </summary>
    public void RefreshInfoPrefab()
    {
        UpdateInfoPrefabTexts();
    }
    
    /// <summary>
    /// Получить значение invertBossRotation (инвертировать поворот босса по Y на 180 градусов)
    /// </summary>
    public bool GetInvertBossRotation()
    {
        return invertBossRotation;
    }
    
    /// <summary>
    /// Установить значение invertBossRotation
    /// </summary>
    public void SetInvertBossRotation(bool value)
    {
        invertBossRotation = value;
    }
    
    /// <summary>
    /// Получить значение unfought (оставлено для совместимости API, всегда false — механика боя удалена)
    /// </summary>
    public bool IsUnfought()
    {
        return false;
    }
    
    /// <summary>
    /// Установить значение unfought (оставлено для совместимости API, без эффекта)
    /// </summary>
    public void SetUnfought(bool value)
    {
    }
    
    /// <summary>
    /// Отметить брейнрота как побеждённого (оставлено для совместимости API, без эффекта)
    /// </summary>
    public void MarkAsDefeated()
    {
    }
    
    private void OnDestroy()
    {
        // Уничтожаем экземпляр префаба при уничтожении объекта
        if (infoPrefabInstance != null)
        {
            Destroy(infoPrefabInstance);
        }
    }
}
