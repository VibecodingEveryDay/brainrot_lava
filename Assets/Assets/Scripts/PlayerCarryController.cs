using UnityEngine;

/// <summary>
/// Контроллер для управления объектами, которые игрок несет в руках.
/// Управляет визуальным следованием объекта за игроком.
/// </summary>
[RequireComponent(typeof(ThirdPersonController))]
public class PlayerCarryController : MonoBehaviour
{
    [Header("Настройки переноски")]
    [Tooltip("Смещение объекта относительно игрока (X, Y, Z)")]
    [SerializeField] private Vector3 holdPointOffset = new Vector3(0f, 1f, 1.5f);
    
    [Tooltip("Скорость следования объекта за игроком (0 = мгновенное, >0 = плавное)")]
    [SerializeField] private float followSpeed = 10f;
    
    [Tooltip("Поворачивать объект вместе с игроком")]
    [SerializeField] private bool rotateWithPlayer = true;
    
    private BrainrotObject currentCarriedObject;
    private ThirdPersonController playerController;
    private Transform playerTransform;
    
    // Кэшированные значения для оптимизации
    private Vector3 cachedOffsets = Vector3.zero;
    private float cachedRotationY = 0f;
    private bool offsetsCached = false;
    
    private void Awake()
    {
        playerController = GetComponent<ThirdPersonController>();
        playerTransform = transform;
    }
    
    private void LateUpdate()
    {
        // Используем LateUpdate для обновления позиции после всех других обновлений
        // Это предотвращает конфликты с другими скриптами и обеспечивает плавное движение
        // ВАЖНО: Проверяем не только currentCarriedObject != null, но и IsCarried()
        // Это предотвращает обновление позиции объекта, который уже размещен (isCarried = false)
        if (currentCarriedObject != null && currentCarriedObject.IsCarried())
        {
            UpdateCarriedObjectPosition();
        }
        else if (currentCarriedObject != null && !currentCarriedObject.IsCarried())
        {
            // ВАЖНО: Если объект больше не в руках, но currentCarriedObject все еще установлен,
            // очищаем ссылку, чтобы избежать проблем с обновлением позиции
            // Это может произойти, если DropObject() не был вызван или был вызван неправильно
            Debug.LogWarning($"[PlayerCarryController] Объект {currentCarriedObject.GetObjectName()} больше не в руках, но currentCarriedObject все еще установлен. Очищаем ссылку.");
            currentCarriedObject = null;
            offsetsCached = false;
        }
    }
    
    /// <summary>
    /// Обновляет позицию объекта, который несет игрок
    /// </summary>
    private void UpdateCarriedObjectPosition()
    {
        if (currentCarriedObject == null || playerTransform == null) return;
        
        // Оптимизация: кэшируем смещения при взятии объекта, обновляем только если нужно
        if (!offsetsCached)
        {
            // Получаем смещения из объекта (если заданы) или используем значения по умолчанию
            float offsetX = currentCarriedObject.GetCarryOffsetX();
            float offsetY = currentCarriedObject.GetCarryOffsetY();
            float offsetZ = currentCarriedObject.GetCarryOffsetZ();
            
            // Если смещения не заданы в объекте (равны 0), используем значения из контроллера
            // Проверяем каждую ось отдельно
            if (Mathf.Approximately(offsetX, 0f))
            {
                offsetX = holdPointOffset.x;
            }
            
            if (Mathf.Approximately(offsetY, 0f))
            {
                offsetY = holdPointOffset.y;
            }
            
            if (Mathf.Approximately(offsetZ, 0f))
            {
                offsetZ = holdPointOffset.z;
            }
            
            cachedOffsets = new Vector3(offsetX, offsetY, offsetZ);
            cachedRotationY = currentCarriedObject.GetCarryRotationY();
            offsetsCached = true;
        }
        
        // Вычисляем целевую позицию относительно игрока
        // Используем кэшированные смещения
        Vector3 targetPosition = playerTransform.position + 
                                playerTransform.forward * cachedOffsets.z +
                                playerTransform.up * cachedOffsets.y +
                                playerTransform.right * cachedOffsets.x;
        
        // Плавно перемещаем объект к целевой позиции
        // Используем более плавную интерполяцию для предотвращения дёргания
        if (followSpeed > 0f)
        {
            // Используем MoveTowards для более предсказуемого движения
            // Увеличена скорость в 100 раз для быстрого следования
            float maxDistanceDelta = followSpeed * Time.deltaTime * 100f;
            currentCarriedObject.transform.position = Vector3.MoveTowards(
                currentCarriedObject.transform.position,
                targetPosition,
                maxDistanceDelta
            );
        }
        else
        {
            // Мгновенное перемещение
            currentCarriedObject.transform.position = targetPosition;
        }
        
        // Поворачиваем объект
        if (rotateWithPlayer)
        {
            if (Mathf.Approximately(cachedRotationY, 0f))
            {
                // Если поворот не задан, используем поворот игрока
                currentCarriedObject.transform.rotation = playerTransform.rotation;
            }
            else
            {
                // Применяем поворот игрока + дополнительный поворот по Y из объекта
                Quaternion baseRotation = playerTransform.rotation;
                Quaternion additionalRotation = Quaternion.Euler(0f, cachedRotationY, 0f);
                currentCarriedObject.transform.rotation = baseRotation * additionalRotation;
            }
        }
    }
    
