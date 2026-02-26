using UnityEngine;
#if EnvirData_yg
using YG;
#endif

/// <summary>
/// Управляет тенями Directional Light: отключает на mobile/tablet, включает на desktop.
/// Размещать на Main или Directional Light (Main->Directional Light).
/// </summary>
public class ShadowsManager : MonoBehaviour
{
    [Tooltip("Прямая ссылка на Directional Light (если не задана, ищется в дочерних объектах)")]
    [SerializeField] private Light directionalLight;
    
    [Tooltip("Тип теней для desktop (Soft или Hard)")]
    [SerializeField] private LightShadows desktopShadowType = LightShadows.Soft;
    
    [Tooltip("Тип теней для mobile/tablet (обычно NoShadows)")]
    [SerializeField] private LightShadows mobileShadowType = LightShadows.None;
    
    private void Awake()
    {
        if (directionalLight == null)
        {
            directionalLight = GetComponent<Light>();
            if (directionalLight == null)
                directionalLight = GetComponentInChildren<Light>();
            if (directionalLight != null && directionalLight.type != LightType.Directional)
            {
                Light[] lights = GetComponentsInChildren<Light>();
                foreach (Light l in lights)
                {
                    if (l.type == LightType.Directional)
                    {
                        directionalLight = l;
                        break;
                    }
                }
            }
        }
        
        if (directionalLight == null)
        {
            Debug.LogWarning("[ShadowsManager] Directional Light не найден на " + gameObject.name);
            return;
        }
        
        bool isMobile = IsMobileOrTablet();
        directionalLight.shadows = isMobile ? mobileShadowType : desktopShadowType;
    }
    
    private bool IsMobileOrTablet()
    {
#if EnvirData_yg
        bool isMobile = YG2.envir.isMobile || YG2.envir.isTablet;
#if UNITY_EDITOR
        if (!isMobile && (YG2.envir.device == YG2.Device.Mobile || YG2.envir.device == YG2.Device.Tablet))
            isMobile = true;
#endif
        return isMobile;
#else
        return Application.isMobilePlatform || Input.touchSupported;
#endif
    }
}
