using UnityEngine;
using TMPro;

/// <summary>
/// Компонент для поворота 3D TextMeshPro к игроку/камере (Billboard эффект).
/// Обеспечивает, что текст всегда смотрит в сторону игрока.
/// </summary>
[RequireComponent(typeof(TextMeshPro))]
public class TextMeshProBillboard : MonoBehaviour
{
    [Header("Настройки Billboard")]
    [Tooltip("Поворачивать к игроку (true) или к камере (false)")]
    [SerializeField] private bool lookAtPlayer = true;
    
    [Tooltip("Поворачивать только по оси Y (горизонтально)")]
    [SerializeField] private bool lockYRotation = false;
    
    [Tooltip("Инвертировать направление (поворачивать спиной к цели)")]
    [SerializeField] private bool invertDirection = false;
    
    [Tooltip("Тег игрока (используется если lookAtPlayer = true)")]
    [SerializeField] private string playerTag = "Player";
    
    [Tooltip("Ссылка на трансформ игрока (приоритет над поиском по тегу)")]
    [SerializeField] private Transform playerTransformReference;
    
    [Tooltip("Ссылка на трансформ камеры (используется если lookAtPlayer = false)")]
    [SerializeField] private Transform cameraTransformReference;
    
    [Header("Оптимизация")]
    [Tooltip("Частота обновления поворота (в кадрах). Больше значение = меньше обновлений")]
    [SerializeField] private int updateFrequency = 1;
    
    private Transform targetTransform;
    private Transform myTransform;
    private int frameCount = 0;
    
    private void Awake()
    {
        myTransform = transform;
        
        // Проверяем наличие TextMeshPro компонента
        TextMeshPro tmp = GetComponent<TextMeshPro>();
        if (tmp == null)
        {
            Debug.LogWarning($"[TextMeshProBillboard] {gameObject.name}: Компонент TextMeshPro не найден!");
        }
        
        // Инициализируем цель для поворота
        InitializeTarget();
    }
    
    private void Start()
    {
        // Повторно инициализируем цель в Start (на случай если игрок/камера создаются позже)
        InitializeTarget();
    }
    
    /// <summary>
    /// Инициализирует цель для поворота (игрок или камера)
    /// </summary>
    private void InitializeTarget()
    {
        if (lookAtPlayer)
        {
            // Приоритет: прямая ссылка > поиск по тегу
            if (playerTransformReference != null)
            {
                targetTransform = playerTransformReference;
            }
            else
            {
                try
                {
                    GameObject player = GameObject.FindGameObjectWithTag(playerTag);
                    if (player != null)
                    {
                        targetTransform = player.transform;
                    }
                }
                catch (UnityException)
                {
                    Debug.LogWarning($"[TextMeshProBillboard] {gameObject.name}: Тег '{playerTag}' не существует!");
                }
            }
        }
        else
        {
            // Поворачиваем к камере
            if (cameraTransformReference != null)
            {
                targetTransform = cameraTransformReference;
            }
            else
            {
                Camera mainCam = Camera.main;
                if (mainCam == null)
                {
                    mainCam = FindFirstObjectByType<Camera>();
                }
                
                if (mainCam != null)
                {
                    targetTransform = mainCam.transform;
                }
            }
        }
    }
    
    private void LateUpdate()
    {
        // Оптимизация: обновляем не каждый кадр
        frameCount++;
        if (frameCount < updateFrequency)
        {
            return;
        }
        frameCount = 0;
        
        // Если цель не найдена, пытаемся найти её снова
        if (targetTransform == null)
        {
            InitializeTarget();
            if (targetTransform == null)
            {
                return;
            }
        }
        
        if (myTransform == null) return;
        
        // Вычисляем направление от текста к цели
        Vector3 directionToTarget = targetTransform.position - myTransform.position;
        
        if (invertDirection)
        {
            directionToTarget = -directionToTarget;
        }
        
        // Если нужно заблокировать поворот по Y, убираем вертикальную составляющую
        if (lockYRotation)
        {
            directionToTarget.y = 0f;
        }
        
        // Если направление слишком маленькое, не поворачиваем
        if (directionToTarget.sqrMagnitude < 0.0001f) return;
        
        // Поворачиваем текст так, чтобы он смотрел на цель
        // Используем LookRotation с инверсией направления, чтобы текст был правильно ориентирован
        myTransform.rotation = Quaternion.LookRotation(-directionToTarget);
    }
    
    /// <summary>
    /// Устанавливает трансформ цели для отслеживания
    /// </summary>
    public void SetTargetTransform(Transform target)
    {
        targetTransform = target;
    }
    
    /// <summary>
    /// Устанавливает трансформ игрока
    /// </summary>
    public void SetPlayerTransform(Transform player)
    {
        playerTransformReference = player;
        if (lookAtPlayer)
        {
            targetTransform = player;
        }
    }
    
    /// <summary>
    /// Устанавливает трансформ камеры
    /// </summary>
    public void SetCameraTransform(Transform camera)
    {
        cameraTransformReference = camera;
        if (!lookAtPlayer)
        {
            targetTransform = camera;
        }
    }
}
