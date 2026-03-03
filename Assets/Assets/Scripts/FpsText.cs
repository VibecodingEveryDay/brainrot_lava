using TMPro;
using UnityEngine;

/// <summary>
/// Отображает FPS в TextMeshPro (TMP_Text) на этом объекте или в дочерних.
/// </summary>
public class FpsText : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Если не задано — будет найдено на этом объекте или в дочерних.")]
    [SerializeField] private TMP_Text targetText;

    [Header("Display")]
    [Tooltip("Обновлять текст не чаще, чем раз в N секунд.")]
    [SerializeField] private float updateInterval = 0.25f;
    [Tooltip("Показывать миллисекунды кадра вместе с FPS.")]
    [SerializeField] private bool showMs = false;
    [Tooltip("Количество знаков после запятой (для ms).")]
    [SerializeField] private int msDecimals = 1;

    [Header("Smoothing")]
    [Tooltip("Экспоненциальное сглаживание FPS. 0 = без сглаживания, 0.9 = сильное сглаживание.")]
    [Range(0f, 0.99f)]
    [SerializeField] private float smoothing = 0.9f;
    [Tooltip("Использовать unscaledDeltaTime (не зависит от Time.timeScale).")]
    [SerializeField] private bool useUnscaledTime = true;

    private float _smoothedFps;
    private float _timeSinceUpdate;

    private void Awake()
    {
        if (targetText == null)
        {
            targetText = GetComponent<TMP_Text>();
            if (targetText == null)
                targetText = GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void OnEnable()
    {
        _smoothedFps = 0f;
        _timeSinceUpdate = 0f;
        UpdateText(0f, 0f);
    }

    private void Update()
    {
        if (targetText == null) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f) return;

        float fps = 1f / dt;
        if (_smoothedFps <= 0f || smoothing <= 0f)
            _smoothedFps = fps;
        else
            _smoothedFps = (_smoothedFps * smoothing) + (fps * (1f - smoothing));

        _timeSinceUpdate += dt;
        if (updateInterval <= 0f || _timeSinceUpdate >= updateInterval)
        {
            _timeSinceUpdate = 0f;
            float ms = dt * 1000f;
            UpdateText(_smoothedFps, ms);
        }
    }

    private void UpdateText(float fps, float ms)
    {
        int fpsInt = Mathf.Max(0, Mathf.RoundToInt(fps));
        if (!showMs)
        {
            targetText.text = $"FPS: {fpsInt}";
            return;
        }

        int d = Mathf.Clamp(msDecimals, 0, 3);
        targetText.text = $"FPS: {fpsInt} ({ms.ToString($"F{d}")} ms)";
    }
}