    /// <summary>
    /// Взять объект в руки
    /// </summary>
    public void CarryObject(BrainrotObject obj)
    {
        if (obj == null)
        {
            Debug.LogWarning("[PlayerCarryController] Попытка взять null объект!");
            return;
        }
        
        if (obj.IsUnfought())
        {
            return;
        }
        
        if (currentCarriedObject != null)
        {
            Debug.LogWarning("[PlayerCarryController] Игрок уже несет объект! Сначала нужно положить текущий объект.");
            return;
        }
        
        currentCarriedObject = obj;
        
        // Сбрасываем кэш смещений для нового объекта
        offsetsCached = false;
        
        // Устанавливаем параметр IsTaking в аниматоре
        if (playerController != null)
        {
            playerController.SetIsTaking(true);
        }
        
        // Устанавливаем родителя объекта (опционально, для организации иерархии)
        // obj.transform.SetParent(playerTransform);
        
        Debug.Log($"[PlayerCarryController] Объект {obj.GetObjectName()} взят в руки");
    }
    
    /// <summary>
    /// Положить объект (освободить руки)
    /// </summary>
    public void DropObject()
    {
        if (currentCarriedObject == null)
        {
            // #region agent log
            try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
                $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"H\",\"location\":\"PlayerCarryController.cs:165\",\"message\":\"DropObject - currentCarriedObject is null\",\"data\":{{}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
            // #endregion
            return;
        }
        
        BrainrotObject droppedObject = currentCarriedObject;
        // #region agent log
        try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
            $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"H\",\"location\":\"PlayerCarryController.cs:171\",\"message\":\"DropObject - setting currentCarriedObject to null\",\"data\":{{\"objectName\":\"{droppedObject.GetObjectName()}\"}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
        // #endregion
        currentCarriedObject = null;
        
        // Сбрасываем кэш смещений
        offsetsCached = false;
        
        // Сбрасываем параметр IsTaking в аниматоре
        if (playerController != null)
        {
            playerController.SetIsTaking(false);
        }
        
        // Убираем родителя объекта (если был установлен)
        // droppedObject.transform.SetParent(null);
        
        // #region agent log
        try { System.IO.File.AppendAllText(@"a:\CODE\unity_projects\Steal_brainrot_fight\.cursor\debug.log", 
            $"{{\"sessionId\":\"debug-session\",\"runId\":\"run1\",\"hypothesisId\":\"H\",\"location\":\"PlayerCarryController.cs:185\",\"message\":\"DropObject - after setting to null, GetCurrentCarriedObject returns\",\"data\":{{\"objectName\":\"{droppedObject.GetObjectName()}\",\"currentCarriedObjectNull\":{(currentCarriedObject == null).ToString().ToLower()}}},\"timestamp\":{System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}}}\n"); } catch {}
        // #endregion
        Debug.Log($"[PlayerCarryController] Объект {droppedObject.GetObjectName()} положен");
    }
    
    /// <summary>
    /// Проверить, может ли игрок взять объект
    /// </summary>
    public bool CanCarry()
    {
        return currentCarriedObject == null;
    }
    
    /// <summary>
    /// Получить текущий объект в руках
    /// </summary>
    public BrainrotObject GetCurrentCarriedObject()
    {
        return currentCarriedObject;
    }
    
    /// <summary>
    /// Получить Transform игрока
    /// </summary>
    public Transform GetPlayerTransform()
    {
        return playerTransform;
    }
    
    /// <summary>
    /// Установить смещение точки удержания
    /// </summary>
    public void SetHoldPointOffset(Vector3 offset)
    {
        holdPointOffset = offset;
    }
    
    /// <summary>
    /// Получить смещение точки удержания
    /// </summary>
    public Vector3 GetHoldPointOffset()
    {
        return holdPointOffset;
    }
}
