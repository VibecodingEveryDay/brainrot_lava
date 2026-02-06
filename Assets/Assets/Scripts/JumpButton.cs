using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if EnvirData_yg
using YG;
#endif

/// <summary>
/// Кнопка прыжка для мобильных устройств
/// Отображается только на mobile/tablet устройствах
/// </summary>
public class JumpButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] private Button jumpButton;
    [SerializeField] private Image buttonImage;
    
    [Header("Settings")]
    [SerializeField] private float pressedScale = 0.9f;
    [SerializeField] private Color pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения в консоли")]
    [SerializeField] private bool debug = false;
    
    private ThirdPersonController playerController;
    private Vector3 originalScale;
    private Color originalColor;
    private bool isMobileDevice = false;
    
    private void Awake()
    {
        // Автоматически найти компоненты, если не назначены
        if (jumpButton == null)
        {
            jumpButton = GetComponent<Button>();
        }
        
        if (buttonImage == null)
        {
            buttonImage = GetComponent<Image>();
        }
        
        // Сохраняем оригинальные значения
        if (buttonImage != null)
        {
            originalScale = buttonImage.transform.localScale;
            originalColor = buttonImage.color;
        }
        
        // Настраиваем кнопку
        if (jumpButton != null)
        {
            jumpButton.onClick.AddListener(OnJumpButtonClick);
            // Убеждаемся, что кнопка интерактивна
            jumpButton.interactable = true;
            
            // Важно: Button должен иметь возможность получать события
            // Проверяем, что transition установлен (иначе Button может не работать)
            if (jumpButton.transition == Selectable.Transition.None && debug)
            {
                Debug.LogWarning("[JumpButton] Button.transition = None. Это может повлиять на обработку событий.");
            }
        }
        
        // Убеждаемся, что Image не блокирует события (RaycastTarget должен быть включен для IPointerDownHandler)
        if (buttonImage != null)
        {
            buttonImage.raycastTarget = true;
        }
    }
    
    private void Start()
    {
        // Найти ThirdPersonController
        playerController = FindFirstObjectByType<ThirdPersonController>();
        
        // Определить, является ли устройство мобильным
        UpdateMobileDeviceStatus();
        
        // Показать/скрыть кнопку в зависимости от устройства
        UpdateButtonVisibility();
        
        // Дополнительная проверка: убеждаемся, что компоненты настроены правильно
        if (jumpButton != null && !jumpButton.interactable)
        {
            Debug.LogWarning("[JumpButton] Кнопка не интерактивна! Включаю interactable.");
            jumpButton.interactable = true;
        }
        
        if (buttonImage != null && !buttonImage.raycastTarget)
        {
            Debug.LogWarning("[JumpButton] Image не принимает raycast! Включаю raycastTarget.");
            buttonImage.raycastTarget = true;
        }
        
        // Проверяем наличие EventSystem
        if (EventSystem.current == null)
        {
            Debug.LogError("[JumpButton] EventSystem не найден в сцене! Кнопка не будет работать.");
        }
        
        // Проверяем и выводим информацию о кнопке
        if (debug)
        {
            Debug.Log($"[JumpButton] Инициализация: jumpButton={jumpButton != null}, buttonImage={buttonImage != null}, " +
                     $"interactable={jumpButton != null && jumpButton.interactable}, " +
                     $"raycastTarget={buttonImage != null && buttonImage.raycastTarget}, " +
                     $"EventSystem={EventSystem.current != null}, " +
                     $"playerController={playerController != null}");
        }
    }
    
    private void Update()
    {
        // Обновляем статус мобильного устройства каждый кадр (на случай если данные YG2 пришли позже)
#if EnvirData_yg
        bool wasMobile = isMobileDevice;
        UpdateMobileDeviceStatus();
        if (wasMobile != isMobileDevice)
        {
            UpdateButtonVisibility();
        }
#endif
    }
    
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
    
    private void UpdateButtonVisibility()
    {
        if (gameObject != null)
        {
            gameObject.SetActive(isMobileDevice);
        }
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        // НЕ используем eventData.Use() - это может заблокировать Button.onClick
        
        if (debug)
        {
            Debug.Log($"[JumpButton] OnPointerDown вызван на объекте {gameObject.name}, позиция: {eventData.position}, activeInHierarchy: {gameObject.activeInHierarchy}, used: {eventData.used}");
        }
        
        // Вызываем прыжок при нажатии
        if (playerController != null)
        {
            playerController.Jump();
            if (debug)
            {
                Debug.Log("[JumpButton] playerController.Jump() вызван из OnPointerDown");
            }
        }
        else
        {
            if (debug)
            {
                Debug.LogWarning("[JumpButton] playerController == null! Прыжок не выполнен.");
            }
        }
        
        // Визуальная обратная связь
        if (buttonImage != null)
        {
            buttonImage.transform.localScale = originalScale * pressedScale;
            buttonImage.color = pressedColor;
        }
    }
    
    // Тестовый метод для проверки, что кнопка получает события
    public void OnPointerClick(PointerEventData eventData)
    {
        if (debug)
        {
            Debug.Log($"[JumpButton] OnPointerClick вызван на объекте {gameObject.name}");
        }
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        // Возвращаем визуальное состояние
        if (buttonImage != null)
        {
            buttonImage.transform.localScale = originalScale;
            buttonImage.color = originalColor;
        }
    }
    
    private void OnJumpButtonClick()
    {
        if (debug)
        {
            Debug.Log($"[JumpButton] OnJumpButtonClick вызван на объекте {gameObject.name}");
        }
        
        // Прыжок теперь происходит только при OnPointerDown (нажатии), а не при отпускании
        // Этот метод оставлен для совместимости, но прыжок не вызывается здесь
    }
    
    private void OnDestroy()
    {
        if (jumpButton != null)
        {
            jumpButton.onClick.RemoveListener(OnJumpButtonClick);
        }
    }
}
