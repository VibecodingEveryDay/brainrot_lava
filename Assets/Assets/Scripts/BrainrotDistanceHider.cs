using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Скрывает/показывает брейнротов в зависимости от дистанции до игрока (для оптимизации).
/// ВАЖНО: по умолчанию выключает только Renderer'ы (объекты остаются активными, логика не ломается).
/// </summary>
public class BrainrotDistanceHider : MonoBehaviour
{
    [Header("Настройки")]
    [Tooltip("Transform игрока. Если не задан, будет найден автоматически по тегу 'Player'.")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("Дистанция, дальше которой брейнроты будут скрываться (от игрока).")]
    [SerializeField] private float hideRange = 35f;

    [Tooltip("Как часто обновлять видимость (сек). 0 = каждый кадр.")]
    [SerializeField] private float updateInterval = 0.2f;

    public enum HideMode
    {
        RenderersOnly = 0,
        DisableGameObject = 1
    }

    [Tooltip("Способ скрытия. RenderersOnly безопаснее для логики; DisableGameObject агрессивнее, но гарантированно скрывает объект полностью.")]
    [SerializeField] private HideMode hideMode = HideMode.RenderersOnly;

    [Tooltip("Если включено, то при скрытии также отключаются Collider'ы (доп. оптимизация, но может влиять на взаимодействие издалека).")]
    [SerializeField] private bool disableCollidersWhenHidden = false;

    [Tooltip("Исключать из скрытия брейнроты, которые сейчас в руках у игрока.")]
    [SerializeField] private bool ignoreCarriedBrainrots = true;

    [Tooltip("Периодически пересобирать кэш брейнротов (полезно, если они спавнятся/удаляются во время игры). 0 = не пересобирать автоматически.")]
    [SerializeField] private float autoRebuildCacheInterval = 5f;

    [Header("Отладка")]
    [SerializeField] private bool debug = false;

    private float timer;
    private float rebuildTimer;

    private readonly List<Entry> entries = new List<Entry>(256);

    private struct Entry
    {
        public BrainrotObject brainrot;
        public Renderer[] renderers;
        public Collider[] colliders;
        public bool currentlyHidden;
    }

    private void Awake()
    {
        EnsurePlayer();
        RebuildCache();
    }

    private void OnEnable()
    {
        timer = 0f;
        rebuildTimer = 0f;
        // На случай, если объекты/сцена изменились пока компонент был выключен
        RebuildCache();
        EnsurePlayer();
        RefreshNow();
    }

    private void Update()
    {
        EnsurePlayer();
        if (playerTransform == null) return;

        if (autoRebuildCacheInterval > 0f)
        {
            rebuildTimer += Time.deltaTime;
            if (rebuildTimer >= autoRebuildCacheInterval)
            {
                rebuildTimer = 0f;
                RebuildCache();
            }
        }

        // Если кэш пуст (например, брейнроты появились позже) — пытаемся пересобрать.
        if (entries.Count == 0)
        {
            RebuildCache();
        }

        if (updateInterval <= 0f)
        {
            RefreshNow();
            return;
        }

        timer += Time.deltaTime;
        if (timer >= updateInterval)
        {
            timer = 0f;
            RefreshNow();
        }
    }

    [ContextMenu("Rebuild Cache")]
    public void RebuildCache()
    {
        entries.Clear();

        BrainrotObject[] brainrots = FindObjectsByType<BrainrotObject>(FindObjectsSortMode.None);
        if (brainrots == null || brainrots.Length == 0) return;

        for (int i = 0; i < brainrots.Length; i++)
        {
            BrainrotObject br = brainrots[i];
            if (br == null) continue;

            Entry e = new Entry
            {
                brainrot = br,
                renderers = br.GetComponentsInChildren<Renderer>(true),
                colliders = disableCollidersWhenHidden ? br.GetComponentsInChildren<Collider>(true) : null,
                currentlyHidden = false
            };

            entries.Add(e);
        }

        if (debug)
        {
            Debug.Log($"[BrainrotDistanceHider] Cache rebuilt. Brainrots: {entries.Count}");
        }
    }

