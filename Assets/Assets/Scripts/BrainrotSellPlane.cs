using UnityEngine;
using System.Collections;
using System.Reflection;

/// <summary>
/// Плоскость для продажи брейнротов. InteractableObject: взаимодействие только при брейнроте в руках.
/// Текст UI: «Продать» / «Sell». По завершении взаимодействия продаёт брейнрот из рук игрока.
/// </summary>
public class BrainrotSellPlane : InteractableObject
{
    [Header("Настройки продажи")]
    [Tooltip("Множитель продажи (сколько раз умножается доход в секунду)")]
    [SerializeField] private float sellMultiplier = 20f;
    
    [Header("Визуал")]
    [Tooltip("Рендерер зоны продажи. Если не задан — берётся с этого объекта или из детей")]
    [SerializeField] private Renderer sellZoneRenderer;
    
    [Tooltip("Длительность мигания цветом при продаже")]
    [SerializeField] private float sellColorFlashDuration = 0.5f;
    
    [Tooltip("Цвет зоны по умолчанию")]
    [SerializeField] private Color defaultZoneColor = new Color(0.9f, 0.15f, 0.15f, 1f);
    
    [Tooltip("Цвет при «вспышке»")]
    [SerializeField] private Color flashColor = new Color(1f, 0.9f, 0.2f, 1f);
    
    [Header("Отладка")]
    [SerializeField] private bool debug = false;
    
    private Material sellZoneMaterial;
    private Coroutine colorFlashRoutine;
    private PlayerCarryController playerCarryController;
    
    protected override void Awake()
    {
        base.Awake();
        
        if (sellZoneRenderer == null)
            sellZoneRenderer = GetComponent<Renderer>();
        if (sellZoneRenderer == null)
            sellZoneRenderer = GetComponentInChildren<Renderer>();
        if (sellZoneRenderer != null && Application.isPlaying)
        {
            sellZoneMaterial = sellZoneRenderer.material;
            if (sellZoneMaterial != null)
            {
                if (sellZoneMaterial.HasProperty("_BaseColor"))
                    defaultZoneColor = sellZoneMaterial.GetColor("_BaseColor");
                else if (sellZoneMaterial.HasProperty("_Color"))
                    defaultZoneColor = sellZoneMaterial.GetColor("_Color");
            }
        }
        
        playerCarryController = FindFirstObjectByType<PlayerCarryController>();
    }
    
    private void EnsurePlayerCarry()
    {
        if (playerCarryController == null)
            playerCarryController = FindFirstObjectByType<PlayerCarryController>();
    }
    
    protected override bool ShouldShowInteractionUI()
    {
        // UI показываем всегда в радиусе; без брейнрота в руках взаимодействие просто ничего не сделает
        return true;
    }
    
    public override bool CanInteract()
    {
        EnsurePlayerCarry();
        return base.CanInteract() && playerCarryController != null && playerCarryController.GetCurrentCarriedObject() != null;
    }
    
    public override string GetInteractionButtonText()
    {
        string lang = LocalizationManager.GetCurrentLanguage();
        return (lang == "ru" || string.IsNullOrEmpty(lang)) ? "Продать" : "Sell";
    }
    
    protected override void ConfigureInteractionText(InteractionTextUpdater textUpdater)
    {
        if (textUpdater != null)
            textUpdater.SetCustomInteractionText("Продать", "Sell");
    }
    
    protected override void CompleteInteraction()
    {
        EnsurePlayerCarry();
        BrainrotObject brainrot = playerCarryController != null ? playerCarryController.GetCurrentCarriedObject() : null;
        if (brainrot == null)
        {
            base.CompleteInteraction();
            return;
        }
        playerCarryController.DropObject();
        SellBrainrot(brainrot);
        base.CompleteInteraction();
    }
    
