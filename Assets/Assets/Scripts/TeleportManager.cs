using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

/// <summary>
/// Управляет телепортацией (дом, зона башни). При 100% огня и нахождении игрока в зоне башни —
/// показ "Вы проиграли!", телепорт в дом, удаление брейнрота из рук.
/// </summary>
public class TeleportManager : MonoBehaviour
{
    [Header("Fade Settings")]
    [Tooltip("Префаб Canvas с Image для fade эффекта (черный экран)")]
    [SerializeField] private GameObject fadeCanvasPrefab;
    
    [Tooltip("Скорость fade эффекта (время затемнения/осветления в секундах)")]
    [SerializeField] private float fadeSpeed = 0.5f;
    
    [Header("References")]
    [Tooltip("Позиция дома для телепортации (после поражения от огня и т.д.)")]
    [SerializeField] private Transform housePos;
    
    [Tooltip("Зона башни (объект с Collider trigger и TowerZoneTrigger). Игрок в зоне при 100% огня — поражение.")]
    [SerializeField] private GameObject towerZoneTrigger;
    
    [Header("Lose Text (100% fire + player in tower zone)")]
    [Tooltip("Текст для сообщения о поражении (красный)")]
    [SerializeField] private TextMeshProUGUI loseText;
    
    [Tooltip("Текст поражения (русский)")]
    [SerializeField] private string loseTextRu = "Вы проиграли!";
    
    [Tooltip("Текст поражения (английский)")]
    [SerializeField] private string loseTextEn = "You lose!";
    
    [Header("Got Brainrot Text (player with brainrot exits tower zone)")]
    [Tooltip("Текст для сообщения «Вы получили {имя брейнрота}» (ярко-зелёный)")]
    [SerializeField] private TextMeshProUGUI gotBrainrotText;
    
    [Tooltip("Формат сообщения (русский), {0} = имя брейнрота")]
    [SerializeField] private string gotBrainrotFormatRu = "Вы получили {0}";
    
    [Tooltip("Формат сообщения (английский), {0} = имя брейнрота")]
    [SerializeField] private string gotBrainrotFormatEn = "You got {0}";
    
    [Header("Notification Animation")]
    [Tooltip("Через сколько секунд скрывать уведомление (0 = не скрывать автоматически)")]
    [SerializeField] private float notificationHideAfterSeconds = 3f;
    
    [Tooltip("Длительность fade-анимации скрытия текста (сек)")]
    [SerializeField] private float notificationFadeDuration = 0.5f;
    
    [Tooltip("Длительность одного пульса масштаба текста (сек)")]
    [SerializeField] private float notificationPulseDuration = 0.4f;
    
    [Tooltip("Максимальный масштаб при пульсе (1 = без увеличения)")]
    [SerializeField] private float notificationPulseMaxScale = 1.2f;
    
    private GameObject fadeCanvasInstance;
    private Image fadeImage;
    private bool isFading = false;
    
    // Позиция игрока в лобби (для возврата)
    private Vector3 lobbyPlayerPosition;
    private Quaternion lobbyPlayerRotation;
    
    // Ссылка на игрока
    private Transform playerTransform;
    private ThirdPersonController playerController;
    
    // Игрок в зоне башни (триггер обновляется TowerZoneTrigger)
    private bool playerInTowerZone = false;
    
    // Уже обработали поражение от огня (чтобы не повторять)
    private bool loseFromFireHandled = false;
    
    private DestroyFireManager destroyFireManager;
    
    private bool teleportingDueToLose = false;
    
    private Coroutine notificationHideCoroutine;
    private Coroutine notificationPulseCoroutine;
    private Vector3 cachedLoseTextBaseScale = Vector3.one;
    private Vector3 cachedGotBrainrotTextBaseScale = Vector3.one;
    
    private static TeleportManager instance;
    
