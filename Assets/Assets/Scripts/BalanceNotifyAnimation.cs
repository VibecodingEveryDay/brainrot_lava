using UnityEngine;
using TMPro;
using System.Collections;
using System;
using System.IO;
using System.Text;
using System.Globalization;

/// <summary>
/// Анимация текста для уведомления о пополнении баланса
/// Анимирует числа от начального значения до суммы пополнения
/// </summary>
public class BalanceNotifyAnimation : MonoBehaviour
{
    [Header("References")]
    [Tooltip("TextMeshProUGUI компонент для отображения суммы (если не назначен, будет найден автоматически)")]
    [SerializeField] private TextMeshProUGUI balanceCountText;
    
    [Tooltip("CanvasGroup для fade анимации (если не назначен, будет найден автоматически)")]
    [SerializeField] private CanvasGroup canvasGroup;
    
    [Header("Animation Settings")]
    [Tooltip("Длительность анимации числа в секундах")]
    [SerializeField] private float animationDuration = 1.5f;
    
    [Tooltip("Кривая анимации числа (EaseOut для плавного замедления в конце)")]
    [SerializeField] private AnimationCurve animationCurve = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f));
    
    [Tooltip("Начальное значение для анимации (0 = начинать с нуля)")]
    [SerializeField] private double startValue = 0.0;
    
    [Header("Fade Settings")]
    [Tooltip("Время до начала fade out (в секундах)")]
    [SerializeField] private float fadeOutDelay = 3f;
    
    [Tooltip("Длительность fade out анимации (в секундах)")]
    [SerializeField] private float fadeOutDuration = 0.5f;
    
    [Tooltip("Длительность fade in анимации (в секундах)")]
    [SerializeField] private float fadeInDuration = 0.3f;
    
    [Header("Pulse Settings")]
    [Tooltip("Включить анимацию пульсации во время earn дохода")]
    [SerializeField] private bool enablePulseAnimation = true;
    
    [Tooltip("Множитель масштаба при пульсации (например, 1.2 = увеличение на 20%)")]
    [SerializeField] private float pulseScale = 1.15f;
    
    [Tooltip("Скорость пульсации (циклов в секунду)")]
    [SerializeField] private float pulseSpeed = 2f;
    
    [Header("Format Settings")]
    [Tooltip("Использовать форматирование через GameStorage (добавляет K, M и т.д.)")]
    [SerializeField] private bool useGameStorageFormatting = true;
    
    [Tooltip("Добавлять символ валюты в конце (например, ' $')")]
    [SerializeField] private string currencySuffix = " $";
    
    [Header("Debug")]
    [Tooltip("Показывать отладочные сообщения")]
    [SerializeField] private bool debug = false;
    
    private GameStorage gameStorage;
    private Coroutine currentAnimation;
    private Coroutine fadeCoroutine;
    private Coroutine pulseCoroutine;
    private Coroutine fadeOutTimerCoroutine;
    private bool isAnimating = false;
    private Vector3 originalScale = Vector3.one;
    private RectTransform rectTransform;
    private float lastUpdateTime = 0f;
    private double currentDisplayedValue = 0.0; // Текущее отображаемое значение
    
    // #region agent log
    private void LogDebug(string hypothesisId, string location, string message, object data = null)
    {
        try
        {
            string logPath = @"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log";
            long timestamp = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
            
            // Простая JSON сериализация для Unity (без System.Text.Json)
            StringBuilder json = new StringBuilder();
            json.Append("{");
            json.Append($"\"sessionId\":\"debug-session\",");
            json.Append($"\"runId\":\"run1\",");
            json.Append($"\"hypothesisId\":\"{hypothesisId}\",");
            json.Append($"\"location\":\"{location}\",");
            json.Append($"\"message\":\"{EscapeJsonString(message)}\",");
            json.Append($"\"timestamp\":{timestamp},");
            json.Append("\"data\":{");
            
            // Сериализуем data объект (простая реализация для основных типов)
            if (data != null)
            {
                try
                {
                    var dataStr = SerializeData(data);
                    json.Append(dataStr);
                }
                catch (Exception e)
                {
                    // Если сериализация не удалась, просто записываем сообщение об ошибке
                    json.Append($"\"serializationError\":\"{EscapeJsonString(e.Message)}\"");
                }
            }
            
            json.Append("}");
            json.Append("}");
            
            File.AppendAllText(logPath, json.ToString() + "\n", Encoding.UTF8);
        }
        catch (Exception e)
        {
            // Если логирование не удалось, выводим в Unity консоль
            Debug.LogWarning($"[BalanceNotifyAnimation] LogDebug failed: {e.Message}");
        }
    }
    
    private string EscapeJsonString(string str)
    {
        if (string.IsNullOrEmpty(str)) return "";
        return str.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }
    
    private string SerializeData(object data)
    {
        if (data == null) return "";
        
        StringBuilder sb = new StringBuilder();
        var type = data.GetType();
        
        // Для анонимных типов используем свойства (они публичные)
        var properties = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        bool first = true;
        
        foreach (var prop in properties)
        {
            try
            {
                if (!first) sb.Append(",");
                var value = prop.GetValue(data);
                sb.Append($"\"{prop.Name}\":{SerializeValue(value)}");
                first = false;
            }
            catch
            {
                // Пропускаем свойства, которые не удалось прочитать
            }
        }
        
        // Если не нашли свойств, пытаемся через поля
        if (first)
        {
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            foreach (var field in fields)
            {
                try
                {
                    if (!first) sb.Append(",");
                    var value = field.GetValue(data);
                    sb.Append($"\"{field.Name}\":{SerializeValue(value)}");
                    first = false;
                }
                catch
                {
                    // Пропускаем поля, которые не удалось прочитать
                }
            }
        }
        
        return sb.ToString();
    }
    
    private string SerializeValue(object value)
    {
        if (value == null) return "null";
        
        if (value is string str)
        {
            return $"\"{EscapeJsonString(str)}\"";
        }
        else if (value is bool b)
        {
            return b ? "true" : "false";
        }
        else if (value is int || value is long || value is short || value is byte)
        {
            return value.ToString();
        }
        else if (value is float f)
        {
            return f.ToString("F6", CultureInfo.InvariantCulture);
        }
        else if (value is double d)
        {
            return d.ToString("F6", CultureInfo.InvariantCulture);
        }
        else if (value is decimal dec)
        {
            return dec.ToString("F6", CultureInfo.InvariantCulture);
        }
        else
        {
            return $"\"{EscapeJsonString(value.ToString())}\"";
        }
    }
    // #endregion
    
    private void Awake()
    {
        Debug.Log($"[BalanceNotifyAnimation] Awake() вызван на объекте: {gameObject.name}, активен: {gameObject.activeInHierarchy}, enabled: {enabled}");
        Initialize();
    }
    
    private void OnEnable()
    {
        // Переинициализируем при включении, на случай если объект был выключен
        if (balanceCountText == null)
        {
            Initialize();
        }
    }
    
    private void Initialize()
    {
        Debug.Log($"[BalanceNotifyAnimation] Initialize() вызван на объекте: {gameObject.name}");
        
        // Автоматически находим TextMeshProUGUI, если не назначен
        if (balanceCountText == null)
        {
            // ВАЖНО: Сначала ищем по имени "balanceCount" (это основной объект с TextMeshProUGUI)
            Transform balanceCountTransform = transform.Find("balanceCount");
            if (balanceCountTransform == null)
            {
                // Рекурсивный поиск
                balanceCountTransform = FindChildByName(transform, "balanceCount");
            }
            
            if (balanceCountTransform != null)
            {
                Debug.Log($"[BalanceNotifyAnimation] Найден объект 'balanceCount': {balanceCountTransform.name}");
                
                // Ищем TextMeshProUGUI НАПРЯМУЮ на объекте balanceCount (не в дочерних SubMeshUI)
                balanceCountText = balanceCountTransform.GetComponent<TextMeshProUGUI>();
                
                if (balanceCountText == null)
                {
                    Debug.LogWarning($"[BalanceNotifyAnimation] TextMeshProUGUI не найден на объекте 'balanceCount'! Проверьте, что компонент TextMeshProUGUI добавлен на объект balanceCount.");
                }
                else
                {
                    Debug.Log($"[BalanceNotifyAnimation] TextMeshProUGUI найден на объекте 'balanceCount': {balanceCountText.name}, текст: '{balanceCountText.text}'");
                }
            }
            
            // Если не найден по имени, ищем на текущем объекте
            if (balanceCountText == null)
            {
                balanceCountText = GetComponent<TextMeshProUGUI>();
                if (balanceCountText != null)
                {
                    Debug.Log($"[BalanceNotifyAnimation] TextMeshProUGUI найден на текущем объекте: {gameObject.name}");
                }
            }
            
            // Если все еще не найден, ищем в дочерних объектах (включая неактивные)
            // ВАЖНО: НО пропускаем SubMeshUI объекты (они не содержат TextMeshProUGUI)
            if (balanceCountText == null)
            {
                TextMeshProUGUI[] allTexts = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (TextMeshProUGUI text in allTexts)
                {
                    // Пропускаем SubMeshUI объекты (они обычно называются "TMP SubMeshUI")
                    if (text.name.Contains("SubMeshUI"))
                    {
                        Debug.Log($"[BalanceNotifyAnimation] Пропущен SubMeshUI объект: {text.name}");
                        continue;
                    }
                    
                    balanceCountText = text;
                    Debug.Log($"[BalanceNotifyAnimation] TextMeshProUGUI найден в дочерних объектах: {text.name}");
                    break;
                }
            }
        }
        
        if (balanceCountText == null)
        {
            Debug.LogError($"[BalanceNotifyAnimation] TextMeshProUGUI не найден на объекте {gameObject.name}! Убедитесь, что компонент находится на объекте с TextMeshProUGUI или его дочернем объекте 'balanceCount'.");
            Debug.LogError($"[BalanceNotifyAnimation] Иерархия объекта {gameObject.name}:");
            LogHierarchy(transform, 0);
        }
        else
        {
            Debug.Log($"[BalanceNotifyAnimation] TextMeshProUGUI успешно найден: объект='{balanceCountText.gameObject.name}', активен={balanceCountText.gameObject.activeInHierarchy}, enabled={balanceCountText.enabled}, текст='{balanceCountText.text}'");
            
            // ВАЖНО: Убеждаемся, что текст активен при инициализации
            if (!balanceCountText.gameObject.activeInHierarchy)
            {
                balanceCountText.gameObject.SetActive(true);
                Debug.Log("[BalanceNotifyAnimation] balanceCountText был неактивен при инициализации, активирован");
            }
            
            // ВАЖНО: Убеждаемся, что текст видим (не скрыт через CanvasGroup на дочернем объекте)
            CanvasGroup textCanvasGroup = balanceCountText.GetComponent<CanvasGroup>();
            if (textCanvasGroup == null)
            {
                textCanvasGroup = balanceCountText.GetComponentInParent<CanvasGroup>();
            }
            if (textCanvasGroup != null && textCanvasGroup.alpha < 0.1f)
            {
                textCanvasGroup.alpha = 1f;
                if (debug)
                {
                    Debug.Log("[BalanceNotifyAnimation] CanvasGroup на тексте был скрыт при инициализации, показан");
                }
            }
            
            if (debug)
            {
                Debug.Log($"[BalanceNotifyAnimation] TextMeshProUGUI найден: {balanceCountText.name}, активен: {balanceCountText.gameObject.activeInHierarchy}, текст: '{balanceCountText.text}'");
            }
        }
        
        // Автоматически находим CanvasGroup, если не назначен
        // ВАЖНО: CanvasGroup должен быть на родительском объекте (BalanceNotify), а не на balanceCount
        if (canvasGroup == null)
        {
            // Сначала ищем в родительском объекте
            canvasGroup = GetComponentInParent<CanvasGroup>();
            if (canvasGroup == null)
            {
                // Если не найден в родителе, ищем на текущем объекте
                canvasGroup = GetComponent<CanvasGroup>();
            }
            if (canvasGroup == null)
            {
                // Создаем CanvasGroup на текущем объекте, если его нет
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
                if (debug)
                {
                    Debug.Log("[BalanceNotifyAnimation] CanvasGroup создан автоматически на текущем объекте");
                }
            }
        }
        
        if (canvasGroup != null && debug)
        {
            Debug.Log($"[BalanceNotifyAnimation] CanvasGroup найден: {canvasGroup.gameObject.name}, alpha={canvasGroup.alpha}");
        }
        
        // Сохраняем оригинальный масштаб для пульсации
        rectTransform = GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            rectTransform = GetComponentInParent<RectTransform>();
        }
        if (rectTransform != null)
        {
            originalScale = rectTransform.localScale;
        }
        else
        {
            originalScale = transform.localScale;
        }
        
        // Находим GameStorage для форматирования
        gameStorage = GameStorage.Instance;
        
        if (gameStorage == null)
        {
            Debug.LogWarning("[BalanceNotifyAnimation] GameStorage не найден, форматирование будет упрощенным");
        }
        
        // Инициализируем кривую анимации, если она не настроена
        // Создаем EaseOut кривую (медленный старт, быстрое ускорение, плавное замедление в конце)
        if (animationCurve == null || animationCurve.keys.Length == 0)
        {
            animationCurve = new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),      // Начало: значение 0, тангенсы 0 (плавный старт)
                new Keyframe(1f, 1f, 0f, 0f)       // Конец: значение 1, тангенсы 0 (плавный конец)
            );
            // Делаем кривую более плавной (EaseOut эффект)
            animationCurve.preWrapMode = WrapMode.Clamp;
            animationCurve.postWrapMode = WrapMode.Clamp;
        }
        
        // ВАЖНО: НЕ скрываем уведомление при инициализации (может быть уже видимо)
        // Скрываем только если это первая инициализация и alpha еще не установлен
        if (canvasGroup != null)
        {
            // Устанавливаем alpha в 0 только если он еще не был установлен (первая инициализация)
            // Это позволяет сохранить видимость, если уведомление уже было показано
            if (canvasGroup.alpha == 0f && !gameObject.activeInHierarchy)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
                Debug.Log($"[BalanceNotifyAnimation] Инициализация: CanvasGroup найден, alpha установлен в 0 (первая инициализация)");
            }
            else
            {
                Debug.Log($"[BalanceNotifyAnimation] Инициализация: CanvasGroup найден, текущий alpha={canvasGroup.alpha} (сохраняем текущее состояние)");
            }
        }
        else
        {
            Debug.LogWarning("[BalanceNotifyAnimation] CanvasGroup не найден при инициализации!");
        }
    }
    
    /// <summary>
    /// Запускает анимацию от начального значения до целевой суммы
    /// </summary>
    /// <param name="targetAmount">Целевая сумма пополнения</param>
    public void AnimateToAmount(double targetAmount)
    {
        AnimateToAmount(startValue, targetAmount);
    }
    
    /// <summary>
    /// Запускает анимацию от указанного начального значения до целевой суммы
    /// </summary>
    /// <param name="fromAmount">Начальное значение</param>
    /// <param name="targetAmount">Целевая сумма пополнения</param>
    public void AnimateToAmount(double fromAmount, double targetAmount)
    {
        // #region agent log
        LogDebug("A,B,E", "AnimateToAmount:entry", "AnimateToAmount вызван", new { fromAmount, targetAmount, currentDisplayedValue, gameObjectName = gameObject.name, balanceCountTextNull = balanceCountText == null });
        // #endregion
        Debug.Log($"[BalanceNotifyAnimation] AnimateToAmount() вызван: fromAmount={fromAmount}, targetAmount={targetAmount}, объект: {gameObject.name}");
        
        // Переинициализируем, если текст не найден
        if (balanceCountText == null || canvasGroup == null)
        {
            Debug.LogWarning("[BalanceNotifyAnimation] balanceCountText или canvasGroup равен null, переинициализируем...");
            Initialize();
        }
        
        if (balanceCountText == null)
        {
            Debug.LogError($"[BalanceNotifyAnimation] TextMeshProUGUI не найден на объекте {gameObject.name}! Убедитесь, что компонент находится на объекте с TextMeshProUGUI или его дочернем объекте 'balanceCount'.");
            return;
        }
        
        Debug.Log($"[BalanceNotifyAnimation] balanceCountText найден: {balanceCountText.name}, активен: {balanceCountText.gameObject.activeInHierarchy}, объект: {balanceCountText.gameObject.name}, текст до установки: '{balanceCountText.text ?? "null"}'");
        
        // Убеждаемся, что объект активен
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[BalanceNotifyAnimation] GameObject неактивен! Активируйте объект перед запуском анимации.");
            return;
        }
        
        // ВАЖНО: Если сумма = 0, все равно показываем уведомление (но можно скрыть сразу)
        // Или показываем "0" для отладки
        if (targetAmount <= 0 && debug)
        {
            Debug.Log("[BalanceNotifyAnimation] Сумма = 0, но все равно обновляем уведомление для тестирования");
        }
        
        // Останавливаем текущие анимации
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
            isAnimating = false;
        }
        
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
        
        // Останавливаем таймер fade out, если он запущен
        if (fadeOutTimerCoroutine != null)
        {
            StopCoroutine(fadeOutTimerCoroutine);
            fadeOutTimerCoroutine = null;
        }
        
        // ВАЖНО: Показываем уведомление ВСЕГДА при вызове AnimateToAmount (даже если сумма = 0 для тестирования)
        // Это гарантирует, что уведомление будет видно
        if (canvasGroup != null)
        {
            // ВАЖНО: Убеждаемся, что GameObject активен ПЕРЕД установкой alpha
            if (!gameObject.activeInHierarchy)
            {
                gameObject.SetActive(true);
                Debug.Log("[BalanceNotifyAnimation] GameObject был неактивен, активирован");
            }
            
            // ВАЖНО: Убеждаемся, что текст активен ПЕРЕД установкой alpha
            if (balanceCountText != null && !balanceCountText.gameObject.activeInHierarchy)
            {
                balanceCountText.gameObject.SetActive(true);
                Debug.Log("[BalanceNotifyAnimation] balanceCountText был неактивен, активирован");
            }
            
            // ВАЖНО: Всегда запускаем fade in, даже если уже видим (для обновления)
            // Это гарантирует, что уведомление будет видно
            if (canvasGroup.alpha < 0.9f)
            {
                Debug.Log($"[BalanceNotifyAnimation] Запускаем FadeIn: currentAlpha={canvasGroup.alpha}, targetAmount={targetAmount}");
                fadeCoroutine = StartCoroutine(FadeInCoroutine());
            }
            else
            {
                // Если уже видим, просто обновляем alpha до 1
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                Debug.Log($"[BalanceNotifyAnimation] Уже видим, устанавливаем alpha=1, targetAmount={targetAmount}");
            }
            
            // ВАЖНО: Принудительно устанавливаем alpha = 1, если targetAmount > 0
            // Это гарантирует, что уведомление будет видно сразу
            if (targetAmount > 0 && canvasGroup.alpha < 0.5f)
            {
                Debug.LogWarning($"[BalanceNotifyAnimation] CanvasGroup.alpha слишком низкий ({canvasGroup.alpha}), принудительно устанавливаем в 1.0");
                canvasGroup.alpha = 1f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
            
            Debug.Log($"[BalanceNotifyAnimation] Показываем уведомление: targetAmount={targetAmount}, currentAlpha={canvasGroup.alpha}, textActive={balanceCountText?.gameObject.activeInHierarchy ?? false}, textFound={balanceCountText != null}, textEnabled={balanceCountText?.enabled ?? false}, text='{balanceCountText?.text ?? "null"}'");
        }
        else
        {
            Debug.LogError("[BalanceNotifyAnimation] CanvasGroup равен null! Уведомление не будет показано.");
        }
        
        // Запускаем анимацию пульсации
        if (enablePulseAnimation)
        {
            if (pulseCoroutine != null)
            {
                StopCoroutine(pulseCoroutine);
            }
            pulseCoroutine = StartCoroutine(PulseCoroutine());
        }
        
        // ВАЖНО: Убеждаемся, что компоненты инициализированы
        if (balanceCountText == null || canvasGroup == null)
        {
            Initialize();
        }
        
        // ВАЖНО: Убеждаемся, что текст активен перед установкой значения
        if (balanceCountText != null && !balanceCountText.gameObject.activeInHierarchy)
        {
            balanceCountText.gameObject.SetActive(true);
            if (debug)
            {
                Debug.Log("[BalanceNotifyAnimation] balanceCountText активирован перед установкой текста");
            }
        }
        
        // ВАЖНО: Определяем начальное значение для анимации
        double actualFromAmount = fromAmount;
        
        // Если fromAmount = 0, определяем начальное значение в зависимости от того, увеличивается или уменьшается баланс
        if (fromAmount == 0.0)
        {
            // Если баланс уменьшается (targetAmount < currentDisplayedValue), начинаем с 0
            if (targetAmount < currentDisplayedValue)
            {
                actualFromAmount = 0.0;
                Debug.Log($"[BalanceNotifyAnimation] Баланс уменьшается ({targetAmount} < {currentDisplayedValue}), начинаем анимацию с 0");
            }
            // Если баланс увеличивается и есть текущее отображаемое значение, используем его
            else if (currentDisplayedValue > 0.0)
            {
                actualFromAmount = currentDisplayedValue;
                Debug.Log($"[BalanceNotifyAnimation] Баланс увеличивается, используем текущее отображаемое значение как начальное: {actualFromAmount} (вместо {fromAmount})");
            }
        }
        
        Debug.Log($"[BalanceNotifyAnimation] actualFromAmount={actualFromAmount}, targetAmount={targetAmount}, разница={Math.Abs(actualFromAmount - targetAmount)}");
        
        // ВАЖНО: Устанавливаем текст СРАЗУ, чтобы он был виден
        // Сначала устанавливаем в actualFromAmount, затем анимируем до targetAmount
        if (balanceCountText != null)
        {
            Debug.Log($"[BalanceNotifyAnimation] Вызываем UpdateText с actualFromAmount={actualFromAmount}, balanceCountText null: {balanceCountText == null}, balanceCountText активен: {balanceCountText?.gameObject.activeInHierarchy ?? false}, balanceCountText объект: {balanceCountText?.gameObject.name ?? "null"}");
            UpdateText(actualFromAmount);
            currentDisplayedValue = actualFromAmount;
            
            // ВАЖНО: Проверяем, что текст действительно установился
            string textAfterUpdate = balanceCountText?.text ?? "";
            Debug.Log($"[BalanceNotifyAnimation] После UpdateText: textAfterUpdate='{textAfterUpdate}', balanceCountText null: {balanceCountText == null}, balanceCountText.text='{balanceCountText?.text ?? "null"}'");
            if (string.IsNullOrEmpty(textAfterUpdate))
            {
                // При actualFromAmount = 0 оставляем пустой текст (не "+ 0$")
                if (actualFromAmount <= 0.0)
                {
                    balanceCountText.SetText("");
                }
                else
                {
                    Debug.LogError($"[BalanceNotifyAnimation] Текст пустой после UpdateText! actualFromAmount={actualFromAmount}, targetAmount={targetAmount}. Пытаемся установить еще раз.");
                    string formattedText = FormatBalanceWithMaxDigits(actualFromAmount);
                    if (string.IsNullOrEmpty(formattedText))
                    {
                        formattedText = "0";
                    }
                    formattedText = "+ " + formattedText;
                    if (!string.IsNullOrEmpty(currencySuffix))
                    {
                        formattedText += currencySuffix;
                    }
                    balanceCountText.SetText(formattedText);
                }
                balanceCountText.ForceMeshUpdate();
                textAfterUpdate = balanceCountText.text ?? "";
                Debug.Log($"[BalanceNotifyAnimation] После принудительной установки текста: '{textAfterUpdate}'");
            }
            
            Debug.Log($"[BalanceNotifyAnimation] Текст установлен сразу в actualFromAmount: {actualFromAmount}, текст: '{textAfterUpdate}', текст пустой: {string.IsNullOrEmpty(textAfterUpdate)}");
        }
        else
        {
            Debug.LogError("[BalanceNotifyAnimation] balanceCountText равен null перед установкой текста!");
            return; // Не продолжаем, если текст не найден
        }
        
        // ВАЖНО: Всегда запускаем анимацию, если targetAmount > 0
        // Это гарантирует, что уведомление будет анимироваться при любом пополнении баланса
        if (targetAmount > 0)
        {
            // Запускаем новую анимацию числа (она будет плавно переходить от actualFromAmount к targetAmount)
            if (currentAnimation != null)
            {
                StopCoroutine(currentAnimation);
            }
            currentAnimation = StartCoroutine(AnimateValueCoroutine(actualFromAmount, targetAmount));
            // #region agent log
            LogDebug("A", "AnimateToAmount:startAnimation", "Анимация запущена", new { actualFromAmount, targetAmount, currentAnimationNull = currentAnimation == null, animationDuration });
            // #endregion
            Debug.Log($"[BalanceNotifyAnimation] Анимация запущена: actualFromAmount={actualFromAmount}, targetAmount={targetAmount}, currentAnimation null: {currentAnimation == null}, разница: {Math.Abs(actualFromAmount - targetAmount)}");
        }
        else
        {
            // Если targetAmount = 0, просто устанавливаем финальное значение без анимации
            // #region agent log
            LogDebug("A", "AnimateToAmount:skipAnimation", "Анимация пропущена - targetAmount = 0", new { actualFromAmount, targetAmount, difference = Math.Abs(actualFromAmount - targetAmount) });
            // #endregion
            UpdateText(targetAmount);
            currentDisplayedValue = targetAmount;
            Debug.Log($"[BalanceNotifyAnimation] targetAmount = 0, анимация не нужна, устанавливаем финальное значение");
        }
        
        // Обновляем время последнего обновления
        lastUpdateTime = Time.time;
        
        if (debug)
        {
            Debug.Log($"[BalanceNotifyAnimation] Анимация запущена: {fromAmount} -> {targetAmount}, объект активен: {gameObject.activeInHierarchy}, текст найден: {balanceCountText != null}, текст активен: {balanceCountText?.gameObject.activeInHierarchy ?? false}, текст: '{balanceCountText?.text ?? "null"}', canvasGroup.alpha: {canvasGroup?.alpha ?? 0f}");
        }
    }
    
    /// <summary>
    /// Корутина для анимации значения
    /// </summary>
    private IEnumerator AnimateValueCoroutine(double fromValue, double toValue)
    {
        isAnimating = true;
        
        // #region agent log
        LogDebug("A,F", "AnimateValueCoroutine:entry", "AnimateValueCoroutine начат", new { fromValue, toValue, animationDuration, balanceCountTextNull = balanceCountText == null, isAnimating });
        // #endregion
        Debug.Log($"[BalanceNotifyAnimation] AnimateValueCoroutine начат: fromValue={fromValue}, toValue={toValue}, длительность: {animationDuration}с, balanceCountText null: {balanceCountText == null}");
        
        // ВАЖНО: Убеждаемся, что текст найден перед началом анимации
        if (balanceCountText == null)
        {
            Initialize();
            if (balanceCountText == null)
            {
                Debug.LogError("[BalanceNotifyAnimation] balanceCountText равен null в AnimateValueCoroutine, анимация не может быть запущена!");
                isAnimating = false;
                currentAnimation = null;
                yield break;
            }
        }
        
        // Устанавливаем начальное значение сразу
        UpdateText(fromValue);
        Debug.Log($"[BalanceNotifyAnimation] AnimateValueCoroutine: Начальное значение установлено: {fromValue}, текст: '{balanceCountText?.text ?? "null"}'");
        
        // ВАЖНО: Проверяем, что длительность анимации > 0
        if (animationDuration <= 0f)
        {
            Debug.LogWarning($"[BalanceNotifyAnimation] animationDuration = {animationDuration}, устанавливаем значение по умолчанию 1.0");
            animationDuration = 1.0f;
        }
        
        float elapsedTime = 0f;
        double valueDifference = toValue - fromValue;
        
        Debug.Log($"[BalanceNotifyAnimation] AnimateValueCoroutine: Начинаем цикл анимации, длительность: {animationDuration}с, разница: {valueDifference}, fromValue={fromValue}, toValue={toValue}, balanceCountText null: {balanceCountText == null}, balanceCountText активен: {balanceCountText?.gameObject.activeInHierarchy ?? false}");
        
        int frameCount = 0;
        while (elapsedTime < animationDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / animationDuration);
            
            // Применяем кривую анимации
            float curveValue = animationCurve.Evaluate(normalizedTime);
            
            // Вычисляем текущее значение
            double currentValue = fromValue + (valueDifference * curveValue);
            
            // Обновляем текст и текущее отображаемое значение
            if (balanceCountText != null && balanceCountText.gameObject.activeInHierarchy)
            {
                // ВАЖНО: Сохраняем старое значение текста для проверки
                string oldText = balanceCountText.text;
                
                // Обновляем текст
                UpdateText(currentValue);
                currentDisplayedValue = currentValue; // Обновляем текущее отображаемое значение во время анимации
                
                // ВАЖНО: Проверяем, что текст действительно обновился
                string newText = balanceCountText.text;
                if (oldText == newText && frameCount > 0 && Math.Abs(currentValue - fromValue) > 0.01)
                {
                    // Текст не изменился, но значение изменилось - принудительно обновляем
                    Debug.LogWarning($"[BalanceNotifyAnimation] Текст не изменился во время анимации! oldText='{oldText}', newText='{newText}', currentValue={currentValue}. Принудительно обновляем через SetText и ForceMeshUpdate.");
                    string formattedText = FormatBalanceWithMaxDigits(currentValue);
                    formattedText = "+ " + formattedText;
                    if (!string.IsNullOrEmpty(currencySuffix))
                    {
                        formattedText += currencySuffix;
                    }
                    balanceCountText.SetText(formattedText);
                    balanceCountText.ForceMeshUpdate();
                    Debug.Log($"[BalanceNotifyAnimation] После принудительного SetText и ForceMeshUpdate: '{balanceCountText.text}'");
                }
            }
            else
            {
                Debug.LogError($"[BalanceNotifyAnimation] balanceCountText равен null или неактивен во время анимации! balanceCountText null: {balanceCountText == null}, activeInHierarchy: {balanceCountText?.gameObject.activeInHierarchy ?? false}");
                isAnimating = false;
                currentAnimation = null;
                yield break;
            }
            
            frameCount++;
            // Логируем каждые 30 кадров (примерно раз в секунду при 30 FPS)
            if (frameCount % 30 == 0)
            {
                Debug.Log($"[BalanceNotifyAnimation] Анимация: elapsedTime={elapsedTime:F2}с, normalizedTime={normalizedTime:F2}, currentValue={currentValue:F2}, текст: '{balanceCountText?.text ?? "null"}'");
            }
            
            yield return null;
        }
        
        // #region agent log
        LogDebug("A,F", "AnimateValueCoroutine:exit", "AnimateValueCoroutine завершен", new { fromValue, toValue, elapsedTime, finalText = balanceCountText?.text ?? "null" });
        // #endregion
        
        // Убеждаемся, что финальное значение установлено точно
        UpdateText(toValue);
        currentDisplayedValue = toValue; // Обновляем текущее отображаемое значение
        
        Debug.Log($"[BalanceNotifyAnimation] Анимация завершена: fromValue={fromValue}, toValue={toValue}, финальный текст: '{balanceCountText?.text ?? "null"}', currentDisplayedValue: {currentDisplayedValue}, elapsedTime: {elapsedTime:F2}с");
        
        isAnimating = false;
        currentAnimation = null;
        
        // Останавливаем пульсацию после завершения анимации числа
        if (pulseCoroutine != null)
        {
            StopCoroutine(pulseCoroutine);
            pulseCoroutine = null;
            
            // Возвращаем масштаб к оригинальному
            if (rectTransform != null)
            {
                rectTransform.localScale = originalScale;
            }
            else
            {
                transform.localScale = originalScale;
            }
        }
        
        // Обновляем время последнего обновления
        lastUpdateTime = Time.time;
        
        // Запускаем таймер для fade out (через 3 секунды, если не было нового обновления)
        if (fadeOutTimerCoroutine != null)
        {
            StopCoroutine(fadeOutTimerCoroutine);
        }
        fadeOutTimerCoroutine = StartCoroutine(FadeOutTimerCoroutine());
        
        if (debug)
        {
            Debug.Log($"[BalanceNotifyAnimation] Анимация завершена, финальное значение: {toValue}");
        }
    }
    
    /// <summary>
    /// Корутина для таймера fade out (ждет 3 секунды и проверяет, не было ли нового обновления)
    /// </summary>
    private IEnumerator FadeOutTimerCoroutine()
    {
        yield return new WaitForSeconds(fadeOutDelay);
        
        // Проверяем, не было ли нового обновления за это время
        // Если прошло больше fadeOutDelay секунд с последнего обновления, скрываем уведомление
        if (Time.time - lastUpdateTime >= fadeOutDelay)
        {
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            fadeCoroutine = StartCoroutine(FadeOutCoroutine());
        }
        
        fadeOutTimerCoroutine = null;
    }
    
    /// <summary>
    /// Корутина для fade in анимации
    /// </summary>
    private IEnumerator FadeInCoroutine()
    {
        if (canvasGroup == null)
        {
            if (debug)
            {
                Debug.LogWarning("[BalanceNotifyAnimation] CanvasGroup равен null в FadeInCoroutine!");
            }
            yield break;
        }
        
        // ВАЖНО: Убеждаемся, что объект активен
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
            if (debug)
            {
                Debug.Log("[BalanceNotifyAnimation] GameObject был неактивен в FadeInCoroutine, активирован");
            }
        }
        
        // ВАЖНО: Убеждаемся, что текст активен
        if (balanceCountText != null && !balanceCountText.gameObject.activeInHierarchy)
        {
            balanceCountText.gameObject.SetActive(true);
            Debug.Log("[BalanceNotifyAnimation] balanceCountText был неактивен в FadeInCoroutine, активирован");
        }
        
        // ВАЖНО: Убеждаемся, что текст виден (не прозрачный)
        if (balanceCountText != null)
        {
            Color textColor = balanceCountText.color;
            if (textColor.a < 0.9f)
            {
                textColor.a = 1f;
                balanceCountText.color = textColor;
                Debug.Log($"[BalanceNotifyAnimation] Цвет текста был прозрачным (alpha={textColor.a}), установлен в непрозрачный");
            }
        }
        
        float elapsedTime = 0f;
        float startAlpha = canvasGroup.alpha;
        
        Debug.Log($"[BalanceNotifyAnimation] FadeIn начат: startAlpha={startAlpha}, targetAlpha=1.0, duration={fadeInDuration}, gameObject.activeInHierarchy={gameObject.activeInHierarchy}, text='{balanceCountText?.text ?? "null"}'");
        
        while (elapsedTime < fadeInDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / fadeInDuration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, normalizedTime);
            yield return null;
        }
        
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        
        // ВАЖНО: Принудительно обновляем текст после fade in
        if (balanceCountText != null)
        {
            balanceCountText.ForceMeshUpdate();
            Debug.Log($"[BalanceNotifyAnimation] FadeIn завершен: alpha={canvasGroup.alpha}, text='{balanceCountText.text}', textEmpty={string.IsNullOrEmpty(balanceCountText.text)}");
        }
        else
        {
            Debug.LogWarning("[BalanceNotifyAnimation] FadeIn завершен, но balanceCountText равен null!");
        }
        
        if (debug)
        {
            Debug.Log($"[BalanceNotifyAnimation] FadeIn завершен: alpha={canvasGroup.alpha}, text={(balanceCountText != null ? balanceCountText.text : "null")}");
        }
    }
    
    /// <summary>
    /// Корутина для fade out анимации
    /// </summary>
    private IEnumerator FadeOutCoroutine()
    {
        if (canvasGroup == null) yield break;
        
        float elapsedTime = 0f;
        float startAlpha = canvasGroup.alpha;
        
        while (elapsedTime < fadeOutDuration)
        {
            elapsedTime += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / fadeOutDuration);
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, normalizedTime);
            yield return null;
        }
        
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
    
    /// <summary>
    /// Корутина для анимации пульсации
    /// </summary>
    private IEnumerator PulseCoroutine()
    {
        Transform targetTransform = rectTransform != null ? rectTransform : transform;
        
        while (true)
        {
            float time = Time.time * pulseSpeed;
            float scale = 1f + (Mathf.Sin(time * Mathf.PI * 2f) * 0.5f + 0.5f) * (pulseScale - 1f);
            
            if (rectTransform != null)
            {
                rectTransform.localScale = originalScale * scale;
            }
            else
            {
                transform.localScale = originalScale * scale;
            }
            
            yield return null;
        }
    }
    
    /// <summary>
    /// Обновляет текст с форматированием (максимум 3 цифры)
    /// </summary>
    private void UpdateText(double value)
    {
        
        // ВАЖНО: Переинициализируем, если текст не найден
        if (balanceCountText == null)
        {
            Initialize();
            if (balanceCountText == null)
            {
                // #region agent log
                LogDebug("C", "UpdateText:nullAfterInit", "balanceCountText null после инициализации", new { value });
                // #endregion
                if (debug)
                {
                    Debug.LogError("[BalanceNotifyAnimation] balanceCountText равен null в UpdateText после инициализации! Проверьте, что TextMeshProUGUI находится на объекте или его дочернем объекте.");
                }
                return;
            }
        }
        
        // ВАЖНО: Убеждаемся, что текст активен
        if (!balanceCountText.gameObject.activeInHierarchy)
        {
            balanceCountText.gameObject.SetActive(true);
            if (debug)
            {
                Debug.Log("[BalanceNotifyAnimation] balanceCountText был неактивен в UpdateText, активирован");
            }
        }
        
        // Когда пополнение 0 — показываем пустой текст, а не "+ 0$"
        if (value <= 0.0)
        {
            balanceCountText.SetText("");
            balanceCountText.ForceMeshUpdate();
            currentDisplayedValue = 0.0;
            return;
        }
        
        string formattedText;
        
        if (useGameStorageFormatting && gameStorage != null)
        {
            // Используем форматирование через GameStorage, но с ограничением до 3 цифр
            formattedText = FormatBalanceWithMaxDigits(value);
        }
        else
        {
            // Простое форматирование с ограничением до 3 цифр
            formattedText = FormatBalanceWithMaxDigits(value);
        }
        
        // ВАЖНО: Проверяем, что форматирование не вернуло пустую строку
        if (string.IsNullOrEmpty(formattedText))
        {
            formattedText = "0";
            Debug.LogWarning($"[BalanceNotifyAnimation] FormatBalanceWithMaxDigits вернул пустую строку для value={value}, используем '0'");
        }
        
        // Добавляем "+ " в начало и суффикс валюты
        formattedText = "+ " + formattedText;
        if (!string.IsNullOrEmpty(currencySuffix))
        {
            formattedText += currencySuffix;
        }
        
        // ВАЖНО: Устанавливаем текст через SetText для TextMeshProUGUI
        if (balanceCountText != null)
        {
            string oldText = balanceCountText.text ?? "";
            
            // ВАЖНО: Проверяем, что formattedText не пустой перед установкой
            if (string.IsNullOrEmpty(formattedText))
            {
                Debug.LogError($"[BalanceNotifyAnimation] formattedText пустой! value={value}, formattedText='{formattedText}'. Используем значение по умолчанию.");
                formattedText = "+ 0";
                if (!string.IsNullOrEmpty(currencySuffix))
                {
                    formattedText += currencySuffix;
                }
            }
            
            // ВАЖНО: Убеждаемся, что balanceCount активен перед установкой текста
            if (!balanceCountText.gameObject.activeInHierarchy)
            {
                balanceCountText.gameObject.SetActive(true);
                Debug.Log($"[BalanceNotifyAnimation] balanceCount был неактивен перед SetText, активирован");
            }
            
            // ВАЖНО: Для TextMeshProUGUI используем SetText вместо .text =
            // Это гарантирует правильное обновление текста
            Debug.Log($"[BalanceNotifyAnimation] Устанавливаем текст в balanceCount: '{formattedText}', объект: {balanceCountText.gameObject.name}, активен: {balanceCountText.gameObject.activeInHierarchy}");
            balanceCountText.SetText(formattedText);
            
            // ВАЖНО: Принудительно обновляем меш текста
            balanceCountText.ForceMeshUpdate();
            
            // ВАЖНО: Проверяем, что текст установился
            string textAfterSet = balanceCountText.text ?? "";
            Debug.Log($"[BalanceNotifyAnimation] После SetText: balanceCountText.text='{textAfterSet}', formattedText='{formattedText}', совпадают: {textAfterSet == formattedText}");
            
            // ВАЖНО: Принудительно обновляем текст, если он не изменился
            // Это нужно для случаев, когда форматирование дает одинаковый результат
            string newText = balanceCountText.text ?? "";
            if (newText == oldText && value > 0 && Math.Abs(value - currentDisplayedValue) > 0.01)
            {
                // Текст не изменился, но значение изменилось - принудительно обновляем через ForceMeshUpdate
                balanceCountText.ForceMeshUpdate();
                if (debug)
                {
                    Debug.LogWarning($"[BalanceNotifyAnimation] Текст не изменился после SetText, вызываем ForceMeshUpdate. value={value}, oldText='{oldText}', newText='{balanceCountText.text}'");
                }
            }
            
            // ВАЖНО: Проверяем, что текст установился
            newText = balanceCountText.text ?? "";
            if (string.IsNullOrEmpty(newText))
            {
                Debug.LogError($"[BalanceNotifyAnimation] Текст пустой после SetText! formattedText='{formattedText}', value={value}. Пытаемся установить еще раз.");
                balanceCountText.SetText(formattedText);
                balanceCountText.ForceMeshUpdate();
                newText = balanceCountText.text ?? "";
            }
            
            // Логируем каждое обновление текста для отладки
            if (oldText != newText || value > 0 || debug)
            {
                Debug.Log($"[BalanceNotifyAnimation] UpdateText: value={value}, formattedText='{formattedText}', oldText='{oldText}', newText='{newText}', textChanged={oldText != newText}, textEmpty={string.IsNullOrEmpty(newText)}");
            }
            
            // ВАЖНО: Если текст все еще не установился, принудительно устанавливаем еще раз
            if (balanceCountText.text != formattedText && !string.IsNullOrEmpty(formattedText))
            {
                // #region agent log
                LogDebug("C", "UpdateText:textNotSet", "Текст не установился, пытаемся SetText еще раз", new { expected = formattedText, actual = balanceCountText.text });
                // #endregion
                Debug.LogWarning($"[BalanceNotifyAnimation] Текст не установился! Пытаемся установить еще раз. Ожидалось: '{formattedText}', получено: '{balanceCountText.text}'");
                balanceCountText.SetText(formattedText);
                balanceCountText.ForceMeshUpdate();
                Debug.Log($"[BalanceNotifyAnimation] После повторного SetText и ForceMeshUpdate: '{balanceCountText.text}'");
            }
        }
        else
        {
            // #region agent log
            LogDebug("C", "UpdateText:null", "balanceCountText null в UpdateText", new { value });
            // #endregion
            Debug.LogError("[BalanceNotifyAnimation] UpdateText: balanceCountText равен null! Текст не может быть установлен.");
        }
        
        // ВАЖНО: Убеждаемся, что текст виден (проверяем CanvasGroup на тексте)
        CanvasGroup textCanvasGroup = balanceCountText.GetComponent<CanvasGroup>();
        if (textCanvasGroup == null)
        {
            textCanvasGroup = balanceCountText.GetComponentInParent<CanvasGroup>();
        }
        if (textCanvasGroup != null && textCanvasGroup.alpha < 0.1f)
        {
            textCanvasGroup.alpha = 1f;
            if (debug)
            {
                Debug.Log("[BalanceNotifyAnimation] CanvasGroup на тексте был скрыт, показан");
            }
        }
        
        // ВАЖНО: Убеждаемся, что компонент TextMeshProUGUI включен
        if (!balanceCountText.enabled)
        {
            balanceCountText.enabled = true;
            if (debug)
            {
                Debug.Log("[BalanceNotifyAnimation] TextMeshProUGUI был выключен, включен");
            }
        }
        
        // ВАЖНО: Убеждаемся, что цвет текста не прозрачный
        if (balanceCountText.color.a < 0.1f)
        {
            Color textColor = balanceCountText.color;
            textColor.a = 1f;
            balanceCountText.color = textColor;
            if (debug)
            {
                Debug.Log("[BalanceNotifyAnimation] Цвет текста был прозрачным, установлен в непрозрачный");
            }
        }
        
        if (debug)
        {
            Debug.Log($"[BalanceNotifyAnimation] Текст обновлен: '{formattedText}' (value={value}), текст активен: {balanceCountText.gameObject.activeInHierarchy}, текст видим: {balanceCountText.enabled}, цвет alpha: {balanceCountText.color.a}");
        }
    }
    
    /// <summary>
    /// Форматирует баланс с максимум 3 цифрами (например, 1.24M, 103K, 500)
    /// </summary>
    private string FormatBalanceWithMaxDigits(double value)
    {
        string result;
        if (value >= 1000000000) // >= 1B
        {
            double billions = value / 1000000000.0;
            // Ограничиваем до 3 цифр: 1.24B, 10.5B, 999B
            if (billions >= 100)
            {
                result = $"{(int)billions}B";
            }
            else if (billions >= 10)
            {
                result = $"{billions:F1}B";
            }
            else
            {
                result = $"{billions:F2}B";
            }
        }
        else if (value >= 1000000) // >= 1M
        {
            double millions = value / 1000000.0;
            // Ограничиваем до 3 цифр: 1.24M, 10.5M, 999M
            if (millions >= 100)
            {
                result = $"{(int)millions}M";
            }
            else if (millions >= 10)
            {
                result = $"{millions:F1}M";
            }
            else
            {
                result = $"{millions:F2}M";
            }
        }
        else if (value >= 1000) // >= 1K
        {
            double thousands = value / 1000.0;
            // Ограничиваем до 3 цифр: 1.24K, 10.5K, 999K
            if (thousands >= 100)
            {
                result = $"{(int)thousands}K";
            }
            else if (thousands >= 10)
            {
                result = $"{thousands:F1}K";
            }
            else
            {
                result = $"{thousands:F2}K";
            }
        }
        else
        {
            // Меньше 1000 - просто целое число
            result = ((int)value).ToString();
        }
        
        return result;
    }
    
    /// <summary>
    /// Останавливает текущую анимацию и устанавливает финальное значение
    /// </summary>
    public void StopAnimation(double finalValue)
    {
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
            currentAnimation = null;
        }
        
        isAnimating = false;
        UpdateText(finalValue);
    }
    
    /// <summary>
    /// Проверяет, идет ли анимация
    /// </summary>
    public bool IsAnimating()
    {
        return isAnimating;
    }
    
    /// <summary>
    /// Устанавливает начальное значение для анимации
    /// </summary>
    public void SetStartValue(double value)
    {
        startValue = value;
    }
    
    /// <summary>
    /// Тестовый метод для проверки работы уведомления (можно вызвать из инспектора)
    /// </summary>
    [ContextMenu("Test Animation")]
    public void TestAnimation()
    {
        Debug.Log("[BalanceNotifyAnimation] Тестовая анимация запущена: 0 -> 1000");
        AnimateToAmount(0.0, 1000.0);
    }
    
    /// <summary>
    /// Тестовый метод для проверки установки текста (можно вызвать из инспектора)
    /// </summary>
    [ContextMenu("Test Set Text")]
    public void TestSetText()
    {
        if (balanceCountText == null)
        {
            Initialize();
        }
        
        if (balanceCountText != null)
        {
            UpdateText(1234.56);
            Debug.Log($"[BalanceNotifyAnimation] Тестовый текст установлен: '{balanceCountText.text}'");
        }
        else
        {
            Debug.LogError("[BalanceNotifyAnimation] balanceCountText равен null! Не могу установить тестовый текст.");
        }
    }
    
    /// <summary>
    /// Принудительно показывает уведомление с указанной суммой (для тестирования)
    /// </summary>
    /// <summary>
    /// Принудительно показывает уведомление с указанной суммой (для тестирования)
    /// </summary>
    public void ForceShow(double amount)
    {
        // Переинициализируем, если нужно
        if (balanceCountText == null || canvasGroup == null)
        {
            Initialize();
        }
        
        // Убеждаемся, что объект активен
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
        }
        
        // Убеждаемся, что текст активен
        if (balanceCountText != null && !balanceCountText.gameObject.activeInHierarchy)
        {
            balanceCountText.gameObject.SetActive(true);
        }
        
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }
        
        UpdateText(amount);
        
        Debug.Log($"[BalanceNotifyAnimation] Уведомление принудительно показано: {amount}, текст: '{balanceCountText?.text ?? "null"}', alpha: {canvasGroup?.alpha ?? 0f}");
    }
    
    /// <summary>
    /// Рекурсивно ищет дочерний объект по имени
    /// </summary>
    private Transform FindChildByName(Transform parent, string name)
    {
        if (parent.name == name)
        {
            return parent;
        }
        
        foreach (Transform child in parent)
        {
            Transform found = FindChildByName(child, name);
            if (found != null)
            {
                return found;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Логирует иерархию объектов для отладки
    /// </summary>
    private void LogHierarchy(Transform parent, int depth)
    {
        string indent = new string(' ', depth * 2);
        string components = "";
        
        // Проверяем компоненты
        if (parent.GetComponent<TextMeshProUGUI>() != null)
        {
            components += " [TextMeshProUGUI]";
        }
        if (parent.GetComponent<CanvasGroup>() != null)
        {
            components += " [CanvasGroup]";
        }
        
        Debug.Log($"{indent}- {parent.name} (активен: {parent.gameObject.activeInHierarchy}){components}");
        
        foreach (Transform child in parent)
        {
            LogHierarchy(child, depth + 1);
        }
    }
    
    /// <summary>
    /// Принудительно показывает текст для тестирования (вызывается из Unity Inspector или через код)
    /// </summary>
    [ContextMenu("Test Show Text")]
    public void TestShowText()
    {
        Initialize();
        ForceShow(12345.67);
        Debug.Log("[BalanceNotifyAnimation] Тестовое уведомление показано. Проверьте, виден ли текст на экране.");
    }
}
