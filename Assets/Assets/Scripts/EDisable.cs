using UnityEngine;
using UnityEngine.UI;
#if EnvirData_yg
using YG;
#endif

/// <summary>
/// Скрипт для скрытия Image компонента на мобильных/планшетных устройствах
/// Добавьте этот скрипт на GameObject с Image компонентом, который нужно скрыть на мобильных
/// </summary>
[RequireComponent(typeof(Image))]
public class EDisable : MonoBehaviour
{
    private Image targetImage;
    private bool isMobileDevice = false;
    private bool wasInitialized = false;
    
    private void Awake()
    {
        targetImage = GetComponent<Image>();
        if (targetImage == null)
        {
            Debug.LogWarning($"[EDisable] {gameObject.name}: Image компонент не найден!");
        }
    }
    
    private void Start()
    {
        UpdateMobileDeviceStatus();
        ApplyVisibility();
        wasInitialized = true;
    }
    
    private void Update()
    {
        // Обновляем статус мобильного устройства (на случай если данные YG2 пришли позже)
#if EnvirData_yg
        bool wasMobile = isMobileDevice;
        UpdateMobileDeviceStatus();
        if (wasMobile != isMobileDevice && wasInitialized)
        {
            ApplyVisibility();
        }
#endif
    }
    
    /// <summary>
    /// Обновляет статус мобильного устройства
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
        isMobileDevice = Application.isMobilePlatform || Input.touchSupported;
        
#if UNITY_EDITOR
        // В редакторе также проверяем симулятор
        if (!isMobileDevice)
        {
            isMobileDevice = Application.isMobilePlatform;
        }
#endif
#endif
    }
    
    /// <summary>
    /// Применяет видимость Image компонента в зависимости от типа устройства
    /// </summary>
    private void ApplyVisibility()
    {
        if (targetImage == null) return;
        
        // На мобильных устройствах скрываем Image (через SetActive GameObject)
        // Это полностью скрывает элемент в иерархии
        if (isMobileDevice)
        {
            if (targetImage.gameObject.activeSelf)
            {
                targetImage.gameObject.SetActive(false);
            }
        }
        else
        {
            // На PC показываем Image
            if (!targetImage.gameObject.activeSelf)
            {
                targetImage.gameObject.SetActive(true);
            }
        }
    }
}