    private void SellBrainrot(BrainrotObject brainrot)
    {
        if (brainrot == null || brainrot.gameObject == null) return;
        
        double incomePerSecond = brainrot.GetFinalIncome();
        double sellPrice = incomePerSecond * sellMultiplier;
        
        if (sellPrice <= 0)
        {
            if (debug)
                Debug.LogWarning($"[BrainrotSellPlane] Цена продажи <= 0 для '{brainrot.GetObjectName()}'");
            return;
        }
        
        string brainrotName = brainrot.GetObjectName();
        if (debug)
            Debug.Log($"[BrainrotSellPlane] Продаём '{brainrotName}': цена = {sellPrice}");
        
        if (GameStorage.Instance != null)
            GameStorage.Instance.AddBalanceDouble(sellPrice);
        else
            Debug.LogError("[BrainrotSellPlane] GameStorage.Instance не найден!");
        
        BalanceNotifyManager notifyManager = FindFirstObjectByType<BalanceNotifyManager>();
        if (notifyManager != null)
            notifyManager.UpdateNotificationImmediately(sellPrice);
        
        PlacementPanel linkedPlacementPanel = FindPlacementPanelWithBrainrot(brainrot);
        if (GameStorage.Instance != null)
        {
            GameStorage.Instance.RemoveBrainrotByName(brainrotName);
            if (linkedPlacementPanel != null)
                GameStorage.Instance.RemovePlacedBrainrot(linkedPlacementPanel.GetPanelID());
        }
        
        if (linkedPlacementPanel != null)
        {
            var placementPanelType = typeof(PlacementPanel);
            var placedBrainrotField = placementPanelType.GetField("placedBrainrot", BindingFlags.NonPublic | BindingFlags.Instance);
            if (placedBrainrotField != null)
                placedBrainrotField.SetValue(linkedPlacementPanel, null);
        }
        
        Destroy(brainrot.gameObject);
        PlaySellEffects();
    }
    
    private void PlaySellEffects()
    {
        if (colorFlashRoutine != null)
            StopCoroutine(colorFlashRoutine);
        colorFlashRoutine = StartCoroutine(AnimateSellZoneColor());
        GameObject effectObj = new GameObject("SellZoneEffect");
        effectObj.transform.SetParent(transform.parent != null ? transform.parent : transform);
        SellZoneCollectEffect effect = effectObj.AddComponent<SellZoneCollectEffect>();
        Transform visualT = sellZoneRenderer != null ? sellZoneRenderer.transform : transform;
        effect.Init(visualT);
    }
    
    private IEnumerator AnimateSellZoneColor()
    {
        if (sellZoneMaterial == null) { colorFlashRoutine = null; yield break; }
        float half = Mathf.Max(0.01f, sellColorFlashDuration * 0.5f);
        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            Color c = Color.Lerp(defaultZoneColor, flashColor, t);
            if (sellZoneMaterial.HasProperty("_BaseColor")) sellZoneMaterial.SetColor("_BaseColor", c);
            if (sellZoneMaterial.HasProperty("_Color")) sellZoneMaterial.SetColor("_Color", c);
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / half);
            Color c = Color.Lerp(flashColor, defaultZoneColor, t);
            if (sellZoneMaterial.HasProperty("_BaseColor")) sellZoneMaterial.SetColor("_BaseColor", c);
            if (sellZoneMaterial.HasProperty("_Color")) sellZoneMaterial.SetColor("_Color", c);
            yield return null;
        }
        if (sellZoneMaterial.HasProperty("_BaseColor")) sellZoneMaterial.SetColor("_BaseColor", defaultZoneColor);
        if (sellZoneMaterial.HasProperty("_Color")) sellZoneMaterial.SetColor("_Color", defaultZoneColor);
        colorFlashRoutine = null;
    }
    
    private PlacementPanel FindPlacementPanelWithBrainrot(BrainrotObject brainrot)
    {
        if (brainrot == null) return null;
        PlacementPanel[] allPanels = FindObjectsByType<PlacementPanel>(FindObjectsSortMode.None);
        var placementPanelType = typeof(PlacementPanel);
        var placedBrainrotField = placementPanelType.GetField("placedBrainrot", BindingFlags.NonPublic | BindingFlags.Instance);
        if (placedBrainrotField == null) return null;
        foreach (PlacementPanel panel in allPanels)
        {
            if (panel == null) continue;
            if (placedBrainrotField.GetValue(panel) as BrainrotObject == brainrot)
                return panel;
        }
        return null;
    }
}
