using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if EnvirData_yg
using YG;
#endif

/// <summary>
/// Скрипт для объектов взаимодействия с 3D UI подсказкой в стиле Roblox.
/// При приближении игрока появляется подсказка с кнопкой, которая заполняется по кругу при удержании клавиши.
/// </summary>
public class InteractableObject : MonoBehaviour
{
    [Header("Настройки взаимодействия")]
    [SerializeField] private float interactionRange = 3f;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private float interactionTime = 2f;
    
    [Header("Позиция взаимодействия")]
    [Tooltip("Точка отсчета для расчета расстояния и позиции UI. Если не назначена, используется transform.position")]
    [SerializeField] private Transform interactionPoint;
    
    [Tooltip("Использовать центр коллайдера для расчета позиции (если interactionPoint не назначен)")]
    [SerializeField] private bool useColliderCenter = false;
    
    [Header("UI Настройки")]
    [SerializeField] private Vector3 uiOffset = new Vector3(0f, 2f, 0f);
    [SerializeField] private GameObject uiPrefab;
    
    [Header("Кольцо вращения UI")]
    [Tooltip("Использовать кольцо для позиционирования UI (UI будет вращаться вокруг объекта)")]
    [SerializeField] private bool useRingRotation = false;
    
    [Tooltip("Радиус кольца вокруг объекта (горизонтальное расстояние от центра)")]
    [SerializeField] private float ringRadius = 1.5f;
    
    [Tooltip("Высота UI над объектом (вертикальное смещение)")]
    [SerializeField] private float ringHeight = 2f;
    
    [Tooltip("Скорость вращения UI по кольцу (0 = мгновенное, >0 = плавное)")]
    [SerializeField] private float ringRotationSpeed = 5f;
    
    [Header("События")]
    [SerializeField] private UnityEvent onInteractionComplete;
    
    [Header("Опциональные настройки")]
    [Tooltip("Прямая ссылка на Transform игрока (приоритетнее, чем поиск по тегу)")]
    [SerializeField] private Transform playerTransformReference;
    
    [Tooltip("Тег игрока для обнаружения. Используется только если playerTransformReference не назначен")]
    [SerializeField] private string playerTag = "Player";
    
    [Tooltip("Автоматически скрывать UI после завершения взаимодействия")]
    [SerializeField] private bool hideUIAfterInteraction = false;
    
    
    [Tooltip("Показывать отладочные сообщения в консоли")]
    [SerializeField] private bool debugMode = false;
    
    [Header("Оптимизация производительности")]
    [Tooltip("Частота обновления позиции UI (в кадрах). Больше значение = меньше обновлений")]
    [SerializeField] private int uiUpdateFrequency = 3; // Обновлять каждые 3 кадра по умолчанию (оптимизация)
    
    [Tooltip("Частота проверки расстояния до игрока (в кадрах). Больше значение = меньше проверок")]
    [SerializeField] private int distanceCheckFrequency = 2; // Проверять каждые 2 кадра по умолчанию (оптимизация)
    
    [Tooltip("Частота поиска игрока если он не найден (в секундах). Больше значение = меньше поисков")]
    [SerializeField] private float playerSearchInterval = 1f; // Искать раз в секунду
    
    [Tooltip("Частота обновления Billboard UI (в кадрах). Больше значение = меньше обновлений")]
    [SerializeField] private int billboardUpdateFrequency = 2; // Обновлять каждые 2 кадра (оптимизация)
    
    // Приватные переменные
    protected Transform playerTransform; // protected для доступа из наследников
    private bool playerNotFoundWarningShown = false;
    private GameObject currentUIInstance;
    private Image progressRingImage;
    private Image buttonIconImage;
    private Canvas uiCanvas;
    private BillboardUI billboardComponent;
    
    protected bool isPlayerInRange = false; // protected для доступа из наследников
    private float currentHoldTime = 0f;
    private bool isHoldingKey = false;
    private bool isMobileInteraction = false; // Флаг для определения мобильного взаимодействия
    private bool interactionCompleted = false;
    private float hideUITimer = 0f;
    
    // Кэшированные компоненты для оптимизации
    private Camera mainCamera;
    private Transform mainCameraTransform;
    private Collider objectCollider;
    private Vector3 cachedInteractionPosition;
    
    // Переменные для кольца вращения
    private float currentRingAngle = 0f; // Текущий угол UI на кольце
    private float targetRingAngle = 0f; // Целевой угол (направление к игроку)
    
    // Переменные для оптимизации
    private int frameCount = 0; // Счетчик кадров для оптимизации обновлений
    private int distanceCheckFrameCount = 0; // Счетчик кадров для проверки расстояния
    private int billboardFrameCount = 0; // Счетчик кадров для обновления Billboard
    private Vector3 lastUIPosition; // Последняя позиция UI для проверки изменений
    private float lastPlayerSearchTime = 0f; // Время последнего поиска игрока
    private float interactionRangeSqr; // Квадрат радиуса взаимодействия (для оптимизации)
    private Color cachedProgressColor; // Кэшированный цвет прогресса
    private bool wasHoldingKeyLastFrame = false; // Флаг для отслеживания изменения состояния клавиши
    private float lastProgressFillAmount = -1f; // Последнее значение fillAmount для оптимизации обновления
    
    protected virtual void Awake()
    {
        // Вычисляем квадрат радиуса взаимодействия один раз (для оптимизации)
        interactionRangeSqr = interactionRange * interactionRange;
        
        // НЕ инициализируем кэшированную позицию здесь - она будет обновляться в CheckPlayerDistance()
        // cachedInteractionPosition будет обновляться при первой проверке расстояния
        
        // Находим главную камеру один раз
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindFirstObjectByType<Camera>();
        }
        
        if (mainCamera != null)
        {
            mainCameraTransform = mainCamera.transform;
        }
        
        // Находим коллайдер для расчета центра (если нужно)
        // ВАЖНО: Ищем только на самом объекте, не в дочерних, чтобы не использовать позицию дочерних объектов
        if (useColliderCenter && interactionPoint == null)
        {
            objectCollider = GetComponent<Collider>();
            // НЕ ищем в дочерних объектах, чтобы избежать использования позиции дочерних мешей
        }
        
