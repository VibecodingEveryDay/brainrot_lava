using UnityEngine;
using TMPro;
#if Localization_yg
using YG;
#endif

/// <summary>
/// Компонент для обновления текста в префабе взаимодействия
/// Обновляет текст в зависимости от того, есть ли брейнрот в руках
/// </summary>
public class InteractionTextUpdater : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI textComponent;
    
    [Header("Localization")]
    [SerializeField] private string ruTextTake = "Взять";
    [SerializeField] private string enTextTake = "Take";
    [SerializeField] private string ruTextPut = "Поставить";
    [SerializeField] private string enTextPut = "Put";
    
    private PlayerCarryController playerCarryController;
    private string currentLanguage = "ru";
    private bool lastHasBrainrotState = false; // Кэш для отслеживания изменений состояния
    
    private void Awake()
    {
        // Находим TextMeshProUGUI компонент, если не назначен
        if (textComponent == null)
        {
            textComponent = GetComponentInChildren<TextMeshProUGUI>(true);
        }
        
        // Находим PlayerCarryController
        FindPlayerCarryController();
    }
    
    private void Start()
    {
        // Инициализируем язык
        InitializeLanguage();
        // Обновляем текст при старте
        UpdateText();
    }
    
    private void OnEnable()
    {
#if Localization_yg
        // Подписываемся на изменение языка
        YG2.onSwitchLang += OnLanguageChanged;
        // Применяем текущий язык
        InitializeLanguage();
        UpdateText();
#else
        UpdateText();
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
    
    private void Update()
    {
        // Обновляем текст только при изменении состояния или языка
        // Проверяем, изменилось ли состояние (есть ли брейнрот в руках)
        bool hasBrainrotInHands = playerCarryController != null && playerCarryController.GetCurrentCarriedObject() != null;
        
        // Проверяем, изменился ли язык
        string lang = GetCurrentLanguage();
        bool languageChanged = lang != currentLanguage;
        
        // Обновляем текст только если состояние или язык изменились
        if (hasBrainrotInHands != lastHasBrainrotState || languageChanged)
        {
            lastHasBrainrotState = hasBrainrotInHands;
            if (languageChanged)
            {
                currentLanguage = lang;
            }
            UpdateText();
        }
    }
    
    /// <summary>
    /// Инициализирует язык
    /// </summary>
    private void InitializeLanguage()
    {
#if Localization_yg
        if (YG2.lang != null)
        {
            currentLanguage = YG2.lang;
        }
        else
#endif
        {
            currentLanguage = LocalizationManager.GetCurrentLanguage();
        }
    }
    
    /// <summary>
    /// Находит PlayerCarryController в сцене
    /// </summary>
    private void FindPlayerCarryController()
    {
        if (playerCarryController == null)
        {
            playerCarryController = FindFirstObjectByType<PlayerCarryController>();
        }
    }
    
    /// <summary>
    /// Обновляет текст в зависимости от текущего состояния
    /// </summary>
    private void UpdateText()
    {
        if (textComponent == null)
        {
            textComponent = GetComponentInChildren<TextMeshProUGUI>(true);
            if (textComponent == null) return;
        }
        
        // Определяем, есть ли брейнрот в руках
        bool hasBrainrotInHands = playerCarryController != null && playerCarryController.GetCurrentCarriedObject() != null;
        
        // Выбираем текст в зависимости от состояния и языка
        string newText = hasBrainrotInHands ? GetPutText() : GetTakeText();
        
        // Обновляем текст только если он изменился
        if (textComponent.text != newText)
        {
            textComponent.text = newText;
        }
    }
    
    /// <summary>
    /// Получает текущий язык
    /// </summary>
    private string GetCurrentLanguage()
    {
#if Localization_yg
        if (YG2.lang != null)
        {
            return YG2.lang;
        }
#endif
        return LocalizationManager.GetCurrentLanguage();
    }
    
    /// <summary>
    /// Получает текст "Взять" для текущего языка
    /// </summary>
    private string GetTakeText()
    {
        switch (currentLanguage.ToLower())
        {
            case "ru":
                return ruTextTake;
            case "en":
            case "us":
            case "as":
            case "ai":
                return enTextTake;
            default:
                return enTextTake;
        }
    }
    
    /// <summary>
    /// Получает текст "Поставить" для текущего языка
    /// </summary>
    private string GetPutText()
    {
        switch (currentLanguage.ToLower())
        {
            case "ru":
                return ruTextPut;
            case "en":
            case "us":
            case "as":
            case "ai":
                return enTextPut;
            default:
                return enTextPut;
        }
    }
}
