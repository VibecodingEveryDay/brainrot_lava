using UnityEngine;
using TMPro;
#if Localization_yg
using YG;
#endif

/// <summary>
/// Компонент для автоматической локализации 3D TextMeshPro текста
/// Использует YG2 Localization для определения языка
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public class LocalizedText3D : MonoBehaviour
{
    [Header("Localization Keys")]
    [TextArea(2, 5)]
    [SerializeField] private string ruText = "Русский текст";
    [TextArea(2, 5)]
    [SerializeField] private string enText = "English text";
    [TextArea(2, 5)]
    [SerializeField] private string trText = "Türkçe metin";
    
    [Header("Settings")]
    [SerializeField] private bool updateOnStart = true;
    [SerializeField] private bool updateOnEnable = true;
    
    private TextMeshPro textComponent;
    private string currentLanguage = "ru";
    
    private void Awake()
    {
        textComponent = GetComponent<TextMeshPro>();
        if (textComponent == null)
        {
            Debug.LogError($"LocalizedText3D: TextMeshPro component not found on {gameObject.name}");
        }
    }
    
    private void Start()
    {
        if (updateOnStart)
        {
            UpdateText();
        }
    }
    
    private void OnEnable()
    {
#if Localization_yg
        // Подписываемся на изменение языка
        YG2.onSwitchLang += OnLanguageChanged;
        // Применяем текущий язык
        if (updateOnEnable)
        {
            OnLanguageChanged(YG2.lang);
        }
#else
        if (updateOnEnable)
        {
            UpdateText();
        }
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
    private void OnLanguageChanged(string lang)
    {
        currentLanguage = lang;
        UpdateText();
    }
#endif
    
    /// <summary>
    /// Обновить текст в зависимости от текущего языка
    /// </summary>
    public void UpdateText()
    {
        if (textComponent == null) return;
        
#if Localization_yg
        string lang = YG2.lang ?? "ru";
#else
        string lang = currentLanguage;
#endif
        
        string textToShow = GetTextForLanguage(lang);
        textComponent.text = textToShow;
    }
    
    /// <summary>
    /// Получить текст для указанного языка
    /// </summary>
    private string GetTextForLanguage(string lang)
    {
        switch (lang.ToLower())
        {
            case "ru":
                return ruText;
            case "en":
            case "us":
            case "as":
            case "ai":
                return enText;
            case "tr":
                return trText;
            default:
                return enText; // По умолчанию английский
        }
    }
    
    /// <summary>
    /// Установить текст для русского языка
    /// </summary>
    public void SetRussianText(string text)
    {
        ruText = text;
        UpdateText();
    }
    
    /// <summary>
    /// Установить текст для английского языка
    /// </summary>
    public void SetEnglishText(string text)
    {
        enText = text;
        UpdateText();
    }
    
    /// <summary>
    /// Установить текст для турецкого языка
    /// </summary>
    public void SetTurkishText(string text)
    {
        trText = text;
        UpdateText();
    }
}
