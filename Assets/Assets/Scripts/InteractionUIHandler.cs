using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Компонент для обработки взаимодействия с UI префабом через тап/ЛКМ
/// При зажатии заполняет radial и вызывает взаимодействие при достижении 100%
/// </summary>
public class InteractionUIHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("References")]
    [SerializeField] private Image radialImage;
    [SerializeField] private CanvasGroup canvasGroup;
    
    [Header("Debug")]
    [SerializeField] private bool debugMode = true; // Включено по умолчанию для диагностики
    
    private InteractableObject parentInteractableObject;
    private bool isPressed = false;
    private float interactionTime = 2f;
    private float currentHoldTime = 0f;
    private bool interactionCompleted = false;
    private float lastPointerDownTime = -1f; // Время последнего вызова OnPointerDown
    private const float POINTER_DOWN_COOLDOWN = 0.03f; // Защита от двойного вызова (30ms), не блокирует первый клик
    private Camera _cachedMainCamera;
    private bool receivedPointerDownThisFrame;
    private bool hadPointerDownThisFrame;
    private Vector2 lastPointerPositionForFallback;
    
    private void Awake()
    {
        // Находим компоненты
        FindComponents();
        
        // Находим родительский InteractableObject
        FindParentInteractableObject();
    }
    
    private void Start()
    {
        // Убеждаемся, что Canvas может получать события
        EnsureCanvasCanReceiveEvents();
        
        // Если radial Image не найден, пытаемся найти его снова
        if (radialImage == null)
        {
            FindComponents();
        }
        
        // Инициализируем radial
        if (radialImage != null)
        {
            radialImage.fillAmount = 0f;
            Color color = radialImage.color;
            color.a = 0f;
            radialImage.color = color;
            if (debugMode)
            {
                Debug.Log($"[InteractionUIHandler] Radial инициализирован: {radialImage.name}");
            }
        }
        else
        {
            if (debugMode)
            {
                Debug.LogWarning($"[InteractionUIHandler] Radial Image не найден!");
            }
        }
        
        // Получаем время взаимодействия от родительского объекта
        if (parentInteractableObject != null)
        {
            interactionTime = parentInteractableObject.GetInteractionTime();
            if (debugMode)
            {
                Debug.Log($"[InteractionUIHandler] Время взаимодействия: {interactionTime}");
            }
        }
        else
        {
            if (debugMode)
            {
                Debug.LogWarning($"[InteractionUIHandler] parentInteractableObject == null!");
            }
        }
    }
    
    private void Update()
    {
        receivedPointerDownThisFrame = false;
        hadPointerDownThisFrame = false;
        
        if (parentInteractableObject == null)
            FindParentInteractableObject();
        
        // Обрабатываем прогресс при зажатии
        if (isPressed)
        {
            if (debugMode && Time.frameCount % 30 == 0)
            {
                Debug.Log($"[InteractionUIHandler] Update: isPressed={isPressed}, interactionCompleted={interactionCompleted}, parentInteractableObject={(parentInteractableObject != null ? parentInteractableObject.name : "null")}");
            }
            
            if (!interactionCompleted && parentInteractableObject != null)
            {
                // Обновляем прогресс через родительский объект
                parentInteractableObject.UpdateMobileInteraction(Time.deltaTime);
                
                // Получаем текущее время удержания
                currentHoldTime = parentInteractableObject.GetCurrentHoldTime();
                
                if (debugMode && Time.frameCount % 30 == 0)
                {
                    Debug.Log($"[InteractionUIHandler] После UpdateMobileInteraction: currentHoldTime={currentHoldTime:F3}, interactionTime={interactionTime:F3}");
                }
                
                // Обновляем визуальный прогресс
                if (radialImage != null && interactionTime > 0f)
                {
                    float fillAmount = Mathf.Clamp01(currentHoldTime / interactionTime);
                    radialImage.fillAmount = fillAmount;
                    
                    // Делаем radial видимым при начале взаимодействия
                    if (fillAmount > 0f)
                    {
                        Color color = radialImage.color;
                        if (color.a < 0.99f)
                        {
                            color.a = 1f;
                            radialImage.color = color;
                        }
                    }
                    
                    if (debugMode && Time.frameCount % 30 == 0)
                    {
                        Debug.Log($"[InteractionUIHandler] Прогресс: {fillAmount * 100:F1}% ({currentHoldTime:F2}/{interactionTime:F2}), radialImage.fillAmount={radialImage.fillAmount}");
                    }
                }
                else
                {
                    if (debugMode && Time.frameCount % 30 == 0)
                    {
                        Debug.LogWarning($"[InteractionUIHandler] radialImage={(radialImage != null ? radialImage.name : "null")}, interactionTime={interactionTime}");
                    }
                }
                
                // Проверяем, завершилось ли взаимодействие
                if (currentHoldTime >= interactionTime && !interactionCompleted)
                {
                    interactionCompleted = true;
                    if (debugMode)
                    {
                        Debug.Log($"[InteractionUIHandler] Взаимодействие завершено!");
                    }
                    // Взаимодействие завершится автоматически через CompleteInteraction() в InteractableObject
                    // Здесь мы просто отмечаем, что оно завершено
                }
            }
            else if (debugMode && Time.frameCount % 30 == 0)
            {
                Debug.LogWarning($"[InteractionUIHandler] Не обновляем прогресс: interactionCompleted={interactionCompleted}, parentInteractableObject={(parentInteractableObject != null ? "not null" : "null")}");
            }
        }
        
        // НЕ вызываем HandleKeyboardInput() здесь, так как это может конфликтовать
        // с мобильным взаимодействием и сбрасывать isHoldingKey
        // HandleKeyboardInput();
        
        // Альтернативная обработка кликов через Input (fallback для World Space Canvas)
        HandleDirectInput();
    }
    
    private bool wasMouseButtonHeldLastFrame = false; // Отслеживание состояния кнопки мыши
    
    /// <summary>
    /// Обрабатывает прямые клики через Input (fallback для World Space Canvas)
    /// </summary>
    private void HandleDirectInput()
    {
        if (parentInteractableObject == null) return;
        
        // Унифицированные флаги "указателя" (мышь или тап)
        bool pointerDown = false;
        bool pointerUp = false;
        bool pointerHeld = false;
        bool hasPointer = false;
        Vector2 pointerPosition = Vector2.zero;
        
#if ENABLE_INPUT_SYSTEM
        // Мышь (Desktop)
        if (Mouse.current != null)
        {
            pointerDown = Mouse.current.leftButton.wasPressedThisFrame;
            pointerUp = Mouse.current.leftButton.wasReleasedThisFrame;
            pointerHeld = Mouse.current.leftButton.isPressed;
            pointerPosition = Mouse.current.position.ReadValue();
            hasPointer = true;
        }
        // Touch (мобильные устройства) через новый Input System
        if (Touchscreen.current != null && Touchscreen.current.touches.Count > 0)
        {
            // Берём первый активный тап
            var touch = Touchscreen.current.touches[0];
            pointerPosition = touch.position.ReadValue();
            hasPointer = true;
            
            if (touch.press.wasPressedThisFrame)
            {
                pointerDown = true;
                pointerHeld = true;
            }
            else if (touch.press.wasReleasedThisFrame)
            {
                pointerUp = true;
            }
            else if (touch.press.isPressed)
            {
                pointerHeld = true;
            }
        }
#else
        // Desktop мышь
        if (Input.mousePresent)
        {
            pointerDown = Input.GetMouseButtonDown(0);
            pointerUp = Input.GetMouseButtonUp(0);
            pointerHeld = Input.GetMouseButton(0);
            pointerPosition = Input.mousePosition;
            hasPointer = true;
        }
        
        // Touch (мобильные устройства, включая симулятор)
        if (Input.touchSupported && Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            pointerPosition = touch.position;
            hasPointer = true;
            
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    pointerDown = true;
                    pointerHeld = true;
                    break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    pointerHeld = true;
                    break;
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    pointerUp = true;
                    break;
            }
        }
