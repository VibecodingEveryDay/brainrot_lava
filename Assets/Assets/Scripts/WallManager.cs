using System.Collections;
using UnityEngine;

/// <summary>
/// Управляет видимостью трех стен через инспектор.
/// Если галочка hide включена, соответствующая стена скрывается.
/// </summary>
public class WallManager : MonoBehaviour
{
    public static WallManager Instance { get; private set; }

    [Header("Wall 1")]
    [SerializeField] private GameObject wall1;
    [SerializeField] private bool hideWall1;

    [Header("Wall 2")]
    [SerializeField] private GameObject wall2;
    [SerializeField] private bool hideWall2;

    [Header("Wall 3")]
    [SerializeField] private GameObject wall3;
    [SerializeField] private bool hideWall3;

    [Header("NPC (скрывать при разблокировке соответствующей стены)")]
    [SerializeField] private GameObject[] npcsForWall1;
    [SerializeField] private GameObject[] npcsForWall2;
    [SerializeField] private GameObject[] npcsForWall3;

    private const float NpcHideDelayAfterPurchase = 5f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        LoadWallStatesFromStorage();
        ApplyWallStates();
    }

    private void OnValidate()
    {
        ApplyWallStates();
    }

    [ContextMenu("Apply Wall States")]
    private void ApplyWallStates()
    {
        SetWallActive(wall1, hideWall1);
        SetWallActive(wall2, hideWall2);
        SetWallActive(wall3, hideWall3);
        ApplyNpcVisibility();
    }

    private void ApplyNpcVisibility()
    {
        SetNpcsActive(npcsForWall1, hideWall1);
        SetNpcsActive(npcsForWall2, hideWall2);
        SetNpcsActive(npcsForWall3, hideWall3);
    }

    private static void SetNpcsActive(GameObject[] npcs, bool wallHidden)
    {
        if (npcs == null) return;
        foreach (GameObject npc in npcs)
        {
            if (npc != null)
                npc.SetActive(!wallHidden);
        }
    }

    /// <param name="delayNpcHide">Если true, NPC для этой стены скрываются через 5 сек (после покупки), а не сразу</param>
    public bool HideWallById(int wallId, bool saveToStorage = true, bool delayNpcHide = false)
    {
        if (!TrySetHideFlagById(wallId, true))
        {
            Debug.LogWarning($"[WallManager] Неизвестный wallId: {wallId}. Используйте 1, 2 или 3.");
            return false;
        }

        SetWallActive(wall1, hideWall1);
        SetWallActive(wall2, hideWall2);
        SetWallActive(wall3, hideWall3);

        if (delayNpcHide)
        {
            StartCoroutine(HideNpcsForWallAfterDelay(wallId));
        }
        else
        {
            ApplyNpcVisibility();
        }

        if (saveToStorage && GameStorage.Instance != null)
        {
            GameStorage.Instance.SetWallHidden(wallId, true);
        }

        return true;
    }

    private IEnumerator HideNpcsForWallAfterDelay(int wallId)
    {
        yield return new WaitForSeconds(NpcHideDelayAfterPurchase);
        ApplyNpcVisibilityForWall(wallId);
    }

    private void ApplyNpcVisibilityForWall(int wallId)
    {
        switch (wallId)
        {
            case 1: SetNpcsActive(npcsForWall1, hideWall1); break;
            case 2: SetNpcsActive(npcsForWall2, hideWall2); break;
            case 3: SetNpcsActive(npcsForWall3, hideWall3); break;
        }
    }

    public bool ShowWallById(int wallId, bool saveToStorage = true)
    {
        if (!TrySetHideFlagById(wallId, false))
        {
            Debug.LogWarning($"[WallManager] Неизвестный wallId: {wallId}. Используйте 1, 2 или 3.");
            return false;
        }

        ApplyWallStates();

        if (saveToStorage && GameStorage.Instance != null)
        {
            GameStorage.Instance.SetWallHidden(wallId, false);
        }

        return true;
    }

    public bool IsWallHidden(int wallId)
    {
        switch (wallId)
        {
            case 1: return hideWall1;
            case 2: return hideWall2;
            case 3: return hideWall3;
            default: return false;
        }
    }

    public void ApplySavedStates()
    {
        LoadWallStatesFromStorage();
        ApplyWallStates();
    }

    private void LoadWallStatesFromStorage()
    {
        if (GameStorage.Instance == null)
        {
            return;
        }

        hideWall1 = GameStorage.Instance.IsWallHidden(1);
        hideWall2 = GameStorage.Instance.IsWallHidden(2);
        hideWall3 = GameStorage.Instance.IsWallHidden(3);
    }

    private bool TrySetHideFlagById(int wallId, bool hide)
    {
        switch (wallId)
        {
            case 1:
                hideWall1 = hide;
                return true;
            case 2:
                hideWall2 = hide;
                return true;
            case 3:
                hideWall3 = hide;
                return true;
            default:
                return false;
        }
    }

    private static void SetWallActive(GameObject wall, bool hide)
    {
        if (wall == null)
        {
            return;
        }

        wall.SetActive(!hide);
    }
}
