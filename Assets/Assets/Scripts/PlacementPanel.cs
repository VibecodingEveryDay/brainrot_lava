using UnityEngine;
using System.Reflection;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Панель для размещения brainrot объектов.
/// Когда игрок находится в зоне панели и зажимает E с brainrot в руках,
/// объект размещается на панели вместо земли.
/// </summary>
public class PlacementPanel : InteractableObject
{
    [Header("Настройки размещения на панели")]
    [Tooltip("ID панели для связи с EarnPanel")]
    [SerializeField] private int panelID = 0;
    
    [Tooltip("Точка размещения на панели (если не назначена, используется центр панели)")]
    [SerializeField] private Transform placementPoint;
    
    [Tooltip("Высота размещения над панелью")]
    [SerializeField] private float placementHeight = 0.1f;
    
    [Header("Эффекты")]
    [Tooltip("Префаб фейерверка для эффекта при размещении brainrot")]
    [SerializeField] private GameObject fireworkPrefab;
    
    [Tooltip("Корректировка позиции эффекта по оси Y (добавляется к позиции размещения)")]
    [SerializeField] private float effectPosY = 0f;
    
    // Статическая ссылка на активную панель размещения
    private static PlacementPanel activePanel = null;
    
    // Статический список всех панелей для определения ближайшей
    private static System.Collections.Generic.List<PlacementPanel> allPanels = new System.Collections.Generic.List<PlacementPanel>();
    
    // Статическая переменная для кэширования ближайшей панели (обновляется один раз на кадр)
    private static PlacementPanel cachedClosestPanel = null;
    private static int lastClosestPanelUpdateFrame = -1;
    
    private PlayerCarryController playerCarryController;
    
    // Размещённый brainrot объект на этой панели
    private BrainrotObject placedBrainrot = null;
    
    // Флаг, указывающий, что идет процесс размещения объекта на панели
    private bool isPlacingObject = false;
    
    private Collider panelCollider;
    
    // Флаг, указывающий, является ли эта панель ближайшей к игроку
    private bool isClosestPanel = false;
    
    // Флаг, указывающий, идет ли загрузка размещенных брейнротов (чтобы не сохранять при загрузке)
    private bool isLoadingPlacedBrainrots = false;
    
    // Флаг, указывающий, что объект только что был взят из панели (чтобы предотвратить немедленное размещение обратно)
    private bool justTookFromPanel = false;
    
    protected override void Awake()
    {
        base.Awake();
        // Регистрируем панель в статическом списке
        if (!allPanels.Contains(this))
        {
            allPanels.Add(this);
        }
        
        // Находим PlayerCarryController
        FindPlayerCarryController();
        
        // Находим коллайдер панели для правильного позиционирования UI
        panelCollider = GetComponent<Collider>();
        if (panelCollider == null)
        {
            panelCollider = GetComponentInChildren<Collider>();
        }
        
        // Создаем interactionPoint в центре коллайдера для правильного позиционирования UI
        // Это гарантирует, что UI будет позиционироваться относительно правильной мировой позиции
        if (panelCollider != null)
        {
            CreateInteractionPoint();
        }
    }
    
    private void OnEnable()
    {
        // Регистрируем панель при включении
        if (!allPanels.Contains(this))
        {
            allPanels.Add(this);
            // Сбрасываем кэш при добавлении новой панели
            ResetClosestPanelCache();
        }
    }
    
    private void OnDisable()
    {
        // Отменяем регистрацию при выключении
        allPanels.Remove(this);
        
        // Сбрасываем кэш при удалении панели
        ResetClosestPanelCache();
        
        // Если эта панель была активной, снимаем регистрацию
        if (activePanel == this)
        {
            activePanel = null;
        }
        
        isClosestPanel = false;
    }
    
    private void Start()
    {
        // Загружаем размещенные брейнроты при старте (с небольшой задержкой, чтобы GameStorage успел загрузить данные)
        StartCoroutine(LoadPlacedBrainrotsDelayed());
    }
    
    /// <summary>
    /// Загружает размещенные брейнроты с задержкой, чтобы GameStorage успел загрузить данные
    /// </summary>
    private IEnumerator LoadPlacedBrainrotsDelayed()
    {
        // Ждем несколько кадров, чтобы GameStorage успел загрузить данные из YG2
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();
        
        // Загружаем размещенные брейнроты
        LoadPlacedBrainrots();
    }
    