#endif
        
        if (!hasPointer)
            return;
        
        // ВАЖНО: Используем fallback только если стандартные события UI не работают
        // Проверяем, было ли событие OnPointerDown вызвано через стандартный механизм
        // Если да, то не используем fallback для mouseButtonDown
        
        // Проверяем, попадает ли клик/тап в UI элемент
        if (pointerDown && !isPressed)
        {
            hadPointerDownThisFrame = true;
            lastPointerPositionForFallback = pointerPosition;
            float timeSinceLastPointerDown = lastPointerDownTime < 0 ? 999f : (Time.time - lastPointerDownTime);
            bool cooldownOk = timeSinceLastPointerDown > POINTER_DOWN_COOLDOWN;
            if (cooldownOk && IsPointerOverUI(pointerPosition) && parentInteractableObject.CanInteract())
            {
                if (debugMode)
                    Debug.Log($"[InteractionUIHandler] HandleDirectInput: клик/тап через fallback");
                OnPointerDown(null);
            }
        }
        else if (pointerUp && isPressed)
        {
            // Кнопка отпущена - вызываем OnPointerUp только если кнопка действительно была отпущена
            // ВАЖНО: Проверяем, что прошло достаточно времени с OnPointerDown, чтобы избежать
            // ложных срабатываний при первом клике
            float timeSinceLastPointerDown = Time.time - lastPointerDownTime;
            if (timeSinceLastPointerDown > 0.05f) // Минимум 50ms между OnPointerDown и OnPointerUp
            {
                if (debugMode)
                {
                    Debug.Log($"[InteractionUIHandler] HandleDirectInput: кнопка отпущена, прошло {timeSinceLastPointerDown:F3}s");
                }
                OnPointerUp(null);
            }
            else if (debugMode)
            {
                Debug.Log($"[InteractionUIHandler] HandleDirectInput: игнорируем OnPointerUp, прошло только {timeSinceLastPointerDown:F3}s с OnPointerDown");
            }
        }
        else if (pointerHeld && isPressed)
        {
            // Продолжаем взаимодействие - НЕ проверяем IsPointerOverUI() каждый кадр,
            // так как это может вызвать ложные срабатывания при первом клике
            // Просто продолжаем взаимодействие пока кнопка зажата
            // НЕ вызываем OnPointerUp() здесь, даже если курсор ушел с UI элемента,
            // так как это может прервать взаимодействие при первом клике
        }
        else if (!pointerHeld && isPressed && wasMouseButtonHeldLastFrame)
        {
            // Кнопка была зажата в предыдущем кадре, но теперь не зажата
            // Это означает, что кнопка была отпущена, но событие OnPointerUp не было вызвано
            if (debugMode)
            {
                Debug.Log($"[InteractionUIHandler] HandleDirectInput: кнопка не зажата, но isPressed=true, вызываем OnPointerUp");
            }
            OnPointerUp(null);
        }
        
        wasMouseButtonHeldLastFrame = pointerHeld;
    }
    
    private void LateUpdate()
    {
        if (!hadPointerDownThisFrame || isPressed) return;
        if (parentInteractableObject == null) return;
        if (receivedPointerDownThisFrame) return;
        if (!IsPointerOverUI(lastPointerPositionForFallback) || !parentInteractableObject.CanInteract()) return;
        if (debugMode)
            Debug.Log($"[InteractionUIHandler] LateUpdate: клик не дошёл по EventSystem, обрабатываем fallback");
        OnPointerDown(null);
    }
    
    /// <summary>
    /// Проверяет, находится ли указатель мыши над UI элементом
    /// </summary>
    private bool IsPointerOverUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
        {
            if (debugMode && Time.frameCount % 60 == 0)
            {
                Debug.LogWarning($"[InteractionUIHandler] EventSystem.current == null");
            }
            return false;
        }
        
        PointerEventData pointerEventData = new PointerEventData(EventSystem.current);
        pointerEventData.position = screenPosition;
        
        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerEventData, results);
        
        // Проверяем, есть ли среди результатов наш UI элемент
        foreach (var result in results)
        {
            if (result.gameObject == gameObject || result.gameObject.transform.IsChildOf(transform))
            {
                if (debugMode && Time.frameCount % 60 == 0)
                {
                    Debug.Log($"[InteractionUIHandler] Клик обнаружен на {result.gameObject.name}");
                }
                return true;
            }
        }
        
        // Также проверяем через Physics Raycast для World Space Canvas
        if (_cachedMainCamera == null)
        {
            _cachedMainCamera = Camera.main;
            if (_cachedMainCamera == null)
                _cachedMainCamera = FindFirstObjectByType<Camera>();
        }
        Camera mainCamera = _cachedMainCamera;
        if (mainCamera != null)
        {
            Ray ray = mainCamera.ScreenPointToRay(screenPosition);
            RaycastHit hit;
            
            // Проверяем, попадает ли луч в наш UI элемент
            if (Physics.Raycast(ray, out hit, Mathf.Infinity))
            {
                if (hit.collider != null)
                {
                    // Проверяем, является ли это нашим UI элементом или его дочерним элементом
                    Transform hitTransform = hit.collider.transform;
                    while (hitTransform != null)
                    {
                        if (hitTransform == transform || hitTransform.IsChildOf(transform))
                        {
                            if (debugMode && Time.frameCount % 60 == 0)
                            {
                                Debug.Log($"[InteractionUIHandler] Клик обнаружен через Physics Raycast на {hitTransform.name}");
                            }
                            return true;
                        }
                        hitTransform = hitTransform.parent;
                    }
                }
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Находит компоненты в иерархии
    /// </summary>
    private void FindComponents()
    {
        // Находим radial Image (Image с Type=Filled, FillMethod=Radial360)
        if (radialImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true); // Ищем также в неактивных
            foreach (Image img in images)
            {
                if (img != null && img.type == Image.Type.Filled && img.fillMethod == Image.FillMethod.Radial360)
                {
                    radialImage = img;
                    if (debugMode)
                    {
                        Debug.Log($"[InteractionUIHandler] Найден radial Image: {img.name}");
                    }
                    break;
                }
            }
        }
        
        // Находим CanvasGroup для управления видимостью
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = GetComponentInParent<CanvasGroup>();
            }
        }
        
        // Убеждаемся, что есть Image компонент для получения событий
        Image buttonImage = GetComponent<Image>();
        if (buttonImage == null)
        {
            // Добавляем невидимый Image для получения событий
            buttonImage = gameObject.AddComponent<Image>();
            Color transparent = Color.white;
            transparent.a = 0f;
            buttonImage.color = transparent;
            buttonImage.raycastTarget = true;
            if (debugMode)
            {
                Debug.Log($"[InteractionUIHandler] Добавлен невидимый Image для получения событий");
            }
        }
        else
        {
            buttonImage.raycastTarget = true;
        }
        
        // Убеждаемся, что все дочерние Image компоненты могут получать события
        Image[] allImages = GetComponentsInChildren<Image>(true);
        foreach (Image img in allImages)
        {
            if (img != null && img != buttonImage)
            {
                img.raycastTarget = true;
            }
        }
    }
    
    /// <summary>
    /// Находит родительский InteractableObject
    /// </summary>
    private void FindParentInteractableObject()
    {
        // Ищем InteractableObject в родительских объектах
        Transform parent = transform.parent;
        int maxDepth = 10; // Защита от бесконечного цикла
        int depth = 0;
        
        while (parent != null && depth < maxDepth)
        {
            parentInteractableObject = parent.GetComponent<InteractableObject>();
            if (parentInteractableObject != null)
            {
                break;
            }
            parent = parent.parent;
            depth++;
        }
        
        // Если не нашли в родителях, ищем в сцене по ближайшему объекту
        if (parentInteractableObject == null)
        {
            // Ищем ближайший InteractableObject
            InteractableObject[] allObjects = FindObjectsByType<InteractableObject>(FindObjectsSortMode.None);
            float closestDistance = float.MaxValue;
            InteractableObject closestObject = null;
            
            foreach (InteractableObject obj in allObjects)
            {
                if (obj == null || !obj.gameObject.activeInHierarchy) continue;
                
                // Проверяем, есть ли у объекта UI, который соответствует этому префабу
                float distance = Vector3.Distance(transform.position, obj.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestObject = obj;
                }
            }
            
            if (closestObject != null && closestDistance < 5f) // Максимальное расстояние
            {
                parentInteractableObject = closestObject;
            }
        }
    }
    
    /// <summary>
    /// Обрабатывает ввод с клавиатуры для совместимости
    /// </summary>
    private void HandleKeyboardInput()
    {
        if (parentInteractableObject == null) return;
        
        // Проверяем, находится ли игрок в радиусе
        if (!parentInteractableObject.CanInteract()) return;
        
        // Проверяем, нажата ли клавиша взаимодействия
        bool keyPressed = false;
        
#if ENABLE_INPUT_SYSTEM
        // Новый Input System
        if (Keyboard.current != null)
        {
            Key key = GetKeyFromKeyCode(KeyCode.E);
            if (key != Key.None)
            {
                keyPressed = Keyboard.current[key].isPressed;
            }
        }
#else
        // Старый Input System
        keyPressed = Input.GetKey(KeyCode.E);
#endif
        
        // Если клавиша нажата и мы еще не начали взаимодействие через UI
        if (keyPressed && !isPressed)
        {
            OnPointerDown(null);
        }
        else if (!keyPressed && isPressed)
        {
            OnPointerUp(null);
        }
    }
    
