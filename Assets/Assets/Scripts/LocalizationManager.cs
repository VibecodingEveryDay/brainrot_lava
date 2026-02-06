using UnityEngine;
#if Localization_yg
using YG;
#endif

/// <summary>
/// Менеджер локализации для игры
/// Использует YG2 модуль Localization для определения языка
/// </summary>
public static class LocalizationManager
{
    private static string currentLanguage = "ru";
    public static event System.Action<string> OnLanguageChangedEvent;

#if Localization_yg
    static LocalizationManager()
    {
        // Инициализировать язык из YG2
        if (YG2.lang != null)
        {
            currentLanguage = YG2.lang;
        }
        
        // Подписаться на изменение языка
        YG2.onSwitchLang += OnLanguageChanged;
    }
    
    private static void OnLanguageChanged(string lang)
    {
        currentLanguage = lang;
        OnLanguageChangedEvent?.Invoke(lang);
    }
#else
    static LocalizationManager()
    {
        currentLanguage = "ru";
    }
#endif

    /// <summary>
    /// Получить текущий язык
    /// </summary>
    public static string GetCurrentLanguage()
    {
#if Localization_yg
        if (YG2.lang != null)
        {
            return YG2.lang;
        }
#endif
        return currentLanguage;
    }
    
    /// <summary>
    /// Проверить, является ли текущий язык английским
    /// </summary>
    public static bool IsEnglish()
    {
        return GetCurrentLanguage() == "en";
    }
    
    /// <summary>
    /// Проверить, является ли текущий язык русским
    /// </summary>
    public static bool IsRussian()
    {
        return GetCurrentLanguage() == "ru";
    }
}