        // Используем прямую ссылку на игрока, если она назначена
        if (playerTransformReference != null)
        {
            playerTransform = playerTransformReference;
            if (debugMode)
            {
                Debug.Log($"[InteractableObject] {gameObject.name}: Используется прямая ссылка на игрока: {playerTransform.name}");
            }
        }
        else
        {
            // Иначе ищем игрока по тегу
            FindPlayer();
        }
    }
    
    private void Start()
    {
        // Проверяем наличие UI Prefab
        if (uiPrefab == null)
        {
            Debug.LogWarning($"[InteractableObject] {gameObject.name}: UI Prefab не назначен! UI не будет отображаться.");
        }
        else if (debugMode)
        {
            Debug.Log($"[InteractableObject] {gameObject.name}: UI Prefab назначен: {uiPrefab.name}");
        }
        
        // Проверяем наличие игрока
        if (playerTransform == null)
        {
            if (playerTransformReference == null)
            {
                FindPlayer();
            }
            else
            {
                playerTransform = playerTransformReference;
                if (debugMode && playerTransform != null)
                {
                    Debug.Log($"[InteractableObject] {gameObject.name}: Используется прямая ссылка на игрока из Start: {playerTransform.name}");
                }
            }
            
            if (playerTransform == null && !playerNotFoundWarningShown)
            {
                Debug.LogWarning($"[InteractableObject] {gameObject.name}: Игрок не найден! Проверьте тег '{playerTag}' или назначьте playerTransformReference в инспекторе.");
                playerNotFoundWarningShown = true;
            }
            else if (playerTransform != null && debugMode)
            {
                Debug.Log($"[InteractableObject] {gameObject.name}: Игрок найден: {playerTransform.name}, позиция: {playerTransform.position}");
            }
        }
    }
    
    protected virtual void Update()
    {
        // Если UI скрыт после взаимодействия, отсчитываем таймер
        // НО продолжаем проверять расстояние, чтобы UI мог появиться снова при входе в радиус
        if (hideUITimer > 0f)
        {
            hideUITimer -= Time.deltaTime;
            if (hideUITimer <= 0f && currentUIInstance != null)
            {
                DestroyUI();
                // Сбрасываем флаг завершения взаимодействия, чтобы UI мог появиться снова
                if (hideUIAfterInteraction)
                {
                    interactionCompleted = false;
                }
            }
            // НЕ выходим здесь - продолжаем проверять расстояние и создавать UI при необходимости
        }
        
        // Проверяем наличие игрока (оптимизировано - не каждый кадр)
        if (playerTransform == null)
        {
            // Если прямая ссылка не назначена, ищем по тегу с интервалом
            if (playerTransformReference == null)
            {
                // Ищем игрока только с заданным интервалом
                if (Time.time - lastPlayerSearchTime >= playerSearchInterval)
                {
                    FindPlayer();
                    lastPlayerSearchTime = Time.time;
                }
            }
            else
            {
                // Используем прямую ссылку
                playerTransform = playerTransformReference;
                if (debugMode && playerTransform != null)
                {
                    Debug.Log($"[InteractableObject] {gameObject.name}: Используется прямая ссылка на игрока: {playerTransform.name}");
                }
            }
            
            if (playerTransform == null)
            {
                // Если игрок не найден, скрываем UI
                if (currentUIInstance != null)
                {
                    DestroyUI();
                }
                
                // Показываем предупреждение только раз в секунду, чтобы не спамить консоль
                if (debugMode && Time.frameCount % 60 == 0)
                {
                    Debug.LogWarning($"[InteractableObject] {gameObject.name}: Игрок не найден! UI не будет отображаться. Проверьте playerTransformReference или тег '{playerTag}'");
                }
                return;
            }
        }
        
        // Проверяем расстояние до игрока (с оптимизацией частоты)
        distanceCheckFrameCount++;
        bool distanceCheckedThisFrame = false;
        if (distanceCheckFrameCount >= distanceCheckFrequency)
        {
            distanceCheckFrameCount = 0;
            CheckPlayerDistance();
            distanceCheckedThisFrame = true;
        }
        
        // ВАЖНО: Если UI не создан и игрок должен быть в радиусе, проверяем расстояние вручную
        // Это нужно для случая, когда игрок входит в радиус между проверками
        if (!distanceCheckedThisFrame && currentUIInstance == null && playerTransform != null && !interactionCompleted)
        {
            // Быстрая проверка расстояния для создания UI (используем кэшированную позицию)
            Vector3 toPlayer = playerTransform.position - cachedInteractionPosition;
            float sqrDistance = toPlayer.sqrMagnitude;
            if (sqrDistance <= interactionRangeSqr)
            {
                // Игрок в радиусе, создаем UI
                isPlayerInRange = true;
                CreateUI();
            }
        }
        
        // Обновляем UI (только если UI существует и активен)
        if (currentUIInstance != null && currentUIInstance.activeSelf)
        {
            UpdateUI();
        }
        
        // Обрабатываем ввод
        HandleInput();
    }
    
    private void LateUpdate()
    {
        // Обновляем поворот UI к камере в LateUpdate (после всех обновлений камеры)
        // Оптимизация: обновляем не каждый кадр, а с заданной частотой
        if (billboardComponent != null && currentUIInstance != null && currentUIInstance.activeSelf)
        {
            billboardFrameCount++;
            if (billboardFrameCount >= billboardUpdateFrequency)
            {
                billboardFrameCount = 0;
                billboardComponent.UpdateRotation();
            }
        }
    }
    
    /// <summary>
    /// Находит игрока по тегу
    /// </summary>
    private void FindPlayer()
    {
        try
        {
            GameObject player = GameObject.FindGameObjectWithTag(playerTag);
            if (player != null)
            {
                playerTransform = player.transform;
                if (debugMode)
                {
                    Debug.Log($"[InteractableObject] {gameObject.name}: Игрок найден по тегу '{playerTag}': {player.name}");
                }
                playerNotFoundWarningShown = false;
            }
            else if (debugMode && !playerNotFoundWarningShown)
            {
                Debug.LogWarning($"[InteractableObject] {gameObject.name}: Игрок с тегом '{playerTag}' не найден в сцене!");
            }
        }
        catch (UnityException)
        {
            if (debugMode && !playerNotFoundWarningShown)
            {
                Debug.LogWarning($"[InteractableObject] {gameObject.name}: Тег '{playerTag}' не существует! Создайте тег в TagManager или назначьте playerTransformReference.");
            }
        }
    }
    
    /// <summary>
    /// Переопределение позиции взаимодействия (для наследников, например Cell с центром по pivot/bounds).
    /// </summary>
    protected virtual bool TryGetCustomInteractionPosition(out Vector3 worldPosition)
    {
        worldPosition = default;
        return false;
    }
    
    /// <summary>
    /// Получает позицию точки взаимодействия
    /// </summary>
    private Vector3 GetInteractionPosition()
    {
        // Приоритет 0: Кастомная позиция от наследника
        if (TryGetCustomInteractionPosition(out Vector3 customPos))
        {
            return customPos;
        }
        
        // Приоритет 1: Используем специальную точку взаимодействия
        if (interactionPoint != null)
        {
            return interactionPoint.position;
        }
        
        // Приоритет 2: Используем центр коллайдера (проверяем и находим коллайдер если нужно)
        if (useColliderCenter)
        {
            // Если коллайдер еще не найден, пытаемся найти его
            if (objectCollider == null)
            {
                // ВАЖНО: Сначала ищем коллайдер на самом объекте, а не в дочерних
                objectCollider = GetComponent<Collider>();
            }
            
            // Если коллайдер найден на самом объекте, используем его центр
            if (objectCollider != null && objectCollider.transform == transform)
            {
                Vector3 center = objectCollider.bounds.center;
                // Проверяем, что центр коллайдера валиден
                if (center != Vector3.zero || transform.position == Vector3.zero)
                {
                    return center;
                }
            }
            // Если коллайдер НЕ найден на самом объекте, или найден в дочернем объекте,
            // НЕ используем центр дочернего коллайдера - используем позицию самого объекта
            // (transform.position будет использован в конце функции)
        }
        
        // Приоритет 3: Используем позицию самого объекта (не дочерних)
        // ВАЖНО: transform.position всегда возвращает мировую позицию объекта
        return transform.position;
    }
    
    /// <summary>
    /// Проверяет расстояние до игрока и обновляет состояние isPlayerInRange
    /// </summary>
    private void CheckPlayerDistance()
    {
        if (playerTransform == null)
        {
            if (debugMode)
            {
                Debug.LogWarning($"[InteractableObject] {gameObject.name}: playerTransform == null, игрок не найден!");
            }
            return;
        }
        
        // Получаем актуальную позицию точки взаимодействия (ВАЖНО: обновляем каждый раз)
        Vector3 interactionPos = GetInteractionPosition();
        
        // ВАЖНО: Если объект имеет родителя, transform.position может быть локальной позицией
        // Используем transform.position напрямую, но проверяем, что он правильный
        // Если interactionPoint не назначен и useColliderCenter не включен, используем transform.position
        if (interactionPoint == null && !useColliderCenter)
        {
            // Используем мировую позицию напрямую
            interactionPos = transform.position;
            
            // Если позиция все еще (0,0,0), возможно объект действительно в начале координат
            // Или это проблема с инициализацией - в этом случае логируем предупреждение
            if (interactionPos == Vector3.zero && debugMode && Time.frameCount % 60 == 0)
            {
                Debug.LogWarning($"[InteractableObject] {gameObject.name}: transform.position = (0,0,0)! " +
                                $"Возможно объект находится в начале координат или имеет проблему с инициализацией. " +
                                $"Проверьте позицию объекта в инспекторе. " +
                                $"World Position: {transform.position}, Local Position: {transform.localPosition}, " +
                                $"Has Parent: {(transform.parent != null ? transform.parent.name : "None")}");
            }
        }
        
        cachedInteractionPosition = interactionPos;
        
        // Используем SqrMagnitude вместо Distance для оптимизации (избегаем вычисления квадратного корня)
        Vector3 toPlayer = playerTransform.position - cachedInteractionPosition;
        float sqrDistanceToPlayer = toPlayer.sqrMagnitude;
        bool wasInRange = isPlayerInRange;
        
        // ВАЖНО: Убеждаемся, что квадрат радиуса актуален (на случай если радиус изменился в инспекторе)
        interactionRangeSqr = interactionRange * interactionRange;
        
        isPlayerInRange = sqrDistanceToPlayer <= interactionRangeSqr;
        
        if (debugMode)
        {
            // Логируем расстояние каждые 0.5 секунды или при изменении состояния
            if (wasInRange != isPlayerInRange || Time.frameCount % 30 == 0)
            {
                float distanceToPlayer = Mathf.Sqrt(sqrDistanceToPlayer); // Вычисляем только для логов
                
                // Детальное логирование для диагностики
                Debug.Log($"[InteractableObject] {gameObject.name}: " +
                         $"Расстояние: {distanceToPlayer:F2}, " +
                         $"Радиус: {interactionRange:F2}, " +
                         $"SqrDistance: {sqrDistanceToPlayer:F2}, " +
                         $"SqrRange: {interactionRangeSqr:F2}, " +
                         $"В радиусе: {isPlayerInRange}, " +
                         $"Позиция объекта: {cachedInteractionPosition}, " +
                         $"Позиция игрока: {playerTransform.position}");
            }
        }
        
        // Если игрок вышел из радиуса во время удержания клавиши, сбрасываем прогресс
        if (wasInRange && !isPlayerInRange)
        {
            // Сбрасываем прогресс, если удерживалась клавиша
            if (isHoldingKey)
            {
                ResetProgress();
            }
            
            // Сбрасываем таймер скрытия UI (если был установлен)
            // Это позволяет UI появиться снова при следующем входе в радиус
            hideUITimer = 0f;
            
            // НЕ сбрасываем флаг завершения взаимодействия при выходе из радиуса
            // После завершения взаимодействия UI больше не должен появляться
            // Для повторного использования нужно вызвать ResetInteraction() или сбросить interactionCompleted вручную
            
            // Уничтожаем UI при выходе из радиуса
            if (currentUIInstance != null)
            {
                if (debugMode)
                {
                    Debug.Log($"[InteractableObject] {gameObject.name}: Игрок вышел из радиуса, уничтожаю UI и сбрасываю состояние. " +
                             $"interactionCompleted: {interactionCompleted}, hideUIAfterInteraction: {hideUIAfterInteraction}, hideUITimer: {hideUITimer}");
                }
                DestroyUI();
            }
            else if (debugMode)
            {
                Debug.Log($"[InteractableObject] {gameObject.name}: Игрок вышел из радиуса, UI уже уничтожен. " +
                         $"Сбрасываю состояние: interactionCompleted={interactionCompleted}, hideUITimer={hideUITimer}");
            }
        }
        
        // Показываем UI при входе в радиус (если UI еще не создан или скрыт)
        // ВАЖНО: Если взаимодействие завершено, UI больше не показывается
        // Наследники могут переопределить ShouldShowInteractionUI() (например, только ближайшая Cell)
        bool shouldCreateUI = isPlayerInRange && !interactionCompleted && ShouldShowInteractionUI();
        bool uiExistsButHidden = currentUIInstance != null && !currentUIInstance.activeSelf;
        
        if (shouldCreateUI)
        {
            if (currentUIInstance == null)
            {
                // UI не существует - создаем новый
                if (debugMode)
                {
                    Debug.Log($"[InteractableObject] {gameObject.name}: Условие для создания UI выполнено! Создаю UI...");
                }
                CreateUI();
            }
            else if (uiExistsButHidden)
            {
                // UI существует но скрыт - показываем его
                ShowUI();
                if (debugMode)
                {
                    Debug.Log($"[InteractableObject] {gameObject.name}: Показываю скрытый UI");
                }
            }
        }
        else if (isPlayerInRange && currentUIInstance == null && interactionCompleted && debugMode)
        {
            Debug.Log($"[InteractableObject] {gameObject.name}: UI не создается - взаимодействие уже завершено");
        }
        // Если в радиусе, но показывать UI не нужно (например, не ближайшая Cell) — скрываем UI
        if (isPlayerInRange && currentUIInstance != null && !ShouldShowInteractionUI())
        {
            DestroyUI();
        }
    }
    
    /// <summary>
    /// Создает экземпляр UI над объектом
    /// </summary>
    private void CreateUI()
    {
        if (uiPrefab == null)
        {
            if (debugMode)
            {
                Debug.LogWarning($"[InteractableObject] {gameObject.name}: Не удалось создать UI - Prefab не назначен!");
            }
            return;
        }
        
        // Получаем позицию для UI
        Vector3 uiPosition = CalculateUIPositionOnRing();
        
        if (debugMode)
        {
            if (useRingRotation)
            {
                Debug.Log($"[InteractableObject] {gameObject.name}: Создаю UI на кольце в позиции {uiPosition} (радиус: {ringRadius}, высота: {ringHeight})");
            }
            else
            {
                Debug.Log($"[InteractableObject] {gameObject.name}: Создаю UI в позиции {uiPosition} (базовая позиция: {GetInteractionPosition()}, offset: {uiOffset})");
            }
        }
        
        // Инициализируем угол кольца
        if (useRingRotation && playerTransform != null)
        {
            // ВАЖНО: Учитываем uiOffset при вычислении направления
            Vector3 ringCenter = GetInteractionPosition() + uiOffset;
            Vector3 toPlayer = playerTransform.position - ringCenter;
            toPlayer.y = 0f;
            if (toPlayer.magnitude > 0.01f)
            {
                currentRingAngle = Mathf.Atan2(toPlayer.x, toPlayer.z) * Mathf.Rad2Deg;
                targetRingAngle = currentRingAngle;
            }
        }
        
        // Создаем экземпляр UI
        currentUIInstance = Instantiate(uiPrefab, uiPosition, Quaternion.identity);
        
        // ВАЖНО: НЕ устанавливаем родителя сразу, чтобы избежать проблем с масштабом и позицией
        // Родитель будет установлен после настройки Canvas
        // currentUIInstance.transform.SetParent(transform);
        
        // Убеждаемся, что UI активен сразу после создания
        if (!currentUIInstance.activeSelf)
        {
            currentUIInstance.SetActive(true);
        }
        
        // Инициализируем последнюю позицию для оптимизации
        lastUIPosition = uiPosition;
        
        // Находим компоненты UI (оптимизированный поиск)
        uiCanvas = currentUIInstance.GetComponent<Canvas>();
        if (uiCanvas == null)
        {
            uiCanvas = currentUIInstance.GetComponentInChildren<Canvas>(false); // false = не искать в неактивных
        }
        
        // Оптимизированный поиск компонентов Image (кэшируем результат)
        // Используем GetComponentsInChildren только один раз
        Image[] images = currentUIInstance.GetComponentsInChildren<Image>(false); // false = не искать в неактивных
        bool progressRingFound = false;
        
        // Сначала ищем кольцо прогресса
        for (int i = 0; i < images.Length; i++)
        {
            Image img = images[i];
            if (img != null && img.type == Image.Type.Filled && img.fillMethod == Image.FillMethod.Radial360)
            {
                progressRingImage = img;
                // Изначально скрываем кольцо прогресса
                progressRingImage.fillAmount = 0f;
                Color progressColor = progressRingImage.color;
                progressColor.a = 0f;
                progressRingImage.color = progressColor;
                progressRingFound = true;
                if (debugMode)
                {
                    Debug.Log($"[InteractableObject] {gameObject.name}: Кольцо прогресса найдено: {img.name}");
                }
                break;
            }
        }
        
        if (!progressRingFound && debugMode)
        {
            Debug.LogWarning($"[InteractableObject] {gameObject.name}: Кольцо прогресса не найдено! Убедитесь, что в UI Prefab есть Image с Type=Filled и Fill Method=Radial 360");
        }
        
        // Находим иконку кнопки (первый Image, который не является кольцом прогресса)
        for (int i = 0; i < images.Length; i++)
        {
            Image img = images[i];
            if (img != null && img != progressRingImage)
            {
                buttonIconImage = img;
                // Убеждаемся, что иконка кнопки видна
                if (buttonIconImage != null)
                {
                    // Проверяем, является ли устройство мобильным
                    bool isMobileDevice = IsMobileDevice();
                    
                    // На мобильных устройствах скрываем иконку кнопки (Image "E")
                    if (isMobileDevice)
                    {
                        buttonIconImage.gameObject.SetActive(false);
                        if (debugMode)
                        {
                            Debug.Log($"[InteractableObject] {gameObject.name}: Иконка кнопки '{buttonIconImage.name}' скрыта на мобильном устройстве");
                        }
                    }
                    else
                    {
                        // Активируем GameObject иконки если он неактивен
                        if (!buttonIconImage.gameObject.activeSelf)
                        {
                            buttonIconImage.gameObject.SetActive(true);
                        }
                        
                        // Делаем иконку полностью видимой
                        Color buttonColor = buttonIconImage.color;
                        buttonColor.a = 1f;
                        buttonIconImage.color = buttonColor;
                        
                        // Убеждаемся, что Image компонент включен
                        buttonIconImage.enabled = true;
                        
                        if (debugMode)
                        {
                            Debug.Log($"[InteractableObject] {gameObject.name}: Иконка кнопки найдена и настроена: {buttonIconImage.name}, Активна: {buttonIconImage.gameObject.activeSelf}, Видима: {buttonColor.a}");
                        }
                    }
                }
                break;
            }
        }
        
        // Предупреждение если иконка кнопки не найдена
        if (buttonIconImage == null && debugMode)
        {
            Debug.LogWarning($"[InteractableObject] {gameObject.name}: Иконка кнопки не найдена! Убедитесь, что в UI Prefab есть Image компонент (не являющийся кольцом прогресса). Найдено изображений: {images.Length}");
        }
        
        // Добавляем компонент Billboard для поворота к камере
        // Если используется кольцо, поворот будет управляться вручную в зависимости от позиции
        if (mainCameraTransform != null)
        {
            billboardComponent = currentUIInstance.GetComponent<BillboardUI>();
            if (billboardComponent == null)
            {
                billboardComponent = currentUIInstance.AddComponent<BillboardUI>();
            }
            
            // Если используется кольцо, отключаем автоматический поворот Billboard
            // и будем управлять поворотом вручную в зависимости от позиции на кольце
            if (useRingRotation)
            {
                // Для кольца поворачиваем UI к игроку/камере, чтобы E была видна (не в центр объекта!)
                Vector3 directionToTarget = Vector3.zero;
                if (playerTransform != null)
                {
                    directionToTarget = playerTransform.position - uiPosition;
                }
                else if (mainCameraTransform != null)
                {
                    directionToTarget = mainCameraTransform.position - uiPosition;
                }
                directionToTarget.y = 0f;
                if (directionToTarget.sqrMagnitude > 0.0001f)
                {
                    directionToTarget.Normalize();
                    float yRotation = Mathf.Atan2(directionToTarget.x, directionToTarget.z) * Mathf.Rad2Deg;
                    currentUIInstance.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
                }
            }
            else
            {
                // Для стандартного режима используем Billboard
                billboardComponent.SetCameraTransform(mainCameraTransform);
                // add180YOffset задан в префабе для коррекции поворота
            }
        }
        
        // Убеждаемся, что Canvas настроен как World Space
        if (uiCanvas != null)
        {
            // Активируем Canvas если он неактивен
            if (!uiCanvas.gameObject.activeSelf)
            {
                uiCanvas.gameObject.SetActive(true);
            }
            
            uiCanvas.renderMode = RenderMode.WorldSpace;
            
            // Проверяем и устанавливаем размер Canvas для World Space
            RectTransform canvasRect = uiCanvas.GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                // Устанавливаем позицию Canvas в мировых координатах
                canvasRect.position = uiPosition;
                
                // Для World Space Canvas обычно нужен маленький размер (например, 0.01)
                // Если размер слишком большой, UI может быть не виден
                if (canvasRect.localScale.x > 0.1f || canvasRect.localScale.y > 0.1f || canvasRect.localScale.z > 0.1f)
                {
                    // Устанавливаем стандартный размер для World Space UI
                    canvasRect.localScale = new Vector3(0.01f, 0.01f, 0.01f);
                    if (debugMode)
                    {
                        Debug.Log($"[InteractableObject] {gameObject.name}: Установлен размер Canvas для World Space: {canvasRect.localScale}");
                    }
                }
                
                // Убеждаемся, что Canvas имеет правильный размер
                if (canvasRect.sizeDelta.x < 0.1f || canvasRect.sizeDelta.y < 0.1f)
                {
                    // Устанавливаем минимальный размер если он слишком маленький
                    canvasRect.sizeDelta = new Vector2(100f, 100f);
                    if (debugMode)
                    {
                        Debug.Log($"[InteractableObject] {gameObject.name}: Установлен размер Canvas RectTransform: {canvasRect.sizeDelta}");
                    }
                }
                
                // Убеждаемся, что локальная позиция правильная (для World Space должна быть 0,0,0 относительно родителя)
                canvasRect.localPosition = Vector3.zero;
            }
            
            // Теперь устанавливаем родителя после настройки Canvas
            currentUIInstance.transform.SetParent(transform, true); // true = сохраняем мировую позицию
            
            // Убеждаемся, что Canvas активен и виден
            if (!currentUIInstance.activeSelf)
            {
                currentUIInstance.SetActive(true);
                if (debugMode)
                {
                    Debug.Log($"[InteractableObject] {gameObject.name}: UI был неактивен, активировал его");
                }
            }
            
            // Убеждаемся, что Canvas виден
            CanvasGroup canvasGroup = uiCanvas.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                if (canvasGroup.alpha < 0.01f)
                {
                    canvasGroup.alpha = 1f;
                    if (debugMode)
                    {
                        Debug.Log($"[InteractableObject] {gameObject.name}: Canvas был прозрачным, установил alpha = 1");
                    }
                }
                // Убеждаемся, что CanvasGroup не блокирует взаимодействие
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            
            if (debugMode)
            {
                Debug.Log($"[InteractableObject] {gameObject.name}: Canvas настроен как World Space. Позиция: {currentUIInstance.transform.position}, Активен: {currentUIInstance.activeSelf}, Canvas активен: {uiCanvas.gameObject.activeSelf}, Scale: {(canvasRect != null ? canvasRect.localScale.ToString() : "N/A")}");
            }
        }
        else
        {
            Debug.LogWarning($"[InteractableObject] {gameObject.name}: Canvas не найден в UI Prefab! UI не будет отображаться.");
        }
        
        // Дополнительная проверка: убеждаемся, что UI виден
        if (currentUIInstance != null)
        {
            // Убеждаемся, что UI активен
            if (!currentUIInstance.activeSelf)
            {
                currentUIInstance.SetActive(true);
                if (debugMode)
                {
                    Debug.Log($"[InteractableObject] {gameObject.name}: UI был неактивен, активировал его");
                }
            }
            
            // Убеждаемся, что все родительские объекты активны
            Transform parent = currentUIInstance.transform.parent;
            while (parent != null)
            {
                if (!parent.gameObject.activeSelf)
                {
                    parent.gameObject.SetActive(true);
                    if (debugMode)
                    {
                        Debug.Log($"[InteractableObject] {gameObject.name}: Активировал родительский объект: {parent.name}");
                    }
                }
                parent = parent.parent;
            }
            
            // Убеждаемся, что Canvas виден
            if (uiCanvas != null)
            {
                // Активируем Canvas если он неактивен
                if (!uiCanvas.gameObject.activeSelf)
                {
                    uiCanvas.gameObject.SetActive(true);
                    if (debugMode)
                    {
                        Debug.Log($"[InteractableObject] {gameObject.name}: Canvas был неактивен, активировал его");
                    }
                }
                
                CanvasGroup canvasGroup = uiCanvas.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    if (canvasGroup.alpha < 0.99f)
                    {
                        canvasGroup.alpha = 1f;
                        if (debugMode)
                        {
                            Debug.Log($"[InteractableObject] {gameObject.name}: Canvas был прозрачным, установил alpha = 1");
                        }
                    }
                    // Убеждаемся, что CanvasGroup не блокирует взаимодействие
                    canvasGroup.interactable = true;
                    canvasGroup.blocksRaycasts = true;
                }
            }
            
            // Убеждаемся, что все дочерние элементы UI активны и видимы
            RectTransform[] allChildren = currentUIInstance.GetComponentsInChildren<RectTransform>(true);
            int activatedCount = 0;
            int visibleImageCount = 0;
            bool isMobileDevice = IsMobileDevice();
            
            foreach (RectTransform child in allChildren)
            {
                if (child != null && child.gameObject != currentUIInstance)
                {
                    // ВАЖНО: На мобильных устройствах НЕ активируем элемент "E" (Image с именем "E")
                    // Проверяем именно по имени, чтобы не скрыть background или другие элементы
                    if (isMobileDevice && child.name.Equals("E", System.StringComparison.OrdinalIgnoreCase))
                    {
                        // Пропускаем активацию элемента "E" на мобильных устройствах
                        continue;
                    }
                    
                    // Активируем все дочерние элементы (кроме "E" на мобильных)
                    if (!child.gameObject.activeSelf)
                    {
                        child.gameObject.SetActive(true);
                        activatedCount++;
                    }
                    
                    // Убеждаемся, что все Image компоненты видимы
                    // ВАЖНО: На мобильных устройствах пропускаем обработку Image "E" (она уже скрыта)
                    if (isMobileDevice && child.name.Equals("E", System.StringComparison.OrdinalIgnoreCase))
                    {
                        // Пропускаем обработку Image "E" на мобильных устройствах
                        continue;
                    }
                    
                    Image childImage = child.GetComponent<Image>();
                    if (childImage != null)
                    {
                        // Включаем Image компонент
                        childImage.enabled = true;
                        
                        // Делаем видимым (кроме кольца прогресса, которое скрыто по умолчанию)
                        if (childImage != progressRingImage)
                        {
                            Color imgColor = childImage.color;
                            if (imgColor.a < 0.99f)
                            {
                                imgColor.a = 1f;
                                childImage.color = imgColor;
                                visibleImageCount++;
                            }
                        }
                    }
                    
                    // Убеждаемся, что все CanvasGroup видимы
                    CanvasGroup childCanvasGroup = child.GetComponent<CanvasGroup>();
                    if (childCanvasGroup != null)
                    {
                        if (childCanvasGroup.alpha < 0.99f)
                        {
                            childCanvasGroup.alpha = 1f;
                        }
                        childCanvasGroup.interactable = true;
                        childCanvasGroup.blocksRaycasts = true;
                    }
                    
                    // Убеждаемся, что все Text компоненты видимы
                    TMPro.TextMeshProUGUI tmpText = child.GetComponent<TMPro.TextMeshProUGUI>();
                    if (tmpText != null)
                    {
                        tmpText.enabled = true;
                        Color textColor = tmpText.color;
                        if (textColor.a < 0.99f)
                        {
                            textColor.a = 1f;
                            tmpText.color = textColor;
                        }
                    }
                    
                    UnityEngine.UI.Text legacyText = child.GetComponent<UnityEngine.UI.Text>();
                    if (legacyText != null)
                    {
                        legacyText.enabled = true;
                        Color textColor = legacyText.color;
                        if (textColor.a < 0.99f)
                        {
                            textColor.a = 1f;
                            legacyText.color = textColor;
                        }
                    }
                }
            }
            
            if (debugMode)
            {
                Debug.Log($"[InteractableObject] {gameObject.name}: Активировано дочерних элементов: {activatedCount}, Видимых изображений: {visibleImageCount}, Всего элементов: {allChildren.Length}");
            }
            
            // Финальная проверка видимости UI
            if (uiCanvas != null && mainCamera != null)
            {
                // Проверяем расстояние от камеры до UI
                float distanceToCamera = Vector3.Distance(currentUIInstance.transform.position, mainCameraTransform.position);
                
                // Проверяем, находится ли UI в поле зрения камеры
                Vector3 viewportPos = mainCamera.WorldToViewportPoint(currentUIInstance.transform.position);
                bool isInViewport = viewportPos.x > 0 && viewportPos.x < 1 && viewportPos.y > 0 && viewportPos.y < 1 && viewportPos.z > 0;
                
                if (debugMode)
                {
                    Debug.Log($"[InteractableObject] {gameObject.name}: UI создан успешно. " +
                             $"GameObject активен: {currentUIInstance.activeSelf}, " +
                             $"Позиция: {currentUIInstance.transform.position}, " +
                             $"Иконка кнопки: {(buttonIconImage != null ? buttonIconImage.name + " (активна: " + buttonIconImage.gameObject.activeSelf + ", видима: " + (buttonIconImage.color.a > 0.5f) + ")" : "не найдена")}, " +
                             $"Canvas активен: {uiCanvas.gameObject.activeSelf}, " +
                             $"Расстояние до камеры: {distanceToCamera:F2}, " +
                             $"В поле зрения: {isInViewport}, " +
                             $"Viewport позиция: {viewportPos}");
                }
                
                // Если UI не в поле зрения, предупреждаем
                if (!isInViewport && debugMode)
                {
                    Debug.LogWarning($"[InteractableObject] {gameObject.name}: UI создан, но не находится в поле зрения камеры! Viewport позиция: {viewportPos}");
                }
            }
            else if (debugMode)
            {
                Debug.Log($"[InteractableObject] {gameObject.name}: UI создан успешно. GameObject активен: {currentUIInstance.activeSelf}, Позиция: {currentUIInstance.transform.position}, Иконка кнопки: {(buttonIconImage != null ? buttonIconImage.name : "не найдена")}, Canvas активен: {(uiCanvas != null ? uiCanvas.gameObject.activeSelf.ToString() : "N/A")}");
            }
        }
    }
    
    /// <summary>
    /// Уничтожает экземпляр UI
    /// </summary>
    private void DestroyUI()
    {
        if (currentUIInstance != null)
        {
            // Сбрасываем прогресс перед уничтожением UI
            if (isHoldingKey)
            {
                ResetProgress();
            }
            
            Destroy(currentUIInstance);
            currentUIInstance = null;
            progressRingImage = null;
            buttonIconImage = null;
            billboardComponent = null;
            uiCanvas = null;
            
            if (debugMode)
            {
                Debug.Log($"[InteractableObject] {gameObject.name}: UI уничтожен, состояние сброшено");
            }
        }
    }
    
    /// <summary>
    /// Скрывает UI подсказку (публичный метод для использования в наследниках)
    /// </summary>
    public void HideUI()
    {
        if (currentUIInstance != null)
        {
            currentUIInstance.SetActive(false);
        }
    }
    
    /// <summary>
    /// Показывает UI подсказку (публичный метод для использования в наследниках)
    /// </summary>
    public void ShowUI()
    {
        if (currentUIInstance != null)
        {
            currentUIInstance.SetActive(true);
        }
    }
    
    /// <summary>
    /// Проверяет, существует ли UI подсказка
    /// </summary>
    public bool HasUI()
    {
        return currentUIInstance != null && currentUIInstance.activeSelf;
    }
    
    /// <summary>
    /// Вычисляет позицию UI на кольце вокруг объекта (оптимизированная версия)
    /// </summary>
    private Vector3 CalculateUIPositionOnRing()
    {
        Vector3 centerPosition = cachedInteractionPosition; // Используем кэшированную позицию
        
        if (!useRingRotation)
        {
            // Стандартное позиционирование с offset
            return centerPosition + uiOffset;
        }
        
        // ВАЖНО: Центр кольца должен учитывать uiOffset
        Vector3 ringCenter = centerPosition + uiOffset;
        
        // Оптимизация: кэшируем вычисления направления
        Vector3 directionToPlayer = Vector3.zero;
        bool directionFound = false;
        
        if (playerTransform != null)
        {
            // Вычисляем направление от смещенного центра кольца к игроку
            Vector3 toPlayer = playerTransform.position - ringCenter;
            float sqrMagnitude = toPlayer.x * toPlayer.x + toPlayer.z * toPlayer.z; // Квадрат расстояния (быстрее чем magnitude)
            if (sqrMagnitude > 0.0001f) // 0.01^2
            {
                // Используем более быстрый способ нормализации для 2D вектора
                float magnitude = Mathf.Sqrt(sqrMagnitude);
                directionToPlayer.x = toPlayer.x / magnitude;
                directionToPlayer.z = toPlayer.z / magnitude;
                directionFound = true;
            }
        }
        
        // Если игрок не найден или слишком близко, используем направление к камере
        if (!directionFound && mainCameraTransform != null)
        {
            // Вычисляем направление от смещенного центра кольца к камере
            Vector3 toCamera = mainCameraTransform.position - ringCenter;
            float sqrMagnitude = toCamera.x * toCamera.x + toCamera.z * toCamera.z;
            if (sqrMagnitude > 0.0001f)
            {
                float magnitude = Mathf.Sqrt(sqrMagnitude);
                directionToPlayer.x = toCamera.x / magnitude;
                directionToPlayer.z = toCamera.z / magnitude;
                directionFound = true;
            }
        }
        
        // Если все еще нет направления, используем направление вперед объекта
        if (!directionFound)
        {
            Vector3 forward = transform.forward;
            float sqrMagnitude = forward.x * forward.x + forward.z * forward.z;
            if (sqrMagnitude > 0.0001f)
            {
                float magnitude = Mathf.Sqrt(sqrMagnitude);
                directionToPlayer.x = forward.x / magnitude;
                directionToPlayer.z = forward.z / magnitude;
            }
            else
            {
                directionToPlayer = Vector3.forward; // Fallback
            }
        }
        
        // Вычисляем целевой угол на кольце
        targetRingAngle = Mathf.Atan2(directionToPlayer.x, directionToPlayer.z) * Mathf.Rad2Deg;
        
        // Плавно поворачиваем к целевому углу
        if (ringRotationSpeed > 0f)
        {
            float angleDifference = Mathf.DeltaAngle(currentRingAngle, targetRingAngle);
            currentRingAngle += angleDifference * ringRotationSpeed * Time.deltaTime;
        }
        else
        {
            currentRingAngle = targetRingAngle;
        }
        
        // Вычисляем позицию на кольце относительно смещенного центра
        // Кольцо расположено на высоте ringHeight относительно смещенного центра
        float angleRad = currentRingAngle * Mathf.Deg2Rad;
        float sinAngle = Mathf.Sin(angleRad);
        float cosAngle = Mathf.Cos(angleRad);
        Vector3 ringPosition = ringCenter + new Vector3(
            sinAngle * ringRadius,
            ringHeight,
            cosAngle * ringRadius
        );
        
        return ringPosition;
    }
    
    /// <summary>
    /// Обновляет визуальное состояние UI
    /// </summary>
    private void UpdateUI()
    {
        // Оптимизация: проверяем активность UI перед обновлением
        if (currentUIInstance == null || !currentUIInstance.activeSelf || progressRingImage == null) return;
        
        // Оптимизация: обновляем позицию UI не каждый кадр, а с заданной частотой
        frameCount++;
        bool shouldUpdatePosition = (frameCount % uiUpdateFrequency == 0);
        
        if (shouldUpdatePosition)
        {
            // Обновляем позицию UI (на случай если объект движется)
            Vector3 uiPosition = CalculateUIPositionOnRing();
            
            // Обновляем позицию только если она изменилась (оптимизация)
            if (Vector3.SqrMagnitude(uiPosition - lastUIPosition) > 0.0001f)
            {
                currentUIInstance.transform.position = uiPosition;
                lastUIPosition = uiPosition;
                
                // Если используется кольцо, обновляем поворот по Y в зависимости от позиции на кольце
                if (useRingRotation)
                {
                    // UI должен смотреть на игрока/камеру, чтобы E была видна
                    Vector3 directionToTarget = Vector3.zero;
                    if (playerTransform != null)
                    {
                        directionToTarget = playerTransform.position - uiPosition;
                    }
                    else if (mainCameraTransform != null)
                    {
                        directionToTarget = mainCameraTransform.position - uiPosition;
                    }
                    directionToTarget.y = 0f;
                    if (directionToTarget.sqrMagnitude > 0.0001f)
                    {
                        directionToTarget.Normalize();
                        float yRotation = Mathf.Atan2(directionToTarget.x, directionToTarget.z) * Mathf.Rad2Deg;
                        currentUIInstance.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
                    }
                }
            }
        }
        
        // Оптимизация: обновляем цвет только при изменении состояния
        bool shouldShowProgress = isHoldingKey && isPlayerInRange;
        bool shouldHideProgress = !isHoldingKey && currentHoldTime == 0f;
        
        // Обновляем видимость только при изменении состояния удержания клавиши
        if (wasHoldingKeyLastFrame != isHoldingKey)
        {
            wasHoldingKeyLastFrame = isHoldingKey;
            
            if (shouldShowProgress)
            {
                // Делаем кольцо видимым (проверяем текущий цвет перед обновлением)
                Color currentColor = progressRingImage.color;
                if (currentColor.a < 0.99f)
                {
                    currentColor.a = 1f;
                    progressRingImage.color = currentColor;
                    cachedProgressColor = currentColor;
                }
            }
            else if (shouldHideProgress)
            {
                // Скрываем кольцо, если не удерживаем клавишу (проверяем текущий цвет перед обновлением)
                Color currentColor = progressRingImage.color;
                if (currentColor.a > 0.01f)
                {
                    currentColor.a = 0f;
                    progressRingImage.color = currentColor;
                    cachedProgressColor = currentColor;
                }
            }
        }
    }
    
    /// <summary>
    /// Обрабатывает ввод от игрока
    /// </summary>
    protected virtual void HandleInput()
    {
        // Если игрок не в радиусе, но клавиша все еще зажата, сбрасываем состояние
        if (!isPlayerInRange)
        {
            if (isHoldingKey)
            {
                ResetProgress();
            }
            return;
        }
        
        if (playerTransform == null) return;
        
        // Проверяем, удерживается ли клавиша взаимодействия
        bool keyPressed = false;
        
#if ENABLE_INPUT_SYSTEM
        // Новый Input System
        if (Keyboard.current != null)
        {
            // Преобразуем KeyCode в Key для нового Input System
            Key key = GetKeyFromKeyCode(interactionKey);
            if (key != Key.None)
            {
                keyPressed = Keyboard.current[key].isPressed;
            }
        }
#else
        // Старый Input System
        keyPressed = Input.GetKey(interactionKey);
#endif
        
        // Обрабатываем удержание клавиши
        if (keyPressed)
        {
            // Если взаимодействие уже завершено, не обрабатываем ввод дальше
            // Ждем, пока игрок отпустит клавишу
            if (interactionCompleted)
            {
                // Оставляем прогресс на 100% и не обрабатываем дальше
                if (progressRingImage != null)
                {
                    progressRingImage.fillAmount = 1f;
                    Color progressColor = progressRingImage.color;
                    progressColor.a = 1f;
                    progressRingImage.color = progressColor;
                }
                return;
            }
            
            if (!isHoldingKey)
            {
                // Начали удерживать клавишу
                isHoldingKey = true;
            }
            
            // Увеличиваем время удержания
            currentHoldTime += Time.deltaTime;
            
            // Ограничиваем время удержания
            currentHoldTime = Mathf.Clamp(currentHoldTime, 0f, interactionTime);
            
            // Обновляем заполнение кольца прогресса (оптимизация: обновляем только при изменении)
            if (progressRingImage != null)
            {
                float fillAmount = currentHoldTime / interactionTime;
                // Обновляем только если значение изменилось значительно (оптимизация)
                if (Mathf.Abs(fillAmount - lastProgressFillAmount) > 0.01f)
                {
                    progressRingImage.fillAmount = fillAmount;
                    lastProgressFillAmount = fillAmount;
                }
            }
            
            // Проверяем, завершено ли взаимодействие
            if (currentHoldTime >= interactionTime && !interactionCompleted)
            {
                CompleteInteraction();
            }
        }
        else
        {
            // Клавиша не удерживается
            // ВАЖНО: НЕ сбрасываем isHoldingKey если взаимодействие происходит через мобильную кнопку
            // Проверяем флаг isMobileInteraction - если взаимодействие мобильное,
            // ResetProgress будет вызван из StopMobileInteraction() при отпускании кнопки
            if (isHoldingKey && !isMobileInteraction)
            {
                // Только что отпустили клавишу на PC - сбрасываем прогресс
                ResetProgress();
            }
            
            // Если взаимодействие было завершено и клавиша отпущена, сбрасываем флаг завершения
            // Это позволяет начать новое взаимодействие при следующем удержании
            if (interactionCompleted && !hideUIAfterInteraction)
            {
                interactionCompleted = false;
                if (progressRingImage != null)
                {
                    progressRingImage.fillAmount = 0f;
                    Color progressColor = progressRingImage.color;
                    progressColor.a = 0f;
                    progressRingImage.color = progressColor;
                }
            }
        }
    }
    
