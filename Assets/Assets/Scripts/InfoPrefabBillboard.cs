using UnityEngine;

/// <summary>
/// Простой компонент для поворота префаба с данными к камере
/// </summary>
public class InfoPrefabBillboard : MonoBehaviour
{
    private Transform cameraTransform;
    private Transform myTransform;
    
    private void Awake()
    {
        myTransform = transform;
        
        // Находим главную камеру
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
    
    private void LateUpdate()
    {
        if (cameraTransform == null || myTransform == null) return;
        
        // Вычисляем направление от текста к камере
        Vector3 directionToCamera = cameraTransform.position - myTransform.position;
        
        // Если направление слишком маленькое, не поворачиваем
        if (directionToCamera.sqrMagnitude < 0.0001f) return;
        
        // Поворачиваем текст так, чтобы он смотрел на камеру
        // Используем LookRotation с инверсией направления, чтобы текст был правильно ориентирован
        myTransform.rotation = Quaternion.LookRotation(-directionToCamera);
    }
}