#if ENABLE_INPUT_SYSTEM
    /// <summary>
    /// Преобразует KeyCode в Key для нового Input System
    /// </summary>
    private Key GetKeyFromKeyCode(KeyCode keyCode)
    {
        switch (keyCode)
        {
            case KeyCode.E: return Key.E;
            case KeyCode.F: return Key.F;
            case KeyCode.Space: return Key.Space;
            default:
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
    /// Вызывается при нажатии на UI (тап/ЛКМ)
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (debugMode)
        {
            Debug.Log($"[InteractionUIHandler] OnPointerDown вызван на {gameObject.name}, eventData={(eventData != null ? "not null" : "null")}");
        }
        
        if (parentInteractableObject == null)
        {
            FindParentInteractableObject();
            if (parentInteractableObject == null)
            {
                if (debugMode)
                {
                    Debug.LogWarning($"[InteractionUIHandler] parentInteractableObject не найден!");
                }
                return;
            }
        }
        
        // Проверяем, может ли игрок взаимодействовать
        if (!parentInteractableObject.CanInteract())
        {
            if (debugMode)
            {
                Debug.LogWarning($"[InteractionUIHandler] Игрок не может взаимодействовать!");
            }
            return;
        }
        
        // Защита от повторных вызовов
        if (isPressed)
        {
            if (debugMode)
            {
                Debug.Log($"[InteractionUIHandler] Уже нажато, игнорируем");
            }
            return;
        }
        
        isPressed = true;
        interactionCompleted = false;
        currentHoldTime = 0f;
        lastPointerDownTime = Time.time;
        receivedPointerDownThisFrame = true;
        
        // Устанавливаем флаг, что кнопка зажата (для HandleDirectInput)
        wasMouseButtonHeldLastFrame = true;
        
        if (debugMode)
        {
            Debug.Log($"[InteractionUIHandler] Начинаем взаимодействие с {parentInteractableObject.gameObject.name}, время: {Time.time:F3}");
        }
        
        // Начинаем взаимодействие через родительский объект
        parentInteractableObject.StartMobileInteraction();
        
        // Обновляем время взаимодействия
        interactionTime = parentInteractableObject.GetInteractionTime();
        
        // Показываем radial
        if (radialImage != null)
        {
            radialImage.fillAmount = 0f;
            Color color = radialImage.color;
            color.a = 1f;
            radialImage.color = color;
            if (debugMode)
            {
                Debug.Log($"[InteractionUIHandler] Radial показан, fillAmount = 0");
            }
        }
        else
        {
            if (debugMode)
            {
                Debug.LogWarning($"[InteractionUIHandler] radialImage == null!");
            }
        }
    }
    
    /// <summary>
    /// Вызывается при отпускании UI (тап/ЛКМ)
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (debugMode)
        {
            Debug.Log($"[InteractionUIHandler] OnPointerUp вызван, isPressed={isPressed}, eventData={(eventData != null ? "not null" : "null")}");
        }
        
        if (!isPressed)
        {
            if (debugMode)
            {
                Debug.Log($"[InteractionUIHandler] OnPointerUp: isPressed=false, игнорируем");
            }
            return;
        }
        
        isPressed = false;
        wasMouseButtonHeldLastFrame = false;
        
        // Останавливаем взаимодействие через родительский объект
        if (parentInteractableObject != null)
        {
            // Проверяем, не завершилось ли взаимодействие
            float currentHoldTimeCheck = parentInteractableObject.GetCurrentHoldTime();
            float interactionTimeCheck = parentInteractableObject.GetInteractionTime();
            
            if (debugMode)
            {
                Debug.Log($"[InteractionUIHandler] OnPointerUp: currentHoldTime={currentHoldTimeCheck:F3}, interactionTime={interactionTimeCheck:F3}");
            }
            
            // Останавливаем только если взаимодействие не завершено
            if (currentHoldTimeCheck < interactionTimeCheck)
            {
                if (debugMode)
                {
                    Debug.Log($"[InteractionUIHandler] OnPointerUp: вызываем StopMobileInteraction");
                }
                parentInteractableObject.StopMobileInteraction();
            }
            else
            {
                if (debugMode)
                {
                    Debug.Log($"[InteractionUIHandler] OnPointerUp: взаимодействие завершено, не останавливаем");
                }
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
        currentHoldTime = 0f;
        interactionCompleted = false;
        
        if (radialImage != null)
        {
            radialImage.fillAmount = 0f;
            Color color = radialImage.color;
            color.a = 0f;
            radialImage.color = color;
        }
    }
    
    /// <summary>
    /// Устанавливает родительский InteractableObject (вызывается из InteractableObject при создании UI)
    /// </summary>
    public void SetParentInteractableObject(InteractableObject interactableObject)
    {
        parentInteractableObject = interactableObject;
        if (interactableObject != null)
        {
            interactionTime = interactableObject.GetInteractionTime();
        }
    }
    
    /// <summary>
    /// Устанавливает ссылку на radial Image (вызывается из InteractableObject при создании UI)
    /// </summary>
    public void SetRadialImage(Image radialImg)
    {
        radialImage = radialImg;
    }
    
    /// <summary>
    /// Убеждается, что Canvas настроен для получения событий
    /// </summary>
    private void EnsureCanvasCanReceiveEvents()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            // Для World Space Canvas нужен GraphicRaycaster
            UnityEngine.UI.GraphicRaycaster raycaster = canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>();
            if (raycaster == null)
            {
                raycaster = canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
                if (debugMode)
                {
                    Debug.Log($"[InteractionUIHandler] Добавлен GraphicRaycaster на Canvas");
                }
            }
            
            // Убеждаемся, что Canvas получает события
            canvas.overrideSorting = true;
            
            // Для World Space Canvas также нужен PhysicsRaycaster на камере
            if (_cachedMainCamera == null)
            {
                _cachedMainCamera = Camera.main;
                if (_cachedMainCamera == null)
                    _cachedMainCamera = FindFirstObjectByType<Camera>();
            }
            Camera mainCamera = _cachedMainCamera;
            if (mainCamera != null && canvas.renderMode == RenderMode.WorldSpace)
            {
                UnityEngine.EventSystems.PhysicsRaycaster physicsRaycaster = mainCamera.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
                if (physicsRaycaster == null)
                {
                    physicsRaycaster = mainCamera.gameObject.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();
                    if (debugMode)
                    {
                        Debug.Log($"[InteractionUIHandler] Добавлен PhysicsRaycaster на камеру {mainCamera.name}");
                    }
                }
            }
            
            // Убеждаемся, что есть EventSystem в сцене
            if (EventSystem.current == null)
            {
                GameObject eventSystemObj = new GameObject("EventSystem");
                eventSystemObj.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                eventSystemObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
                eventSystemObj.AddComponent<StandaloneInputModule>();
#endif
                if (debugMode)
                {
                    Debug.Log($"[InteractionUIHandler] Создан EventSystem");
                }
            }
        }
    }
}
