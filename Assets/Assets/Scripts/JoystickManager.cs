using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif
#if EnvirData_yg
using YG;
#endif

/// <summary>
/// Менеджер для создания и управления виртуальным джойстиком
/// Показывает джойстик только на mobile/tablet устройствах, определяемых через YG2 envirdata
/// </summary>
public class JoystickManager : MonoBehaviour
{
    private static JoystickManager _instance;
    
    [Header("Настройки джойстика")]
    [SerializeField] private Canvas joystickCanvas;
    [SerializeField] private Vector2 joystickPosition = new Vector2(150, 150); // Позиция джойстика на экране
    
    private VirtualJoystick virtualJoystick;
    private ThirdPersonController playerController;
    private bool isInitialized = false;
    
    /// <summary>
    /// Singleton экземпляр
    /// </summary>
    public static JoystickManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<JoystickManager>();
                if (_instance == null)
                {
                    GameObject managerObject = new GameObject("JoystickManager");
                    _instance = managerObject.AddComponent<JoystickManager>();
                }
            }
            return _instance;
        }
    }
    
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        DontDestroyOnLoad(transform.root.gameObject); // root — DontDestroyOnLoad работает только для корневых объектов
    }
    
    private void OnEnable()
    {
#if EnvirData_yg
        // Подписываемся на событие получения данных SDK
        YG2.onGetSDKData += InitializeJoystick;
#endif
    }
    
    private void OnDisable()
    {
#if EnvirData_yg
        // Отписываемся от события
        YG2.onGetSDKData -= InitializeJoystick;
#endif
    }
    
    private void Start()
    {
        // Попытка инициализации сразу (на случай если данные уже получены)
        InitializeJoystick();
    }
    
    /// <summary>
    /// Инициализация джойстика на основе данных YG2 envirdata
    /// </summary>
    private void InitializeJoystick()
    {
        if (isInitialized) return;
        
#if EnvirData_yg
        // Определяем тип устройства через YG2 envirdata
        bool isMobile = YG2.envir.isMobile;
        bool isTablet = YG2.envir.isTablet;
        bool needsJoystick = isMobile || isTablet;
        
        Debug.Log($"[JoystickManager] Проверка устройства: isMobile={isMobile}, isTablet={isTablet}, needsJoystick={needsJoystick}, deviceType={YG2.envir.deviceType}");
#else
        // Если модуль EnvirData не подключен, используем стандартную проверку
        bool needsJoystick = Application.isMobilePlatform || Input.touchSupported;
        Debug.Log($"[JoystickManager] EnvirData модуль не найден, используется стандартная проверка: needsJoystick={needsJoystick}");
#endif
        
        if (!needsJoystick)
        {
            // На desktop джойстик не нужен - скрыть Canvas
            if (joystickCanvas != null)
            {
                joystickCanvas.gameObject.SetActive(false);
            }
            Debug.Log("[JoystickManager] Desktop устройство - джойстик не создан");
            isInitialized = true;
            return;
        }
        
        Debug.Log("[JoystickManager] Мобильное устройство обнаружено - создаем джойстик");
        
        // Найти ThirdPersonController
        playerController = FindFirstObjectByType<ThirdPersonController>();
        if (playerController == null)
        {
            Debug.LogWarning("[JoystickManager] ThirdPersonController не найден! Джойстик будет создан, но ввод не будет работать.");
        }
        
        // Создать Canvas для джойстика, если не назначен
        if (joystickCanvas == null)
        {
            CreateJoystickCanvas();
        }
        
        // Убедиться, что Canvas активен
        if (joystickCanvas != null)
        {
            joystickCanvas.gameObject.SetActive(true);
            joystickCanvas.enabled = true;
            
            // Убедиться, что Canvas видим
            CanvasGroup canvasGroup = joystickCanvas.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = joystickCanvas.gameObject.AddComponent<CanvasGroup>();
            }
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            
            Debug.Log($"[JoystickManager] Canvas активирован: activeSelf={joystickCanvas.gameObject.activeSelf}, enabled={joystickCanvas.enabled}");
        }
        
        // Создать джойстик
        CreateJoystick();
        isInitialized = true;
        Debug.Log("[JoystickManager] Джойстик создан и инициализирован");
    }
    
    /// <summary>
    /// Создать Canvas для джойстика
    /// </summary>
    private void CreateJoystickCanvas()
    {
        GameObject canvasObj = new GameObject("JoystickCanvas");
        canvasObj.transform.SetParent(transform);
        
        joystickCanvas = canvasObj.AddComponent<Canvas>();
        joystickCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        joystickCanvas.sortingOrder = 1000; // Очень высокий порядок отрисовки
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        
        canvasObj.AddComponent<GraphicRaycaster>();
        
        canvasObj.SetActive(true);
        Debug.Log($"[JoystickManager] Canvas создан: {canvasObj.name}");
        
        // Добавить EventSystem, если его нет
        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<EventSystem>();
            
#if ENABLE_INPUT_SYSTEM
            // Используем InputSystemUIInputModule для нового Input System
            eventSystemObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            // Используем StandaloneInputModule для старого Input System
            eventSystemObj.AddComponent<StandaloneInputModule>();
#endif
            Debug.Log("[JoystickManager] EventSystem создан");
        }
    }
    
    /// <summary>
    /// Создать виртуальный джойстик
    /// </summary>
    private void CreateJoystick()
    {
        // Создать контейнер для джойстика
        GameObject joystickContainer = new GameObject("JoystickContainer");
        joystickContainer.transform.SetParent(joystickCanvas.transform, false);
        
        RectTransform containerRect = joystickContainer.AddComponent<RectTransform>();
        containerRect.anchorMin = new Vector2(0, 0);
        containerRect.anchorMax = new Vector2(0, 0);
        containerRect.anchoredPosition = joystickPosition;
        containerRect.sizeDelta = new Vector2(600, 600); // Увеличено на 50%: 400 * 1.5 = 600
        
        // Создать фон джойстика
        GameObject backgroundObj = new GameObject("JoystickBackground");
        backgroundObj.transform.SetParent(joystickContainer.transform, false);
        
        RectTransform backgroundRect = backgroundObj.AddComponent<RectTransform>();
        backgroundRect.anchorMin = new Vector2(0.5f, 0.5f);
        backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.anchoredPosition = Vector2.zero;
        backgroundRect.sizeDelta = new Vector2(450, 450); // Увеличено на 50%: 300 * 1.5 = 450
        
        Image backgroundImage = backgroundObj.AddComponent<Image>();
        backgroundImage.color = new Color(1f, 1f, 1f, 0.3f);
        backgroundImage.sprite = CreateCircleSprite(450, new Color(1f, 1f, 1f, 0.3f)); // Увеличено на 50%: 300 * 1.5 = 450
        backgroundImage.raycastTarget = true;
        
        // Создать ручку джойстика
        GameObject handleObj = new GameObject("JoystickHandle");
        handleObj.transform.SetParent(joystickContainer.transform, false);
        
        RectTransform handleRect = handleObj.AddComponent<RectTransform>();
        handleRect.anchorMin = new Vector2(0.5f, 0.5f);
        handleRect.anchorMax = new Vector2(0.5f, 0.5f);
        handleRect.anchoredPosition = Vector2.zero;
        handleRect.sizeDelta = new Vector2(180, 180); // Увеличено на 50%: 120 * 1.5 = 180
        
        Image handleImage = handleObj.AddComponent<Image>();
        handleImage.color = new Color(1f, 1f, 1f, 0.8f);
        handleImage.sprite = CreateCircleSprite(180, new Color(1f, 1f, 1f, 0.8f)); // Увеличено на 50%: 120 * 1.5 = 180
        handleImage.raycastTarget = false;
        
        // Добавить компонент VirtualJoystick
        virtualJoystick = joystickContainer.AddComponent<VirtualJoystick>();
        virtualJoystick.joystickBackground = backgroundRect;
        virtualJoystick.joystickHandle = handleRect;
        
        // Подписаться на события джойстика
        virtualJoystick.OnJoystickInput += OnJoystickInput;
        
        joystickContainer.SetActive(true);
        Debug.Log("[JoystickManager] Виртуальный джойстик создан");
    }
    
    /// <summary>
    /// Создать круглый спрайт
    /// </summary>
    private Sprite CreateCircleSprite(int size, Color color)
    {
        Texture2D texture = new Texture2D(size, size);
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 2f;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance <= radius)
                {
                    texture.SetPixel(x, y, color);
                }
                else
                {
                    texture.SetPixel(x, y, Color.clear);
                }
            }
        }
        
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
    
    /// <summary>
    /// Обработка ввода от джойстика
    /// </summary>
    private void OnJoystickInput(Vector2 input)
    {
        if (playerController != null)
        {
            playerController.SetJoystickInput(input);
        }
        else
        {
            // Попытаться найти ThirdPersonController снова
            playerController = FindFirstObjectByType<ThirdPersonController>();
            if (playerController != null)
            {
                playerController.SetJoystickInput(input);
            }
        }
    }
    
    /// <summary>
    /// Проверяет, находится ли точка экрана в области джойстика
    /// </summary>
    public bool IsPointOnJoystick(Vector2 screenPosition)
    {
        if (joystickCanvas == null || !joystickCanvas.gameObject.activeInHierarchy)
            return false;
        
        // Используем EventSystem для проверки UI элементов под точкой
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null)
        {
            PointerEventData pointerData = new PointerEventData(eventSystem);
            pointerData.position = screenPosition;
            
            var results = new System.Collections.Generic.List<RaycastResult>();
            eventSystem.RaycastAll(pointerData, results);
            
            // Проверяем, есть ли UI элементы джойстика под точкой
            foreach (var result in results)
            {
                if (result.gameObject.name.Contains("Joystick") || 
                    result.gameObject.name.Contains("JoystickContainer") ||
                    result.gameObject.name.Contains("JoystickBackground") ||
                    result.gameObject.name.Contains("JoystickHandle"))
                {
                    return true;
                }
            }
        }
        
        return false;
    }
    
    private void OnDestroy()
    {
        if (virtualJoystick != null && virtualJoystick.OnJoystickInput != null)
        {
            virtualJoystick.OnJoystickInput -= OnJoystickInput;
        }
    }
    
    /// <summary>
    /// Получить VirtualJoystick для доступа к вводу
    /// </summary>
    public VirtualJoystick GetVirtualJoystick()
    {
        return virtualJoystick;
    }
}
