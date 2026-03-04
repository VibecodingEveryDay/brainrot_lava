using System.Collections;
using UnityEngine;

/// <summary>
/// NPC для покупки: списывает деньги игрока и скрывает выбранную стену.
/// </summary>
public class NpcGuy : InteractableObject
{
    [Header("Purchase Settings")]
    [Tooltip("Цена покупки")]
    [SerializeField] private long price = 100;
    
    [Tooltip("ID стены для скрытия (1, 2 или 3)")]
    [SerializeField] private int wallId = 1;
    
    [Tooltip("Уникальный ключ для сохранения «NPC убран» (если пусто — wallId + имя объекта)")]
    [SerializeField] private string npcPersistId;

    [Header("Animator")]
    [SerializeField] private Animator animator;
    [SerializeField] private string actionTriggerName = "action";
    [SerializeField] private string actionStateName = "action";

    private WallManager wallManager;
    private Coroutine resetTriggerCoroutine;
    private Coroutine removeAfterDelayCoroutine;

    protected override void Awake()
    {
        base.Awake();
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
    }

    private void Start()
    {
        if (GameStorage.Instance != null && GameStorage.Instance.IsNpcGuyRemoved(GetNpcPersistKey()))
        {
            gameObject.SetActive(false);
        }
    }

    private string GetNpcPersistKey()
    {
        if (!string.IsNullOrEmpty(npcPersistId)) return npcPersistId;
        return "NpcGuy_" + wallId + "_" + gameObject.name;
    }

    protected override bool ShouldShowInteractionUI()
    {
        return !IsTargetWallHidden();
    }

    protected override void ConfigureInteractionText(InteractionTextUpdater textUpdater)
    {
        if (textUpdater != null)
        {
            textUpdater.SetCustomInteractionText("Купить", "Buy");
        }
    }

    protected override void OnInteractionComplete()
    {
        if (IsTargetWallHidden())
        {
            return;
        }

        if (price <= 0)
        {
            Debug.LogWarning($"[NpcGuy] {gameObject.name}: цена <= 0, покупка отменена.");
            ResetInteraction();
            return;
        }

        if (GameStorage.Instance == null)
        {
            Debug.LogWarning($"[NpcGuy] {gameObject.name}: GameStorage.Instance не найден.");
            ResetInteraction();
            return;
        }

        bool moneySpent = GameStorage.Instance.SubtractBalanceLong(price);
        if (!moneySpent)
        {
            Debug.Log($"[NpcGuy] {gameObject.name}: недостаточно денег для покупки. Цена: {price}");
            ResetInteraction();
            return;
        }

        EnsureWallManager();
        if (wallManager == null)
        {
            Debug.LogWarning($"[NpcGuy] {gameObject.name}: WallManager не найден.");
            ResetInteraction();
            return;
        }

        bool hidden = wallManager.HideWallById(wallId, saveToStorage: true, delayNpcHide: true);
        if (!hidden)
        {
            Debug.LogWarning($"[NpcGuy] {gameObject.name}: не удалось скрыть стену с wallId={wallId}.");
            ResetInteraction();
            return;
        }

        TriggerActionAnimation();
        
        // Через 5 сек удалить NPC и не показывать снова, если стена с wallId разблокирована (скрыта)
        if (removeAfterDelayCoroutine != null)
            StopCoroutine(removeAfterDelayCoroutine);
        removeAfterDelayCoroutine = StartCoroutine(RemoveAfterDelayIfWallUnlocked());
    }

    private IEnumerator RemoveAfterDelayIfWallUnlocked()
    {
        yield return new WaitForSeconds(5f);
        removeAfterDelayCoroutine = null;
        
        if (!IsTargetWallHidden())
            yield break;
        
        if (GameStorage.Instance != null)
        {
            GameStorage.Instance.SetNpcGuyRemoved(GetNpcPersistKey());
        }
        gameObject.SetActive(false);
    }

    private bool IsTargetWallHidden()
    {
        if (GameStorage.Instance != null && GameStorage.Instance.IsWallHidden(wallId))
        {
            return true;
        }

        EnsureWallManager();
        if (wallManager != null)
        {
            return wallManager.IsWallHidden(wallId);
        }

        return false;
    }

    private void EnsureWallManager()
    {
        if (wallManager == null)
        {
            wallManager = WallManager.Instance;
            if (wallManager == null)
            {
                wallManager = FindFirstObjectByType<WallManager>();
            }
        }
    }

    private void TriggerActionAnimation()
    {
        if (animator == null)
        {
            return;
        }

        animator.ResetTrigger(actionTriggerName);
        animator.SetTrigger(actionTriggerName);

        if (resetTriggerCoroutine != null)
        {
            StopCoroutine(resetTriggerCoroutine);
        }
        resetTriggerCoroutine = StartCoroutine(ResetActionTriggerWhenFinished());
    }

    private IEnumerator ResetActionTriggerWhenFinished()
    {
        if (animator == null)
        {
            yield break;
        }

        const int layer = 0;
        float enterTimeout = 5f;

        while (enterTimeout > 0f && !animator.GetCurrentAnimatorStateInfo(layer).IsName(actionStateName))
        {
            enterTimeout -= Time.deltaTime;
            yield return null;
        }

        float exitTimeout = 8f;
        while (exitTimeout > 0f && animator.GetCurrentAnimatorStateInfo(layer).IsName(actionStateName))
        {
            exitTimeout -= Time.deltaTime;
            yield return null;
        }

        animator.ResetTrigger(actionTriggerName);
        resetTriggerCoroutine = null;
    }
}
