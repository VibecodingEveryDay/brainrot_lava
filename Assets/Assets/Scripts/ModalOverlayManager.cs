using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// При открытии любого модального окна: показывает полупрозрачный оверлей (затемнение 20%) позади модалки
/// и выставляет IsAnyModalOpen = true (камера отдаляется на 25%).
/// Список модальных контейнеров задаётся в инспекторе или ищется по имени.
/// </summary>
public class ModalOverlayManager : MonoBehaviour
{
    public static bool IsAnyModalOpen { get; private set; }
    
    [Header("Модальные контейнеры")]
    [Tooltip("Объекты модальных окон (SpeedModalContainer, LocksModalContainer и т.д.). Если пусто — ищем по именам.")]
    [SerializeField] private GameObject[] modalContainers;
    
    [Header("Оверлей (затемнение 20%)")]
    [Tooltip("Полноэкранная панель затемнения. Если не назначена — создаётся автоматически.")]
    [SerializeField] private Image overlayImage;
    
    [Tooltip("Прозрачность затемнения (0–1). 0.2 = 20% затемнения.")]
    [SerializeField] [Range(0f, 1f)] private float overlayAlpha = 0.2f;
    
    private Canvas _canvas;
    private GameObject _overlayRoot;
    private bool _wasAnyOpen;

    private void Awake()
    {
        if (modalContainers == null || modalContainers.Length == 0)
        {
            var list = new System.Collections.Generic.List<GameObject>();
            TryFindModal(list, "SpeedModalContainer");
            TryFindModal(list, "LocksModalContainer");
            TryFindModal(list, "ModalContainer");
            modalContainers = list.ToArray();
        }
        
        EnsureOverlay();
    }
    
    private static void TryFindModal(System.Collections.Generic.List<GameObject> list, string name)
    {
        var go = GameObject.Find(name);
        if (go != null) list.Add(go);
    }
    
    private void EnsureOverlay()
    {
        if (overlayImage != null)
        {
            _overlayRoot = overlayImage.gameObject;
            if (!_overlayRoot.activeSelf) _overlayRoot.SetActive(false);
            return;
        }
        
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null) return;
        
        _canvas = canvas;
        _overlayRoot = new GameObject("ModalBlurOverlay");
        _overlayRoot.transform.SetParent(canvas.transform, false);
        _overlayRoot.SetActive(false);
        
        RectTransform rect = _overlayRoot.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        
        overlayImage = _overlayRoot.AddComponent<Image>();
        overlayImage.color = new Color(0f, 0f, 0f, overlayAlpha);
        overlayImage.raycastTarget = false;
        
        _overlayRoot.transform.SetAsFirstSibling();
    }
    
    private void Update()
    {
        bool anyOpen = false;
        if (modalContainers != null)
        {
            for (int i = 0; i < modalContainers.Length; i++)
            {
                if (modalContainers[i] != null && modalContainers[i].activeInHierarchy)
                {
                    anyOpen = true;
                    break;
                }
            }
        }
        
        IsAnyModalOpen = anyOpen;

        if (_overlayRoot != null)
        {
            if (anyOpen)
            {
                if (!_overlayRoot.activeSelf)
                    _overlayRoot.SetActive(true);
                if (!_wasAnyOpen)
                    _overlayRoot.transform.SetAsFirstSibling();
            }
            else if (_overlayRoot.activeSelf)
                _overlayRoot.SetActive(false);
        }

        _wasAnyOpen = anyOpen;
        
        if (overlayImage != null && overlayImage.color.a != overlayAlpha)
            overlayImage.color = new Color(0f, 0f, 0f, overlayAlpha);
    }
}
