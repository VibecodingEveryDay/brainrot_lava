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
    
    [Tooltip("Плавность высоты пола (устраняет дёргание на ступеньках)")]
    [SerializeField] private float floorSmoothTime = 0.15f;
    
    [Tooltip("Плавность вертикальной позиции камеры по Y")]
    [SerializeField] private float verticalSmoothTime = 0.08f;
    
    [Header("Collision Control")]
    [Tooltip("Включать ли обработку столкновений камеры со стенами. Если выключено, камера только следует за ThirdPersonCamera.")]
    [SerializeField] private bool enableCollisionHandling = false;
    
    [Header("Debug")]
    [Tooltip("Показывать лучи в редакторе")]
    [SerializeField] private bool showDebugRays = false;
    
    private const string WallBallTag = "WallBall";
    private const string StartFinishTag = "StartFinish";
    private const string PlayerTag = "Player";
    
    // Текущее расстояние камеры (изменяется при столкновениях)
    private float currentDistance;
    
    // Для SmoothDamp
    private float distanceVelocity;
    
    // Сглаживание высоты пола (ступеньки)
    private float smoothedFloorHeight;
    private float floorHeightVelocity;
    private bool floorHeightInitialized;
    
    // Сглаживание вертикальной позиции камеры
    private float lastCameraY;
    private float cameraYVelocity;
    private bool cameraYInitialized;
    
    // Кэш компонента камеры
    private Camera cam;
    
    // Сохраненное направление обзора (для сохранения угла камеры)
    private Vector3 lastValidDirection;
    
    // Ссылка на ThirdPersonCamera для получения стандартной дистанции
    private ThirdPersonCamera thirdPersonCamera;
    // Ссылка на контроллер игрока, чтобы знать, когда он падает
    private ThirdPersonController playerController;
    
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

                // Кэшируем контроллер игрока
                playerController = player.GetComponent<ThirdPersonController>();
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
        if (!enableCollisionHandling) return;
        if (target == null) return;

        // Во время падения не применяем коллизионную логику камеры,
        // чтобы не перебивать преднамеренное отдаление камеры при падении.
        if (playerController != null && playerController.IsFallingForCamera())
        {
            return;
        }
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
        
        // Максимальная дистанция для коллизий основана на собственном defaultDistance
        // (не завязана напрямую на текущей дистанции ThirdPersonCamera, чтобы избежать циклов).
        float effectiveMaxDistance = defaultDistance * (ModalOverlayManager.IsAnyModalOpen ? 1.25f : 1f);
        
        float desiredDistance = CheckForObstacles(directionToTarget, effectiveMaxDistance);
        
        if (Mathf.Abs(desiredDistance - effectiveMaxDistance) < 0.01f && currentDistance < effectiveMaxDistance - 0.1f)
        {
            // Препятствий нет, но камера слишком близко — плавно отдаляем
        }
        
        currentDistance = Mathf.SmoothDamp(currentDistance, desiredDistance, ref distanceVelocity, smoothTime);
        currentDistance = Mathf.Clamp(currentDistance, minDistance, effectiveMaxDistance);
        
        // Вычисляем новую позицию камеры, сохраняя направление обзора
        // Используем lastValidDirection для сохранения угла обзора
        Vector3 newPosition = target.position + lastValidDirection * currentDistance;
        
        // Дополнительная проверка: убеждаемся, что камера не под полом/потолком
        // Проверяем вертикальные препятствия сверху и снизу
        newPosition = CheckVerticalObstacles(newPosition);
        
        // Сглаживание по Y только когда камера ограничена полом (ступеньки), иначе — без задержки
        const float floorConstraintTolerance = 0.35f;
        bool cameraConstrainedByFloor = floorHeightInitialized && newPosition.y <= smoothedFloorHeight + floorConstraintTolerance;
        
        if (!cameraYInitialized)
        {
            lastCameraY = newPosition.y;
            cameraYVelocity = 0f;
            cameraYInitialized = true;
        }
        else if (cameraConstrainedByFloor)
        {
            lastCameraY = Mathf.SmoothDamp(lastCameraY, newPosition.y, ref cameraYVelocity, verticalSmoothTime);
            newPosition.y = lastCameraY;
        }
        else
        {
            lastCameraY = newPosition.y;
            cameraYVelocity = 0f;
        }
        
        // Устанавливаем скорректированную позицию камеры
        transform.position = newPosition;
    }
    
    /// <summary>
    /// Дополнительная проверка тонким лучом от цели к камере — гарантирует попадание в меш ступенек
    /// и тонкие грани, которые SphereCast может пропустить.
    /// </summary>
    private float GetRaycastSafeDistanceFromTarget(float maxDistance)
    {
        if (target == null) return maxDistance;
        Vector3 dirFromTargetToCamera = (transform.position - target.position);
        float len = dirFromTargetToCamera.magnitude;
        if (len < 0.001f) return maxDistance;
        dirFromTargetToCamera /= len;
        RaycastHit hit;
        if (RaycastIgnoreWallBall(target.position, dirFromTargetToCamera, maxDistance, out hit))
        {
            float safe = hit.distance - collisionOffset;
            safe = Mathf.Clamp(safe, minDistance, maxDistance);
            if (showDebugRays)
                Debug.DrawRay(target.position, dirFromTargetToCamera * hit.distance, Color.cyan);
            return safe;
        }
        return maxDistance;
    }
    
    private static bool IsIgnoredObstacle(Collider c)
    {
        if (c == null) return true;
        // Игнорируем специальные объекты, которые не должны влиять на позицию камеры
        if (c.CompareTag(WallBallTag)) return true;
        if (c.CompareTag(StartFinishTag)) return true;
        // Игнорируем коллайдеры игрока и его дочерних объектов
        Transform root = c.transform.root;
        if (root != null && root.CompareTag(PlayerTag)) return true;
        return false;
    }
    
    private bool RaycastIgnoreWallBall(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit hit)
    {
        RaycastHit[] hits = Physics.RaycastAll(origin, direction, maxDistance, obstacleMask, QueryTriggerInteraction.Ignore);
        hit = default;
        float bestDist = maxDistance + 1f;
        for (int i = 0; i < hits.Length; i++)
        {
            if (IsIgnoredObstacle(hits[i].collider)) continue;
            if (hits[i].distance < bestDist) { bestDist = hits[i].distance; hit = hits[i]; }
        }
        return hit.collider != null;
    }
    
    private bool SphereCastIgnoreWallBall(Vector3 origin, float radius, Vector3 direction, out RaycastHit hit, float maxDistance)
    {
        RaycastHit[] hits = Physics.SphereCastAll(origin, radius, direction, maxDistance, obstacleMask, QueryTriggerInteraction.Ignore);
        hit = default;
        float bestDist = maxDistance + 1f;
        for (int i = 0; i < hits.Length; i++)
        {
            if (IsIgnoredObstacle(hits[i].collider)) continue;
            if (hits[i].distance < bestDist) { bestDist = hits[i].distance; hit = hits[i]; }
        }
        return hit.collider != null;
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
        
        if (distanceToTarget >= maxDistance - 0.1f)
        {
            Vector3 directionFromTarget = -directionToTarget;
            RaycastHit quickCheck;
            if (!SphereCastIgnoreWallBall(target.position, collisionRadius, directionFromTarget, out quickCheck, maxDistance))
            {
                return GetRaycastSafeDistanceFromTarget(maxDistance);
            }
        }
        
        // ВАЖНО: Сначала проверяем, не находится ли камера уже внутри препятствия
        // Это решает проблему, когда камера находится внутри полого блока
        
        // Метод 1: OverlapSphere для проверки пересечения с коллайдерами
        Collider[] overlappingCollidersRaw = Physics.OverlapSphere(
            rayStart,
            collisionRadius, // Используем полный радиус для проверки
            obstacleMask,
            QueryTriggerInteraction.Ignore
        );
        int overlappingCount = 0;
        for (int o = 0; o < overlappingCollidersRaw.Length; o++)
            if (!IsIgnoredObstacle(overlappingCollidersRaw[o])) overlappingCount++;
        bool isInsideObstacle = overlappingCount > 0;
        
        if (!isInsideObstacle)
        {
            // Проверяем через короткий Raycast к цели
            // Если препятствие обнаружено сразу (расстояние < 0.1f), камера внутри
            RaycastHit immediateHit;
            if (RaycastIgnoreWallBall(rayStart, directionToTarget, 0.1f, out immediateHit))
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
            bool hasHitFromCamera = RaycastIgnoreWallBall(rayStart, directionToTarget, distanceToTarget, out hitFromCamera);
            
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
                
                return GetRaycastSafeDistanceFromTarget(safeDistance);
            }
            else
            {
                // Камера внутри препятствия, но между камерой и целью препятствий нет
                // Это означает, что камера находится сзади препятствия (персонаж виден)
                // Используем обратный SphereCast от цели к камере, чтобы найти ближайшую безопасную позицию
                
                Vector3 reverseDirection = -directionToTarget; // От цели к камере
                RaycastHit reverseHit;
                
                // Выполняем SphereCast от цели в направлении камеры
                bool hasReverseHit = SphereCastIgnoreWallBall(target.position, collisionRadius, reverseDirection, out reverseHit, distanceToTarget);
                
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
                    
                    return GetRaycastSafeDistanceFromTarget(safeDistance);
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
                    
                    return GetRaycastSafeDistanceFromTarget(aggressiveDistance);
                }
            }
        }
        
        // Камера не внутри препятствия, выполняем обычный SphereCast
        // Используем небольшой отступ от камеры, чтобы избежать самопересечения
        Vector3 adjustedStart = rayStart + directionToTarget * collisionRadius;
        float adjustedDistance = distanceToTarget - collisionRadius;
        
        RaycastHit hit;
        bool hasHit = SphereCastIgnoreWallBall(adjustedStart, collisionRadius, directionToTarget, out hit, adjustedDistance);
        
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
            
            // Учитываем тонкий луч (ступеньки, грани меша) — камера не зайдёт за геометрию
            return GetRaycastSafeDistanceFromTarget(safeDistance);
        }
        
        return GetRaycastSafeDistanceFromTarget(maxDistance);
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
        
        if (RaycastIgnoreWallBall(target.position, upDirection, maxUpDistance, out hitUp))
        {
            float ceilingHeight = hitUp.point.y - collisionOffset;
            if (desiredPosition.y > ceilingHeight)
            {
                desiredPosition.y = ceilingHeight;
            }
        }
        
        // === УЛУЧШЕННАЯ ПРОВЕРКА ПОЛА (со сглаживанием для ступенек) ===
        
        const float minFloorNormalY = 0.7f; // Только горизонтальные поверхности (исключаем стенки ступенек)
        
        // 1. Проверяем пол под target (основной пол уровня)
        RaycastHit hitDownFromTarget;
        Vector3 downDirection = Vector3.down;
        float maxDownDistance = 50f;
        
        float floorHeight = float.MinValue;
        
        if (RaycastIgnoreWallBall(target.position, downDirection, maxDownDistance, out hitDownFromTarget))
        {
            if (hitDownFromTarget.normal.y >= minFloorNormalY)
                floorHeight = hitDownFromTarget.point.y + collisionOffset;
        }
        
        // 2. Проверяем пол под желаемой позицией камеры
        RaycastHit hitDownFromCamera;
        if (RaycastIgnoreWallBall(desiredPosition + Vector3.up * 2f, downDirection, maxDownDistance, out hitDownFromCamera))
        {
            if (hitDownFromCamera.normal.y >= minFloorNormalY)
            {
                float cameraFloorHeight = hitDownFromCamera.point.y + collisionOffset;
                if (cameraFloorHeight > floorHeight)
                    floorHeight = cameraFloorHeight;
            }
        }
        
        // 2.1 Сглаживаем высоту пола (устраняет дёргание на ступеньках)
        if (floorHeight > float.MinValue)
        {
            if (!floorHeightInitialized)
            {
                smoothedFloorHeight = floorHeight;
                floorHeightVelocity = 0f;
                floorHeightInitialized = true;
            }
            else
            {
                smoothedFloorHeight = Mathf.SmoothDamp(smoothedFloorHeight, floorHeight, ref floorHeightVelocity, floorSmoothTime);
            }
            if (desiredPosition.y < smoothedFloorHeight)
                desiredPosition.y = smoothedFloorHeight;
        }
        
        // 3. Проверяем, не находится ли камера уже под полом (SphereCast вверх)
        RaycastHit hitUpFromCamera;
        if (SphereCastIgnoreWallBall(desiredPosition, collisionRadius, upDirection, out hitUpFromCamera, 5f))
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
        
        // 4. Дополнительно: проверяем OverlapSphere для обнаружения пересечения с любыми коллайдерами
        Collider[] overlaps = Physics.OverlapSphere(desiredPosition, collisionRadius, obstacleMask, QueryTriggerInteraction.Ignore);
        if (overlaps.Length > 0)
        {
            // Камера пересекается с чем-то - пробуем поднять её (игнорируем WallBall)
            foreach (Collider col in overlaps)
            {
                if (IsIgnoredObstacle(col)) continue;
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
        
        // Сбрасываем сглаживание пола и по Y, чтобы после телепортации значения пересчитались
        floorHeightInitialized = false;
        cameraYInitialized = false;

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
