using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Компонент для отображения баланса игрока из GameStorage в TextMeshProUGUI
/// </summary>
public class BalanceCountUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("TextMeshProUGUI компонент для отображения баланса (если не назначен, будет найден автоматически)")]
    [SerializeField] private TextMeshProUGUI balanceText;
    
    [Header("Settings")]
    [Tooltip("Обновлять баланс каждый кадр (если false, обновляется только при изменении)")]
    [SerializeField] private bool updateEveryFrame = false;
    
    [Tooltip("Интервал обновления в секундах (если updateEveryFrame = false)")]
    [SerializeField] private float updateInterval = 0.05f; // Уменьшено для более частого обновления
    
    [Header("Animation Settings")]
    [Tooltip("Длительность анимации масштабирования в секундах")]
    [SerializeField] private float animationDuration = 0.3f;
    
    [Tooltip("Множитель масштаба при анимации (например, 1.2 = увеличение на 20%)")]
    [SerializeField] private float scaleMultiplier = 1.2f;
    
    [Header("Debug")]
    [SerializeField] private bool debug = false;
    
    private GameStorage gameStorage;
    private double lastBalance = -1;
    private float updateTimer = 0f;
    private string lastFormattedBalance = "";
    private bool isAnimating = false;
    private Vector3 originalScale = Vector3.one;
    
    private void Awake()
    {
        // Автоматически находим TextMeshProUGUI компонент, если не назначен
        if (balanceText == null)
        {
            balanceText = GetComponent<TextMeshProUGUI>();
            if (balanceText == null)
            {
                balanceText = GetComponentInChildren<TextMeshProUGUI>();
            }
            
            if (balanceText == null)
            {
                Debug.LogError($"[BalanceCountUI] TextMeshProUGUI компонент не найден на {gameObject.name}!");
            }
            else if (debug)
            {
                Debug.Log($"[BalanceCountUI] TextMeshProUGUI компонент найден на {gameObject.name}");
            }
        }
    }
    
    private void Start()
    {
        // Получаем ссылку на GameStorage
        gameStorage = GameStorage.Instance;
        
        if (gameStorage == null)
        {
            Debug.LogError("[BalanceCountUI] GameStorage.Instance не найден!");
            return;
        }
        
        // Сохраняем оригинальный масштаб текста
        if (balanceText != null)
        {
            originalScale = balanceText.transform.localScale;
        }
        
        // Обновляем баланс при старте
        UpdateBalance();
    }
    
    private void Update()
    {
        if (gameStorage == null || balanceText == null)
        {
            return;
        }
        
        if (updateEveryFrame)
        {
            // Обновляем каждый кадр
            UpdateBalance();
        }
        else
        {
            // Обновляем с интервалом (но проверка изменений происходит всегда)
            updateTimer += Time.deltaTime;
            if (updateTimer >= updateInterval)
            {
                updateTimer = 0f;
                UpdateBalance();
            }
        }
    }
    
    /// <summary>
    /// Обновляет текст баланса из GameStorage
    /// </summary>
    private void UpdateBalance()
    {
        if (gameStorage == null || balanceText == null)
        {
            return;
        }
        
        // Получаем текущий баланс
        double currentBalance = gameStorage.GetBalanceDouble();
        
        // Форматируем баланс через GameStorage
        string formattedBalance = gameStorage.FormatBalance(currentBalance);
        
        // Обновляем текст, если баланс или форматированная строка изменились
        // Используем сравнение форматированной строки для более надежной проверки
        if (formattedBalance != lastFormattedBalance || Mathf.Abs((float)(currentBalance - lastBalance)) > 0.0001f)
        {
            // Устанавливаем текст с " $" в конце
            balanceText.text = formattedBalance + " $";
            
            // Запускаем анимацию только если баланс увеличился (не уменьшился)
            bool balanceIncreased = currentBalance > lastBalance && lastBalance >= 0;
            
            if (debug)
            {
                Debug.Log($"[BalanceCountUI] Баланс обновлен: {formattedBalance} $ (raw: {currentBalance}, предыдущий: {lastBalance}, увеличился: {balanceIncreased})");
            }
            
            lastBalance = currentBalance;
            lastFormattedBalance = formattedBalance;
            
            // Запускаем анимацию только если она не запущена и баланс увеличился
            if (balanceIncreased && !isAnimating)
            {
                StartCoroutine(AnimateBalanceChange());
            }
        }
    }
    
    /// <summary>
    /// Принудительно обновить баланс (можно вызвать извне)
    /// </summary>
    public void RefreshBalance()
    {
        lastBalance = -1; // Сбрасываем, чтобы принудительно обновить
        lastFormattedBalance = ""; // Сбрасываем форматированную строку
        UpdateBalance();
    }
    
    private void OnEnable()
    {
        // Обновляем баланс при включении объекта
        if (gameStorage != null)
        {
            RefreshBalance();
        }
    }
    
    /// <summary>
    /// Анимация изменения баланса (увеличение и затем уменьшение масштаба)
    /// </summary>
    private IEnumerator AnimateBalanceChange()
    {
        if (balanceText == null) yield break;
        
        isAnimating = true;
        
        Transform textTransform = balanceText.transform;
        Vector3 startScale = originalScale;
        Vector3 targetScale = originalScale * scaleMultiplier;
        
        float halfDuration = animationDuration / 2f;
        float elapsedTime = 0f;
        
        // Фаза 1: Увеличение масштаба
        while (elapsedTime < halfDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / halfDuration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            
            textTransform.localScale = Vector3.Lerp(startScale, targetScale, smoothT);
            yield return null;
        }
        
        // Убеждаемся, что достигли целевого масштаба
        textTransform.localScale = targetScale;
        
        // Фаза 2: Уменьшение масштаба обратно
        elapsedTime = 0f;
        while (elapsedTime < halfDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / halfDuration;
            float smoothT = Mathf.SmoothStep(0f, 1f, t);
            
            textTransform.localScale = Vector3.Lerp(targetScale, startScale, smoothT);
            yield return null;
        }
        
        // Убеждаемся, что вернулись к исходному масштабу
        textTransform.localScale = startScale;
        
        isAnimating = false;
    }
}
