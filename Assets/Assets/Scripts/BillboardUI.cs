using UnityEngine;
using TMPro;

/// <summary>
/// Компонент для поворота UI к камере (Billboard эффект).
/// Обеспечивает, что UI всегда смотрит в сторону камеры игрока.
/// Центр вращения по горизонтали зависит от alignment текста (или от явно заданного PivotSource).
/// </summary>
[RequireComponent(typeof(Transform))]
public class BillboardUI : MonoBehaviour
{
    [Header("Настройки Billboard")]
    [Tooltip("Поворачивать только по оси Y (горизонтально)")]
    [SerializeField] private bool lockYRotation = false;
    
    [Tooltip("Инвертировать направление (поворачивать спиной к камере)")]
    [SerializeField] private bool invertDirection = false;
    
    [Tooltip("Добавить 180° по Y для коррекции перевёрнутой иконки (например, E)")]
    [SerializeField] private bool add180YOffset = false;
    
    [Header("Центр вращения (горизонталь)")]
    [Tooltip("Если задан — используется для pivot вместо чтения из TextMeshPro")]
    [SerializeField] private HorizontalPivotSource horizontalPivotSource = HorizontalPivotSource.FromTextAlignment;
    
    private Transform cameraTransform;
    private Transform myTransform;
    private RectTransform rectTransform;
    private bool pivotApplied;
    
    public enum HorizontalPivotSource
    {
        FromTextAlignment,
        Left,
        Center,
        Right
    }
    
    private void Awake()
    {
        myTransform = transform;
        rectTransform = GetComponent<RectTransform>();
        
        // Автоматически находим главную камеру, если не установлена
        if (cameraTransform == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                mainCam = FindFirstObjectByType<Camera>();
            }
            
            if (mainCam != null)
            {
                cameraTransform = mainCam.transform;
            }
        }
    }
    
    private void Start()
    {
        ApplyPivotFromAlignment();
    }
    
    /// <summary>
    /// Устанавливает pivot RectTransform по горизонтали в зависимости от alignment (или заданного источника).
    /// </summary>
    private void ApplyPivotFromAlignment()
    {
        if (rectTransform == null || pivotApplied) return;
        
        float pivotX = 0f;
        
        if (horizontalPivotSource == HorizontalPivotSource.FromTextAlignment)
        {
            TMP_Text tmp = GetComponent<TMP_Text>();
            if (tmp == null) tmp = GetComponentInChildren<TMP_Text>();
            if (tmp != null)
            {
                TextAlignmentOptions a = tmp.alignment;
                if ((a & TextAlignmentOptions.Right) != 0)
                    pivotX = 1f;
                else if ((a & TextAlignmentOptions.Center) != 0)
                    pivotX = 0.5f;
                else
                    pivotX = 0f;
            }
        }
        else
        {
            switch (horizontalPivotSource)
            {
                case HorizontalPivotSource.Left:   pivotX = 0f;   break;
                case HorizontalPivotSource.Center: pivotX = 0.5f; break;
                case HorizontalPivotSource.Right:  pivotX = 1f;   break;
            }
        }
        
        Vector2 oldPivot = rectTransform.pivot;
        Vector2 newPivot = new Vector2(pivotX, oldPivot.y);
        if (Mathf.Approximately(oldPivot.x, newPivot.x)) return;
        
        rectTransform.pivot = newPivot;
        Vector2 size = rectTransform.rect.size;
        rectTransform.anchoredPosition += (oldPivot - newPivot) * size;
        pivotApplied = true;
    }
    
    /// <summary>
    /// Устанавливает трансформ камеры для отслеживания
    /// </summary>
    public void SetCameraTransform(Transform camTransform)
    {
        cameraTransform = camTransform;
    }
    
    /// <summary>
    /// Включить коррекцию 180° по Y (если иконка E отображается перевёрнутой)
    /// </summary>
    public void SetAdd180YOffset(bool value)
    {
        add180YOffset = value;
    }
    
    /// <summary>
    /// Обновляет поворот UI к камере (оптимизированная версия)
    /// Этот метод должен вызываться в LateUpdate() для корректной работы
    /// </summary>
    public void UpdateRotation()
    {
        if (cameraTransform == null || myTransform == null) return;
        
        // Вычисляем направление от UI к камере
        Vector3 directionToCamera = cameraTransform.position - myTransform.position;
        
        if (invertDirection)
        {
            directionToCamera = -directionToCamera;
        }
        
        // Если нужно заблокировать поворот по Y, убираем вертикальную составляющую
        if (lockYRotation)
        {
            directionToCamera.y = 0f;
        }
        
        // Оптимизация: используем квадрат расстояния вместо magnitude
        float sqrMagnitude = directionToCamera.sqrMagnitude;
        if (sqrMagnitude > 0.000001f) // 0.001^2
        {
            // Используем более быстрый способ нормализации
            float magnitude = Mathf.Sqrt(sqrMagnitude);
            directionToCamera.x /= magnitude;
            directionToCamera.y /= magnitude;
            directionToCamera.z /= magnitude;
            
            // Вычисляем поворот
            Quaternion targetRotation = Quaternion.LookRotation(directionToCamera);
            if (add180YOffset)
            {
                targetRotation *= Quaternion.Euler(0f, 180f, 0f);
            }
            
            // Применяем поворот только если он изменился (оптимизация)
            if (Quaternion.Angle(myTransform.rotation, targetRotation) > 0.1f)
            {
                myTransform.rotation = targetRotation;
            }
        }
    }
    
    /// <summary>
    /// Устанавливает поворот по Y вручную (для использования с кольцом)
    /// </summary>
    public void SetRotationY(float yRotation)
    {
        if (myTransform == null) return;
        Vector3 euler = myTransform.rotation.eulerAngles;
        myTransform.rotation = Quaternion.Euler(euler.x, yRotation, euler.z);
    }
    
    private void LateUpdate()
    {
        // Автоматически обновляем поворот, если камера доступна
        if (cameraTransform != null)
        {
            UpdateRotation();
        }
    }
}
