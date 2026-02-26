using UnityEngine;

/// <summary>
/// Плавная анимация платформы: лёгкое покачивание по rotation X и левитация по Y.
/// Для паркура — летающие платформы.
/// </summary>
public class PlatformsAnimate : MonoBehaviour
{
    [Header("Левитация (Y)")]
    [Tooltip("Амплитуда подъёма/опускания в метрах")]
    [SerializeField] private float levitateAmplitude = 0.15f;
    [Tooltip("Скорость левитации")]
    [SerializeField] private float levitateSpeed = 1.5f;
    [Tooltip("Смещение фазы (0–2π), чтобы платформы не двигались синхронно")]
    [SerializeField] private float levitatePhase;

    [Header("Покачивание (Rotation X)")]
    [Tooltip("Амплитуда наклона в градусах")]
    [SerializeField] private float tiltAmplitude = 3f;
    [Tooltip("Скорость покачивания")]
    [SerializeField] private float tiltSpeed = 2f;
    [Tooltip("Смещение фазы")]
    [SerializeField] private float tiltPhase;

    private Vector3 _startPosition;
    private Quaternion _startRotation;

    private void Start()
    {
        _startPosition = transform.position;
        _startRotation = transform.rotation;
    }

    private void Update()
    {
        float t = Time.time;

        float y = _startPosition.y + Mathf.Sin(t * levitateSpeed + levitatePhase) * levitateAmplitude;
        transform.position = new Vector3(_startPosition.x, y, _startPosition.z);

        float angleX = _startRotation.eulerAngles.x + Mathf.Sin(t * tiltSpeed + tiltPhase) * tiltAmplitude;
        transform.rotation = Quaternion.Euler(angleX, _startRotation.eulerAngles.y, _startRotation.eulerAngles.z);
    }
}