    private void RefreshNow()
    {
        if (playerTransform == null) return;

        float hideRangeSqr = hideRange * hideRange;

        // Проходим по кэшу; если брейнрот уничтожен — пропускаем (периодически можно RebuildCache вручную)
        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];
            if (e.brainrot == null)
            {
                continue;
            }

            if (ignoreCarriedBrainrots && e.brainrot.IsCarried())
            {
                if (e.currentlyHidden)
                {
                    SetHidden(ref e, false);
                    entries[i] = e;
                }
                continue;
            }

            float distSqr = (e.brainrot.transform.position - playerTransform.position).sqrMagnitude;
            bool shouldHide = distSqr > hideRangeSqr;

            if (shouldHide != e.currentlyHidden)
            {
                SetHidden(ref e, shouldHide);
                entries[i] = e;
            }
        }
    }

    private void SetHidden(ref Entry e, bool hidden)
    {
        e.currentlyHidden = hidden;

        if (hideMode == HideMode.DisableGameObject)
        {
            // ВАЖНО: если объект в руках — мы выше уже выходим continue, так что сюда он не попадет.
            e.brainrot.gameObject.SetActive(!hidden);
            return;
        }

        if (e.renderers != null)
        {
            for (int r = 0; r < e.renderers.Length; r++)
            {
                if (e.renderers[r] != null)
                {
                    e.renderers[r].enabled = !hidden;
                }
            }
        }

        if (disableCollidersWhenHidden && e.colliders != null)
        {
            for (int c = 0; c < e.colliders.Length; c++)
            {
                if (e.colliders[c] != null)
                {
                    e.colliders[c].enabled = !hidden;
                }
            }
        }

        if (debug)
        {
            Debug.Log($"[BrainrotDistanceHider] {(hidden ? "HIDE" : "SHOW")} {e.brainrot.name}");
        }
    }

    private void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        playerTransform = player != null ? player.transform : null;
    }

    private void EnsurePlayer()
    {
        if (playerTransform != null) return;
        FindPlayer();
    }
    
    /// <summary>
    /// Принудительно обновляет видимость всех брейнротов (полезно после телепортации)
    /// </summary>
    public void ForceRefresh()
    {
        // Сбрасываем таймеры, чтобы обновление произошло немедленно
        timer = 0f;
        
        // ВАЖНО: Пересобираем кэш, чтобы убедиться, что все брейнроты учтены
        RebuildCache();
        
        // ВАЖНО: Сначала принудительно показываем все брейнроты (включаем рендереры)
        // Это гарантирует, что они будут видны, даже если система считает их скрытыми
        for (int i = 0; i < entries.Count; i++)
        {
            Entry e = entries[i];
            if (e.brainrot == null)
            {
                continue;
            }
            
            // Пропускаем брейнроты в руках
            if (ignoreCarriedBrainrots && e.brainrot.IsCarried())
            {
                continue;
            }
            
            // Принудительно показываем все рендереры
            if (e.renderers != null)
            {
                for (int r = 0; r < e.renderers.Length; r++)
                {
                    if (e.renderers[r] != null)
                    {
                        e.renderers[r].enabled = true;
                    }
                }
            }
            
            // Принудительно включаем коллайдеры, если они были отключены
            if (disableCollidersWhenHidden && e.colliders != null)
            {
                for (int c = 0; c < e.colliders.Length; c++)
                {
                    if (e.colliders[c] != null)
                    {
                        e.colliders[c].enabled = true;
                    }
                }
            }
            
            // Если используется режим DisableGameObject, активируем объект
            if (hideMode == HideMode.DisableGameObject)
            {
                if (e.brainrot.gameObject != null)
                {
                    e.brainrot.gameObject.SetActive(true);
                }
            }
            
            // Сбрасываем флаг скрытости, чтобы система пересчитала видимость
            e.currentlyHidden = false;
            entries[i] = e;
        }
        
        // Немедленно обновляем видимость (теперь система правильно скроет те, что далеко)
        RefreshNow();
        
        if (debug)
        {
            Debug.Log($"[BrainrotDistanceHider] ForceRefresh вызван - кэш пересобран, все брейнроты принудительно показаны, затем видимость пересчитана. Всего брейнротов: {entries.Count}");
        }
    }
}

