using System.Collections;
using UnityEngine;

/// <summary>
/// Пушка: с заданным интервалом выстреливает префаб шара в направлении по angleX/angleY с заданной силой.
/// Шар должен иметь Rigidbody и коллайдер (с Physic Material для отскоков).
/// </summary>
public class BallSpawner : MonoBehaviour
{
    [Header("Prefab")]
    [Tooltip("Префаб шара с Rigidbody и коллайдером")]
    [SerializeField] private GameObject ballPrefab;

    [Header("Direction (degrees)")]
    [SerializeField] private float angleX = 25f;
    [SerializeField] private float angleY = 0f;

    [Header("Aim at player")]
    [Tooltip("С заданным шансом направление выстрела — в сторону игрока (игнорируются angleX, angleY)")]
    [SerializeField] private bool angleToPlayer = false;
    [Range(0f, 1f)]
    [SerializeField] private float chance = 0.3f;
    [Tooltip("Смещение угла по вертикали при прицеле в игрока (градусы). Положительное — выше, отрицательное — ниже.")]
    [SerializeField] private float toPlayerAngleOffsetX = 0f;

    [Header("Shot — случайное значение между min и max для каждого выстрела")]
    [SerializeField] private float powerMin = 12f;
    [SerializeField] private float powerMax = 20f;

    [Header("Interval (сек) — случайная пауза между выстрелами")]
    [SerializeField] private float intervalMin = 2f;
    [SerializeField] private float intervalMax = 5f;

    [Header("Scale (доля от масштаба префаба, 1 = 100%)")]
    [SerializeField] private float scaleMin = 0.8f;
    [SerializeField] private float scaleMax = 1.2f;

    [Header("Stop spawning")]
    [Tooltip("Если Z игрока > Z этого спавнера - этот параметр, новые шары больше не спавнятся")]
    [SerializeField] private float stopSpawnOffsetZ = 0f;
    [Tooltip("Максимальная дистанция до игрока, при превышении которой шары временно не спавнятся")]
    [SerializeField] private float maxDistanceFromPlayer = 400f;

    [Header("Optional")]
    [Tooltip("Точка спавна шара. Если не задана — позиция этого объекта")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("Уничтожать шар через N секунд после спавна (0 = не уничтожать)")]
    [SerializeField] private float destroyBallAfterSeconds = 0f;
    [Tooltip("Макс. шаров в сцене (0 = без лимита). Превышение — не спавнить новый (снижает нагрузку на физику).")]
    [SerializeField] private int maxBallsInScene = 0;

    private Coroutine _spawnCoroutine;
    private Transform _playerTransform;
    private float _maxDistanceSqr;

    private void OnEnable()
    {
        if (_playerTransform == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) _playerTransform = p.transform;
        }
        _maxDistanceSqr = maxDistanceFromPlayer > 0f ? maxDistanceFromPlayer * maxDistanceFromPlayer : float.MaxValue;
        _spawnCoroutine = StartCoroutine(SpawnLoop());
    }

    private void OnDisable()
    {
        if (_spawnCoroutine != null)
        {
            StopCoroutine(_spawnCoroutine);
            _spawnCoroutine = null;
        }
    }

    private IEnumerator SpawnLoop()
    {
        if (ballPrefab == null)
        {
            Debug.LogWarning("[BallSpawner] ballPrefab не назначен.", this);
            yield break;
        }

        while (true)
        {
            // Ждём, пока игрок в допустимой зоне (по Z и по дистанции)
            if (!CanSpawnNow())
            {
                yield return new WaitForSeconds(0.2f);
                continue;
            }

            SpawnBall();

            float delay = Random.Range(intervalMin, intervalMax);
            float elapsed = 0f;
            while (elapsed < delay)
            {
                if (!CanSpawnNow())
                    break;
                elapsed += 0.2f;
                yield return new WaitForSeconds(0.2f);
            }
        }
    }

    private void SpawnBall()
    {
        if (ballPrefab == null) return;
        if (maxBallsInScene > 0)
        {
            Ball[] balls = FindObjectsByType<Ball>(FindObjectsSortMode.None);
            if (balls != null && balls.Length >= maxBallsInScene)
                return;
        }

        float power = Random.Range(powerMin, powerMax);
        Vector3 position = spawnPoint != null ? spawnPoint.position : transform.position;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        GameObject ball = Instantiate(ballPrefab, position, rotation);

        // Масштаб: случайный процент от масштаба префаба
        float scalePercent = Random.Range(scaleMin, scaleMax);
        ball.transform.localScale = ballPrefab.transform.localScale * scalePercent;

        Vector3 direction;
        if (angleToPlayer && Random.value < chance && _playerTransform != null)
        {
            Vector3 toPlayer = _playerTransform.position - position;
            if (toPlayer.sqrMagnitude > 0.01f)
            {
                direction = toPlayer.normalized;
                if (Mathf.Abs(toPlayerAngleOffsetX) > 0.001f)
                {
                    Vector3 right = Vector3.Cross(Vector3.up, direction);
                    if (right.sqrMagnitude > 0.001f)
                    {
                        right.Normalize();
                        direction = Quaternion.AngleAxis(toPlayerAngleOffsetX, right) * direction;
                    }
                }
            }
            else
                direction = GetShotDirection(angleX, angleY);
        }
        else
        {
            direction = GetShotDirection(angleX, angleY);
        }

        Rigidbody rb = ball.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(direction * power, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * (power * 0.1f), ForceMode.Impulse);
        }
        else
        {
            Debug.LogWarning("[BallSpawner] У префаба шара нет Rigidbody — физика не будет применена.", ball);
        }

        if (destroyBallAfterSeconds > 0f)
        {
            Destroy(ball, destroyBallAfterSeconds);
        }
    }

    /// <summary>
    /// Можно ли сейчас спавнить шары (по Z и по дистанции до игрока).
    /// </summary>
    private bool CanSpawnNow()
    {
        if (_playerTransform == null)
        {
            return false;
        }

        if (ShouldStopSpawningByPlayerZ())
        {
            return false;
        }

        if (IsPlayerTooFar())
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Возвращает true, если игрок по оси Z ушёл дальше спавнера (с учётом смещения), и нужно временно остановить спавн.
    /// </summary>
    private bool ShouldStopSpawningByPlayerZ()
    {
        float limitZ = transform.position.z - stopSpawnOffsetZ;
        return _playerTransform.position.z > limitZ;
    }

    /// <summary>
    /// Возвращает true, если игрок слишком далеко от спавнера по 3D-дистанции.
    /// </summary>
    private bool IsPlayerTooFar()
    {
        if (maxDistanceFromPlayer <= 0f)
            return false;
        Vector3 d = _playerTransform.position - transform.position;
        return d.sqrMagnitude > _maxDistanceSqr;
    }

    /// <summary>
    /// Направление выстрела в мировых координатах с учётом ориентации пушки и углов angleX, angleY.
    /// </summary>
    private Vector3 GetShotDirection(float angleX, float angleY)
    {
        Vector3 baseDirection = transform.forward;
        Quaternion yaw = Quaternion.AngleAxis(angleY, Vector3.up);
        Vector3 right = yaw * transform.right;
        Quaternion pitch = Quaternion.AngleAxis(angleX, right);
        Vector3 direction = pitch * yaw * baseDirection;
        return direction.normalized;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        float power = (powerMin + powerMax) * 0.5f;
        Vector3 origin = spawnPoint != null ? spawnPoint.position : transform.position;
        Vector3 dir = GetShotDirection(angleX, angleY);
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(origin, dir * Mathf.Min(power * 0.5f, 5f));
    }
#endif
}
