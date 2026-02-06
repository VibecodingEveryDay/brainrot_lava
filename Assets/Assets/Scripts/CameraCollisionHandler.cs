using UnityEngine;

/// <summary>
/// Обрабатывает столкновения камеры с препятствиями и автоматически приближает камеру к персонажу
/// Работает как в GTA/Roblox: плавно приближается при столкновениях со стенами, не застревает в углах, не дрожит
/// 
/// ВАЖНО: Этот скрипт должен работать ПОСЛЕ ThirdPersonCamera в порядке выполнения скриптов
/// В Unity: выберите скрипт в Inspector -> Script Execution Order -> установите порядок выше, чем у ThirdPersonCamera
/// </summary>
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(100)] // Выполняется после ThirdPersonCamera (обычно 0)
public class CameraCollisionHandler : MonoBehaviour
{
    [Header("Target Settings")]
    [Tooltip("Точка обзора камеры (CameraTarget на персонаже)")]
    [SerializeField] public Transform target;
    
    [Header("Distance Settings")]
    [Tooltip("Стандартная дистанция камеры от персонажа")]
    [SerializeField] private float defaultDistance = 5f;
    
    [Tooltip("Минимальное приближение камеры к персонажу")]
    [SerializeField] private float minDistance = 1.5f;
    
    [Header("Collision Detection")]
    [Tooltip("Радиус SphereCast для обнаружения препятствий")]
    [SerializeField] private float collisionRadius = 0.3f;
    
    [Tooltip("Зазор между камерой и препятствием")]
    [SerializeField] private float collisionOffset = 0.2f;
    
    [Tooltip("Слои для проверки препятствий (должен включать Obstacle и Default, исключать Player)")]
    [SerializeField] private LayerMask obstacleMask = -1;
    
    [Header("Smoothing")]
    [Tooltip("Плавность движения камеры (меньше = плавнее)")]
    [SerializeField] private float smoothTime = 0.2f;
    
    [Header("Debug")]
    [Tooltip("Показывать лучи в редакторе")]
    [SerializeField] private bool showDebugRays = false;
    
    // Текущее расстояние камеры (изменяется при столкновениях)
    private float currentDistance;
    
    // Для SmoothDamp
    private float distanceVelocity;
    
    // Кэш компонента камеры
    private Camera cam;
    
    // Сохраненное направление обзора (для сохранения угла камеры)
    private Vector3 lastValidDirection;
    
    // Ссылка на ThirdPersonCamera для получения стандартной дистанции
    private ThirdPersonCamera thirdPersonCamera;
    
