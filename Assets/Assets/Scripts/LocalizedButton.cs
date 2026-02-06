using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if Localization_yg
using YG;
#endif

/// <summary>
/// Кнопка с локализованным текстом
/// </summary>
[RequireComponent(typeof(Button))]
public class LocalizedButton : MonoBehaviour
{
    [Header("Button Text")]
    [SerializeField] private TextMeshProUGUI buttonText;
    
    [Header("Localization")]
    [SerializeField] private string ruText = "Кнопка";
    [SerializeField] private string enText = "Button";
    [SerializeField] private string trText = "Düğme";
    
    private Button button;
    
    private void Awake()
    {
        button = GetComponent<Button>();
        
        // Автоматически найти TextMeshProUGUI, если не назначен
        if (buttonText == null)
        {
            buttonText = GetComponentInChildren<TextMeshProUGUI>();
        }
    }
    
    private void OnEnable()
    {
#if Localization_yg
        YG2.onSwitchLang += UpdateButtonText;
        UpdateButtonText(YG2.lang);
#else
        UpdateButtonText("ru");
#endif
    }
    
    private void OnDisable()
    {
#if Localization_yg
        YG2.onSwitchLang -= UpdateButtonText;
#endif
    }
    
    public void UpdateButtonText(string lang = null)
    {
        if (buttonText == null) return;
        
        if (lang == null)
        {
#if Localization_yg
            lang = YG2.lang ?? "ru";
#else
            lang = "ru";
#endif
        }
        
        string text = lang switch
        {
            "ru" => ruText,
            "en" or "us" or "as" or "ai" => enText,
            "tr" => trText,
            _ => enText
        };
        
        buttonText.text = text;
    }
    
    /// <summary>
    /// Установить локализованные тексты
    /// </summary>
    public void SetLocalizedTexts(string ru, string en, string tr = "")
    {
        ruText = ru;
        enText = en;
        trText = tr;
        UpdateButtonText();
    }
}
