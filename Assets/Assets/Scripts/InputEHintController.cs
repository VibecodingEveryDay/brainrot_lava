using UnityEngine;
using TMPro;
#if EnvirData_yg
using YG;
#endif

/// <summary>
/// Контроллер подсказки "нажмите E" для PlacementPanel.
/// Показывает подсказку когда игрок рядом с любым placement.
/// Скрывает на мобильных устройствах и планшетах (только Desktop).
/// </summary>
public class InputEHintController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Ссылка на TextMeshProUGUI с подсказкой (если не задана, ищется в дочерних)")]
    [SerializeField] private TextMeshProUGUI hintText;
    
    [Tooltip("Ссылка на CanvasGroup для плавного появления/исчезновения (опционально)")]
    [SerializeField] private CanvasGroup canvasGroup;
    
    [Header("Settings")]
    [Tooltip("Скорость появления/исчезновения (если есть CanvasGroup)")]
    [SerializeField] private float fadeSpeed = 5f;
    
    private bool isMobileDevice = false;
    private bool isVisible = false;
    private float targetAlpha = 0f;
    
    private void Awake()
    {
        // Находим TextMeshProUGUI если не назначен
        if (hintText == null)
        {
            hintText = GetComponentInChildren<TextMeshProUGUI>(true);
        }
        
        // Находим CanvasGroup если не назначен
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
        
        // Изначально скрываем
        HideImmediate();
        
        // Определяем тип устройства
        UpdateMobileDeviceStatus();
    }
    
    private void Start()
    {
        // Повторно проверяем на случай если YG2 инициализировался позже
        UpdateMobileDeviceStatus();
        
        // На мобильных устройствах полностью отключаем
        if (isMobileDevice)
        {
            gameObject.SetActive(false);
        }
    }
    
    private void Update()
    {
        // Обновляем статус устройства (на случай если данные YG2 пришли позже)
#if EnvirData_yg
        UpdateMobileDeviceStatus();
#endif
        
        // На мобильных устройствах не показываем
        if (isMobileDevice)
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
            return;
        }
        
        // Проверяем есть ли активная панель placement
        bool hasActivePanel = PlacementPanel.GetActivePanel() != null;
        
        // Обновляем видимость
        if (hasActivePanel && !isVisible)
        {
            Show();
        }
        else if (!hasActivePanel && isVisible)
        {
            Hide();
        }
        
        // Плавное изменение alpha если есть CanvasGroup
        if (canvasGroup != null)
        {
            canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha, targetAlpha, fadeSpeed * Time.deltaTime);
        }
    }
    
    /// <summary>
    /// Определяет, является ли устройство мобильным или планшетом.
    /// </summary>
    private void UpdateMobileDeviceStatus()
    {
#if EnvirData_yg
        // Используем YG2 envirdata для определения устройства
        isMobileDevice = YG2.envir.isMobile || YG2.envir.isTablet;
        
    #if UNITY_EDITOR
        // В редакторе также проверяем симулятор
        if (!isMobileDevice)
        {
            if (YG2.envir.device == YG2.Device.Mobile || YG2.envir.device == YG2.Device.Tablet)
            {
                isMobileDevice = true;
            }
        }
    #endif
#else
        // Если модуль EnvirData не подключен, используем стандартную проверку
    #if UNITY_EDITOR
        // В редакторе считаем что это Desktop
        isMobileDevice = false;
    #else
        isMobileDevice = Application.isMobilePlatform || Input.touchSupported;
    #endif
#endif
    }
    
    /// <summary>
    /// Показывает подсказку.
    /// </summary>
    private void Show()
    {
        isVisible = true;
        targetAlpha = 1f;
        
        if (hintText != null)
        {
            hintText.gameObject.SetActive(true);
        }
        
        if (canvasGroup == null && hintText != null)
        {
            // Если нет CanvasGroup, просто показываем текст
            hintText.alpha = 1f;
        }
    }
    
    /// <summary>
    /// Скрывает подсказку.
    /// </summary>
    private void Hide()
    {
        isVisible = false;
        targetAlpha = 0f;
        
        if (canvasGroup == null && hintText != null)
        {
            // Если нет CanvasGroup, просто скрываем текст
            hintText.alpha = 0f;
            hintText.gameObject.SetActive(false);
        }
    }
    
    /// <summary>
    /// Мгновенно скрывает подсказку (без анимации).
    /// </summary>
    private void HideImmediate()
    {
        isVisible = false;
        targetAlpha = 0f;
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
        }
        
        if (hintText != null)
        {
            hintText.alpha = 0f;
            hintText.gameObject.SetActive(false);
        }
    }
}
