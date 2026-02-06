using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if EnvirData_yg
using YG;
#endif

[RequireComponent(typeof(Camera))]
public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform target; // Объект Player
    [SerializeField] public Transform cameraTarget; // Дочерний объект CameraTarget для точки обзора
    
    [Header("Camera Distance")]
    [SerializeField] private float distance = 5f;
    [SerializeField] private float distanceWhenMoving = 6f; // Дистанция при движении/прыжке
    [SerializeField] private float distanceTransitionSpeed = 3f; // Скорость перехода между дистанциями
    
    private float currentDistance; // Текущая интерполированная дистанция
    private ThirdPersonController playerController; // Кэшированная ссылка на контроллер игрока
    
    [Header("Mouse Settings")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private bool invertY = false;
    
    [Header("Touch Settings")]
    [SerializeField] private float touchSensitivity = 2f; // Чувствительность для touch-ввода
    
    [Header("Camera Smoothing")]
    [Tooltip("Сглаживание вращения камеры (0 = без сглаживания, 1 = максимальное сглаживание)")]
    [SerializeField] [Range(0f, 1f)] private float rotationSmoothing = 0.1f;
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения в консоли")]
    [SerializeField] private bool debugCameraInput = false;
    
    [Header("Vertical Angle Limits")]
    [SerializeField] private float minVerticalAngle = -80f;
    [SerializeField] private float maxVerticalAngle = 80f;
    
    [Header("Camera Shake")]
    [Tooltip("Интенсивность лёгкой тряски (>33%)")]
    [SerializeField] private float lightShakeIntensity = 0.05f;
    [Tooltip("Интенсивность средней тряски (>66%)")]
    [SerializeField] private float mediumShakeIntensity = 0.1f;
    [Tooltip("Интенсивность сильной тряски (100%)")]
    [SerializeField] private float strongShakeIntensity = 0.25f;
    [Tooltip("Длительность тряски при достижении порога")]
    [SerializeField] private float shakeDuration = 0.5f;
    
    private float currentHorizontalAngle;
    private float currentVerticalAngle;
    
    // Переменные для тряски камеры
    private float shakeIntensity = 0f;
    private float shakeTimeRemaining = 0f;
    private Vector3 shakeOffset = Vector3.zero;
    
    // Целевые углы для плавного сглаживания (без ускорения/замедления)
    private float targetHorizontalAngle;
    private float targetVerticalAngle;
    private Camera cam;
    
    // Для touch-ввода на мобильных устройствах
    private Vector2 lastTouchPosition;
    private bool isTouching = false;
    private bool isMobileDevice = false;
#pragma warning disable CS0414 // Используется в #else блоке (мобильная сборка), в редакторе не вызывается
    private int cameraTouchFingerId = -1; // ID пальца, который управляет камерой
#pragma warning restore CS0414
    
    // Защита от вечной блокировки камеры
    private float lastInputTime = 0f;
    private const float INPUT_TIMEOUT = 0.5f; // Если нет ввода 0.5 секунды, сбрасываем состояние
    
    // Векторы направления камеры для использования в ThirdPersonController
    private Vector3 cameraForward;
    private Vector3 cameraRight;
    
    private void Awake()
    {
        cam = GetComponent<Camera>();
        currentDistance = distance; // Инициализируем текущую дистанцию
        
        // Автоматически найти Player, если не назначен
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }
        
        // Кэшируем ссылку на контроллер игрока
        if (target != null)
        {
            playerController = target.GetComponent<ThirdPersonController>();
        }
        
        // Автоматически находим CameraTarget, если не назначен
        if (cameraTarget == null && target != null)
        {
            // Ищем дочерний объект с именем "CameraTarget"
            Transform foundTarget = target.Find("CameraTarget");
            if (foundTarget != null)
            {
                cameraTarget = foundTarget;
            }
        }
        
        // Инициализация углов камеры на основе текущей позиции камеры
        if (target != null)
        {
            Vector3 targetCenter = GetTargetCenter();
            Vector3 directionToCamera = transform.position - targetCenter;
            float initialDistance = directionToCamera.magnitude;
            
            if (initialDistance > 0.001f)
            {
                // Нормализуем направление
                Vector3 normalizedDirection = directionToCamera / initialDistance;
                
                // Вычисляем горизонтальный угол
                Vector3 horizontalDir = new Vector3(normalizedDirection.x, 0f, normalizedDirection.z);
                if (horizontalDir.magnitude > 0.001f)
                {
                    horizontalDir.Normalize();
                    currentHorizontalAngle = Mathf.Atan2(horizontalDir.x, horizontalDir.z) * Mathf.Rad2Deg;
                }
                
                // Вычисляем вертикальный угол
                currentVerticalAngle = Mathf.Asin(normalizedDirection.y) * Mathf.Rad2Deg;
                currentVerticalAngle = Mathf.Clamp(currentVerticalAngle, minVerticalAngle, maxVerticalAngle);
            }
            else
            {
                // Если камера слишком близко, используем значения по умолчанию
                currentHorizontalAngle = 0f;
                currentVerticalAngle = 0f;
            }
        }
        
        // Инициализируем целевые углы равными текущим
        targetHorizontalAngle = currentHorizontalAngle;
        targetVerticalAngle = currentVerticalAngle;
    }
    
    private void Start()
    {
        // Курсор не блокируется автоматически - только при зажатой ПКМ
        
        // Определяем, является ли устройство мобильным/планшетом
        UpdateMobileDeviceStatus();
        
        // Инициализируем время последнего ввода
        lastInputTime = Time.time;
    }
    
    /// <summary>
    /// Обновляет статус мобильного устройства
    /// </summary>
    private void UpdateMobileDeviceStatus()
    {
#if EnvirData_yg
        // Используем YG2 envirdata для определения устройства
        isMobileDevice = YG2.envir.isMobile || YG2.envir.isTablet;
        
        // В редакторе также проверяем симулятор
#if UNITY_EDITOR
        if (!isMobileDevice)
        {
            // Проверяем настройки симуляции в YG2
            if (YG2.envir.device == YG2.Device.Mobile || YG2.envir.device == YG2.Device.Tablet)
            {
                isMobileDevice = true;
            }
        }
        // В редакторе/симуляторе всегда разрешаем touch-управление, если устройство определено как мобильное
        // даже если Input.touchSupported = false (это нормально для симулятора)
#else
        // На реальном устройстве проверяем touch support
        if (isMobileDevice && !Input.touchSupported)
        {
            // Если устройство мобильное, но touch не поддерживается, используем эмуляцию мыши
        }
#endif
#else
        // Если модуль EnvirData не подключен, используем стандартную проверку
        isMobileDevice = Application.isMobilePlatform || Input.touchSupported;
        
#if UNITY_EDITOR
        // В редакторе также проверяем симулятор через настройки проекта
        if (!isMobileDevice)
        {
            // Проверяем, запущен ли симулятор мобильного устройства
            // В Unity Editor можно симулировать мобильные устройства через Device Simulator
            isMobileDevice = Application.isMobilePlatform;
        }
#endif
#endif
    }
    
    // Получает позицию точки обзора камеры (CameraTarget или позиция target)
    // Этот метод вызывается каждый кадр, чтобы камера всегда следовала за движущимся персонажем
    private Vector3 GetTargetCenter()
    {
        // Если назначен cameraTarget, используем его позицию
        // Это обеспечивает точное позиционирование и отсутствие дрожания
        if (cameraTarget != null)
        {
            return cameraTarget.position;
        }
        
        // Если cameraTarget не назначен, пытаемся найти его автоматически
        if (target != null)
        {
            Transform foundTarget = target.Find("CameraTarget");
            if (foundTarget != null)
            {
                cameraTarget = foundTarget;
                return cameraTarget.position;
            }
        }
        
        // Если cameraTarget не найден, используем позицию target как fallback
        if (target != null)
        {
            return target.position;
        }
        
        return Vector3.zero;
    }
    
    private void LateUpdate()
    {
        if (target == null) return;
        
        // ВАЖНО: LateUpdate вызывается после всех Update()
        // Это гарантирует, что позиция персонажа уже обновлена
        
        // Обновляем тряску камеры
        UpdateShake();
        
        // Сначала обрабатываем ввод мыши (обновляем целевые углы)
        HandleMouseInput();
        
        // Плавно интерполируем углы к целевым значениям
        SmoothCameraRotation();
        
        // Обновляем дистанцию камеры в зависимости от состояния игрока
        UpdateCameraDistance();
        
        // Затем обновляем позицию камеры относительно текущей позиции CameraTarget
        // Это должно быть последним, чтобы использовать самую актуальную позицию персонажа
        UpdateCameraPosition();
        
        // И наконец обновляем векторы направления для контроллера
        UpdateCameraDirection();
    }
    
    /// <summary>
    /// Обновляет дистанцию камеры в зависимости от состояния игрока (движение/прыжок/idle)
    /// </summary>
    private void UpdateCameraDistance()
    {
        // Определяем целевую дистанцию
        float targetDistance = distance;
        
        if (playerController != null)
        {
            // Проверяем, двигается ли игрок или прыгает
            bool isMovingOrJumping = playerController.GetCurrentSpeed() > 0.1f || !playerController.IsGrounded();
            
            if (isMovingOrJumping)
            {
                targetDistance = distanceWhenMoving;
            }
        }
        
        // Плавно интерполируем к целевой дистанции
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, distanceTransitionSpeed * Time.deltaTime);
    }
    
    private void HandleMouseInput()
    {
        // Обновляем статус мобильного устройства (на случай если данные YG2 пришли позже)
#if EnvirData_yg
        // Проверяем YG2 данные каждый кадр в редакторе, так как они могут прийти позже
#if UNITY_EDITOR
        bool yg2IsMobile = YG2.envir.isMobile || YG2.envir.isTablet;
        if (yg2IsMobile != isMobileDevice)
        {
            UpdateMobileDeviceStatus();
        }
#else
        if (!isMobileDevice && (YG2.envir.isMobile || YG2.envir.isTablet))
        {
            UpdateMobileDeviceStatus();
        }
#endif
#endif
        
        // На мобильных устройствах всегда используем touch-ввод (или эмуляцию мыши в редакторе)
        // ВАЖНО: Эта проверка должна быть ПЕРВОЙ, так как isMobileDevice уже установлен в true
        if (isMobileDevice)
        {
            HandleTouchInput();
            return;
        }
        
        // В редакторе: проверяем наличие JoystickManager для определения симулятора (fallback)
        // НО только если устройство уже определено как мобильное через YG2
#if UNITY_EDITOR
        // Проверяем, действительно ли мы в симуляторе мобильного устройства через YG2
        bool isSimulatorMobile = false;
#if EnvirData_yg
        isSimulatorMobile = YG2.envir.isMobile || YG2.envir.isTablet || 
                           YG2.envir.device == YG2.Device.Mobile || 
                           YG2.envir.device == YG2.Device.Tablet;
#endif
        
        // Если симулятор мобильного устройства активен И JoystickManager активен, используем touch-ввод
        if (isSimulatorMobile)
        {
            JoystickManager joystickManager = JoystickManager.Instance;
            bool joystickActive = joystickManager != null && joystickManager.gameObject.activeInHierarchy;
            
            if (!joystickActive)
            {
                joystickManager = FindFirstObjectByType<JoystickManager>();
                joystickActive = joystickManager != null && joystickManager.gameObject.activeInHierarchy;
            }
            
            if (joystickActive)
            {
                // В симуляторе мобильного устройства используем touch-ввод (ЛКМ)
                if (!isMobileDevice)
                {
                    isMobileDevice = true;
                }
                HandleTouchInput();
                return;
            }
        }
#endif
        
        // Проверяем наличие touch-ввода (на случай если устройство не определено как мобильное, но есть touch)
        bool hasTouchInput = Input.touchCount > 0;
        if (hasTouchInput)
        {
            HandleTouchInput();
            return;
        }
        
        // На desktop используем ПКМ
        HandleDesktopInput();
    }
    
    /// <summary>
    /// Обработка ввода на desktop (ПКМ)
    /// </summary>
    private void HandleDesktopInput()
    {
        // Проверяем, зажата ли правая кнопка мыши
        bool rightMouseButtonPressed = false;
        
#if ENABLE_INPUT_SYSTEM
        // Новый Input System
        if (Mouse.current != null)
        {
            rightMouseButtonPressed = Mouse.current.rightButton.isPressed;
        }
#else
        // Старый Input System
        rightMouseButtonPressed = Input.GetMouseButton(1); // 1 = правая кнопка мыши
#endif
        
        // Управление камерой только при зажатой ПКМ
        if (!rightMouseButtonPressed)
        {
            // Разблокируем курсор, если ПКМ не зажата
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            return;
        }
        
        // Блокируем курсор при зажатой ПКМ
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        
        float mouseX = 0f;
        float mouseY = 0f;
        
#if ENABLE_INPUT_SYSTEM
        // Новый Input System
        if (Mouse.current != null)
        {
            Vector2 mouseDelta = Mouse.current.delta.ReadValue();
            mouseX = mouseDelta.x * mouseSensitivity;
            mouseY = mouseDelta.y * mouseSensitivity;
        }
#else
        // Старый Input System
        mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
#endif
        
        ApplyCameraRotation(mouseX, mouseY);
    }
    
    /// <summary>
    /// Обработка touch-ввода на мобильных устройствах
    /// </summary>
    private void HandleTouchInput()
    {
        // В редакторе или если touch не поддерживается, эмулируем touch через мышь
#if UNITY_EDITOR
        // В редакторе всегда используем эмуляцию мыши для touch-ввода
        HandleMouseAsTouch();
        return;
#else
        // На реальном устройстве проверяем поддержку touch
        if (!Input.touchSupported)
        {
            // Если touch не поддерживается, но устройство мобильное, используем эмуляцию мыши
            HandleMouseAsTouch();
            return;
        }
        
        // Проверяем наличие touch-ввода
        if (Input.touchCount == 0)
        {
            // Если нет touch-ввода, но мы были в режиме тапа, сбрасываем состояние
            if (isTouching)
            {
                isTouching = false;
                cameraTouchFingerId = -1;
            }
            return;
        }
        
        // Ищем touch для управления камерой
        Touch cameraTouch = default(Touch);
        bool foundCameraTouch = false;
        
        // Если уже есть активный touch для камеры, ищем его по fingerId
        if (isTouching && cameraTouchFingerId >= 0)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch currentTouch = Input.GetTouch(i);
                if (currentTouch.fingerId == cameraTouchFingerId)
                {
                    // Нашли тот же touch - используем его
                    cameraTouch = currentTouch;
                    foundCameraTouch = true;
                    break;
                }
            }
        }
        
        // Если не нашли активный touch, ищем новый touch, который НЕ на джойстике
        if (!foundCameraTouch)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch currentTouch = Input.GetTouch(i);
                // Игнорируем touch на джойстике
                if (!IsTouchOnJoystick(currentTouch.position))
                {
                    cameraTouch = currentTouch;
                    foundCameraTouch = true;
                    break;
                }
            }
        }
        
        // Если не нашли touch для камеры, сбрасываем состояние
        if (!foundCameraTouch)
        {
            if (isTouching)
            {
                isTouching = false;
                cameraTouchFingerId = -1;
            }
            return;
        }
        
        Touch touch = cameraTouch;
        
        // Обрабатываем начало тапа
        // Явно указываем UnityEngine.TouchPhase для избежания конфликта с InputSystem.TouchPhase
        if (touch.phase == UnityEngine.TouchPhase.Began)
        {
            // Проверяем, не на Canvas UI элементе ли тап (не на 3D объектах)
            if (IsPointerOverCanvasUI(touch.position))
            {
                // Тап на UI элементе - игнорируем для управления камерой
                isTouching = false;
                cameraTouchFingerId = -1;
                return;
            }
            
            // Проверяем, не на джойстике ли тап
            if (IsTouchOnJoystick(touch.position))
            {
                // Тап на джойстике - игнорируем для управления камерой
                isTouching = false;
                cameraTouchFingerId = -1;
                return;
            }
            
            // ВАЖНО: Проверяем, не попал ли тап в 3D объекты с коллайдерами (например, GradePanel)
            // Эти объекты не должны начинать управление камерой
            // ВАЖНО: При быстрых кликах НЕ блокируем камеру - просто не начинаем управление для этого конкретного тапа
            if (CheckIfRaycastHitsInteractive3DObjects(touch.position))
            {
                // Тап попал в интерактивный 3D объект (GradePanel и т.д.) - не начинаем управление камерой для ЭТОГО тапа
                // НО: НЕ блокируем камеру полностью - следующий тап (если не на GradePanel) сможет начать управление
                if (debugCameraInput)
                {
                    Debug.Log("[ThirdPersonCamera] Тап на интерактивном 3D объекте - не начинаем управление для этого тапа (камера не заблокирована)");
                }
                // ВАЖНО: НЕ устанавливаем isTouching = false здесь, так как это может заблокировать камеру
                // Просто не начинаем управление для этого конкретного тапа
                return;
            }
            
            // Тап не на джойстике, не на UI и не на интерактивных 3D объектах - начинаем управление камерой
            isTouching = true;
            cameraTouchFingerId = touch.fingerId; // Сохраняем ID пальца для отслеживания
            lastTouchPosition = touch.position;
            lastInputTime = Time.time; // Обновляем время последнего ввода
            return;
        }
        
        // Если мы уже в режиме тапа, но тап был на джойстике, сбрасываем состояние
        // Не проверяем UI во время движения - если тап начался не на UI, продолжаем управление
        if (isTouching && IsTouchOnJoystick(touch.position))
        {
            isTouching = false;
            cameraTouchFingerId = -1;
            return;
        }
        
        // Обрабатываем движение тапа (только если не на джойстике)
        if (isTouching && (touch.phase == UnityEngine.TouchPhase.Moved || touch.phase == UnityEngine.TouchPhase.Stationary))
        {
            if (!IsTouchOnJoystick(touch.position))
            {
                Vector2 touchDelta = touch.position - lastTouchPosition;
                
                // Применяем вращение только если есть движение
                if (touchDelta.magnitude > 0.01f)
                {
                    // Нормализуем чувствительность (не умножаем на Time.deltaTime, так как это уже дельта позиции)
                    float touchX = touchDelta.x * touchSensitivity * 0.1f; // Увеличили с 0.01f до 0.1f для более отзывчивого управления
                    float touchY = touchDelta.y * touchSensitivity * 0.1f;
                    
                    ApplyCameraRotation(touchX, touchY);
                }
                
                lastTouchPosition = touch.position;
            }
        }
        
        // Обрабатываем окончание тапа
        if (touch.phase == UnityEngine.TouchPhase.Ended || touch.phase == UnityEngine.TouchPhase.Canceled)
        {
            isTouching = false;
            cameraTouchFingerId = -1; // Сбрасываем ID пальца
        }
