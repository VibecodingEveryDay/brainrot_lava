using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// Компонент виртуального джойстика для мобильных устройств
/// </summary>
public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    [Header("Ссылки на элементы джойстика")]
    public RectTransform joystickBackground;
    public RectTransform joystickHandle;
    
    [Header("Настройки")]
    [SerializeField] private float maxDistance = 120f; // Максимальное расстояние движения ручки
    
    private Vector2 joystickInput = Vector2.zero;
    private bool isDragging = false;
    
    public System.Action<Vector2> OnJoystickInput;
    
    private void Update()
    {
        if (isDragging && OnJoystickInput != null)
        {
            OnJoystickInput.Invoke(joystickInput);
        }
        else if (!isDragging && joystickInput != Vector2.zero)
        {
            // Плавно возвращаем джойстик в центр при отпускании
            joystickInput = Vector2.Lerp(joystickInput, Vector2.zero, Time.deltaTime * 10f);
            if (joystickInput.magnitude < 0.01f)
            {
                joystickInput = Vector2.zero;
            }
            UpdateHandlePosition();
            
            if (OnJoystickInput != null)
            {
                OnJoystickInput.Invoke(joystickInput);
            }
        }
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        isDragging = true;
        OnDrag(eventData);
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (joystickBackground == null || joystickHandle == null) return;
        
        Vector2 localPoint;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            joystickBackground,
            eventData.position,
            eventData.pressEventCamera,
            out localPoint
        );
        
        // Ограничиваем движение ручки радиусом фона
        Vector2 clampedPoint = Vector2.ClampMagnitude(localPoint, maxDistance);
        
        // Обновляем позицию ручки
        joystickHandle.anchoredPosition = clampedPoint;
        
        // Вычисляем нормализованный ввод (-1 до 1)
        joystickInput = clampedPoint / maxDistance;
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        isDragging = false;
        joystickInput = Vector2.zero;
        
        // Возвращаем ручку в центр
        if (joystickHandle != null)
        {
            joystickHandle.anchoredPosition = Vector2.zero;
        }
        
        if (OnJoystickInput != null)
        {
            OnJoystickInput.Invoke(Vector2.zero);
        }
    }
    
    private void UpdateHandlePosition()
    {
        if (joystickHandle != null)
        {
            joystickHandle.anchoredPosition = joystickInput * maxDistance;
        }
    }
    
    /// <summary>
    /// Установить видимость джойстика
    /// </summary>
    public void SetJoystickVisible(bool visible)
    {
        if (joystickBackground != null)
        {
            joystickBackground.gameObject.SetActive(visible);
        }
        if (joystickHandle != null)
        {
            joystickHandle.gameObject.SetActive(visible);
        }
    }
    
    /// <summary>
    /// Получить текущий ввод джойстика
    /// </summary>
    public Vector2 GetInput()
    {
        return joystickInput;
    }
}
