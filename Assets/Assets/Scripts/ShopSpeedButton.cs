using UnityEngine;
using System.Collections;

/// <summary>
/// Скрипт для 3D кнопки открытия магазина скорости
/// При наступлении игрока на кнопку показывает SpeedModalContainer
/// При уходе игрока скрывает модальное окно
/// </summary>
[RequireComponent(typeof(Collider))]
public class ShopSpeedButton : MonoBehaviour
{
    [Header("References")]
    [Tooltip("SpeedModalContainer GameObject, который будет показан при наступлении на кнопку")]
    [SerializeField] private GameObject speedModalContainer;
    
    [Header("Settings")]
    [Tooltip("Скрывать SpeedModalContainer при старте (если true, контейнер будет скрыт в Start)")]
    [SerializeField] private bool hideOnStart = true;
    
    [Header("Player Detection")]
    [Tooltip("Тег игрока (по умолчанию 'Player')")]
    [SerializeField] private string playerTag = "Player";
    
    private void Awake()
    {
        // Проверяем наличие триггера
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            // Убеждаемся, что коллайдер настроен как триггер
            if (!col.isTrigger)
            {
                col.isTrigger = true;
            }
        }
    }
    
    private void Start()
    {
        // Автоматически находим SpeedModalContainer, если не назначен
        if (speedModalContainer == null)
        {
            // Метод 1: Ищем в сцене объект с именем "SpeedModalContainer" (только активные объекты)
            GameObject foundContainer = GameObject.Find("SpeedModalContainer");
            
            // Метод 2: Если не найден, ищем через Resources.FindObjectsOfTypeAll (включая неактивные)
            if (foundContainer == null)
            {
                // Используем более надежный метод поиска через все объекты в сцене
                UnityEngine.SceneManagement.Scene currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                GameObject[] rootObjects = currentScene.GetRootGameObjects();
                
                // Рекурсивно ищем во всех объектах сцены
                foundContainer = FindGameObjectInHierarchy(rootObjects, "SpeedModalContainer");
                
                if (foundContainer != null)
                {
                    Debug.Log($"[ShopSpeedButton] SpeedModalContainer найден через рекурсивный поиск: {foundContainer.name}, activeSelf={foundContainer.activeSelf}, activeInHierarchy={foundContainer.activeInHierarchy}");
                }
            }
            
            // Метод 3: Ищем через поиск по тегу или компоненту ShopSpeedManager
            if (foundContainer == null)
            {
                ShopSpeedManager speedManager = FindFirstObjectByType<ShopSpeedManager>();
                if (speedManager != null)
                {
                    // Ищем родителя с именем, содержащим "Speed" или "Modal"
                    Transform parent = speedManager.transform;
                    while (parent != null)
                    {
                        if (parent.name.Contains("Speed") || parent.name.Contains("Modal") || parent.name.Contains("Container"))
                        {
                            foundContainer = parent.gameObject;
                            Debug.Log($"[ShopSpeedButton] SpeedModalContainer найден через ShopSpeedManager: {parent.name}");
                            break;
                        }
                        parent = parent.parent;
                    }
                    
                    // Если не нашли по имени, используем прямой родитель ShopSpeedManager
                    if (foundContainer == null && speedManager.transform.parent != null)
                    {
                        foundContainer = speedManager.transform.parent.gameObject;
                        Debug.Log($"[ShopSpeedButton] SpeedModalContainer найден как родитель ShopSpeedManager: {foundContainer.name}");
                    }
                }
            }
            
            if (foundContainer != null)
            {
                speedModalContainer = foundContainer;
                Debug.Log($"[ShopSpeedButton] SpeedModalContainer найден автоматически: {foundContainer.name}, activeSelf={foundContainer.activeSelf}, activeInHierarchy={foundContainer.activeInHierarchy}");
            }
            else
            {
                Debug.LogError("[ShopSpeedButton] SpeedModalContainer не найден! Убедитесь, что объект существует в сцене с именем 'SpeedModalContainer' или назначьте его вручную в инспекторе.");
            }
        }
        else
        {
            Debug.Log($"[ShopSpeedButton] SpeedModalContainer назначен вручную: {speedModalContainer.name}");
        }
        
        // Проверяем коллайдер
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Debug.Log($"[ShopSpeedButton] Collider найден: isTrigger={col.isTrigger}, enabled={col.enabled}");
        }
        else
        {
            Debug.LogError("[ShopSpeedButton] Collider не найден! Добавьте Collider к объекту.");
        }
        
        // Скрываем контейнер при старте, если требуется
        if (speedModalContainer != null && hideOnStart)
        {
            speedModalContainer.SetActive(false);
            Debug.Log("[ShopSpeedButton] SpeedModalContainer скрыт при старте");
        }
    }
    
    /// <summary>
    /// Вызывается когда объект входит в триггер
    /// </summary>
    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"[ShopSpeedButton] OnTriggerEnter вызван: other={other.name}, tag={other.tag}, playerTag={playerTag}");
        
        // Проверяем, что это игрок
        if (other.CompareTag(playerTag))
        {
            Debug.Log($"[ShopSpeedButton] Игрок вошел в триггер, открываем магазин");
            OpenShop();
        }
        else
        {
            Debug.LogWarning($"[ShopSpeedButton] Объект с тегом '{other.tag}' вошел в триггер, но ожидался тег '{playerTag}'");
        }
    }
    
    /// <summary>
    /// Вызывается когда объект выходит из триггера
    /// </summary>
    private void OnTriggerExit(Collider other)
    {
        // Проверяем, что это игрок
        if (other.CompareTag(playerTag))
        {
            // Скрываем модальное окно при уходе игрока
            CloseShop();
        }
    }
    
    /// <summary>
    /// Публичный метод для открытия магазина (можно вызвать извне)
    /// </summary>
    public void OpenShop()
    {
        if (speedModalContainer != null)
        {
            Debug.Log($"[ShopSpeedButton] Открываем магазин: speedModalContainer={speedModalContainer.name}, activeSelf={speedModalContainer.activeSelf}, activeInHierarchy={speedModalContainer.activeInHierarchy}");
            
            // ВАЖНО: Проверяем, не находится ли модальное окно в неактивном родителе
            // Если родитель неактивен, активируем его тоже
            Transform parent = speedModalContainer.transform.parent;
            if (parent != null && !parent.gameObject.activeSelf)
            {
                Debug.LogWarning($"[ShopSpeedButton] Родитель модального окна неактивен: {parent.name}, активируем его");
                parent.gameObject.SetActive(true);
            }
            
            // Активируем модальное окно
            speedModalContainer.SetActive(true);
            
            Debug.Log($"[ShopSpeedButton] Модальное окно активировано: activeSelf={speedModalContainer.activeSelf}, activeInHierarchy={speedModalContainer.activeInHierarchy}");
            
            // Обновляем локализацию после активации с небольшой задержкой
            // Это необходимо, чтобы YG2 успел инициализироваться
            StartCoroutine(UpdateLocalizationDelayed());
        }
        else
        {
            Debug.LogError("[ShopSpeedButton] Не удалось открыть магазин: speedModalContainer == null");
        }
    }
    
    /// <summary>
    /// Обновляет все LocalizedText компоненты в модальном окне с небольшой задержкой
    /// Это необходимо, чтобы YG2 успел инициализироваться перед обновлением текстов
    /// Также обновляет цвета цен в зависимости от баланса
    /// </summary>
    private IEnumerator UpdateLocalizationDelayed()
    {
        // Ждем конец кадра, чтобы YG2 успел инициализироваться
        yield return new WaitForEndOfFrame();
        
        if (speedModalContainer != null && speedModalContainer.activeSelf)
        {
            // Находим все LocalizedText компоненты в модальном окне (включая дочерние объекты)
            LocalizedText[] localizedTexts = speedModalContainer.GetComponentsInChildren<LocalizedText>(true);
            
            foreach (LocalizedText localizedText in localizedTexts)
            {
                if (localizedText != null)
                {
                    // Обновляем текст с правильным языком
                    localizedText.UpdateText();
                }
            }
            
            // Обновляем цвета цен в ShopSpeedManager
            ShopSpeedManager speedManager = speedModalContainer.GetComponentInChildren<ShopSpeedManager>();
            if (speedManager != null)
            {
                // Вызываем метод обновления всех баров для обновления цветов цен
                speedManager.UpdateAllSpeedBars();
            }
        }
    }
    
    /// <summary>
    /// Публичный метод для закрытия магазина
    /// </summary>
    public void CloseShop()
    {
        if (speedModalContainer != null)
        {
            speedModalContainer.SetActive(false);
        }
    }
    
    /// <summary>
    /// Рекурсивно ищет GameObject по имени в иерархии
    /// </summary>
    private GameObject FindGameObjectInHierarchy(GameObject[] rootObjects, string name)
    {
        foreach (GameObject root in rootObjects)
        {
            GameObject found = FindGameObjectInTransform(root.transform, name);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Рекурсивно ищет GameObject по имени в Transform и его дочерних объектах
    /// </summary>
    private GameObject FindGameObjectInTransform(Transform parent, string name)
    {
        if (parent.name == name)
        {
            return parent.gameObject;
        }
        
        foreach (Transform child in parent)
        {
            GameObject found = FindGameObjectInTransform(child, name);
            if (found != null)
            {
                return found;
            }
        }
        
        return null;
    }
}