    /// <summary>
    /// Загружает размещенные брейнроты из GameStorage и размещает их на панели
    /// </summary>
    private void LoadPlacedBrainrots()
    {
        if (GameStorage.Instance == null)
        {
            Debug.LogWarning($"[PlacementPanel] GameStorage.Instance не найден, не могу загрузить размещенные брейнроты для панели {panelID}");
            return;
        }
        
        // Получаем данные о размещенном брейнроте для этой панели
        PlacementData placementData = GameStorage.Instance.GetPlacedBrainrot(panelID);
        
        if (placementData == null || string.IsNullOrEmpty(placementData.brainrotName))
        {
            // Нет сохраненного брейнрота для этой панели
            return;
        }
        
        // Проверяем, не размещен ли уже брейнрот на панели
        if (placedBrainrot != null)
        {
            // Брейнрот уже размещен, не загружаем из сохранения
            return;
        }
        
        // Загружаем префаб брейнрота по имени
        GameObject brainrotPrefab = LoadBrainrotPrefabByName(placementData.brainrotName);
        
        if (brainrotPrefab == null)
        {
            Debug.LogWarning($"[PlacementPanel] Не удалось загрузить префаб брейнрота '{placementData.brainrotName}' для панели {panelID}");
            return;
        }
        
        // Создаем экземпляр брейнрота
        GameObject brainrotInstance = Instantiate(brainrotPrefab);
        BrainrotObject brainrotObject = brainrotInstance.GetComponent<BrainrotObject>();
        
        if (brainrotObject == null)
        {
            Debug.LogWarning($"[PlacementPanel] У префаба '{placementData.brainrotName}' нет компонента BrainrotObject!");
            Destroy(brainrotInstance);
            return;
        }
        
        // ВАЖНО: Восстанавливаем ВСЕ параметры брейнрота из сохранения ПЕРЕД размещением
        // Это критично, чтобы сохранить именно тот брейнрот, который был сгенерирован и размещён игроком
        brainrotObject.SetLevel(placementData.level);
        
        // Восстанавливаем редкость из сохранения (если не пустая)
        if (!string.IsNullOrEmpty(placementData.rarity))
        {
            brainrotObject.SetRarity(placementData.rarity);
        }
        else
        {
            Debug.LogWarning($"[PlacementPanel] Редкость не найдена в сохранении для брейнрота '{placementData.brainrotName}' на панели {panelID}, используется значение по умолчанию из префаба");
        }
        
        // Восстанавливаем базовый доход из сохранения (если больше 0)
        if (placementData.baseIncome > 0)
        {
            brainrotObject.SetBaseIncome(placementData.baseIncome);
        }
        else
        {
            Debug.LogWarning($"[PlacementPanel] baseIncome не найден в сохранении для брейнрота '{placementData.brainrotName}' на панели {panelID}, используется значение по умолчанию из префаба");
        }
        
        // ВАЖНО: Размещенные на панели брейнроты автоматически считаются побежденными
        brainrotObject.SetUnfought(false);
        
        // Устанавливаем флаг загрузки, чтобы не сохранять при размещении
        isLoadingPlacedBrainrots = true;
        
        // Размещаем брейнрот на панели
        PlaceOnPanel(brainrotObject);
        
        // Сбрасываем флаг загрузки
        isLoadingPlacedBrainrots = false;
        
        Debug.Log($"[PlacementPanel] Загружен и размещен брейнрот '{placementData.brainrotName}' уровня {placementData.level}, редкость: {placementData.rarity}, baseIncome: {placementData.baseIncome} на панели {panelID}");
    }
    
    /// <summary>
    /// Загружает префаб брейнрота по имени
    /// </summary>
    private GameObject LoadBrainrotPrefabByName(string brainrotName)
    {
        if (string.IsNullOrEmpty(brainrotName))
        {
            return null;
        }
        
#if UNITY_EDITOR
        // В редакторе используем AssetDatabase для загрузки из папки
        string folderPath = "Assets/Assets/Resources/game/Brainrots";
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folderPath });
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            
            if (prefab != null)
            {
                BrainrotObject brainrotObject = prefab.GetComponent<BrainrotObject>();
                if (brainrotObject != null)
                {
                    // Используем GetObjectName() для получения имени
                    string prefabName = brainrotObject.GetObjectName();
                    if (prefabName == brainrotName)
                    {
                        return prefab;
                    }
                }
            }
        }
#else
        // В билде используем Resources (путь относительно папки Resources)
        GameObject[] allPrefabs = Resources.LoadAll<GameObject>("game/Brainrots");
        
        foreach (GameObject prefab in allPrefabs)
        {
            if (prefab != null)
            {
                BrainrotObject brainrotObject = prefab.GetComponent<BrainrotObject>();
                if (brainrotObject != null)
                {
                    // Используем GetObjectName() для получения имени
                    string prefabName = brainrotObject.GetObjectName();
                    if (prefabName == brainrotName)
                    {
                        return prefab;
                    }
                }
            }
        }
