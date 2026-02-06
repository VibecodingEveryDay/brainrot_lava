using UnityEngine;

/// <summary>
/// Панель для удаления размещённого брейнрота.
/// При клике на панель удаляет брейнрот из PlacementPanel и из GameStorage.
/// </summary>
public class TrashPlacement : MonoBehaviour
{
    [Header("Настройки панели")]
    [Tooltip("Ссылка на GradePanel (для получения PlacementPanel и брейнрота)")]
    [SerializeField] private GradePanel gradePanel;
    
    [Tooltip("Временно отключить автоматическое скрытие панели (для тестирования). Видимость берётся из GradePanel.")]
    [SerializeField] private bool disableAutoHide = false;
    
    // Transform игрока (находится автоматически, не показывается в Inspector)
    private Transform playerTransform;
    
    // Ссылка на связанную PlacementPanel (получается через GradePanel)
    private PlacementPanel linkedPlacementPanel;
    
    // Ссылка на размещённый brainrot объект
    private BrainrotObject placedBrainrot;
    
    // Кэш для всех Renderer компонентов (для визуального скрытия)
    private Renderer[] renderers;
    
    // Кэш для всех Collider компонентов (для отключения взаимодействия)
    private Collider[] colliders;
    
    // Флаг для отслеживания предыдущего состояния видимости
    private bool wasVisible = true;
    
    private void Awake()
    {
        // Пытаемся найти игрока автоматически, если не назначен
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
        
        // Кэшируем все Renderer компоненты для визуального скрытия
        renderers = GetComponentsInChildren<Renderer>(true);
        
        // Кэшируем все Collider компоненты для отключения взаимодействия
        colliders = GetComponentsInChildren<Collider>(true);
    }
    