    private void Awake()
    {
        cam = GetComponent<Camera>();
        
        // Ищем ThirdPersonCamera для синхронизации дистанции
        thirdPersonCamera = GetComponent<ThirdPersonCamera>();
        if (thirdPersonCamera == null)
        {
            thirdPersonCamera = FindFirstObjectByType<ThirdPersonCamera>();
        }
        
        // Автоматически находим CameraTarget, если не назначен
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                Transform cameraTarget = player.transform.Find("CameraTarget");
                if (cameraTarget != null)
                {
                    target = cameraTarget;
                }
                else
                {
                    // Если CameraTarget не найден, используем сам Player
                    target = player.transform;
                }
            }
        }
        
        // Инициализируем текущее расстояние
        if (target != null)
        {
            Vector3 directionToCamera = transform.position - target.position;
            currentDistance = directionToCamera.magnitude;
            
            // Сохраняем начальное направление
            if (directionToCamera.magnitude > 0.001f)
            {
                lastValidDirection = directionToCamera.normalized;
            }
            else
            {
                lastValidDirection = transform.forward * -1f;
            }
            
            // Ограничиваем начальное расстояние
            currentDistance = Mathf.Clamp(currentDistance, minDistance, defaultDistance);
        }
        else
        {
            currentDistance = defaultDistance;
            lastValidDirection = transform.forward * -1f;
        }
        
        // Синхронизируем defaultDistance с ThirdPersonCamera, если он найден
        if (thirdPersonCamera != null)
        {
            // Автоматически синхронизируем дистанцию с ThirdPersonCamera
            defaultDistance = thirdPersonCamera.GetDistance();
        }
    }
    
    private void LateUpdate()
    {
        if (target == null) return;
        
        // ВАЖНО: ThirdPersonCamera уже установил позицию камеры в своем LateUpdate
        // Мы корректируем эту позицию, учитывая препятствия
        
        // Получаем текущее направление от цели к камере (установленное ThirdPersonCamera)
        Vector3 directionToCamera = transform.position - target.position;
        float currentDist = directionToCamera.magnitude;
        
        // Если направление валидно, обновляем сохраненное направление
        // Это сохраняет угол обзора камеры при изменении дистанции
        if (currentDist > 0.001f)
        {
            lastValidDirection = directionToCamera / currentDist;
        }
        // Если камера слишком близко к цели, используем сохраненное направление
        else if (lastValidDirection.magnitude < 0.001f)
        {
            // Если нет сохраненного направления, используем направление камеры
            if (transform.forward != Vector3.zero)
            {
                lastValidDirection = -transform.forward.normalized;
            }
            else
            {
                lastValidDirection = new Vector3(0f, 0.3f, -1f).normalized;
            }
        }
        
        // Выполняем SphereCast от камеры к цели для обнаружения препятствий
        // Направление: от камеры к цели (обратное к направлению от цели к камере)
        Vector3 directionToTarget = (target.position - transform.position);
        float distToTarget = directionToTarget.magnitude;
        
        // Нормализуем направление только если оно валидно
        if (distToTarget > 0.001f)
        {
            directionToTarget = directionToTarget / distToTarget;
        }
        else
        {
        // Если направление не валидно, используем сохраненное
            directionToTarget = -lastValidDirection;
        }
        
        // ВАЖНО: Проверяем препятствия только если текущее расстояние близко к стандартному
        // Это предотвращает ложные срабатывания после телепортации
        // Если камера находится на стандартном расстоянии (или близко к нему), проверяем препятствия
        // Если камера слишком близко, это может быть из-за предыдущей проверки, поэтому проверяем снова
        float desiredDistance = CheckForObstacles(directionToTarget, defaultDistance);
        
        // ВАЖНО: Если desiredDistance равен defaultDistance, но currentDistance меньше,
        // это означает, что препятствий нет, и камера должна отдалиться
        // Но если currentDistance уже близок к defaultDistance, не нужно резко менять расстояние
        if (Mathf.Abs(desiredDistance - defaultDistance) < 0.01f && currentDistance < defaultDistance - 0.1f)
        {
            // Препятствий нет, но камера слишком близко - плавно отдаляем
            // Это исправляет проблему, когда камера остается близко после телепортации
        }
        
        // Плавно изменяем текущее расстояние с помощью SmoothDamp
        currentDistance = Mathf.SmoothDamp(currentDistance, desiredDistance, ref distanceVelocity, smoothTime);
        
        // Ограничиваем расстояние минимальным и максимальным значениями
        currentDistance = Mathf.Clamp(currentDistance, minDistance, defaultDistance);
        
        // Вычисляем новую позицию камеры, сохраняя направление обзора
        // Используем lastValidDirection для сохранения угла обзора
        Vector3 newPosition = target.position + lastValidDirection * currentDistance;
        
        // Дополнительная проверка: убеждаемся, что камера не под полом/потолком
        // Проверяем вертикальные препятствия сверху и снизу
        newPosition = CheckVerticalObstacles(newPosition);
        
        // Устанавливаем скорректированную позицию камеры
        transform.position = newPosition;
    }
    
    /// <summary>
    /// Проверяет препятствия между камерой и целью с помощью SphereCast
    /// SphereCast выполняется от камеры к CameraTarget (как указано в требованиях)
    /// Также проверяет, не находится ли камера уже внутри препятствия
    /// </summary>
    /// <param name="directionToTarget">Направление от камеры к цели (нормализованное)</param>
    /// <param name="maxDistance">Максимальное расстояние для проверки</param>
    /// <returns>Желаемое расстояние от цели до камеры (с учетом препятствий)</returns>
    private float CheckForObstacles(Vector3 directionToTarget, float maxDistance)
    {
        // Начальная позиция SphereCast - текущая позиция камеры
        Vector3 rayStart = transform.position;
        
        // Вычисляем расстояние до цели
        float distanceToTarget = Vector3.Distance(rayStart, target.position);
        
        // Если расстояние слишком мало, возвращаем минимальное расстояние
        if (distanceToTarget < 0.1f)
        {
            return minDistance;
        }
        
        // ВАЖНО: Если камера находится на стандартном расстоянии или дальше, сначала проверяем,
        // есть ли препятствия на пути к стандартному расстоянию. Если препятствий нет,
        // возвращаем стандартное расстояние без дополнительных проверок.
        // Это предотвращает ложные срабатывания после телепортации.
        if (distanceToTarget >= defaultDistance - 0.1f)
        {
            // Камера уже на стандартном расстоянии или дальше - проверяем препятствия только на пути к стандартному расстоянию
            // Вычисляем направление от цели к камере (обратное к directionToTarget)
            Vector3 directionFromTarget = -directionToTarget;
            
            // Выполняем быструю проверку: есть ли препятствия между целью и стандартным расстоянием
            RaycastHit quickCheck;
            if (!Physics.SphereCast(
                target.position,
                collisionRadius,
                directionFromTarget,
                out quickCheck,
                defaultDistance,
                obstacleMask,
                QueryTriggerInteraction.Ignore))
            {
                // Препятствий нет на пути к стандартному расстоянию - возвращаем стандартное расстояние
                return defaultDistance;
            }
        }
        
        // ВАЖНО: Сначала проверяем, не находится ли камера уже внутри препятствия
        // Это решает проблему, когда камера находится внутри полого блока
        
        // Метод 1: OverlapSphere для проверки пересечения с коллайдерами
        Collider[] overlappingColliders = Physics.OverlapSphere(
            rayStart,
            collisionRadius, // Используем полный радиус для проверки
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );
        
        // Метод 2: Дополнительная проверка через Raycast от камеры к цели
        // Если луч сразу попадает в препятствие на очень маленьком расстоянии, камера внутри
        bool isInsideObstacle = overlappingColliders.Length > 0;
        
        if (!isInsideObstacle)
        {
            // Проверяем через короткий Raycast к цели
            // Если препятствие обнаружено сразу (расстояние < 0.1f), камера внутри
            RaycastHit immediateHit;
            if (Physics.Raycast(rayStart, directionToTarget, out immediateHit, 0.1f, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                isInsideObstacle = true;
            }
        }
        
        // Если камера находится внутри препятствия, принудительно приближаем её
        if (isInsideObstacle)
        {
            // Используем несколько методов для определения безопасной позиции:
            // 1. Проверяем, есть ли препятствия между камерой и целью
            // 2. Если препятствие есть, приближаем камеру к цели
            
            // Выполняем Raycast от камеры к цели для поиска препятствий
            RaycastHit hitFromCamera;
            bool hasHitFromCamera = Physics.Raycast(
                rayStart,
                directionToTarget,
                out hitFromCamera,
                distanceToTarget,
                obstacleMask,
                QueryTriggerInteraction.Ignore
            );
            
            if (hasHitFromCamera)
            {
                // Найдено препятствие между камерой и целью
                // Вычисляем безопасное расстояние: от цели до препятствия минус радиус и зазор
                float safeDistance = distanceToTarget - hitFromCamera.distance - collisionRadius - collisionOffset;
                safeDistance = Mathf.Max(safeDistance, minDistance);
                
                if (showDebugRays)
                {
                    Debug.DrawRay(rayStart, directionToTarget * hitFromCamera.distance, Color.red);
                    Debug.DrawRay(rayStart, Vector3.up * 0.5f, Color.red);
                }
                
                return safeDistance;
            }
            else
            {
                // Камера внутри препятствия, но между камерой и целью препятствий нет
                // Это означает, что камера находится сзади препятствия (персонаж виден)
                // Используем обратный SphereCast от цели к камере, чтобы найти ближайшую безопасную позицию
                
                Vector3 reverseDirection = -directionToTarget; // От цели к камере
                RaycastHit reverseHit;
                
                // Выполняем SphereCast от цели в направлении камеры
                bool hasReverseHit = Physics.SphereCast(
                    target.position,
                    collisionRadius,
                    reverseDirection,
                    out reverseHit,
                    distanceToTarget,
                    obstacleMask,
                    QueryTriggerInteraction.Ignore
                );
                
                if (hasReverseHit)
                {
                    // Найдена точка выхода из препятствия
                    // Безопасное расстояние = расстояние от цели до точки выхода + радиус + зазор
                    float safeDistance = reverseHit.distance + collisionRadius + collisionOffset;
                    safeDistance = Mathf.Clamp(safeDistance, minDistance, distanceToTarget);
                    
                    if (showDebugRays)
                    {
                        Debug.DrawRay(target.position, reverseDirection * reverseHit.distance, Color.magenta);
                        Debug.DrawRay(rayStart, Vector3.up * 0.5f, Color.magenta);
                    }
                    
                    return safeDistance;
                }
                else
                {
                    // Не удалось найти точку выхода через SphereCast
                    // Используем более агрессивное приближение: уменьшаем расстояние
                    float aggressiveDistance = Mathf.Max(currentDistance * 0.9f, minDistance);
                    
                    if (showDebugRays)
                    {
                        Debug.DrawRay(rayStart, Vector3.up * 0.5f, Color.magenta);
                        Debug.DrawRay(rayStart, directionToTarget * distanceToTarget, Color.magenta);
                    }
                    
                    return aggressiveDistance;
                }
            }
        }
        
        // Камера не внутри препятствия, выполняем обычный SphereCast
        // Используем небольшой отступ от камеры, чтобы избежать самопересечения
        Vector3 adjustedStart = rayStart + directionToTarget * collisionRadius;
        float adjustedDistance = distanceToTarget - collisionRadius;
        
        RaycastHit hit;
        bool hasHit = Physics.SphereCast(
            adjustedStart,
            collisionRadius,
            directionToTarget,
            out hit,
            adjustedDistance,
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );
        
        if (showDebugRays)
        {
            // Визуализация в редакторе
            Color rayColor = hasHit ? Color.red : Color.green;
            Debug.DrawRay(adjustedStart, directionToTarget * adjustedDistance, rayColor);
            
            if (hasHit)
            {
                // Рисуем сферу в точке столкновения
                Debug.DrawRay(hit.point, Vector3.up * 0.5f, Color.yellow);
                // Рисуем нормаль препятствия
                Debug.DrawRay(hit.point, hit.normal * 0.3f, Color.magenta);
            }
        }
        
        if (hasHit)
        {
            // Обнаружено препятствие
            // Вычисляем расстояние от скорректированной стартовой позиции до препятствия
            float distanceToObstacle = hit.distance;
            
            // Вычисляем безопасное расстояние от цели до камеры
            // Расстояние от цели = расстояние до цели - расстояние до препятствия - радиус - зазор
            // Учитываем, что мы начали с adjustedStart
            float safeDistance = distanceToTarget - distanceToObstacle - collisionRadius - collisionOffset;
            
            // Ограничиваем безопасное расстояние минимальным значением
            safeDistance = Mathf.Max(safeDistance, minDistance);
            
            return safeDistance;
        }
        
        // Препятствий нет, возвращаем стандартную дистанцию
        return defaultDistance;
    }
    
    /// <summary>
    /// Проверяет вертикальные препятствия (пол/потолок) и корректирует позицию камеры
    /// </summary>
    private Vector3 CheckVerticalObstacles(Vector3 desiredPosition)
    {
        // Проверяем препятствия сверху (потолок) от target
        RaycastHit hitUp;
        Vector3 upDirection = Vector3.up;
        float maxUpDistance = 10f;
        
        if (Physics.Raycast(target.position, upDirection, out hitUp, maxUpDistance, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            float ceilingHeight = hitUp.point.y - collisionOffset;
            if (desiredPosition.y > ceilingHeight)
            {
                desiredPosition.y = ceilingHeight;
            }
        }
        
        // === УЛУЧШЕННАЯ ПРОВЕРКА ПОЛА ===
        
        // 1. Проверяем пол под target (основной пол уровня)
        RaycastHit hitDownFromTarget;
        Vector3 downDirection = Vector3.down;
        float maxDownDistance = 50f;
        
        float floorHeight = float.MinValue;
        
        if (Physics.Raycast(target.position, downDirection, out hitDownFromTarget, maxDownDistance, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            floorHeight = hitDownFromTarget.point.y + collisionOffset;
        }
        
        // 2. Проверяем пол под желаемой позицией камеры
        RaycastHit hitDownFromCamera;
        if (Physics.Raycast(desiredPosition + Vector3.up * 2f, downDirection, out hitDownFromCamera, maxDownDistance, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            float cameraFloorHeight = hitDownFromCamera.point.y + collisionOffset;
            // Используем более высокий пол
            if (cameraFloorHeight > floorHeight)
            {
                floorHeight = cameraFloorHeight;
            }
        }
        
        // 3. Проверяем, не находится ли камера уже под полом (SphereCast вверх)
        RaycastHit hitUpFromCamera;
        if (Physics.SphereCast(desiredPosition, collisionRadius, upDirection, out hitUpFromCamera, 5f, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            // Если над камерой есть препятствие очень близко, возможно камера под полом
            // Проверяем, является ли это препятствие "полом" (нормаль смотрит вниз)
            if (hitUpFromCamera.normal.y < -0.5f && hitUpFromCamera.distance < 1f)
            {
                // Камера под перевёрнутым plane - поднимаем её выше препятствия
                float surfaceHeight = hitUpFromCamera.point.y + collisionOffset;
                if (desiredPosition.y < surfaceHeight)
                {
                    desiredPosition.y = surfaceHeight;
                    
                    if (showDebugRays)
                    {
                        Debug.DrawRay(desiredPosition, Vector3.up * 2f, Color.red);
                    }
                }
            }
        }
        
        // 4. Применяем ограничение по полу
        if (floorHeight > float.MinValue && desiredPosition.y < floorHeight)
        {
            desiredPosition.y = floorHeight;
        }
        
        // 5. Дополнительно: проверяем OverlapSphere для обнаружения пересечения с любыми коллайдерами
        Collider[] overlaps = Physics.OverlapSphere(desiredPosition, collisionRadius, obstacleMask, QueryTriggerInteraction.Ignore);
        if (overlaps.Length > 0)
        {
            // Камера пересекается с чем-то - пробуем поднять её
            foreach (Collider col in overlaps)
            {
                // ClosestPoint поддерживается только для Box, Sphere, Capsule и выпуклого MeshCollider
                if (!IsClosestPointSupported(col))
                    continue;
                
                // Получаем ближайшую точку на коллайдере
                Vector3 closestPoint = col.ClosestPoint(desiredPosition);
                
                // Если камера ниже ближайшей точки, поднимаем
                if (desiredPosition.y < closestPoint.y + collisionOffset)
                {
                    // Проверяем, что это горизонтальная поверхность (пол)
                    Vector3 dirToCamera = (desiredPosition - closestPoint).normalized;
                    if (Mathf.Abs(dirToCamera.y) > 0.3f)
                    {
                        desiredPosition.y = closestPoint.y + collisionRadius + collisionOffset;
                        
                        if (showDebugRays)
                        {
                            Debug.DrawLine(closestPoint, desiredPosition, Color.magenta);
                        }
                    }
                }
            }
        }
        
        return desiredPosition;
    }
    
    /// <summary>
    /// Проверяет, поддерживает ли коллайдер ClosestPoint (Box, Sphere, Capsule, выпуклый MeshCollider).
    /// </summary>
    private static bool IsClosestPointSupported(Collider col)
    {
        if (col is BoxCollider || col is SphereCollider || col is CapsuleCollider)
            return true;
        if (col is MeshCollider meshCol)
            return meshCol.convex;
        return false;
    }
    
    /// <summary>
    /// Устанавливает целевую точку обзора
    /// </summary>
    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        
        // Пересчитываем направление при смене цели
        if (target != null)
        {
            Vector3 directionToCamera = transform.position - target.position;
            if (directionToCamera.magnitude > 0.001f)
            {
                lastValidDirection = directionToCamera.normalized;
            }
        }
    }
    
    /// <summary>
    /// Получает текущее расстояние камеры
    /// </summary>
    public float GetCurrentDistance()
    {
        return currentDistance;
    }
    
    /// <summary>
    /// Сбрасывает расстояние камеры к стандартному значению
    /// </summary>
    public void ResetDistance()
    {
        currentDistance = defaultDistance;
        distanceVelocity = 0f;
    }
    
    /// <summary>
    /// Принудительно сбрасывает камеру после телепортации
    /// Полностью пересчитывает направление и расстояние на основе текущей позиции камеры
    /// </summary>
    public void ForceResetAfterTeleport()
    {
        if (target == null) return;
        
        // ВАЖНО: Сначала получаем правильное направление от ThirdPersonCamera
        // Это гарантирует, что направление будет правильным после телепортации
        ThirdPersonCamera thirdPersonCamera = GetComponent<ThirdPersonCamera>();
        if (thirdPersonCamera == null)
        {
            thirdPersonCamera = FindFirstObjectByType<ThirdPersonCamera>();
        }
        
        // Ждем один кадр, чтобы ThirdPersonCamera успел обновить позицию камеры
        // Но так как это вызывается из корутины, камера уже должна быть обновлена
        
        // Вычисляем новое направление на основе текущей позиции камеры
        Vector3 directionToCamera = transform.position - target.position;
        float actualDistance = directionToCamera.magnitude;
        
        // ВАЖНО: Если камера находится в неправильной позиции, используем направление от ThirdPersonCamera
        // Это происходит когда камера еще не обновилась после телепортации
        if (actualDistance < 0.1f || actualDistance > defaultDistance * 2f)
        {
            // Используем направление камеры как основу
            if (transform.forward != Vector3.zero)
            {
                // Направление назад от камеры (к цели) - это правильное направление для lastValidDirection
                lastValidDirection = -transform.forward.normalized;
            }
            else
            {
                // Fallback: используем стандартное направление
                lastValidDirection = new Vector3(0f, 0.3f, -1f).normalized;
            }
        }
        else
        {
            // Обновляем lastValidDirection на основе текущей позиции камеры
            lastValidDirection = directionToCamera.normalized;
        }
        
        // КРИТИЧНО: Сбрасываем расстояние к стандартному значению
        // Это предотвращает камеру от приближения после телепортации
        currentDistance = defaultDistance;
        distanceVelocity = 0f; // Обнуляем скорость изменения расстояния
        
        // Принудительно устанавливаем позицию камеры на правильное расстояние
        // Это предотвращает быстрое приближение после телепортации
        Vector3 correctPosition = target.position + lastValidDirection * defaultDistance;
        transform.position = correctPosition;
        
        // ВАЖНО: После установки позиции пересчитываем направление еще раз
        // Это гарантирует, что lastValidDirection соответствует реальной позиции камеры
        directionToCamera = transform.position - target.position;
        if (directionToCamera.magnitude > 0.001f)
        {
            lastValidDirection = directionToCamera.normalized;
        }
    }
    
    /// <summary>
    /// Устанавливает стандартную дистанцию (синхронизация с ThirdPersonCamera)
    /// </summary>
    public void SetDefaultDistance(float distance)
    {
        defaultDistance = distance;
        // Ограничиваем текущее расстояние новым максимумом
        if (currentDistance > defaultDistance)
        {
            currentDistance = defaultDistance;
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (target == null) return;
        
        // Рисуем сферу вокруг камеры для визуализации радиуса коллизии
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, collisionRadius);
        
        // Рисуем линию от камеры к цели
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, target.position);
        
        // Рисуем сферу в точке цели
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(target.position, 0.2f);
        
        // Рисуем минимальную и максимальную дистанции
        Vector3 direction = (transform.position - target.position).normalized;
        if (direction.magnitude > 0.001f)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(target.position, minDistance);
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(target.position, defaultDistance);
        }
        
        // Рисуем направление SphereCast (от камеры к цели)
        if (showDebugRays)
        {
            Gizmos.color = Color.magenta;
            Vector3 directionToTarget = (target.position - transform.position).normalized;
            Gizmos.DrawRay(transform.position, directionToTarget * Vector3.Distance(transform.position, target.position));
        }
    }
}
