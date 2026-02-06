using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
#if EnvirData_yg || Localization_yg
using YG;
#endif

/// <summary>
/// Кнопка взаимодействия для мобильных устройств
/// Отображается только на mobile/tablet устройствах
/// При зажатии заполняет радиальный прогресс и выполняет взаимодействие с ближайшим InteractableObject
/// </summary>
public class InteractButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("References")]
    [SerializeField] private Image radialImage;
    [SerializeField] private TextMeshProUGUI textTMP;
    private CanvasGroup canvasGroup;
    
    [Header("Settings")]
    [Tooltip("Частота поиска ближайшего объекта (в кадрах). Больше значение = меньше поисков")]
    [SerializeField] private int searchFrequency = 10;
    
    
    // Компоненты
    private Transform playerTransform;
    private PlayerCarryController playerCarryController;
    private InteractableObject currentInteractableObject;
    
    // Состояние
    private bool isMobileDevice = false;
    private bool isPressed = false;
    private float interactionTime = 2f;
    
    // Кэш для оптимизации
    private int searchFrameCount = 0;
    private string defaultText = "ВЗЯТЬ"; // Будет обновлено через локализацию
    private string putText = "ПОСТАВИТЬ"; // Будет обновлено через локализацию
    private string currentLanguage = "ru"; // Текущий язык для отслеживания изменений
    
    private void Awake()
    {
        // Находим компоненты в иерархии
        FindComponents();
        
        // Получаем или создаем CanvasGroup для управления видимостью
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
        
        // Убеждаемся, что кнопка активна для вызова Update()
        // Видимость будет управляться через CanvasGroup.alpha
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }
    }
    
    private void Start()
    {
        // Найти игрока
        FindPlayer();
        
        // Найти PlayerCarryController
        FindPlayerCarryController();
        
        // Определить, является ли устройство мобильным
        UpdateMobileDeviceStatus();
        
        // Ищем ближайший объект сразу (без задержки)
        FindNearestInteractableObject();
        
        // Показать/скрыть кнопку в зависимости от устройства и наличия объекта
        UpdateButtonVisibility();
        
        // Инициализируем радиальный прогресс
        if (radialImage != null)
        {
            radialImage.fillAmount = 0f;
        }
        
        // Инициализируем локализацию
        InitializeLocalization();
    }
    
    private void OnEnable()
    {
#if Localization_yg
        // Подписываемся на изменение языка
        YG2.onSwitchLang += OnLanguageChanged;
        // Применяем текущий язык
        UpdateLocalizedTexts();
#endif
    }
    
    private void OnDisable()
    {
#if Localization_yg
        // Отписываемся от события
        YG2.onSwitchLang -= OnLanguageChanged;
#endif
    }
    
#if Localization_yg
    /// <summary>
    /// Обработчик изменения языка
    /// </summary>
    private void OnLanguageChanged(string lang)
    {
        currentLanguage = lang;
        UpdateLocalizedTexts();
        // Обновляем текст кнопки сразу после изменения языка
        UpdateButtonText();
    }
