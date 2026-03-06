using UnityEngine;

/// <summary>
/// Контроллер переноски брейнрота для бота. Обновляет позицию объекта в LateUpdate относительно трансформа бота.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class BotCarryController : MonoBehaviour, ICarryController
{
    [Header("Настройки переноски")]
    [Tooltip("Смещение объекта относительно бота (X, Y, Z)")]
    [SerializeField] private Vector3 holdPointOffset = new Vector3(0f, 1f, 1.5f);
    
    [Tooltip("Скорость следования объекта за ботом (0 = мгновенное, >0 = плавное)")]
    [SerializeField] private float followSpeed = 10f;
    
    [Tooltip("Поворачивать объект вместе с ботом")]
    [SerializeField] private bool rotateWithCarrier = true;
    
    private BrainrotObject currentCarriedObject;
    private Transform carrierTransform;
    private Vector3 cachedOffsets = Vector3.zero;
    private float cachedRotationY = 0f;
    private bool offsetsCached = false;
    
    private void Awake()
    {
        carrierTransform = transform;
    }
    
    private void LateUpdate()
    {
        // Работаем только на боте: не двигаем объекты, если этот контроллер висит на игроке или другом объекте
        if (GetComponent<BotBehavior>() == null)
        {
            if (currentCarriedObject != null)
            {
                currentCarriedObject = null;
                offsetsCached = false;
            }
            return;
        }
        
        if (currentCarriedObject != null && currentCarriedObject.IsCarried())
        {
            // Не перемещаем брейнрот, если он размещён на панели (игрок только что положил его в placement)
            if (PlacementPanel.IsBrainrotPlacedOnPanel(currentCarriedObject))
            {
                currentCarriedObject = null;
                offsetsCached = false;
                return;
            }
            UpdateCarriedObjectPosition();
        }
        else if (currentCarriedObject != null && !currentCarriedObject.IsCarried())
        {
            currentCarriedObject = null;
            offsetsCached = false;
        }
    }
    
    private void UpdateCarriedObjectPosition()
    {
        if (currentCarriedObject == null || carrierTransform == null) return;
        
        if (!offsetsCached)
        {
            float offsetX = currentCarriedObject.GetCarryOffsetX();
            float offsetY = currentCarriedObject.GetCarryOffsetY();
            float offsetZ = currentCarriedObject.GetCarryOffsetZ();
            if (Mathf.Approximately(offsetX, 0f)) offsetX = holdPointOffset.x;
            if (Mathf.Approximately(offsetY, 0f)) offsetY = holdPointOffset.y;
            if (Mathf.Approximately(offsetZ, 0f)) offsetZ = holdPointOffset.z;
            cachedOffsets = new Vector3(offsetX, offsetY, offsetZ);
            cachedRotationY = currentCarriedObject.GetCarryRotationY();
            offsetsCached = true;
        }
        
        Vector3 targetPosition = carrierTransform.position +
            carrierTransform.forward * cachedOffsets.z +
            carrierTransform.up * cachedOffsets.y +
            carrierTransform.right * cachedOffsets.x;
        
        if (followSpeed > 0f)
        {
            float maxDistanceDelta = followSpeed * Time.deltaTime * 100f;
            currentCarriedObject.transform.position = Vector3.MoveTowards(
                currentCarriedObject.transform.position, targetPosition, maxDistanceDelta);
        }
        else
            currentCarriedObject.transform.position = targetPosition;
        
        if (rotateWithCarrier)
        {
            if (Mathf.Approximately(cachedRotationY, 0f))
                currentCarriedObject.transform.rotation = carrierTransform.rotation;
            else
                currentCarriedObject.transform.rotation = carrierTransform.rotation * Quaternion.Euler(0f, cachedRotationY, 0f);
        }
    }
    
    public void CarryObject(BrainrotObject obj)
    {
        if (obj == null) return;
        if (obj.IsUnfought()) return;
        if (currentCarriedObject != null) return;
        
        // ВАЖНО: снимаем этот брейнрот с игрока и с других ботов, чтобы только один носитель двигал объект
        PlayerCarryController playerCarry = FindFirstObjectByType<PlayerCarryController>();
        if (playerCarry != null && playerCarry.GetCurrentCarriedObject() == obj)
            playerCarry.DropObject();
        BotCarryController[] allBots = FindObjectsByType<BotCarryController>(FindObjectsSortMode.None);
        for (int i = 0; i < allBots.Length; i++)
        {
            if (allBots[i] != null && allBots[i] != this && allBots[i].GetCurrentCarriedObject() == obj)
                allBots[i].DropObject();
        }
        
        currentCarriedObject = obj;
        offsetsCached = false;
    }
    
    public void DropObject()
    {
        if (currentCarriedObject == null) return;
        currentCarriedObject = null;
        offsetsCached = false;
    }
    
    public bool CanCarry() => currentCarriedObject == null;
    public BrainrotObject GetCurrentCarriedObject() => currentCarriedObject;
    public Transform GetCarrierTransform() => carrierTransform;
}