    private void Start()
    {
        // Получаем PlacementPanel через GradePanel
        if (gradePanel != null)
        {
            // Используем рефлексию для получения linkedPlacementPanel из GradePanel
            var gradePanelType = typeof(GradePanel);
            var placementPanelField = gradePanelType.GetField("linkedPlacementPanel", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (placementPanelField != null)
            {
                linkedPlacementPanel = placementPanelField.GetValue(gradePanel) as PlacementPanel;
            }
        }
    }
    
    private void Update()
    {
        // Обновляем ссылку на размещённый brainrot объект
        UpdatePlacedBrainrot();
        
        // Проверяем видимость панели (расстояние до игрока и наличие брейнрота)
        UpdatePanelVisibility();
        
        // Обрабатываем клик мыши через Raycast (всегда, так как объект активен)
        HandleMouseClick();
    }
    
    /// <summary>
    /// Обновляет ссылку на размещённый brainrot объект
    /// </summary>
    private void UpdatePlacedBrainrot()
    {
        if (linkedPlacementPanel == null)
        {
            // Пытаемся получить PlacementPanel через GradePanel снова
            if (gradePanel != null)
            {
                var gradePanelType = typeof(GradePanel);
                var placementPanelField = gradePanelType.GetField("linkedPlacementPanel", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (placementPanelField != null)
                {
                    linkedPlacementPanel = placementPanelField.GetValue(gradePanel) as PlacementPanel;
                }
            }
            
            if (linkedPlacementPanel == null)
            {
                placedBrainrot = null;
                return;
            }
        }
        
        // Получаем размещённый brainrot из связанной панели
        placedBrainrot = linkedPlacementPanel.GetPlacedBrainrot();
    }
    
    /// <summary>
    /// Обновляет видимость панели, используя данные из GradePanel (чтобы не дублировать проверки)
    /// </summary>
    private void UpdatePanelVisibility()
    {
        // Получаем информацию о видимости из GradePanel
        bool shouldBeVisible = false;
        
        if (gradePanel != null)
        {
            // Проверяем, видима ли GradePanel визуально
            bool gradePanelVisible = gradePanel.IsVisuallyVisible();
            
            // Если GradePanel скрыта, то и TrashPlacement должен быть скрыт
            if (!gradePanelVisible)
            {
                shouldBeVisible = false;
            }
            else
            {
                // Если GradePanel видима, используем метод ShouldBeVisible() из GradePanel
                shouldBeVisible = gradePanel.ShouldBeVisible();
                
                // Если автоскрытие отключено в TrashPlacement, переопределяем
                if (disableAutoHide)
                {
                    shouldBeVisible = placedBrainrot != null;
                }
            }
        }
        else
        {
            // Если GradePanel не назначена, используем старую логику как запасной вариант
            bool hasBrainrot = placedBrainrot != null;
            shouldBeVisible = disableAutoHide || hasBrainrot;
        }
        
        // Обновляем видимость панели (визуально, но объект остаётся активным)
        SetPanelVisibility(shouldBeVisible);
        
        // Отслеживаем изменение видимости
        if (wasVisible != shouldBeVisible && Time.frameCount > 1)
        {
            wasVisible = shouldBeVisible;
        }
    }
    
    /// <summary>
    /// Устанавливает визуальную видимость панели (объект остаётся активным)
    /// </summary>
    private void SetPanelVisibility(bool visible)
    {
        // Скрываем/показываем все Renderer компоненты
        if (renderers != null)
        {
            foreach (Renderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }
        
        // Отключаем/включаем все Collider компоненты (чтобы нельзя было кликнуть когда скрыто)
        if (colliders != null)
        {
            foreach (Collider col in colliders)
            {
                if (col != null)
                {
                    col.enabled = visible;
                }
            }
        }
    }
    
    /// <summary>
    /// Обрабатывает клик мыши через Raycast
    /// </summary>
    private void HandleMouseClick()
    {
        // Проверяем, была ли нажата левая кнопка мыши
        if (!Input.GetMouseButtonDown(0))
        {
            return;
        }
        
        // Получаем камеру
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindFirstObjectByType<Camera>();
        }
        
        if (mainCamera == null)
        {
            return;
        }
        
        // Создаём луч из позиции мыши
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        
        // Проверяем, попал ли луч в коллайдер этого объекта или его дочерних объектов
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            col = GetComponentInChildren<Collider>();
        }
        
        if (col == null)
        {
            return;
        }
        
        // Проверяем пересечение луча с коллайдером
        if (col.Raycast(ray, out hit, Mathf.Infinity))
        {
            // Клик попал в панель - обрабатываем удаление
            ProcessDeletion();
        }
    }
    
    /// <summary>
    /// Обрабатывает удаление брейнрота
    /// </summary>
    private void ProcessDeletion()
    {
        // Проверяем, что есть размещённый брейнрот
        if (placedBrainrot == null)
        {
            return;
        }
        
        string brainrotName = placedBrainrot.GetObjectName();
        
        // Удаляем брейнрот из GameStorage
        if (GameStorage.Instance != null)
        {
            // Удаляем из списка всех брейнротов
            GameStorage.Instance.RemoveBrainrotByName(brainrotName);
            
            // Удаляем из списка размещенных брейнротов (если есть linkedPlacementPanel)
            if (linkedPlacementPanel != null)
            {
                int panelID = linkedPlacementPanel.GetPanelID();
                GameStorage.Instance.RemovePlacedBrainrot(panelID);
            }
        }
        
        // Удаляем объект из сцены
        Destroy(placedBrainrot.gameObject);
        
        // Очищаем ссылку в PlacementPanel
        if (linkedPlacementPanel != null)
        {
            // Используем рефлексию для доступа к приватному полю placedBrainrot
            var placementPanelType = typeof(PlacementPanel);
            var placedBrainrotField = placementPanelType.GetField("placedBrainrot", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (placedBrainrotField != null)
            {
                placedBrainrotField.SetValue(linkedPlacementPanel, null);
            }
        }
        
        // Очищаем ссылку
        placedBrainrot = null;
        
        Debug.Log($"[TrashPlacement] Брейнрот '{brainrotName}' удален из мира и из storage");
    }
}