#endif
    
    private void Update()
    {
        // Обновляем статус мобильного устройства (на случай если данные YG2 пришли позже)
#if EnvirData_yg
        bool wasMobile = isMobileDevice;
        UpdateMobileDeviceStatus();
        if (wasMobile != isMobileDevice)
        {
            UpdateButtonVisibility();
        }
#endif
        
        // Если не мобильное устройство, скрываем кнопку и выходим
        if (!isMobileDevice)
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
            return;
        }
        
        // ВАЖНО: Обновляем видимость ДО проверки activeInHierarchy, чтобы кнопка могла появиться
        // Но сначала ищем объект, чтобы знать, нужно ли показывать кнопку
        
        // Поиск ближайшего объекта (с оптимизацией частоты)
        // ВАЖНО: НЕ обновляем currentInteractableObject во время активного взаимодействия (isPressed),
        // чтобы не прервать процесс взаимодействия
        if (!isPressed)
        {
            // Обновляем объект только если кнопка не зажата
            searchFrameCount++;
            if (searchFrameCount >= searchFrequency)
            {
                searchFrameCount = 0;
                FindNearestInteractableObject();
            }
        }
        
        // Обновление видимости кнопки (вызываем при изменении объекта или каждый кадр)
        UpdateButtonVisibility();
        
        // Если кнопка активна, обновляем остальное
        if (gameObject.activeInHierarchy)
        {
            // Обновление текста кнопки
            UpdateButtonText();
            
            // Обработка прогресса при зажатии
            // ВАЖНО: Проверяем isPressed ПЕРЕД проверкой currentInteractableObject,
            // чтобы не пропустить обновление, если объект временно стал null
            if (isPressed)
            {
                if (currentInteractableObject != null)
                {
                    UpdateProgress();
                }
            }
        }
    }
    
    /// <summary>
    /// Находит компоненты в иерархии
    /// </summary>
    private void FindComponents()
    {
        // Ищем Radial (Image с Type=Filled, FillMethod=Radial360)
        if (radialImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(false);
            foreach (Image img in images)
            {
                if (img != null && img.type == Image.Type.Filled && img.fillMethod == Image.FillMethod.Radial360)
                {
                    radialImage = img;
                    break;
                }
            }
        }
        
        // Ищем Text_tmp (TextMeshProUGUI)
        if (textTMP == null)
        {
            textTMP = GetComponentInChildren<TextMeshProUGUI>(false);
        }
        
        // ВАЖНО: Radial (Image) НЕ должен иметь raycastTarget = true,
        // так как он должен быть фоновым элементом. События должны обрабатываться родительским Container.
        // Убеждаемся, что Radial не перехватывает события
        if (radialImage != null)
        {
            radialImage.raycastTarget = false; // Radial не должен перехватывать события
        }
        
        // ВАЖНО: Text_tmp (TextMeshProUGUI) также НЕ должен перехватывать события
        // чтобы события обрабатывались родительским Container
        if (textTMP != null)
        {
            textTMP.raycastTarget = false; // Text_tmp не должен перехватывать события
        }
        
        // Убеждаемся, что сам GameObject (Container) имеет Image компонент с raycastTarget для получения событий IPointerDownHandler
        Image buttonImage = GetComponent<Image>();
        if (buttonImage == null)
        {
            // Если Image нет, добавляем невидимый Image для получения событий
            buttonImage = gameObject.AddComponent<Image>();
            Color transparent = Color.white;
            transparent.a = 0f; // Прозрачный
            buttonImage.color = transparent;
        }
        buttonImage.raycastTarget = true; // Container должен получать события
    }
    
    /// <summary>
    /// Находит игрока в сцене
    /// </summary>
    private void FindPlayer()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
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
    /// Обновляет статус мобильного устройства
    /// </summary>
    private void UpdateMobileDeviceStatus()
    {
#if EnvirData_yg
        isMobileDevice = YG2.envir.isMobile || YG2.envir.isTablet;
        
#if UNITY_EDITOR
        // В редакторе проверяем симулятор
        if (!isMobileDevice)
        {
            if (YG2.envir.device == YG2.Device.Mobile || YG2.envir.device == YG2.Device.Tablet)
            {
                isMobileDevice = true;
            }
        }
#endif
#else
        isMobileDevice = Application.isMobilePlatform || Input.touchSupported;
#endif
    }
    
    /// <summary>
    /// Находит ближайший InteractableObject в радиусе взаимодействия
    /// </summary>
    private void FindNearestInteractableObject()
    {
        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null)
            {
                currentInteractableObject = null;
                return;
            }
        }
        
        InteractableObject closestObject = null;
        float closestDistance = float.MaxValue;
        
        // ПРИОРИТЕТ 1: Проверяем активный PlacementPanel с UI
        // PlacementPanel имеет высший приоритет, когда рядом с игроком и имеет активный UI
        PlacementPanel activePanel = PlacementPanel.GetActivePanel();
        if (activePanel != null && activePanel.HasUI())
        {
            closestObject = activePanel;
            closestDistance = 0f; // Минимальное расстояние для PlacementPanel
        }
        else
        {
            // ПРИОРИТЕТ 2: Ищем переносимый BrainrotObject (для "Поставить")
            // Находим все InteractableObject в сцене
            InteractableObject[] allObjects = FindObjectsByType<InteractableObject>(FindObjectsSortMode.None);
            
            foreach (InteractableObject obj in allObjects)
            {
                if (obj == null || !obj.gameObject.activeInHierarchy)
                {
                    continue;
                }
                
                // Для BrainrotObject проверяем специальные состояния
                BrainrotObject brainrotObj = obj as BrainrotObject;
                bool isCarried = brainrotObj != null && brainrotObj.IsCarried();
                
                // Для переносимого объекта всегда разрешаем взаимодействие (для "Поставить")
                // Расстояние не проверяем для переносимых объектов - они всегда с игроком
                if (isCarried)
                {
                    // Для переносимого объекта всегда показываем кнопку
                    closestObject = obj;
                    closestDistance = 0f; // Минимальное расстояние для переносимого объекта
                    break; // Переносимый объект имеет приоритет - сразу выбираем его
                }
                
                // ПРИОРИТЕТ 3: Ищем другие объекты по расстоянию и наличию UI
                // Получаем радиус взаимодействия через публичный метод
                float interactionRange = obj.GetInteractionRange();
                
                // Получаем позицию взаимодействия через публичный метод
                Vector3 interactionPos = obj.GetInteractionPositionPublic();
                
                // Вычисляем расстояние
                float distance = Vector3.Distance(playerTransform.position, interactionPos);
                
                // Проверяем, находится ли объект в радиусе взаимодействия
                bool inRange = distance <= interactionRange;
                
                // Для непереносимого объекта проверяем, есть ли активный UI
                bool hasUI = obj.HasUI();
                bool canInteract = obj.CanInteract();
                
                bool isCloser = distance < closestDistance;
                
                // ВАЖНО: Если объект имеет активный UI, это означает, что он уже прошел все проверки в InteractableObject
                // и игрок может с ним взаимодействовать. Поэтому выбираем объект по hasUI, а не по canInteract.
                // canInteract может быть false в некоторых случаях, но если UI активен, взаимодействие возможно.
                
                if (inRange && hasUI && isCloser)
                {
                    closestObject = obj;
                    closestDistance = distance;
                }
            }
        }
        
        // Обновляем текущий объект
        if (currentInteractableObject != closestObject)
        {
            currentInteractableObject = closestObject;
            
            // Обновляем interactionTime через публичный метод
            if (currentInteractableObject != null)
            {
                interactionTime = currentInteractableObject.GetInteractionTime();
            }
        }
    }
    
    /// <summary>
    /// Обновляет видимость кнопки
    /// </summary>
    private void UpdateButtonVisibility()
    {
        // ВАЖНО: Во время активного взаимодействия (isPressed) НЕ меняем видимость кнопки,
        // чтобы не прервать процесс взаимодействия
        if (isPressed)
        {
            // Убеждаемся, что кнопка остается видимой и интерактивной во время взаимодействия
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            return;
        }
        
        // Кнопка должна быть видна только если:
        // 1. Это мобильное устройство
        // 2. Есть ближайший объект
        // 3. Игрок может взаимодействовать с объектом ИЛИ объект переносится (для "Поставить")
        // ВАЖНО: Когда текст "Take" (нет брейнрота в руках), НЕ показываем кнопку,
        // так как взаимодействие теперь происходит через префаб Interaction
        // Когда текст "Put" (есть брейнрот в руках), показываем кнопку как обычно
        bool shouldShow = false;
        
        if (isMobileDevice && currentInteractableObject != null)
        {
            // Проверяем, есть ли брейнрот в руках
            bool hasBrainrotInHands = playerCarryController != null && playerCarryController.GetCurrentCarriedObject() != null;
            
            // Для BrainrotObject проверяем, переносится ли он
            BrainrotObject brainrotObj = currentInteractableObject as BrainrotObject;
            bool isCarried = brainrotObj != null && brainrotObj.IsCarried();
            
            if (isCarried)
            {
                // Если объект переносится, всегда показываем кнопку (для "Поставить")
                shouldShow = true;
            }
            else
            {
                // Если объект не переносится и нет брейнрота в руках (текст "Take"),
                // НЕ показываем кнопку, так как взаимодействие через префаб Interaction
                if (!hasBrainrotInHands)
                {
                    shouldShow = false;
                }
                else
                {
                    // Если есть брейнрот в руках (текст "Put"), проверяем CanInteract()
                    shouldShow = currentInteractableObject.CanInteract();
                }
            }
        }
        
        // Управляем видимостью через CanvasGroup (alpha и interactable)
        // Это позволяет Update() вызываться даже когда кнопка невидима
        if (canvasGroup != null)
        {
            canvasGroup.alpha = shouldShow ? 1f : 0f;
            canvasGroup.interactable = shouldShow;
            canvasGroup.blocksRaycasts = shouldShow;
        }
        else
        {
            // Fallback на SetActive, если CanvasGroup не найден
            if (gameObject.activeSelf != shouldShow)
            {
                gameObject.SetActive(shouldShow);
            }
        }
    }
    
    /// <summary>
    /// Инициализирует локализованные тексты
    /// </summary>
    private void InitializeLocalization()
    {
#if Localization_yg
        if (YG2.lang != null)
        {
            currentLanguage = YG2.lang;
        }
#endif
        UpdateLocalizedTexts();
    }
    
    /// <summary>
    /// Обновляет локализованные тексты в зависимости от текущего языка
    /// </summary>
    private void UpdateLocalizedTexts()
    {
        string lang = GetCurrentLanguage();
        
        if (lang == "ru")
        {
            defaultText = "ВЗЯТЬ";
            putText = "ПОСТАВИТЬ";
        }
        else
        {
            defaultText = "TAKE";
            putText = "PUT";
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
    /// Обновляет текст кнопки
    /// </summary>
    private void UpdateButtonText()
    {
        if (textTMP == null)
        {
            textTMP = GetComponentInChildren<TextMeshProUGUI>(true);
            if (textTMP == null) return;
        }
        
        // Проверяем, изменился ли язык (для обновления текстов)
        string currentLang = GetCurrentLanguage();
        if (currentLang != currentLanguage)
        {
            currentLanguage = currentLang;
            UpdateLocalizedTexts();
        }
        
        // Текст: в руках брейнрот — "Поставить"; иначе — "Взять"
        bool hasBrainrotInHands = playerCarryController != null && playerCarryController.GetCurrentCarriedObject() != null;
        string newText = hasBrainrotInHands ? putText : defaultText;
        
        if (textTMP.text != newText)
            textTMP.text = newText;
        
        if (!textTMP.gameObject.activeSelf)
            textTMP.gameObject.SetActive(true);
        if (!textTMP.enabled)
            textTMP.enabled = true;
    }
    
    /// <summary>
    /// Обновляет прогресс заполнения при зажатии кнопки
    /// </summary>
    private void UpdateProgress()
    {
        if (radialImage == null)
        {
            return;
        }
        
        if (currentInteractableObject == null)
        {
            isPressed = false;
            ResetProgress();
            return;
        }
        
        // Обновляем прогресс каждый кадр для плавности
        // Используем Time.deltaTime напрямую
        float deltaTime = Time.deltaTime;
        
        // Обновляем прогресс через публичный метод InteractableObject
        // Это автоматически обновит внутренний прогресс и вызовет CompleteInteraction при необходимости
        currentInteractableObject.UpdateMobileInteraction(deltaTime);
        
        // Получаем текущее время удержания из InteractableObject
        float currentHoldTimeFromObject = currentInteractableObject.GetCurrentHoldTime();
        
        // Обновляем визуальный прогресс на кнопке (от 0 до 1)
        float fillAmount = interactionTime > 0f ? currentHoldTimeFromObject / interactionTime : 0f;
        
        // Ограничиваем fillAmount до 1.0 максимум
        fillAmount = Mathf.Clamp01(fillAmount);
        
        radialImage.fillAmount = fillAmount;
        
        // Проверяем, завершилось ли взаимодействие (если currentHoldTime достиг или превысил interactionTime)
        // Используем более точную проверку для надежности
        if (currentHoldTimeFromObject >= interactionTime)
        {
            // Принудительно устанавливаем fillAmount в 1.0 для визуального завершения
            radialImage.fillAmount = 1.0f;
            
            // Взаимодействие завершено, сбрасываем isPressed
            isPressed = false;
            // НЕ вызываем ResetProgress() здесь, так как CompleteInteraction уже вызван
            // и может понадобиться оставить прогресс на 100% на некоторое время
        }
    }
    
    /// <summary>
    /// Вызывается при нажатии на кнопку
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        // НЕ используем eventData.Use() - это может вызвать проблемы
        
        if (currentInteractableObject == null)
        {
            return;
        }
        
        // ВАЖНО: Не сбрасываем isPressed если уже нажато (защита от повторных вызовов)
        if (isPressed)
        {
            return;
        }
        
        isPressed = true;
        
        // Начинаем взаимодействие через публичный метод
        currentInteractableObject.StartMobileInteraction();
        
        // Обновляем interactionTime
        interactionTime = currentInteractableObject.GetInteractionTime();
        
        // Убеждаемся, что радиальное изображение видно
        if (radialImage != null)
        {
            Color color = radialImage.color;
            color.a = 1f;
            radialImage.color = color;
            radialImage.fillAmount = 0f; // Сбрасываем fillAmount при начале
        }
    }
    
    /// <summary>
    /// Вызывается при отпускании кнопки
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        // НЕ используем eventData.Use() - это может вызвать проблемы
        
        // Проверяем, что кнопка действительно была нажата
        if (!isPressed)
        {
            return;
        }
        
        isPressed = false;
        
        // Останавливаем взаимодействие через публичный метод
        if (currentInteractableObject != null)
        {
            // Проверяем, не завершилось ли взаимодействие (чтобы не сбрасывать завершенное)
            float currentHoldTime = currentInteractableObject.GetCurrentHoldTime();
            float interactionTimeCheck = currentInteractableObject.GetInteractionTime();
            
            // Останавливаем только если взаимодействие не завершено
            if (currentHoldTime < interactionTimeCheck)
            {
                currentInteractableObject.StopMobileInteraction();
            }
        }
        
        // Сбрасываем прогресс
        ResetProgress();
    }
    
    /// <summary>
    /// Сбрасывает прогресс взаимодействия
    /// </summary>
    private void ResetProgress()
    {
        if (radialImage != null)
        {
            radialImage.fillAmount = 0f;
        }
    }
    
}