#if ENABLE_INPUT_SYSTEM
    /// <summary>
    /// Преобразует KeyCode в Key для нового Input System
    /// </summary>
    private Key GetKeyFromKeyCode(KeyCode keyCode)
    {
        // Базовое преобразование наиболее используемых клавиш
        switch (keyCode)
        {
            case KeyCode.E: return Key.E;
            case KeyCode.F: return Key.F;
            case KeyCode.Space: return Key.Space;
            case KeyCode.Return: return Key.Enter;
            case KeyCode.KeypadEnter: return Key.NumpadEnter;
            default:
                // Для остальных клавиш пытаемся преобразовать через строку
                string keyName = keyCode.ToString();
                if (System.Enum.TryParse<Key>(keyName, out Key result))
                {
                    return result;
                }
                return Key.None;
        }
    }
#endif
    
    /// <summary>
    /// Сбрасывает прогресс взаимодействия
    /// </summary>
    private void ResetProgress()
    {
        isHoldingKey = false;
        isMobileInteraction = false; // Сбрасываем флаг мобильного взаимодействия
        currentHoldTime = 0f;
        wasHoldingKeyLastFrame = false;
        lastProgressFillAmount = -1f;
        
        if (progressRingImage != null)
        {
            progressRingImage.fillAmount = 0f;
            Color progressColor = progressRingImage.color;
            progressColor.a = 0f;
            progressRingImage.color = progressColor;
        }
    }
    
    /// <summary>
    /// Завершает взаимодействие и вызывает событие
    /// </summary>
    protected virtual void CompleteInteraction()
    {
        interactionCompleted = true;
        
        // Вызываем виртуальный метод для переопределения в наследниках
        OnInteractionComplete();
        
        // Вызываем событие
        onInteractionComplete.Invoke();
        
        // НЕ сбрасываем прогресс сразу - оставляем его на 100%
        // Прогресс будет сброшен только когда игрок отпустит клавишу
        if (progressRingImage != null)
        {
            // Устанавливаем прогресс на 100%
            progressRingImage.fillAmount = 1f;
            Color progressColor = progressRingImage.color;
            progressColor.a = 1f;
            progressRingImage.color = progressColor;
        }
        
        // Скрываем UI сразу после завершения взаимодействия
        if (currentUIInstance != null)
        {
            if (debugMode)
            {
                Debug.Log($"[InteractableObject] {gameObject.name}: Взаимодействие завершено, скрываю UI");
            }
            DestroyUI();
        }
        
        // НЕ сбрасываем isHoldingKey и currentHoldTime здесь
        // Они будут сброшены, когда игрок отпустит клавишу
    }
    
    /// <summary>
    /// Виртуальный метод для переопределения в наследниках.
    /// Вызывается при завершении взаимодействия перед вызовом события.
    /// </summary>
    protected virtual void OnInteractionComplete()
    {
        // Переопределяется в наследниках для кастомной логики
    }
    
    /// <summary>
    /// Публичный метод для сброса состояния взаимодействия (полезно для повторного использования)
    /// </summary>
    public void ResetInteraction()
    {
        interactionCompleted = false;
        ResetProgress();
        hideUITimer = 0f;
    }
    
    /// <summary>
    /// Устанавливает новую клавишу взаимодействия
    /// </summary>
    public void SetInteractionKey(KeyCode newKey)
    {
        interactionKey = newKey;
    }
    
    /// <summary>
    /// Устанавливает новое время взаимодействия
    /// </summary>
    public void SetInteractionTime(float newTime)
    {
        interactionTime = Mathf.Max(0.1f, newTime);
    }
    
    /// <summary>
    /// Устанавливает новый радиус взаимодействия
    /// </summary>
    public void SetInteractionRange(float newRange)
    {
        interactionRange = Mathf.Max(0.1f, newRange);
        interactionRangeSqr = interactionRange * interactionRange; // Обновляем квадрат радиуса
    }
    
    /// <summary>
    /// Получает время взаимодействия (публичный геттер)
    /// </summary>
    public float GetInteractionTime()
    {
        return interactionTime;
    }
    
    /// <summary>
    /// Получает радиус взаимодействия (публичный геттер)
    /// </summary>
    public float GetInteractionRange()
    {
        return interactionRange;
    }
    
    /// <summary>
    /// Получает позицию взаимодействия (публичный метод)
    /// </summary>
    public Vector3 GetInteractionPositionPublic()
    {
        return GetInteractionPosition();
    }
    
    /// <summary>
    /// Нужно ли показывать UI взаимодействия (например, только для ближайшей Cell).
    /// Переопределите в наследниках, чтобы ограничить показ UI.
    /// </summary>
    protected virtual bool ShouldShowInteractionUI()
    {
        return true;
    }
    
    /// <summary>
    /// Проверяет, может ли игрок взаимодействовать с объектом (публичный метод)
    /// </summary>
    public bool CanInteract()
    {
        return isPlayerInRange && !interactionCompleted;
    }
    
    /// <summary>
    /// Начинает взаимодействие с мобильной кнопки
    /// </summary>
    public void StartMobileInteraction()
    {
        // ВАЖНО: Для переносимых объектов (BrainrotObject) не проверяем isPlayerInRange,
        // так как объект всегда с игроком и взаимодействие должно начинаться
        // Проверяем только interactionCompleted
        if (interactionCompleted)
        {
            if (debugMode)
            {
                Debug.LogWarning($"[InteractableObject] {gameObject.name}: StartMobileInteraction: interactionCompleted=true, сбрасываем");
            }
            // Сбрасываем interactionCompleted для повторного использования
            interactionCompleted = false;
        }
        
        // Устанавливаем флаг мобильного взаимодействия
        isMobileInteraction = true;
        
        if (!isHoldingKey)
        {
            isHoldingKey = true;
            currentHoldTime = 0f;
            if (debugMode)
            {
                Debug.Log($"[InteractableObject] {gameObject.name}: StartMobileInteraction: isHoldingKey установлен в true, currentHoldTime сброшен");
            }
        }
        else if (debugMode)
        {
            Debug.Log($"[InteractableObject] {gameObject.name}: StartMobileInteraction: isHoldingKey уже true, currentHoldTime={currentHoldTime:F3}");
        }
    }
    
    /// <summary>
    /// Обновляет прогресс взаимодействия с мобильной кнопки
    /// </summary>
    public void UpdateMobileInteraction(float deltaTime)
    {
        // ВАЖНО: Для переносимых объектов (BrainrotObject) не проверяем isPlayerInRange,
        // так как объект всегда с игроком и взаимодействие должно продолжаться
        // Проверяем только isHoldingKey и interactionCompleted
        // НЕ проверяем interactionCompleted здесь, так как оно может быть установлено в true
        // во время CompleteInteraction(), но нам нужно позволить завершить обновление
        if (!isHoldingKey)
        {
            if (debugMode && Time.frameCount % 30 == 0)
            {
                Debug.Log($"[InteractableObject] {gameObject.name}: UpdateMobileInteraction: isHoldingKey=false, выход");
            }
            return;
        }
        
        // Если взаимодействие уже завершено, не обновляем прогресс
        // но проверяем это после проверки isHoldingKey
        if (interactionCompleted)
        {
            if (debugMode && Time.frameCount % 30 == 0)
            {
                Debug.Log($"[InteractableObject] {gameObject.name}: UpdateMobileInteraction: interactionCompleted=true, выход");
            }
            return;
        }
        
        // Увеличиваем время удержания
        currentHoldTime += deltaTime;
        
        // Проверяем, завершено ли взаимодействие ДО ограничения
        // Это гарантирует, что currentHoldTime точно достигнет interactionTime
        if (currentHoldTime >= interactionTime && !interactionCompleted)
        {
            // Принудительно устанавливаем currentHoldTime в interactionTime для точности
            currentHoldTime = interactionTime;
            
            // Устанавливаем fillAmount в 1.0 для визуального завершения
            if (progressRingImage != null)
            {
                progressRingImage.fillAmount = 1.0f;
                lastProgressFillAmount = 1.0f;
            }
            
            CompleteInteraction();
        }
        else
        {
            // Ограничиваем время удержания только если взаимодействие еще не завершено
            currentHoldTime = Mathf.Clamp(currentHoldTime, 0f, interactionTime);
            
            // Обновляем заполнение кольца прогресса
            if (progressRingImage != null)
            {
                float fillAmount = currentHoldTime / interactionTime;
                if (Mathf.Abs(fillAmount - lastProgressFillAmount) > 0.01f)
                {
                    progressRingImage.fillAmount = fillAmount;
                    lastProgressFillAmount = fillAmount;
                }
            }
        }
    }
    
    /// <summary>
    /// Останавливает взаимодействие с мобильной кнопки
    /// </summary>
    public void StopMobileInteraction()
    {
        // Сбрасываем флаг мобильного взаимодействия
        isMobileInteraction = false;
        
        if (isHoldingKey)
        {
            ResetProgress();
        }
    }
    
    /// <summary>
    /// Получает текущее время удержания (публичный геттер для мобильной кнопки)
    /// </summary>
    public float GetCurrentHoldTime()
    {
        return currentHoldTime;
    }
    
    /// <summary>
    /// Проверяет, является ли устройство мобильным/планшетным
    /// </summary>
    private bool IsMobileDevice()
    {
#if EnvirData_yg
        // Используем YG2 envirdata для определения устройства
        bool isMobile = YG2.envir.isMobile || YG2.envir.isTablet;
        
#if UNITY_EDITOR
        // В редакторе также проверяем симулятор
        if (!isMobile)
        {
            if (YG2.envir.device == YG2.Device.Mobile || YG2.envir.device == YG2.Device.Tablet)
            {
                isMobile = true;
            }
        }
#endif
        return isMobile;
#else
        // Если модуль EnvirData не подключен, используем стандартную проверку
        return Application.isMobilePlatform || Input.touchSupported;
#endif
    }
    
    // Визуализация радиуса взаимодействия в редакторе
    private void OnDrawGizmosSelected()
    {
        Vector3 interactionPos = GetInteractionPosition();
        
        // Рисуем радиус взаимодействия
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(interactionPos, interactionRange);
        
        // Если используется кольцо вращения, рисуем его
        if (useRingRotation)
        {
            // ВАЖНО: Центр кольца учитывает uiOffset
            Vector3 ringCenter = interactionPos + uiOffset;
            
            // Рисуем кольцо (окружность на высоте ringHeight относительно смещенного центра)
            Gizmos.color = Color.cyan;
            int segments = 32;
            float angleStep = 360f / segments;
            Vector3 prevPoint = ringCenter + new Vector3(0, ringHeight, ringRadius);
            
            for (int i = 1; i <= segments; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector3 currentPoint = ringCenter + new Vector3(
                    Mathf.Sin(angle) * ringRadius,
                    ringHeight,
                    Mathf.Cos(angle) * ringRadius
                );
                Gizmos.DrawLine(prevPoint, currentPoint);
                prevPoint = currentPoint;
            }
            
            // Рисуем линию от центра кольца к текущей позиции UI на кольце
            if (Application.isPlaying && currentUIInstance != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(ringCenter, currentUIInstance.transform.position);
            }
            else
            {
                // В редакторе показываем примерную позицию
                Vector3 previewPos = ringCenter + new Vector3(0, ringHeight, ringRadius);
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(previewPos, 0.2f);
                Gizmos.DrawLine(ringCenter, previewPos);
            }
            
            // Рисуем центр кольца (с учетом uiOffset)
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(ringCenter, 0.1f);
        }
        else
        {
            // Стандартное позиционирование
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(interactionPos + uiOffset, 0.2f);
            
            // Рисуем линию от объекта к UI
            Gizmos.color = Color.green;
            Gizmos.DrawLine(interactionPos, interactionPos + uiOffset);
        }
        
        // Если используется центр коллайдера, показываем его
        if (useColliderCenter)
        {
            Collider col = objectCollider;
            if (col == null)
            {
                col = GetComponent<Collider>();
                if (col == null)
                {
                    col = GetComponentInChildren<Collider>();
                }
            }
            
            if (col != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
            }
        }
        
        // Если используется interactionPoint, показываем его
        if (interactionPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(interactionPoint.position, 0.15f);
            Gizmos.DrawLine(transform.position, interactionPoint.position);
        }
    }
}