    public static TeleportManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<TeleportManager>();
            }
            return instance;
        }
    }
    
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        // Создаем fade canvas если префаб не назначен
        if (fadeCanvasPrefab == null)
        {
            CreateFadeCanvas();
        }
    }
    
    private void Start()
    {
        FindPlayer();
        
        if (loseText != null)
            loseText.gameObject.SetActive(false);
        if (gotBrainrotText != null)
            gotBrainrotText.gameObject.SetActive(false);
        
        destroyFireManager = FindFirstObjectByType<DestroyFireManager>();
        if (destroyFireManager != null)
            destroyFireManager.OnProgressComplete += OnDestroyFireProgressComplete;
    }
    
    private void OnDestroy()
    {
        if (destroyFireManager != null)
            destroyFireManager.OnProgressComplete -= OnDestroyFireProgressComplete;
    }
    
    private void Update()
    {
        if (loseFromFireHandled && destroyFireManager != null && destroyFireManager.GetProgress() < 1f)
            loseFromFireHandled = false;
    }
    
    /// <summary>
    /// Вызывается TowerZoneTrigger при входе игрока в зону башни.
    /// </summary>
    public void SetPlayerInTowerZone(bool inside)
    {
        playerInTowerZone = inside;
    }
    
    /// <summary>
    /// Вызывается TowerZoneTrigger при выходе игрока из зоны с брейнротом в руках.
    /// </summary>
    public void OnPlayerExitedTowerZoneWithBrainrot(string brainrotName)
    {
        if (string.IsNullOrEmpty(brainrotName)) return;
        if (gotBrainrotText == null) return;
        
        string format = IsRussianLanguage() ? gotBrainrotFormatRu : gotBrainrotFormatEn;
        gotBrainrotText.text = string.Format(format, brainrotName);
        gotBrainrotText.color = new Color(0.2f, 1f, 0.2f);
        gotBrainrotText.gameObject.SetActive(true);
        cachedGotBrainrotTextBaseScale = gotBrainrotText.transform.localScale;
        SetNotificationAlpha(gotBrainrotText, 1f);
        StartNotificationPulse(gotBrainrotText.transform, cachedGotBrainrotTextBaseScale);
        StartNotificationHideAfterDelay(gotBrainrotText, cachedGotBrainrotTextBaseScale);
    }
    
    private void OnDestroyFireProgressComplete()
    {
        if (loseFromFireHandled) return;
        if (!playerInTowerZone) return;
        
        loseFromFireHandled = true;
        teleportingDueToLose = true;
        ShowLoseText();
        RemoveCarriedBrainrot();
        TeleportToHouse();
    }
    
    private void ShowLoseText()
    {
        if (loseText == null) return;
        loseText.text = IsRussianLanguage() ? loseTextRu : loseTextEn;
        loseText.color = Color.red;
        loseText.gameObject.SetActive(true);
        cachedLoseTextBaseScale = loseText.transform.localScale;
        SetNotificationAlpha(loseText, 1f);
        StartNotificationPulse(loseText.transform, cachedLoseTextBaseScale);
        StartNotificationHideAfterDelay(loseText, cachedLoseTextBaseScale);
    }
    
    private void StartNotificationPulse(Transform textTransform, Vector3 baseScale)
    {
        if (textTransform == null || notificationPulseDuration <= 0f) return;
        if (notificationPulseCoroutine != null)
            StopCoroutine(notificationPulseCoroutine);
        notificationPulseCoroutine = StartCoroutine(PulseScaleOnceCoroutine(textTransform, notificationPulseDuration, notificationPulseMaxScale, baseScale));
    }
    
    private IEnumerator PulseScaleOnceCoroutine(Transform textTransform, float duration, float maxScale, Vector3 baseScale)
    {
        if (textTransform == null) yield break;
        float half = duration * 0.5f;
        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            float s = Mathf.Lerp(1f, maxScale, t);
            textTransform.localScale = baseScale * s;
            yield return null;
        }
        textTransform.localScale = baseScale * maxScale;
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            float s = Mathf.Lerp(maxScale, 1f, t);
            textTransform.localScale = baseScale * s;
            yield return null;
        }
        textTransform.localScale = baseScale;
        notificationPulseCoroutine = null;
    }
    
    private void SetNotificationAlpha(TextMeshProUGUI text, float alpha)
    {
        if (text == null) return;
        Color c = text.color;
        c.a = alpha;
        text.color = c;
    }
    
    private void StartNotificationHideAfterDelay(TextMeshProUGUI text, Vector3 baseScale)
    {
        if (text == null || notificationHideAfterSeconds <= 0f) return;
        if (notificationHideCoroutine != null)
            StopCoroutine(notificationHideCoroutine);
        notificationHideCoroutine = StartCoroutine(HideNotificationAfterDelayCoroutine(text, baseScale));
    }
    
    private IEnumerator HideNotificationAfterDelayCoroutine(TextMeshProUGUI text, Vector3 baseScale)
    {
        if (text == null) yield break;
        yield return new WaitForSeconds(notificationHideAfterSeconds);
        if (text == null) yield break;
        if (notificationFadeDuration <= 0f)
        {
            text.transform.localScale = baseScale;
            text.gameObject.SetActive(false);
            notificationHideCoroutine = null;
            yield break;
        }
        float elapsed = 0f;
        Color startColor = text.color;
        while (elapsed < notificationFadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, Mathf.Clamp01(elapsed / notificationFadeDuration));
            Color c = startColor;
            c.a = alpha;
            text.color = c;
            yield return null;
        }
        SetNotificationAlpha(text, 0f);
        text.transform.localScale = baseScale;
        text.gameObject.SetActive(false);
        notificationHideCoroutine = null;
    }
    
    private void RemoveCarriedBrainrot()
    {
        if (playerTransform == null) return;
        PlayerCarryController carry = playerTransform.GetComponent<PlayerCarryController>();
        if (carry == null)
            carry = FindFirstObjectByType<PlayerCarryController>();
        if (carry == null) return;
        
        BrainrotObject carried = carry.GetCurrentCarriedObject();
        if (carried != null)
        {
            carry.DropObject();
            if (carried.gameObject != null)
                Destroy(carried.gameObject);
        }
    }
    
    private static bool IsRussianLanguage()
    {
        string lang = LocalizationManager.GetCurrentLanguage();
        return lang == "ru" || string.IsNullOrEmpty(lang);
    }
    
    /// <summary>
    /// Находит игрока в сцене
    /// </summary>
    private void FindPlayer()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
                playerController = player.GetComponent<ThirdPersonController>();
            }
            else
            {
                ThirdPersonController controller = FindFirstObjectByType<ThirdPersonController>();
                if (controller != null)
                {
                    playerTransform = controller.transform;
                    playerController = controller;
                }
            }
        }
    }
    
    /// <summary>
    /// Создает fade canvas вручную если префаб не назначен
    /// </summary>
    private void CreateFadeCanvas()
    {
        // Создаем Canvas
        GameObject canvasObj = new GameObject("FadeCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // Высокий приоритет
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Создаем Image для затемнения
        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(canvasObj.transform, false);
        
        Image image = imageObj.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f); // Прозрачный
        
        RectTransform rectTransform = imageObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.anchoredPosition = Vector2.zero;
        
        fadeCanvasInstance = canvasObj;
        fadeImage = image;
        
        // Скрываем canvas по умолчанию
        canvasObj.SetActive(false);
    }
    
    /// <summary>
    /// Телепортирует игрока в зону сражения (ОТКЛЮЧЕНО - BattleZone удалена)
    /// </summary>
    public void TeleportToBattleZone(BrainrotObject brainrotObject)
    {
        Debug.LogWarning("[TeleportManager] TeleportToBattleZone отключен - BattleZone удалена из проекта");
    }
    
    /// <summary>
    /// Телепортирует игрока в указанную позицию
    /// </summary>
    public void TeleportToPosition(Vector3 position, Quaternion rotation)
    {
        if (isFading)
        {
            Debug.LogWarning("[TeleportManager] Телепортация уже выполняется, пропускаем");
            return;
        }
        
        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null)
            {
                Debug.LogError("[TeleportManager] Игрок не найден!");
                return;
            }
        }
        
        StartCoroutine(TeleportToPositionCoroutine(position, rotation));
    }
    
    /// <summary>
    /// Телепортирует игрока в дом (после победы над боссом)
    /// </summary>
    public void TeleportToHouse()
    {
        if (housePos == null)
        {
            Debug.LogError("[TeleportManager] HousePos не назначен! Установите Transform в инспекторе.");
            return;
        }
        
        TeleportToPosition(housePos.position, housePos.rotation);
    }
    
    /// <summary>
    /// Телепортирует игрока в дом и помещает брейнрота в руки после телепортации
    /// </summary>
    public void TeleportToHouseWithBrainrot(BrainrotObject brainrot)
    {
        if (housePos == null)
        {
            Debug.LogError("[TeleportManager] HousePos не назначен!");
            return;
        }
        
        StartCoroutine(TeleportToHouseWithBrainrotCoroutine(brainrot));
    }
    
    private IEnumerator TeleportToHouseWithBrainrotCoroutine(BrainrotObject brainrot)
    {
        isFading = true;
        
        // Затемняем экран
        yield return StartCoroutine(FadeOut());
        
        // Отключаем CharacterController перед телепортацией
        CharacterController characterController = playerTransform.GetComponent<CharacterController>();
        bool wasControllerEnabled = false;
        if (characterController != null)
        {
            wasControllerEnabled = characterController.enabled;
            characterController.enabled = false;
        }
        
        // Телепортируем игрока в дом
        playerTransform.position = housePos.position;
        playerTransform.rotation = housePos.rotation;
        
        // Ждём кадр чтобы позиция применилась
        yield return null;
        
        // Включаем CharacterController обратно
        if (characterController != null && wasControllerEnabled)
        {
            characterController.enabled = true;
        }
        
        // Сбрасываем камеру
        ResetCameraAfterTeleport();
        
        // Обновляем видимость брейнротов
        BrainrotDistanceHider distanceHider = FindFirstObjectByType<BrainrotDistanceHider>();
        if (distanceHider != null)
        {
            distanceHider.ForceRefresh();
        }
        
        // ПОСЛЕ телепортации помещаем брейнрота в руки
        if (brainrot != null)
        {
            PlayerCarryController carryController = playerTransform.GetComponent<PlayerCarryController>();
            if (carryController == null)
            {
                carryController = FindFirstObjectByType<PlayerCarryController>();
            }
            
            if (carryController != null && carryController.CanCarry())
            {
                // Активируем брейнрота
                brainrot.SetUnfought(false);
                brainrot.gameObject.SetActive(true);
                
                // Активируем все дочерние объекты и рендереры
                foreach (Transform child in brainrot.transform)
                {
                    if (child != null)
                    {
                        child.gameObject.SetActive(true);
                    }
                }
                
                Renderer[] renderers = brainrot.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = true;
                        renderer.gameObject.SetActive(true);
                    }
                }
                
                // Устанавливаем позицию брейнрота к игроку
                Vector3 carryOffset = new Vector3(
                    brainrot.GetCarryOffsetX(),
                    brainrot.GetCarryOffsetY(),
                    brainrot.GetCarryOffsetZ()
                );
                brainrot.transform.position = playerTransform.position + 
                    playerTransform.forward * carryOffset.z + 
                    playerTransform.right * carryOffset.x + 
                    playerTransform.up * carryOffset.y;
                
                // Берём брейнрота в руки
                brainrot.Take();
            }
        }
        
        // Осветляем экран
        yield return StartCoroutine(FadeIn());
        
        isFading = false;
    }
    
    /// <summary>
    /// Телепортирует игрока обратно в дом (после поражения)
    /// </summary>
    public void TeleportToLobby()
    {
        // Останавливаем все текущие корутины телепортации
        StopAllCoroutines();
        isFading = false;
        
        if (playerTransform == null)
        {
            FindPlayer();
            if (playerTransform == null)
            {
                Debug.LogError("[TeleportManager] Игрок не найден!");
                return;
            }
        }
        
        if (!gameObject.activeInHierarchy)
        {
            gameObject.SetActive(true);
        }
        
        StartCoroutine(TeleportToLobbyCoroutine());
    }
    
    /// <summary>
    /// Телепортирует игрока в указанную позицию (корутина)
    /// </summary>
    private IEnumerator TeleportToPositionCoroutine(Vector3 position, Quaternion rotation)
    {
        isFading = true;
        
        // Затемняем экран
        yield return StartCoroutine(FadeOut());
        
        // Телепортируем игрока в указанную позицию
        if (playerTransform == null)
        {
            FindPlayer();
        }
        
        if (playerTransform != null)
        {
            // Отключаем CharacterController перед телепортацией
            CharacterController characterController = playerTransform.GetComponent<CharacterController>();
            bool wasControllerEnabled = false;
            if (characterController != null)
            {
                wasControllerEnabled = characterController.enabled;
                characterController.enabled = false;
            }
            
            playerTransform.position = position;
            playerTransform.rotation = rotation;
            
            // Ждем несколько кадров, чтобы позиция игрока установилась
            yield return null;
            yield return null;
            
            // Включаем CharacterController обратно
            if (characterController != null && wasControllerEnabled)
            {
                characterController.enabled = true;
            }
        }
        
        // Сбрасываем камеру после телепортации
        ResetCameraAfterTeleport();
        
        // ВАЖНО: Обновляем видимость брейнротов после телепортации
        BrainrotDistanceHider distanceHider = FindFirstObjectByType<BrainrotDistanceHider>();
        if (distanceHider != null)
        {
            distanceHider.ForceRefresh();
            Debug.Log("[TeleportManager] Видимость брейнротов обновлена после телепортации");
        }
        
        // Ждем еще один кадр после сброса камеры
        yield return null;
        
        // Осветляем экран
        yield return StartCoroutine(FadeIn());
        
        if (teleportingDueToLose)
            teleportingDueToLose = false;
        
        isFading = false;
    }
    
    private IEnumerator TeleportToLobbyCoroutine()
    {
        isFading = true;
        
        // Ждём 2 секунды перед телепортацией (чтобы игрок увидел результат поражения)
        yield return new WaitForSeconds(2f);
        
        // Затемняем экран
        yield return StartCoroutine(FadeOut());
        
        // ВАЖНО: Отключаем CharacterController перед телепортацией
        CharacterController characterController = playerTransform.GetComponent<CharacterController>();
        bool wasControllerEnabled = false;
        if (characterController != null)
        {
            wasControllerEnabled = characterController.enabled;
            characterController.enabled = false;
        }
        
        // Телепортируем игрока в дом (housePos), а не в лобби
        if (housePos != null)
        {
            playerTransform.position = housePos.position;
            playerTransform.rotation = housePos.rotation;
        }
        else
        {
            // Fallback на лобби если housePos не задан
            playerTransform.position = lobbyPlayerPosition;
            playerTransform.rotation = lobbyPlayerRotation;
        }
        
        // Ждём кадр чтобы позиция применилась
        yield return null;
        
        // Включаем CharacterController обратно
        if (characterController != null && wasControllerEnabled)
        {
            characterController.enabled = true;
        }
        
        // Сбрасываем камеру после телепортации
        ResetCameraAfterTeleport();
        
        // ВАЖНО: Обновляем видимость брейнротов после телепортации
        BrainrotDistanceHider distanceHider = FindFirstObjectByType<BrainrotDistanceHider>();
        if (distanceHider != null)
        {
            distanceHider.ForceRefresh();
        }
        
        // Осветляем экран
        yield return StartCoroutine(FadeIn());
        
        isFading = false;
    }
    
    /// <summary>
    /// Затемняет экран
    /// </summary>
    private IEnumerator FadeOut()
    {
        if (fadeImage == null)
        {
            Debug.LogWarning("[TeleportManager] FadeImage равен null, пропускаем fade");
            yield break;
        }
        
        float elapsed = 0f;
        Color color = fadeImage.color;
        
        // ВАЖНО: Убеждаемся, что цвет черный (RGB = 0, 0, 0)
        color.r = 0f;
        color.g = 0f;
        color.b = 0f;
        
        while (elapsed < fadeSpeed)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / fadeSpeed);
            color.a = alpha;
            fadeImage.color = color;
            yield return null;
        }
        
        // Устанавливаем полностью черный экран
        color.a = 1f;
        color.r = 0f;
        color.g = 0f;
        color.b = 0f;
        fadeImage.color = color;
    }
    
    /// <summary>
    /// Осветляет экран
    /// </summary>
    private IEnumerator FadeIn()
    {
        if (fadeImage == null)
        {
            Debug.LogWarning("[TeleportManager] FadeImage равен null, пропускаем fade");
            yield break;
        }
        
        float elapsed = 0f;
        Color color = fadeImage.color;
        
        // ВАЖНО: Убеждаемся, что цвет черный (RGB = 0, 0, 0)
        color.r = 0f;
        color.g = 0f;
        color.b = 0f;
        
        while (elapsed < fadeSpeed)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - (elapsed / fadeSpeed));
            color.a = alpha;
            fadeImage.color = color;
            yield return null;
        }
        
        // Полностью прозрачный черный
        color.a = 0f;
        color.r = 0f;
        color.g = 0f;
        color.b = 0f;
        fadeImage.color = color;
        
        // Скрываем canvas после завершения
        if (fadeCanvasInstance != null)
        {
            fadeCanvasInstance.SetActive(false);
        }
    }
    
    
    /// <summary>
    /// Сбрасывает камеру после телепортации, чтобы предотвратить слишком сильное приближение
    /// </summary>
    private void ResetCameraAfterTeleport()
    {
        // Находим камеру
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            mainCamera = FindFirstObjectByType<Camera>();
        }
        
        if (mainCamera != null)
        {
            // Сначала обновляем ThirdPersonCamera, чтобы камера была на правильной позиции
            ThirdPersonCamera thirdPersonCamera = mainCamera.GetComponent<ThirdPersonCamera>();
            if (thirdPersonCamera != null)
            {
                // Принудительно обновляем позицию камеры
                // Это гарантирует, что камера будет на правильном расстоянии
                thirdPersonCamera.ForceUpdateCameraPosition();
            }
            
            // Затем сбрасываем CameraCollisionHandler, чтобы он пересчитал расстояние
            CameraCollisionHandler collisionHandler = mainCamera.GetComponent<CameraCollisionHandler>();
            if (collisionHandler != null)
            {
                // Обновляем цель камеры, если нужно
                if (playerTransform != null)
                {
                    Transform cameraTarget = playerTransform.Find("CameraTarget");
                    if (cameraTarget != null)
                    {
                        collisionHandler.SetTarget(cameraTarget);
                    }
                }
                
                // Принудительно сбрасываем камеру после телепортации
                // Это полностью пересчитывает направление и расстояние, предотвращая быстрое приближение
                collisionHandler.ForceResetAfterTeleport();
            }
        }
    }
}