#endif
    }
    
    /// <summary>
    /// Эмуляция touch-ввода через мышь в редакторе/симуляторе
    /// </summary>
    private void HandleMouseAsTouch()
    {
        // Убираем избыточный лог - он спамит каждый кадр
        // if (debugCameraInput)
        // {
        //     Debug.Log($"[ThirdPersonCamera] HandleMouseAsTouch вызван, isTouching={isTouching}");
        // }
        
        bool mouseButtonDown = false;
        bool mouseButtonJustPressed = false;
        Vector2 mousePosition = Vector2.zero;
        
#if ENABLE_INPUT_SYSTEM
        // Новый Input System
        // В симуляторе Unity Device Simulator лучше использовать Pointer.current вместо Mouse.current
        UnityEngine.InputSystem.Pointer pointer = UnityEngine.InputSystem.Pointer.current;
        
        if (pointer != null)
        {
            // Используем Pointer для получения позиции (работает для touch и mouse)
            try
            {
                mousePosition = pointer.position.ReadValue();
                mouseButtonDown = pointer.press.isPressed;
                mouseButtonJustPressed = pointer.press.wasPressedThisFrame;
            }
            catch
            {
                // Fallback на Mouse
                if (Mouse.current != null)
                {
                    mouseButtonDown = Mouse.current.leftButton.isPressed;
                    mouseButtonJustPressed = Mouse.current.leftButton.wasPressedThisFrame;
                    try
                    {
                        mousePosition = Mouse.current.position.ReadValue();
                    }
                    catch
                    {
                        mousePosition = Vector2.zero;
                    }
                }
            }
        }
        else if (Mouse.current != null)
        {
            // Fallback на Mouse, если Pointer недоступен
            mouseButtonDown = Mouse.current.leftButton.isPressed;
            mouseButtonJustPressed = Mouse.current.leftButton.wasPressedThisFrame;
            try
            {
                mousePosition = Mouse.current.position.ReadValue();
            }
            catch
            {
                mousePosition = Vector2.zero;
            }
        }
        else
        {
            // Ни Pointer, ни Mouse не доступны
            if (isTouching)
            {
                isTouching = false;
            }
            return;
        }
        
        // Если позиция нулевая, но кнопка нажата, используем центр экрана как fallback
        if (mousePosition == Vector2.zero && (mouseButtonDown || mouseButtonJustPressed))
        {
            mousePosition = new Vector2(Screen.width / 2f, Screen.height / 2f);
        }
#else
        // Старый Input System (если не используется новый)
        mouseButtonDown = Input.GetMouseButton(0);
        mouseButtonJustPressed = Input.GetMouseButtonDown(0);
        mousePosition = Input.mousePosition;
#endif
        
        // При начале клика проверяем, не на UI элементе ли (проверяем только в момент начала тапа)
        if (mouseButtonJustPressed)
        {
            // Проверяем только Canvas UI элементы, а не все GameObjects
            bool isOverUI = IsPointerOverCanvasUI(mousePosition);
            
            if (debugCameraInput)
            {
                Debug.Log($"[ThirdPersonCamera] mouseButtonJustPressed: mousePosition={mousePosition}, isOverUI={isOverUI}");
            }
            
            if (isOverUI)
            {
                // Клик на UI элементе - игнорируем для управления камерой
                if (debugCameraInput)
                {
                    Debug.Log("[ThirdPersonCamera] Клик на UI элементе - игнорируем для камеры");
                }
                isTouching = false;
                return;
            }
            
            bool onJoystick = IsTouchOnJoystick(mousePosition);
            if (onJoystick)
            {
                // Клик на джойстике - игнорируем для управления камерой
                isTouching = false;
                return;
            }
            
            // ВАЖНО: Проверяем, не попал ли клик в 3D объекты с коллайдерами (например, GradePanel)
            // Эти объекты не должны начинать управление камерой
            // Проверяем ТОЛЬКО в момент начала клика, и больше не проверяем до отпускания кнопки
            // ВАЖНО: При быстрых кликах НЕ блокируем камеру - просто не начинаем управление для этого конкретного клика
            // Это позволяет камере работать нормально даже при частых кликах на GradePanel
            // ВАЖНО: Если GradePanel не может обработать клик (анимация идет), НЕ блокируем камеру вообще
            bool hitsInteractiveObject = CheckIfRaycastHitsInteractive3DObjects(mousePosition);
            if (hitsInteractiveObject)
            {
                // Клик попал в интерактивный 3D объект (GradePanel и т.д.) - не начинаем управление камерой для ЭТОГО клика
                // НО: НЕ блокируем камеру полностью - следующий клик (если не на GradePanel) сможет начать управление
                // ВАЖНО: CheckIfRaycastHitsInteractive3DObjects уже проверил CanProcessClick(), поэтому если мы здесь,
                // значит панель может обработать клик, и мы просто не начинаем управление для этого конкретного клика
                if (debugCameraInput)
                {
                    Debug.Log("[ThirdPersonCamera] Клик на интерактивном 3D объекте - не начинаем управление для этого клика (камера не заблокирована, следующий клик сможет начать управление)");
                }
                // ВАЖНО: НЕ устанавливаем isTouching = false здесь, так как это может заблокировать камеру
                // Просто не начинаем управление для этого конкретного клика
                // Камера сможет начать управление при следующем клике, если он не на GradePanel
                return;
            }
            
            // Клик не на джойстике, не на UI и не на интерактивных 3D объектах - начинаем управление камерой
            isTouching = true;
            lastTouchPosition = mousePosition;
            lastInputTime = Time.time; // Обновляем время последнего ввода
            return;
        }
        
        // ВАЖНО: Если управление камерой уже началось (isTouching = true), НЕ проверяем GradePanel
        // Это предотвращает блокировку камеры когда пользователь случайно проводит пальцем над GradePanel
        // Проверка GradePanel выполняется ТОЛЬКО в момент начала клика (mouseButtonJustPressed)
        
        // ВАЖНО: Если ЛКМ нажата, но isTouching еще не установлен (пропустили mouseButtonJustPressed)
        // Это может произойти при быстрых кликах на GradePanel
        // В этом случае проверяем только джойстик и UI, НЕ GradePanel (чтобы не блокировать камеру при быстрых кликах)
        // Это позволяет камере начать работу даже если пользователь быстро кликал на GradePanel
        if (mouseButtonDown && !isTouching)
        {
            // Проверяем только UI и джойстик, НЕ GradePanel
            // Это критично для предотвращения блокировки камеры при быстрых кликах
            bool onJoystick = IsTouchOnJoystick(mousePosition);
            bool isOverUI = IsPointerOverCanvasUI(mousePosition);
            
            // ВАЖНО: НЕ проверяем GradePanel здесь - это может блокировать камеру при быстрых кликах
            // Если пользователь пропустил момент начала клика (быстро кликал на GradePanel),
            // позволяем начать управление камерой, если клик не на UI/джойстике
            if (!onJoystick && !isOverUI)
            {
                // Начинаем управление камерой
                isTouching = true;
                lastTouchPosition = mousePosition;
                lastInputTime = Time.time; // Обновляем время последнего ввода
                
                if (debugCameraInput)
                {
                    Debug.Log("[ThirdPersonCamera] Начинаем управление камерой (пропустили mouseButtonJustPressed, возможно из-за быстрых кликов на GradePanel)");
                }
            }
        }
        
        // Защита от вечной блокировки: если нет ввода долгое время, сбрасываем состояние
        // ВАЖНО: Проверяем только если кнопка НЕ нажата, чтобы не сбрасывать во время активного управления
        // ВАЖНО: Также сбрасываем, если кнопка нажата, но нет движения долгое время (защита от блокировки при быстрых кликах на GradePanel)
        if (isTouching)
        {
            if (!mouseButtonDown)
            {
                // Кнопка отпущена - сбрасываем состояние
                if (Time.time - lastInputTime > INPUT_TIMEOUT)
                {
                    if (debugCameraInput)
                    {
                        Debug.Log($"[ThirdPersonCamera] Таймаут ввода ({(Time.time - lastInputTime) * 1000:F0}ms), mouseButtonDown={mouseButtonDown}, сбрасываем isTouching");
                    }
                    isTouching = false;
                }
            }
            else if (mouseButtonDown && Time.time - lastInputTime > INPUT_TIMEOUT)
            {
                // Кнопка нажата, но нет движения долгое время - возможно, клик был на GradePanel
                // Сбрасываем состояние, чтобы камера могла начать работу при следующем движении
                if (debugCameraInput)
                {
                    Debug.Log($"[ThirdPersonCamera] Таймаут ввода при нажатой кнопке ({(Time.time - lastInputTime) * 1000:F0}ms), возможно клик на GradePanel, сбрасываем isTouching для разблокировки камеры");
                }
                isTouching = false;
            }
        }
        
        // Обрабатываем движение мыши при зажатой кнопке
        // ВАЖНО: Если управление камерой уже началось (isTouching = true), не проверяем GradePanel во время движения
        // Это предотвращает блокировку камеры когда пользователь случайно проводит пальцем над GradePanel
        if (isTouching && mouseButtonDown)
        {
            Vector2 mouseDelta = mousePosition - lastTouchPosition;
            
            // Проверяем только если мышь действительно двигается
            if (mouseDelta.magnitude > 0.001f)
            {
                if (debugCameraInput)
                {
                    Debug.Log($"[ThirdPersonCamera] isTouching={isTouching}, mouseButtonDown={mouseButtonDown}, обрабатываем движение, mouseDelta.magnitude={mouseDelta.magnitude}");
                }
                
                // ВАЖНО: Во время движения проверяем ТОЛЬКО UI и джойстик, НЕ GradePanel
                // GradePanel проверяется только при начале клика (mouseButtonJustPressed)
                // Это позволяет камере продолжать работать даже если палец случайно проходит над GradePanel
                bool onJoystick = IsTouchOnJoystick(mousePosition);
                bool isOverUI = IsPointerOverCanvasUI(mousePosition);
                
                if (onJoystick || isOverUI)
                {
                    if (debugCameraInput)
                    {
                        Debug.Log($"[ThirdPersonCamera] Тап переместился на UI/джойстик, сбрасываем управление камерой. isOverUI={isOverUI}, onJoystick={onJoystick}");
                    }
                    isTouching = false;
                    return;
                }
                
                // Применяем вращение камеры
                float touchX = mouseDelta.x * touchSensitivity * 0.1f;
                float touchY = mouseDelta.y * touchSensitivity * 0.1f;
                
                ApplyCameraRotation(touchX, touchY);
                
                // Обновляем позицию для следующего кадра
                lastTouchPosition = mousePosition;
                lastInputTime = Time.time; // Обновляем время последнего ввода
            }
            // Если мышь не двигается, просто обновляем lastTouchPosition если позиция изменилась
            else if (Vector2.Distance(mousePosition, lastTouchPosition) > 0.0001f)
            {
                lastTouchPosition = mousePosition;
            }
        }
        // ВАЖНО: Если кнопка нажата, но isTouching = false (клик был на GradePanel), и мышь двигается,
        // позволяем начать управление камерой - это предотвращает блокировку камеры при быстрых кликах
        else if (mouseButtonDown && !isTouching)
        {
            Vector2 mouseDelta = mousePosition - lastTouchPosition;
            
            // Если мышь двигается (не просто клик), позволяем начать управление камерой
            // Это критично для предотвращения блокировки камеры при быстрых кликах на GradePanel
            if (mouseDelta.magnitude > 0.01f)
            {
                // Проверяем только UI и джойстик, НЕ GradePanel
                bool onJoystick = IsTouchOnJoystick(mousePosition);
                bool isOverUI = IsPointerOverCanvasUI(mousePosition);
                
                if (!onJoystick && !isOverUI)
                {
                    // Начинаем управление камерой при движении мыши
                    isTouching = true;
                    lastTouchPosition = mousePosition;
                    lastInputTime = Time.time;
                    
                    if (debugCameraInput)
                    {
                        Debug.Log($"[ThirdPersonCamera] Начинаем управление камерой при движении мыши (после клика на GradePanel), mouseDelta.magnitude={mouseDelta.magnitude}");
                    }
                }
            }
            else
            {
                // Обновляем lastTouchPosition для следующей проверки
                lastTouchPosition = mousePosition;
            }
        }
        // Обрабатываем отпускание кнопки
        if (!mouseButtonDown && isTouching)
        {
            // Отпускание кнопки - сбрасываем состояние
            isTouching = false;
        }
    }
    
    /// <summary>
    /// Проверяет, находится ли тап в области джойстика
    /// </summary>
    private bool IsTouchOnJoystick(Vector2 screenPosition)
    {
        JoystickManager joystickManager = JoystickManager.Instance;
        if (joystickManager == null) return false;
        
        return joystickManager.IsPointOnJoystick(screenPosition);
    }
    
    /// <summary>
    /// Проверяет, находится ли указатель над Canvas UI элементом (не 3D объектами)
    /// Фильтрует только интерактивные UI элементы (Button, Selectable и т.д.), игнорируя фоновые Canvas элементы
    /// </summary>
    private bool IsPointerOverCanvasUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null) return false;
        
        // Используем EventSystem.RaycastAll для проверки UI элементов
        PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
        pointerEventData.position = screenPosition;
        
        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerEventData, results);
        
        // Фильтруем результаты - проверяем только интерактивные UI элементы из GraphicRaycaster
        // Игнорируем контейнеры MobileUIContainer и OverflowUiContainer, но учитываем их дочерние элементы
        foreach (var result in results)
        {
            // Проверяем, что это UI элемент Canvas (GraphicRaycaster)
            if (result.module is GraphicRaycaster)
            {
                GameObject hitObject = result.gameObject;
                
                // Пропускаем контейнеры MobileUIContainer и OverflowUiContainer
                if (hitObject.name == "MobileUIContainer" || hitObject.name == "OverflowUiContainer")
                {
                    if (debugCameraInput)
                    {
                        Debug.Log($"[ThirdPersonCamera] IsPointerOverCanvasUI: пропускаем контейнер: {hitObject.name}");
                    }
                    continue; // Пропускаем контейнер и проверяем следующие элементы
                }
                
                // Проверяем, является ли элемент интерактивным (Button, Selectable, IPointerDownHandler и т.д.)
                // Игнорируем фоновые Canvas элементы, которые не должны блокировать клики
                bool isInteractive = 
                    hitObject.GetComponent<Selectable>() != null ||
                    hitObject.GetComponent<Button>() != null ||
                    hitObject.GetComponent<IPointerClickHandler>() != null ||
                    hitObject.GetComponent<IPointerDownHandler>() != null ||
                    hitObject.GetComponent<Toggle>() != null ||
                    hitObject.GetComponent<Slider>() != null ||
                    hitObject.GetComponent<Scrollbar>() != null ||
                    hitObject.GetComponent<Dropdown>() != null ||
                    hitObject.GetComponent<InputField>() != null ||
                    hitObject.GetComponent<TMP_InputField>() != null;
                
                // Также проверяем, имеет ли Image raycastTarget (интерактивный элемент)
                Image image = hitObject.GetComponent<Image>();
                if (image != null && image.raycastTarget)
                {
                    isInteractive = true;
                }
                
                if (isInteractive)
                {
                    if (debugCameraInput)
                    {
                        Debug.Log($"[ThirdPersonCamera] IsPointerOverCanvasUI: найдено интерактивное UI: {hitObject.name}");
                    }
                    return true;
                }
                else if (debugCameraInput)
                {
                    Debug.Log($"[ThirdPersonCamera] IsPointerOverCanvasUI: UI элемент найден, но не интерактивный: {hitObject.name} (игнорируем)");
                }
            }
        }
        
        // Убираем логирование каждый кадр - это создаёт лишнюю нагрузку
        // Логируем только если действительно найдены интерактивные элементы
        // if (debugCameraInput)
        // {
        //     Debug.Log($"[ThirdPersonCamera] IsPointerOverCanvasUI: интерактивные UI элементы не найдены, results.Count={results.Count}");
        // }
        
        return false;
    }
    
    /// <summary>
    /// Проверяет, попадает ли луч в интерактивные 3D объекты (GradePanel и т.д.)
    /// Эти объекты не должны начинать управление камерой при клике
    /// ВАЖНО: Проверяет только активные коллайдеры, чтобы избежать ложных срабатываний
    /// </summary>
    private bool CheckIfRaycastHitsInteractive3DObjects(Vector2 screenPosition)
    {
        if (cam == null)
        {
            if (debugCameraInput)
            {
                Debug.Log("[ThirdPersonCamera] CheckIfRaycastHitsInteractive3DObjects: cam == null");
            }
            return false;
        }
        
        // Убираем избыточные логи - они спамятся каждый кадр
        // if (debugCameraInput)
        // {
        //     Debug.Log($"[ThirdPersonCamera] CheckIfRaycastHitsInteractive3DObjects: проверяем позицию {screenPosition}");
        // }
        
        // Создаём луч из позиции экрана
        Ray ray = cam.ScreenPointToRay(screenPosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity);
        
        // Убираем избыточные логи - они спамятся каждый кадр
        // if (debugCameraInput)
        // {
        //     Debug.Log($"[ThirdPersonCamera] CheckIfRaycastHitsInteractive3DObjects: найдено {hits.Length} попаданий");
        // }
        
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null || hit.collider.gameObject == null) continue;
            
            // ВАЖНО: Проверяем, что коллайдер активен и включен
            // Это предотвращает обнаружение скрытых/неактивных GradePanel
            if (!hit.collider.enabled || !hit.collider.gameObject.activeInHierarchy)
            {
                continue;
            }
            
            // Проверяем, является ли объект интерактивным 3D объектом (GradePanel, InteractableObject и т.д.)
            // Эти объекты обрабатывают клики и не должны начинать управление камерой
            GradePanel gradePanel = hit.collider.GetComponent<GradePanel>();
            if (gradePanel != null)
            {
                // ВАЖНО: Проверяем несколько условий для предотвращения ложных срабатываний:
                // Порядок проверок важен - сначала быстрые проверки, потом медленные
                
                // 1. Проверяем активность объекта (быстрая проверка)
                if (!gradePanel.gameObject.activeInHierarchy)
                {
                    // Объект неактивен, не блокируем камеру
                    continue;
                }
                
                // 2. Проверяем коллайдер (быстрая проверка)
                if (!hit.collider.enabled)
                {
                    // Коллайдер отключен - панель скрыта, не блокируем камеру
                    continue;
                }
                
                // 3. ВАЖНО: Проверяем ShouldBeVisible() - это проверяет все условия (брейнрот, расстояние до игрока)
                // Это самая надёжная проверка - если панель не должна быть видима, не блокируем камеру
                if (!gradePanel.ShouldBeVisible())
                {
                    // Панель не должна быть видима, не блокируем камеру
                    continue;
                }
                
                // 4. Дополнительно проверяем IsVisuallyVisible() для надёжности
                if (!gradePanel.IsVisuallyVisible())
                {
                    // Панель не видна визуально, не блокируем камеру
                    continue;
                }
                
                // 5. ВАЖНО: При быстрых кликах проверяем, может ли панель обработать клик
                // Если панель не может обработать клик (например, идет анимация), не блокируем камеру
                // Это предотвращает блокировку камеры при частых кликах
                if (!gradePanel.CanProcessClick())
                {
                    // Панель не может обработать клик (идет анимация или не видима), не блокируем камеру
                    if (debugCameraInput)
                    {
                        Debug.Log($"[ThirdPersonCamera] CheckIfRaycastHitsInteractive3DObjects: GradePanel не может обработать клик, не блокируем камеру");
                    }
                    continue;
                }
                
                // Все условия выполнены - панель видима, активна и может обработать клик
                // ВАЖНО: Возвращаем true только если панель действительно может обработать клик
                // Это предотвращает блокировку камеры при быстрых кликах, когда панель уже обрабатывает предыдущий клик
                if (debugCameraInput)
                {
                    Debug.Log($"[ThirdPersonCamera] CheckIfRaycastHitsInteractive3DObjects: найден видимый GradePanel: {hit.collider.gameObject.name}, панель может обработать клик - не начинаем управление камерой");
                }
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Применяет вращение камеры на основе ввода
    /// </summary>
    private void ApplyCameraRotation(float inputX, float inputY)
    {
        // Инверсия по Y, если нужно
        if (invertY)
        {
            inputY = -inputY;
        }
        
        // Обновляем целевые углы (вращение вокруг персонажа)
        targetHorizontalAngle += inputX;
        
        // Обновляем целевой вертикальный угол с ограничениями
        targetVerticalAngle -= inputY;
        targetVerticalAngle = Mathf.Clamp(targetVerticalAngle, minVerticalAngle, maxVerticalAngle);
    }
    
    /// <summary>
    /// Плавно интерполирует углы камеры к целевым значениям
    /// Использует фиксированную скорость интерполяции для плавности без ускорения/замедления
    /// </summary>
    private void SmoothCameraRotation()
    {
        // Если сглаживание отключено, сразу применяем целевые углы
        if (rotationSmoothing <= 0f)
        {
            currentHorizontalAngle = targetHorizontalAngle;
            currentVerticalAngle = targetVerticalAngle;
            return;
        }
        
        // Вычисляем разницу между текущим и целевым углом
        float horizontalDelta = Mathf.DeltaAngle(currentHorizontalAngle, targetHorizontalAngle);
        float verticalDelta = targetVerticalAngle - currentVerticalAngle;
        
        // Используем фиксированную скорость интерполяции для плавности
        // rotationSmoothing определяет долю изменения за кадр (0 = мгновенно, 1 = очень медленно)
        // Используем обратную величину для более интуитивного управления
        float smoothingFactor = 1f - rotationSmoothing;
        
        // Применяем интерполяцию с фиксированной скоростью
        currentHorizontalAngle += horizontalDelta * smoothingFactor;
        currentVerticalAngle += verticalDelta * smoothingFactor;
        
        // Ограничиваем вертикальный угол
        currentVerticalAngle = Mathf.Clamp(currentVerticalAngle, minVerticalAngle, maxVerticalAngle);
    }
    
    private void UpdateCameraPosition()
    {
        // ВАЖНО: Получаем текущую позицию CameraTarget каждый кадр
        // Это гарантирует, что камера всегда следует за движущимся персонажем
        // Автоматически находим CameraTarget, если не назначен
        if (cameraTarget == null && target != null)
        {
            Transform foundTarget = target.Find("CameraTarget");
            if (foundTarget != null)
            {
                cameraTarget = foundTarget;
            }
        }
        
        // Получаем актуальную позицию точки обзора (обновляется каждый кадр)
        Vector3 targetCenter = GetTargetCenter();
        
        // Преобразуем углы в радианы для вычислений
        // Используем точные вычисления без накопления ошибок
        float horizontalRad = currentHorizontalAngle * Mathf.Deg2Rad;
        float verticalRad = currentVerticalAngle * Mathf.Deg2Rad;
        
        // Вычисляем единичный вектор направления в сферических координатах
        // Эта формула гарантирует, что вектор всегда единичной длины
        float cosVertical = Mathf.Cos(verticalRad);
        float sinVertical = Mathf.Sin(verticalRad);
        float cosHorizontal = Mathf.Cos(horizontalRad);
        float sinHorizontal = Mathf.Sin(horizontalRad);
        
        Vector3 direction = new Vector3(
            sinHorizontal * cosVertical,  // X: горизонтальное смещение
            sinVertical,                  // Y: вертикальное смещение  
            cosHorizontal * cosVertical   // Z: глубина
        );
        
        // ВАЖНО: Вычисляем позицию камеры относительно ТЕКУЩЕЙ позиции CameraTarget
        // Каждый кадр мы берем актуальную позицию и вычисляем позицию заново
        // Это гарантирует, что камера всегда точно следует за персонажем
        
        // Вычисляем желаемую позицию камеры на точном расстоянии от CameraTarget
        Vector3 desiredCameraPosition = targetCenter + direction * currentDistance;
        
        // Применяем тряску камеры
        desiredCameraPosition += shakeOffset;
        
        // Устанавливаем позицию камеры
        transform.position = desiredCameraPosition;
        
        // КРИТИЧНО: Проверяем и корректируем расстояние для предотвращения накопления ошибок
        // Вычисляем фактическое расстояние от камеры до CameraTarget
        Vector3 actualDirection = transform.position - targetCenter;
        float actualDistance = actualDirection.magnitude;
        
        // Если расстояние отличается от желаемого, принудительно корректируем
        // Это предотвращает постепенное смещение камеры
        if (Mathf.Abs(actualDistance - currentDistance) > 0.001f)
        {
            if (actualDistance > 0.001f)
            {
                // Нормализуем направление и устанавливаем точное расстояние
                Vector3 correctedDirection = actualDirection / actualDistance;
                transform.position = targetCenter + correctedDirection * currentDistance;
            }
            else
            {
                // Если камера слишком близко, используем вычисленное направление
                transform.position = targetCenter + direction * currentDistance;
            }
        }
        
        // Камера всегда смотрит точно на текущую позицию CameraTarget
        // Вычисляем направление от камеры к точке обзора
        Vector3 lookDirection = targetCenter - transform.position;
        float lookDistance = lookDirection.magnitude;
        
        // Применяем поворот только если направление валидно
        if (lookDistance > 0.001f)
        {
            // Нормализуем направление и применяем поворот
            // Это гарантирует, что камера всегда смотрит точно на CameraTarget
            transform.rotation = Quaternion.LookRotation(lookDirection / lookDistance);
        }
    }
    
    private void UpdateCameraDirection()
    {
        // Обновляем векторы направления камеры для использования в ThirdPersonController
        cameraForward = transform.forward;
        cameraRight = transform.right;
        
        // Убираем вертикальную составляющую для движения по горизонтали
        cameraForward.y = 0f;
        cameraRight.y = 0f;
        cameraForward.Normalize();
        cameraRight.Normalize();
    }
    
    // Публичные методы для получения направления камеры (используются в ThirdPersonController)
    public Vector3 GetCameraForward()
    {
        return cameraForward;
    }
    
    public Vector3 GetCameraRight()
    {
        return cameraRight;
    }
    
    // Метод для получения дистанции камеры (используется в CameraCollisionHandler)
    public float GetDistance()
    {
        return currentDistance;
    }
    
    /// <summary>
    /// Принудительно обновляет позицию камеры (используется после телепортации)
    /// </summary>
    public void ForceUpdateCameraPosition()
    {
        if (target == null) return;
        
        // Обновляем позицию камеры немедленно
        UpdateCameraPosition();
        
        // Также обновляем направление камеры
        UpdateCameraDirection();
    }
    
    // Метод для разблокировки курсора (может быть полезен для меню)
    public void SetCursorLocked(bool locked)
    {
        if (locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
    
    #region Camera Shake
    
    /// <summary>
    /// Обновляет эффект тряски камеры
    /// </summary>
    private void UpdateShake()
    {
        if (shakeTimeRemaining > 0)
        {
            shakeTimeRemaining -= Time.deltaTime;
            
            // Генерируем случайное смещение
            float x = Random.Range(-1f, 1f) * shakeIntensity;
            float y = Random.Range(-1f, 1f) * shakeIntensity;
            float z = Random.Range(-1f, 1f) * shakeIntensity * 0.5f; // Z меньше для естественности
            
            // Плавное затухание
            float damping = shakeTimeRemaining / shakeDuration;
            shakeOffset = new Vector3(x, y, z) * damping;
        }
        else
        {
            shakeOffset = Vector3.zero;
        }
    }
    
    /// <summary>
    /// Запустить тряску камеры с указанной интенсивностью
    /// </summary>
    public void Shake(float intensity, float duration = -1f)
    {
        shakeIntensity = intensity;
        shakeTimeRemaining = duration > 0 ? duration : shakeDuration;
    }
    
    /// <summary>
    /// Запустить лёгкую тряску (>33% прогресса)
    /// </summary>
    public void ShakeLight()
    {
        Shake(lightShakeIntensity);
    }
    
    /// <summary>
    /// Запустить среднюю тряску (>66% прогресса)
    /// </summary>
    public void ShakeMedium()
    {
        Shake(mediumShakeIntensity);
    }
    
    /// <summary>
    /// Запустить сильную тряску (100% прогресса)
    /// </summary>
    public void ShakeStrong()
    {
        Shake(strongShakeIntensity, shakeDuration * 2f); // Сильная тряска длится дольше
    }
    
    #endregion
    
    private void OnDrawGizmosSelected()
    {
        // Визуализация в редакторе для отладки
        Vector3 targetCenter = GetTargetCenter();
        
        if (targetCenter != Vector3.zero)
        {
            // Рисуем сферу в точке обзора CameraTarget (желтый)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(targetCenter, 0.3f);
            
            // Рисуем линию от точки обзора к камере (зеленый)
            Gizmos.color = Color.green;
            Gizmos.DrawLine(targetCenter, transform.position);
            
            // Рисуем позицию объекта Player (красный)
            if (target != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(target.position, 0.2f);
            }
        }
    }
}