#endif
        
        return null;
    }
    
    /// <summary>
    /// Сбрасывает кэш ближайшей панели (вызывается при изменении списка панелей)
    /// </summary>
    private static void ResetClosestPanelCache()
    {
        cachedClosestPanel = null;
        lastClosestPanelUpdateFrame = -1;
    }
    
    /// <summary>
    /// Создает точку взаимодействия в центре коллайдера панели
    /// </summary>
    private void CreateInteractionPoint()
    {
        // Используем рефлексию для установки interactionPoint
        FieldInfo interactionPointField = typeof(InteractableObject).GetField("interactionPoint", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (interactionPointField != null)
        {
            Transform existingPoint = interactionPointField.GetValue(this) as Transform;
            
            // Если точка взаимодействия еще не создана, создаем её
            if (existingPoint == null)
            {
                GameObject interactionPointObj = new GameObject("InteractionPoint_" + gameObject.name);
                // ВАЖНО: НЕ устанавливаем родителя, чтобы избежать проблем с локальными координатами
                // interactionPointObj.transform.SetParent(null);
                
                // Устанавливаем мировую позицию в центр коллайдера
                // Используем bounds.center, который всегда возвращает мировую позицию
                Vector3 worldCenter = panelCollider.bounds.center;
                interactionPointObj.transform.position = worldCenter;
                
                // Устанавливаем точку взаимодействия через рефлексию
                interactionPointField.SetValue(this, interactionPointObj.transform);
                
                Debug.Log($"[PlacementPanel] Создана точка взаимодействия в позиции {worldCenter} (центр коллайдера панели)");
            }
            else
            {
                // Если точка уже существует, обновляем её позицию
                Vector3 worldCenter = panelCollider.bounds.center;
                existingPoint.position = worldCenter;
            }
        }
    }
    
    protected override void Update()
    {
        // Определяем ближайшую панель к игроку
        DetermineClosestPanel();
        
        // ВАЖНО: Проверяем размещённый brainrot ПОСЛЕ определения ближайшей панели
        // Это предотвращает очистку ссылки во время размещения
        // ВАЖНО: НЕ проверяем, если идет процесс размещения объекта на этой панели
        // Также НЕ проверяем, если объект только что размещен (isPlaced = true и !isCarried)
        bool isPlacing = isClosestPanel && placedBrainrot != null && placedBrainrot.IsCarried();
        bool justPlaced = isClosestPanel && placedBrainrot != null && placedBrainrot.IsPlaced() && !placedBrainrot.IsCarried();
        if (!isPlacing && !justPlaced && !isPlacingObject)
        {
            // Проверяем для всех панелей, кроме случая когда идет размещение на ближайшей панели
            // или когда объект только что размещен, или когда идет процесс размещения
        CheckPlacedBrainrot();
        }
        
        // Проверяем, есть ли brainrot в руках у игрока
        // ВАЖНО: Проверяем состояние через PlayerCarryController, но также учитываем состояние размещенного объекта
        // Если объект размещен на панели, он не должен считаться в руках, даже если GetCurrentCarriedObject() еще возвращает его
        bool hasBrainrotInHands = false;
        if (playerCarryController == null)
        {
            FindPlayerCarryController();
        }
        if (playerCarryController != null)
        {
            BrainrotObject carriedObject = playerCarryController.GetCurrentCarriedObject();
            // ВАЖНО: Объект считается в руках только если он действительно в руках (IsCarried() == true)
            // Это предотвращает проблему, когда GetCurrentCarriedObject() возвращает объект, который уже размещен
            if (carriedObject != null)
            {
                // Проверяем состояние объекта напрямую
                hasBrainrotInHands = carriedObject.IsCarried();
            }
        }
        
        // ВАЖНО: Проверяем, должны ли мы показать UI (предварительная проверка)
        // Это нужно для сброса interactionCompleted ПЕРЕД base.Update()
        // Используем текущее значение isPlayerInRange (может быть устаревшим, но это нормально для предварительной проверки)
        bool mightShowUI = isClosestPanel && 
            ((placedBrainrot != null && !hasBrainrotInHands) || (hasBrainrotInHands && placedBrainrot == null));
        
        // ВАЖНО: Если можем показать UI, но interactionCompleted = true, сбрасываем его ПЕРЕД base.Update()
        // Это нужно, чтобы base.Update() мог создать UI
        if (mightShowUI)
        {
            // Используем рефлексию для проверки и сброса interactionCompleted
            System.Reflection.FieldInfo interactionCompletedField = typeof(InteractableObject).GetField("interactionCompleted", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (interactionCompletedField != null)
            {
                bool interactionCompleted = (bool)interactionCompletedField.GetValue(this);
                if (interactionCompleted)
                {
                    // Сбрасываем состояние взаимодействия, чтобы UI мог появиться
                    ResetInteraction();
                }
            }
        }
        
        // Всегда вызываем base.Update() для проверки расстояния до игрока
        // Это нужно, чтобы isPlayerInRange обновлялся корректно
        base.Update();
        
        // Показываем UI если:
        // 1. Эта панель является ближайшей к игроку
        // 2. Игрок в радиусе взаимодействия (обновлено в base.Update())
        // 3. (Есть размещённый brainrot на панели И у игрока НЕТ brainrot в руках - чтобы можно было взять обратно) 
        //    ИЛИ (у игрока есть brainrot в руках И на панели НЕТ размещённого brainrot - чтобы можно было разместить)
        // НЕ показываем UI если у игрока в руках brainrot И на панели уже есть brainrot (чтобы не разместить два брейнрота в один placement)
        bool shouldShowUI = isClosestPanel && isPlayerInRange && 
            ((placedBrainrot != null && !hasBrainrotInHands) || (hasBrainrotInHands && placedBrainrot == null));
        
        // Если эта панель не ближайшая, принудительно отключаем взаимодействие
        if (!isClosestPanel)
        {
            // Используем рефлексию для установки isPlayerInRange = false
            System.Reflection.FieldInfo isPlayerInRangeField = typeof(InteractableObject).GetField("isPlayerInRange", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (isPlayerInRangeField != null)
            {
                isPlayerInRangeField.SetValue(this, false);
            }
            
            // Скрываем UI если он показан
            if (HasUI())
            {
                HideUI();
            }
            
            // Если эта панель была активной, снимаем регистрацию (игрок не в зоне этой панели)
            if (activePanel == this)
            {
                activePanel = null;
            }
            return;
        }
        
        // Управляем видимостью UI в зависимости от условий
        if (!shouldShowUI)
        {
            // Нет ни размещённого brainrot, ни brainrot в руках - скрываем UI
            if (HasUI())
            {
                HideUI();
            }
        }
        else
        {
            // Условия выполнены - убеждаемся, что UI показан
            // ВАЖНО: base.Update() уже вызван выше, но он может не создать UI если interactionCompleted = true
            // Поэтому сбрасываем состояние взаимодействия, чтобы UI мог появиться
            if (!HasUI() && isPlayerInRange)
            {
                // Если UI не создан, но игрок в радиусе, сбрасываем состояние взаимодействия
                // Это позволит base.Update() создать UI в следующем кадре
                ResetInteraction();
            }
        }
        
        // Регистрируем/снимаем регистрацию панели в зависимости от условий
        // (только для ближайшей панели и только если должны показать UI)
        if (shouldShowUI && isPlayerInRange)
        {
            // Условия для показа UI выполнены - регистрируем эту панель как активную
            if (activePanel != this)
            {
                activePanel = this;
            }
        }
        else
        {
            // Условия не выполнены - если эта панель была активной, снимаем регистрацию
            if (activePanel == this)
            {
                activePanel = null;
            }
        }
    }
    
    /// <summary>
    /// Определяет ближайшую панель к игроку из всех панелей в радиусе
    /// Оптимизировано: вычисляется один раз на кадр для всех панелей
    /// </summary>
    private void DetermineClosestPanel()
    {
        // Если ближайшая панель уже определена в этом кадре, используем кэш
        if (lastClosestPanelUpdateFrame == Time.frameCount && cachedClosestPanel != null)
        {
            isClosestPanel = (cachedClosestPanel == this);
            return;
        }
        
        // Получаем Transform игрока
        Transform playerTransform = GetPlayerTransform();
        if (playerTransform == null)
        {
            isClosestPanel = false;
            cachedClosestPanel = null;
            lastClosestPanelUpdateFrame = Time.frameCount;
            return;
        }
        
        // Находим все панели в радиусе взаимодействия
        PlacementPanel closestPanel = null;
        float closestDistance = float.MaxValue;
        
        // Получаем радиус взаимодействия через рефлексию (используем значение из первой панели)
        System.Reflection.FieldInfo interactionRangeField = typeof(InteractableObject).GetField("interactionRange", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        float interactionRange = 3f; // Значение по умолчанию
        if (interactionRangeField != null)
        {
            interactionRange = (float)interactionRangeField.GetValue(this);
        }
        
        // Проходим по всем панелям и находим ближайшую
        foreach (PlacementPanel panel in allPanels)
        {
            if (panel == null || !panel.gameObject.activeInHierarchy) continue;
            
            // Получаем позицию панели
            Vector3 panelPosition = panel.GetPlacementPosition();
            float distance = Vector3.Distance(playerTransform.position, panelPosition);
            
            // Если панель в радиусе взаимодействия и ближе текущей ближайшей
            if (distance <= interactionRange && distance < closestDistance)
            {
                closestPanel = panel;
                closestDistance = distance;
            }
        }
        
        // Кэшируем результат
        cachedClosestPanel = closestPanel;
        lastClosestPanelUpdateFrame = Time.frameCount;
        
        // Устанавливаем флаг для этой панели
        isClosestPanel = (closestPanel == this);
    }
    
    /// <summary>
    /// Получает Transform игрока
    /// </summary>
    private Transform GetPlayerTransform()
    {
        // Пытаемся получить через PlayerCarryController
        if (playerCarryController == null)
        {
            FindPlayerCarryController();
        }
        
        if (playerCarryController != null)
        {
            Transform playerTransform = playerCarryController.GetPlayerTransform();
            if (playerTransform != null)
            {
                return playerTransform;
            }
        }
        
        // Если не получилось, используем рефлексию для получения из базового класса
        System.Reflection.FieldInfo playerTransformField = typeof(InteractableObject).GetField("playerTransform", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (playerTransformField != null)
        {
            return playerTransformField.GetValue(this) as Transform;
        }
        
        return null;
    }
    
    /// <summary>
    /// Проверяет, есть ли размещённый brainrot на панели
    /// </summary>
    private void CheckPlacedBrainrot()
    {
        // Если уже есть размещённый brainrot, проверяем, что он всё ещё размещён
        if (placedBrainrot != null)
        {
            // Проверяем, что объект существует
            if (placedBrainrot == null || placedBrainrot.gameObject == null)
            {
                placedBrainrot = null;
                return;
            }
            
            // ВАЖНО: Сначала проверяем, взят ли объект - если да, очищаем ссылку независимо от других проверок
            // Это критично, чтобы можно было поставить объект обратно на панель после взятия
            if (placedBrainrot.IsCarried())
            {
                Debug.Log($"[PlacementPanel] Очищаем ссылку на placedBrainrot: объект взят, панель {panelID}");
                placedBrainrot = null;
                return;
            }
            
            // ВАЖНО: Если объект размещен на этой панели (проверяем через IsBrainrotPlacedOnPanel),
            // НЕ очищаем ссылку, даже если состояние еще не обновлено
            bool isOnThisPanel = IsBrainrotPlacedOnPanel(placedBrainrot);
            
            if (isOnThisPanel)
            {
                // Объект размещен на этой панели - не очищаем ссылку
                return;
            }
            
            // Проверяем, что объект существует и всё ещё размещён
            if (!placedBrainrot.IsPlaced())
            {
                // Объект больше не размещён - очищаем ссылку
                Debug.Log($"[PlacementPanel] Очищаем ссылку на placedBrainrot: объект не размещен, панель {panelID}");
                placedBrainrot = null;
            }
            else
            {
                // Проверяем расстояние до объекта - если он слишком далеко от панели, он больше не на панели
                // ВАЖНО: Увеличиваем радиус проверки до 3 единиц для более надёжного определения
                float distance = Vector3.Distance(placedBrainrot.transform.position, GetPlacementPosition());
                if (distance > 3f) // Если объект дальше 3 единиц от центра панели
                {
                    Debug.Log($"[PlacementPanel] Очищаем ссылку на placedBrainrot: объект слишком далеко (расстояние {distance:F2}), панель {panelID}");
                    placedBrainrot = null;
                }
            }
        }
        else
        {
            // Ищем размещённые brainrot объекты рядом с панелью
            // ВАЖНО: Ищем только среди объектов, которые НЕ размещены на других панелях
            // Это предотвращает конфликты, когда один объект может быть найден несколькими панелями
            BrainrotObject[] allBrainrots = FindObjectsByType<BrainrotObject>(FindObjectsSortMode.None);
            Vector3 panelCenter = GetPlacementPosition();
            
            foreach (BrainrotObject brainrot in allBrainrots)
            {
                if (brainrot != null && brainrot.IsPlaced() && !brainrot.IsCarried())
                {
                    // ВАЖНО: Проверяем, не размещён ли этот объект уже на другой панели
                    // Если он уже размещён на другой панели, пропускаем его
                    bool alreadyOnAnotherPanel = false;
                    foreach (PlacementPanel otherPanel in allPanels)
                    {
                        if (otherPanel != null && otherPanel != this && otherPanel.placedBrainrot == brainrot)
                        {
                            alreadyOnAnotherPanel = true;
                            break;
                        }
                    }
                    
                    if (alreadyOnAnotherPanel)
                    {
                        continue; // Пропускаем объекты, которые уже размещены на других панелях
                    }
                    
                    // Проверяем расстояние до панели
                    // ВАЖНО: Увеличиваем радиус поиска до 3 единиц для более надёжного определения
                    float distance = Vector3.Distance(brainrot.transform.position, panelCenter);
                    if (distance < 3f) // Если объект близко к панели (в радиусе 3 единиц)
                    {
                        // Нашли размещённый brainrot на этой панели
                        placedBrainrot = brainrot;
                        break;
                    }
                }
            }
        }
    }
    
    private void LateUpdate()
    {
        // Принудительно обновляем кэшированную позицию взаимодействия ПОСЛЕ всех обновлений базового класса
        // Это важно, так как CheckPlayerDistance() в базовом Update() может перезаписать позицию
        // Вызываем в LateUpdate, чтобы наше обновление было последним
        UpdateInteractionPosition();
    }
    
    /// <summary>
    /// Принудительно обновляет кэшированную позицию взаимодействия
    /// Использует центр коллайдера панели для правильного позиционирования UI
    /// </summary>
    private void UpdateInteractionPosition()
    {
        if (panelCollider == null) return;
        
        // ВАЖНО: Всегда используем bounds.center как источник истины
        // bounds.center всегда возвращает правильную мировую позицию независимо от масштаба родителя
        Vector3 correctPosition = panelCollider.bounds.center;
        
        // Обновляем interactionPoint, если он существует
        FieldInfo interactionPointField = typeof(InteractableObject).GetField("interactionPoint", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (interactionPointField != null)
        {
            Transform interactionPointTransform = interactionPointField.GetValue(this) as Transform;
            if (interactionPointTransform != null)
            {
                // Устанавливаем правильную мировую позицию
                interactionPointTransform.position = correctPosition;
            }
        }
        
        // Обновляем cachedInteractionPosition через рефлексию
        FieldInfo cachedPositionField = typeof(InteractableObject).GetField("cachedInteractionPosition", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (cachedPositionField != null)
        {
            cachedPositionField.SetValue(this, correctPosition);
        }
        
        // Также обновляем позицию UI напрямую, если он уже создан
        // Это исправляет проблему, если UI был создан с неправильной позицией
        FieldInfo currentUIInstanceField = typeof(InteractableObject).GetField("currentUIInstance", 
            BindingFlags.NonPublic | BindingFlags.Instance);
        
        if (currentUIInstanceField != null)
        {
            GameObject currentUI = currentUIInstanceField.GetValue(this) as GameObject;
            if (currentUI != null && currentUI.activeSelf)
            {
                // Получаем uiOffset через рефлексию
                FieldInfo uiOffsetField = typeof(InteractableObject).GetField("uiOffset", 
                    BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (uiOffsetField != null)
                {
                    Vector3 uiOffset = (Vector3)uiOffsetField.GetValue(this);
                    // Вычисляем правильную позицию UI с учетом offset
                    Vector3 uiPosition = correctPosition + uiOffset;
                    currentUI.transform.position = uiPosition;
                }
            }
        }
    }
    
    /// <summary>
    /// Находит PlayerCarryController в сцене
    /// </summary>
    private void FindPlayerCarryController()
    {
        if (playerCarryController == null)
        {
            playerCarryController = FindFirstObjectByType<PlayerCarryController>();
        }
    }
    
    /// <summary>
    /// Переопределяем CompleteInteraction для обработки размещения на панели
    /// </summary>
    protected override void CompleteInteraction()
    {
        // Проверяем, есть ли brainrot в руках
        if (playerCarryController == null)
        {
            FindPlayerCarryController();
            if (playerCarryController == null)
            {
                Debug.LogWarning("[PlacementPanel] PlayerCarryController не найден!");
                base.CompleteInteraction();
                return;
            }
        }
        
        BrainrotObject carriedObject = playerCarryController.GetCurrentCarriedObject();
        
        if (carriedObject != null)
        {
            // ВАЖНО: Если объект только что был взят из этой панели, не размещаем его обратно сразу
            // Это предотвращает проблему, когда объект сразу возвращается в placement после взятия
            if (justTookFromPanel)
            {
                // Сбрасываем флаг и состояние взаимодействия
                justTookFromPanel = false;
                ResetInteraction();
                return;
            }
            
            // ВАЖНО: Проверяем, что на панели нет размещённого брейнрота
            // Нельзя разместить брейнрот, если на панели уже есть размещённый брейнрот
            // НО: Если placedBrainrot указывает на объект, который сейчас в руках (IsCarried() == true),
            // это означает, что объект был взят из панели, и мы можем разместить его обратно
            if (placedBrainrot != null && !placedBrainrot.IsCarried())
            {
                Debug.LogWarning($"[PlacementPanel] Нельзя разместить брейнрот на панели {panelID}: на панели уже есть размещённый брейнрот {placedBrainrot.GetObjectName()}");
                // Сбрасываем состояние взаимодействия, но не размещаем объект
                ResetInteraction();
                return;
            }
            
            // ВАЖНО: Если placedBrainrot указывает на объект, который сейчас в руках,
            // очищаем ссылку, чтобы можно было разместить объект обратно
            if (placedBrainrot != null && placedBrainrot.IsCarried() && placedBrainrot == carriedObject)
            {
                placedBrainrot = null;
            }
            
            // Размещаем объект на панели (только если панель пустая)
            PlaceOnPanel(carriedObject);
            
            // Сбрасываем состояние взаимодействия
            ResetInteraction();
        }
        else if (placedBrainrot != null)
        {
            // Если нет объекта в руках, но есть размещённый brainrot - берём его обратно
            TakePlacedBrainrot();
            
            // Сбрасываем состояние взаимодействия
            ResetInteraction();
        }
        else
        {
            // Если нет объекта в руках и нет размещённого brainrot, вызываем базовую логику
            base.CompleteInteraction();
        }
    }
    
    /// <summary>
    /// Берёт размещённый brainrot обратно в руки
    /// </summary>
    private void TakePlacedBrainrot()
    {
        if (placedBrainrot == null)
        {
            return;
        }
        
        if (playerCarryController == null)
        {
            FindPlayerCarryController();
            if (playerCarryController == null)
            {
                Debug.LogWarning("[PlacementPanel] PlayerCarryController не найден!");
                return;
            }
        }
        
        // Проверяем, может ли игрок взять объект
        if (!playerCarryController.CanCarry())
        {
            Debug.Log("[PlacementPanel] Игрок уже несет другой объект!");
            return;
        }
        
        // Берём объект обратно в руки
        placedBrainrot.Take();
        placedBrainrot.SetJustTakenFromPanel();
        
        // Удаляем размещенный брейнрот из GameStorage
        if (GameStorage.Instance != null)
        {
            GameStorage.Instance.RemovePlacedBrainrot(panelID);
        }
        
        // Очищаем ссылку на размещённый объект
        placedBrainrot = null;
        
        // Сбрасываем флаг размещения, если он был установлен
        isPlacingObject = false;
        
        // ВАЖНО: Устанавливаем флаг, что объект только что был взят из панели
        // Это предотвратит немедленное размещение обратно в том же кадре
        justTookFromPanel = true;
        
        // ВАЖНО: Сбрасываем флаг в следующем кадре, чтобы можно было разместить объект обратно позже
        StartCoroutine(ResetJustTookFromPanelNextFrame());
        
        Debug.Log("[PlacementPanel] Размещённый brainrot взят обратно в руки");
    }
    
    /// <summary>
    /// Размещает brainrot объект на панели с учетом всех настроек размещения
    /// ВАЖНО: Нельзя разместить брейнрот, если на панели уже есть размещённый брейнрот
    /// </summary>
    public void PlaceOnPanel(BrainrotObject brainrotObject)
    {
        if (brainrotObject == null)
        {
            Debug.LogWarning("[PlacementPanel] Попытка разместить null объект!");
            return;
        }
        
        // ВАЖНО: Проверяем, что на панели нет размещённого брейнрота
        // Нельзя разместить брейнрот, если на панели уже есть размещённый брейнрот
        if (placedBrainrot != null)
        {
            Debug.LogWarning($"[PlacementPanel] Нельзя разместить брейнрот {brainrotObject.GetObjectName()} на панели {panelID}: на панели уже есть размещённый брейнрот {placedBrainrot.GetObjectName()}");
            // Сбрасываем флаг размещения, если он был установлен
            isPlacingObject = false;
            return;
        }
        
        if (playerCarryController == null)
        {
            FindPlayerCarryController();
            if (playerCarryController == null)
            {
                Debug.LogWarning("[PlacementPanel] PlayerCarryController не найден!");
                return;
            }
        }
        
        // Получаем позицию панели для размещения
        Vector3 panelPosition = GetPlacementPosition();
        
        // Получаем ориентацию панели
        Vector3 panelForward = transform.forward;
        Vector3 panelRight = transform.right;
        Vector3 panelUp = transform.up;
        
        // Вычисляем позицию размещения с учетом смещений из BrainrotObject
        float putOffsetX = brainrotObject.GetPutOffsetX();
        float putOffsetZ = brainrotObject.GetPutOffsetZ();
        float placementOffsetX = brainrotObject.GetPlacementOffsetX();
        float placementOffsetZ = brainrotObject.GetPlacementOffsetZ();
        
        // Вычисляем финальную позицию относительно панели
        Vector3 placementPosition = panelPosition + 
                                   panelForward * (putOffsetZ + placementOffsetZ) + 
                                   panelRight * (putOffsetX + placementOffsetX) +
                                   panelUp * placementHeight;
        
        // Используем Raycast для определения точной позиции на поверхности панели
        RaycastHit hit;
        if (Physics.Raycast(placementPosition + panelUp * 0.5f, -panelUp, out hit, 1f))
        {
            // Если нашли поверхность, используем точку попадания
            placementPosition = hit.point + panelUp * placementHeight;
        }
        
        // Вычисляем поворот объекта при размещении
        float placementRotationY = brainrotObject.GetPlacementRotationY();
        Quaternion placementRotation;
        
        if (Mathf.Abs(placementRotationY) > 0.01f)
        {
            // Поворачиваем относительно панели
            Quaternion panelRotation = transform.rotation;
            Quaternion additionalRotation = Quaternion.Euler(0f, placementRotationY, 0f);
            placementRotation = panelRotation * additionalRotation;
        }
        else
        {
            // Если поворот не задан, используем поворот панели
            placementRotation = transform.rotation;
        }
        
        // ВАЖНО: Размещенные на панели брейнроты автоматически считаются побежденными
        brainrotObject.SetUnfought(false);
        
        // ВАЖНО: Устанавливаем флаг размещения ПЕРЕД установкой ссылки
        // Это предотвратит CheckPlacedBrainrot() от очистки ссылки во время размещения
        isPlacingObject = true;
        
        // ВАЖНО: Сохраняем ссылку на размещённый объект ПЕРЕД размещением
        // Это критично, чтобы IsBrainrotPlacedOnPanel() могла правильно определить, что объект размещён на панели
        placedBrainrot = brainrotObject;
        
        Debug.Log($"[PlacementPanel] Начинаем размещение брейнрота {brainrotObject.GetObjectName()} на панели {panelID}, placedBrainrot установлен: {placedBrainrot != null}, isCarried до размещения: {brainrotObject.IsCarried()}");
        
        // ВАЖНО: Освобождаем объект из рук ПЕРЕД вызовом PutAtPosition
        // Это нужно, чтобы PutAtPosition правильно определил, что объект размещается на панели
        if (playerCarryController != null)
        {
            playerCarryController.DropObject();
            // ВАЖНО: Принудительно проверяем, что объект больше не в руках
            // Это нужно, чтобы избежать проблем с задержкой обновления состояния
            BrainrotObject stillCarried = playerCarryController.GetCurrentCarriedObject();
            if (stillCarried == brainrotObject)
            {
                Debug.LogWarning($"[PlacementPanel] Объект {brainrotObject.GetObjectName()} все еще в руках после DropObject(), принудительно очищаем ссылку");
                // Если объект все еще в руках, принудительно очищаем ссылку в PlayerCarryController
                // Это может произойти, если DropObject() не успел обновить состояние
                playerCarryController.DropObject();
            }
        }
        
        // ВАЖНО: Убеждаемся, что объект активен и виден перед размещением
        if (brainrotObject.gameObject != null)
        {
            brainrotObject.gameObject.SetActive(true);
            // Убеждаемся, что все дочерние объекты (моделька) активны
            SetAllChildrenActive(brainrotObject.transform, true);
        }
        
        // Используем метод PutAtPosition из BrainrotObject для размещения
        // Этот метод установит позицию, поворот, масштаб, включит физику и коллайдеры,
        // установит состояние (isCarried = false, isPlaced = true)
        // ВАЖНО: Ссылка placedBrainrot уже установлена выше, поэтому IsBrainrotPlacedOnPanel() найдет объект
        brainrotObject.PutAtPosition(placementPosition, placementRotation);
        
        // ВАЖНО: После размещения принудительно устанавливаем ссылку ещё раз
        // Это гарантирует, что даже если CheckPlacedBrainrot() очистит ссылку в том же кадре,
        // она будет восстановлена
        placedBrainrot = brainrotObject;
        
        // ВАЖНО: Убеждаемся, что объект активен и виден после размещения
        if (brainrotObject.gameObject != null)
        {
            brainrotObject.gameObject.SetActive(true);
            SetAllChildrenActive(brainrotObject.transform, true);
        }
        
        // ВАЖНО: Проверяем после размещения, что объект правильно размещен
        bool isPlacedOnPanelAfter = PlacementPanel.IsBrainrotPlacedOnPanel(brainrotObject);
        Debug.Log($"[PlacementPanel] После размещения: isPlacedOnPanel = {isPlacedOnPanelAfter}, isCarried = {brainrotObject.IsCarried()}, isPlaced = {brainrotObject.IsPlaced()}, placedBrainrot = {placedBrainrot != null}, объект активен: {brainrotObject.gameObject.activeSelf}");
        
        // ВАЖНО: Убеждаемся, что объект действительно размещен и не в руках
        // Это нужно для корректной работы CheckPlacedBrainrot()
        if (!brainrotObject.IsPlaced() || brainrotObject.IsCarried())
        {
            Debug.LogWarning($"[PlacementPanel] После размещения объект {brainrotObject.GetObjectName()} имеет неправильное состояние: isPlaced={brainrotObject.IsPlaced()}, isCarried={brainrotObject.IsCarried()}");
        }
        
        // ВАЖНО: Если объект не размещен на панели после всех операций, это ошибка
        if (!isPlacedOnPanelAfter)
        {
            Debug.LogError($"[PlacementPanel] КРИТИЧЕСКАЯ ОШИБКА: Объект {brainrotObject.GetObjectName()} не размещен на панели после PlaceOnPanel! Проверяем ссылки...");
            // Проверяем все панели
            foreach (PlacementPanel panel in allPanels)
            {
                if (panel != null && panel.placedBrainrot == brainrotObject)
                {
                    Debug.Log($"[PlacementPanel] Объект найден на панели {panel.panelID}");
                }
            }
        }
        
        // ВАЖНО: Сбрасываем флаг размещения ПОСЛЕ завершения размещения
        // Это позволит CheckPlacedBrainrot() работать нормально в следующих кадрах
        isPlacingObject = false;
        
        // ВАЖНО: Сбрасываем флаг взятия из панели после успешного размещения
        // Это позволит снова взять объект из панели в будущем
        justTookFromPanel = false;
        
        // Создаём эффект фейерверка на месте размещения (только если это не загрузка из сохранения)
        if (!isLoadingPlacedBrainrots)
        {
            SpawnFireworkEffect(placementPosition);
        }
        
        // Сохраняем размещенный брейнрот в GameStorage со ВСЕМИ параметрами (только если это не загрузка из сохранения)
        // ВАЖНО: Сохраняем именно тот брейнрот, который был сгенерирован и размещён игроком
        if (GameStorage.Instance != null && !isLoadingPlacedBrainrots)
        {
            GameStorage.Instance.SavePlacedBrainrot(
                panelID, 
                brainrotObject.GetObjectName(), 
                brainrotObject.GetLevel(),
                brainrotObject.GetRarity(),
                brainrotObject.GetBaseIncome()
            );
        }
        
        Debug.Log($"[PlacementPanel] Объект {brainrotObject.GetObjectName()} размещен на панели в позиции {placementPosition}, placedBrainrot установлен: {placedBrainrot != null}, isPlaced={brainrotObject.IsPlaced()}, isCarried={brainrotObject.IsCarried()}, ID панели: {panelID}");
    }
    
    /// <summary>
    /// Создаёт эффект фейерверка на указанной позиции
    /// </summary>
    private void SpawnFireworkEffect(Vector3 position)
    {
        // Если префаб не назначен, пытаемся загрузить из Resources
        if (fireworkPrefab == null)
        {
            // Пытаемся загрузить из Resources (если префаб перемещён в Resources)
            fireworkPrefab = Resources.Load<GameObject>("CFXR4 Firework 1 Cyan-Purple (HDR)");
            
            // Если не найдено в Resources, пытаемся загрузить по полному пути (только в редакторе)
            #if UNITY_EDITOR
            if (fireworkPrefab == null)
            {
                fireworkPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Assets/Downloads/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR4 Firework 1 Cyan-Purple (HDR).prefab");
            }
            #endif
        }
        
        if (fireworkPrefab != null)
        {
            // Корректируем позицию по оси Y
            Vector3 effectPosition = position;
            effectPosition.y += effectPosY;
            
            // Создаём экземпляр фейерверка на позиции размещения
            GameObject fireworkInstance = Instantiate(fireworkPrefab, effectPosition, Quaternion.identity);
            
            // Увеличиваем масштаб эффекта в 2 раза
            fireworkInstance.transform.localScale = Vector3.one * 2f;
            
            // Фейерверк автоматически проиграется и уничтожится сам (если настроен в префабе)
            // Уничтожаем объект после завершения эффекта
            ParticleSystem particles = fireworkInstance.GetComponent<ParticleSystem>();
            if (particles != null)
            {
                // Получаем длительность эффекта
                ParticleSystem.MainModule main = particles.main;
                float duration = main.duration;
                
                // Получаем максимальное время жизни частиц
                float maxLifetime = 0f;
                if (main.startLifetime.mode == ParticleSystemCurveMode.Constant)
                {
                    maxLifetime = main.startLifetime.constant;
                }
                else if (main.startLifetime.mode == ParticleSystemCurveMode.TwoConstants)
                {
                    maxLifetime = main.startLifetime.constantMax;
                }
                else
                {
                    // Для кривых используем максимальное значение по умолчанию
                    maxLifetime = 2f; // Примерное значение для фейерверка
                }
                
                // Уничтожаем объект после завершения эффекта
                Destroy(fireworkInstance, duration + maxLifetime + 1f); // +1 секунда для безопасности
            }
            else
            {
                // Если нет ParticleSystem, уничтожаем через 5 секунд
                Destroy(fireworkInstance, 5f);
            }
            
            Debug.Log($"[PlacementPanel] Создан эффект фейерверка на позиции {position}");
        }
        else
        {
            Debug.LogWarning("[PlacementPanel] Префаб фейерверка не найден! Назначьте fireworkPrefab в инспекторе или переместите префаб в папку Resources.");
        }
    }
    
    /// <summary>
    /// Получает позицию размещения на панели
    /// </summary>
    public Vector3 GetPlacementPosition()
    {
        if (placementPoint != null)
        {
            return placementPoint.position;
        }
        
        // Используем центр коллайдера панели, если есть
        Collider panelCollider = GetComponent<Collider>();
        if (panelCollider != null)
        {
            return panelCollider.bounds.center;
        }
        
        // Иначе используем позицию панели
        return transform.position;
    }
    
    /// <summary>
    /// Получить активную панель размещения
    /// </summary>
    public static PlacementPanel GetActivePanel()
    {
        return activePanel;
    }
    
    /// <summary>
    /// Проверяет, можно ли разместить указанный brainrot на этой панели
    /// </summary>
    public bool CanPlaceBrainrot(BrainrotObject brainrot)
    {
        if (brainrot == null)
        {
            return false;
        }
        
        // Можно разместить, если на панели нет размещённого объекта
        // ИЛИ если это тот же объект, который был взят из этой панели (но ссылка уже очищена)
        bool canPlace = placedBrainrot == null;
        return canPlace;
    }
    
    /// <summary>
    /// Получить панель по ID
    /// </summary>
    public static PlacementPanel GetPanelByID(int id)
    {
        foreach (PlacementPanel panel in allPanels)
        {
            if (panel != null && panel.GetPanelID() == id)
            {
                return panel;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Проверить, является ли эта панель активной
    /// </summary>
    public bool IsActive()
    {
        return activePanel == this;
    }
    
    /// <summary>
    /// Получить ID панели
    /// </summary>
    public int GetPanelID()
    {
        return panelID;
    }
    
    /// <summary>
    /// Установить ID панели
    /// </summary>
    public void SetPanelID(int id)
    {
        panelID = id;
    }
    
    /// <summary>
    /// Проверяет, размещён ли указанный brainrot на какой-либо панели
    /// </summary>
    public static bool IsBrainrotPlacedOnPanel(BrainrotObject brainrot)
    {
        if (brainrot == null)
        {
            return false;
        }
        
        // ВАЖНО: Проверяем ссылку на панели ПЕРЕД проверкой состояния
        // Это позволяет определить размещение даже если состояние еще не обновлено
        foreach (PlacementPanel panel in allPanels)
        {
            if (panel != null && panel.placedBrainrot == brainrot)
            {
                // Если объект найден в placedBrainrot, он размещен на панели
                // Проверяем состояние только для дополнительной валидации
                if (brainrot.IsPlaced() && !brainrot.IsCarried())
            {
                    return true;
                }
                // Даже если состояние еще не обновлено, но ссылка есть - считаем размещенным
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Получить размещённый brainrot объект на этой панели
    /// </summary>
    public BrainrotObject GetPlacedBrainrot()
    {
        return placedBrainrot;
    }
    
    private void OnDestroy()
    {
        // Отменяем регистрацию панели при уничтожении
        allPanels.Remove(this);
        
        // Сбрасываем кэш при удалении панели
        ResetClosestPanelCache();
        
        // При уничтожении панели снимаем регистрацию, если она была активной
        if (activePanel == this)
        {
            activePanel = null;
        }
    }
    
    /// <summary>
    /// Рекурсивно активирует/деактивирует все дочерние объекты
    /// </summary>
    private void SetAllChildrenActive(Transform parent, bool active)
    {
        if (parent == null) return;
        
        parent.gameObject.SetActive(active);
        
        foreach (Transform child in parent)
        {
            if (child != null)
            {
                SetAllChildrenActive(child, active);
            }
        }
    }
    
    /// <summary>
    /// Сбрасывает флаг justTookFromPanel в следующем кадре
    /// Это позволяет разместить объект обратно после того, как он был взят
    /// </summary>
    private IEnumerator ResetJustTookFromPanelNextFrame()
    {
        yield return null; // Ждем один кадр
        justTookFromPanel = false;
    }
}
