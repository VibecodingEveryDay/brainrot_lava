using UnityEngine;

/// <summary>
/// Скрипт лестницы. Размещается на объекте с BoxCollider (isTrigger = true).
/// Когда игрок входит в триггер — включает режим лестницы.
/// Лестница может быть заблокирована и требовать покупки в магазине.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class Ladder : MonoBehaviour
{
    [Header("Идентификатор лестницы")]
    [Tooltip("Уникальный ID лестницы для системы разблокировки (1, 2, и т.д.)")]
    [SerializeField] private int ladderId = 1;
    
    [Header("Настройки лестницы")]
    [Tooltip("Скорость подъёма/спуска по лестнице")]
    [SerializeField] private float climbSpeed = 3f;
    
    [Tooltip("Смещение игрока к центру лестницы по X и Z (0 = не центрировать)")]
    [SerializeField] private bool centerPlayerOnLadder = true;
    
    [Tooltip("Скорость центрирования игрока")]
    [SerializeField] private float centeringSpeed = 5f;
    
    [Tooltip("Инвертировать поворот по Y. Выкл = -90°, Вкл = +90° относительно лестницы")]
    [SerializeField] private bool invertY = false;
    
    [Header("Точки выхода")]
    [Tooltip("Точка выхода сверху лестницы (опционально)")]
    [SerializeField] private Transform topExitPoint;
    
    [Tooltip("Точка выхода снизу лестницы (опционально)")]
    [SerializeField] private Transform bottomExitPoint;
    
    [Header("Блокировка")]
    [Tooltip("Дочерний объект Lock (автоматически находится, если не назначен)")]
    [SerializeField] private GameObject lockObject;
    
    // Флаг разблокировки
    private bool isUnlocked = false;
    private GameStorage gameStorage;
    
    // Публичные свойства для доступа из контроллера игрока
    public float ClimbSpeed => climbSpeed;
    public bool CenterPlayerOnLadder => centerPlayerOnLadder;
    public float CenteringSpeed => centeringSpeed;
    public bool InvertY => invertY;
    public Transform TopExitPoint => topExitPoint;
    public Transform BottomExitPoint => bottomExitPoint;
    public int LadderId => ladderId;
    public bool IsUnlocked => isUnlocked;
    
    /// <summary>
    /// Возвращает позицию центра лестницы (X и Z)
    /// </summary>
    public Vector3 GetLadderCenter()
    {
        return transform.position;
    }
    
    /// <summary>
    /// Возвращает направление "вперёд" лестницы (куда смотрит игрок на лестнице)
    /// </summary>
    public Vector3 GetLadderForward()
    {
        return -transform.forward; // Игрок смотрит "от" лестницы (на неё)
    }
    
    private void Awake()
    {
        // Убеждаемся, что коллайдер настроен как триггер
        BoxCollider col = GetComponent<BoxCollider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning($"[Ladder] {gameObject.name}: BoxCollider автоматически переключён в режим isTrigger");
        }
        
        // Автоматически находим объект Lock, если не назначен
        if (lockObject == null)
        {
            Transform lockTransform = transform.Find("Lock");
            if (lockTransform != null)
            {
                lockObject = lockTransform.gameObject;
            }
        }
    }
    
    private void Start()
    {
        // Получаем ссылку на GameStorage
        gameStorage = GameStorage.Instance;
        
        // Проверяем статус разблокировки
        UpdateUnlockStatus();
    }
    
    /// <summary>
    /// Обновляет статус разблокировки лестницы
    /// </summary>
    public void UpdateUnlockStatus()
    {
        if (gameStorage == null)
        {
            gameStorage = GameStorage.Instance;
        }
        
        if (gameStorage != null)
        {
            isUnlocked = gameStorage.IsLadderUnlocked(ladderId);
        }
        else
        {
            isUnlocked = false;
        }
        
        // Обновляем состояние объекта Lock
        UpdateLockVisual();
    }
    
    /// <summary>
    /// Обновляет визуальное состояние замка
    /// </summary>
    private void UpdateLockVisual()
    {
        if (lockObject != null)
        {
            // Если разблокировано — скрываем замок, иначе показываем
            lockObject.SetActive(!isUnlocked);
        }
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        
        // Проверяем, разблокирована ли лестница
        if (!isUnlocked)
        {
            Debug.Log($"[Ladder] Лестница {ladderId} заблокирована. Требуется покупка в магазине.");
            return;
        }
        
        // Находим контроллер игрока и включаем режим лестницы
        ThirdPersonController controller = other.GetComponent<ThirdPersonController>();
        if (controller != null)
        {
            controller.EnterLadder(this);
        }
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        
        // Находим контроллер игрока и выключаем режим лестницы
        ThirdPersonController controller = other.GetComponent<ThirdPersonController>();
        if (controller != null)
        {
            controller.ExitLadder();
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Визуализация точек выхода
        Gizmos.color = Color.green;
        if (topExitPoint != null)
        {
            Gizmos.DrawWireSphere(topExitPoint.position, 0.3f);
            Gizmos.DrawLine(transform.position, topExitPoint.position);
        }
        
        Gizmos.color = Color.red;
        if (bottomExitPoint != null)
        {
            Gizmos.DrawWireSphere(bottomExitPoint.position, 0.3f);
            Gizmos.DrawLine(transform.position, bottomExitPoint.position);
        }
    }
}
