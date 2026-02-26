using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

/// <summary>
/// Эффект при сборе дохода с EarnPanel: полупрозрачный блок вокруг панели,
/// анимируется (увеличивается), исчезает через 500ms.
/// Создаётся EarnPanel при CollectBalance.
/// </summary>
public class EarnPanelCollectEffect : MonoBehaviour
{
    [SerializeField] private float duration = 0.5f;
    [SerializeField] [Range(1.2f, 1.8f)] private float sizeMultiplierXZ = 1.45f; // Больше по X, Z
    [SerializeField] [Range(1.2f, 3f)] private float sizeMultiplierY = 2.2f;     // Ниже и выше по Y
    [SerializeField] private Color effectColor = new Color(1f, 0.9f, 0.3f, 0.4f);

    private MeshRenderer meshRenderer;
    private Material effectMaterial;
    private float elapsed;
    private Vector3 startScale;
    private Vector3 endScale;

    /// <summary>
    /// Инициализирует эффект. Вызывается из EarnPanel.
    /// </summary>
    /// <param name="earnPanelTransform">Transform меша EarnPanel (кнопки) для позиции и размера</param>
    public void Init(Transform earnPanelTransform)
    {
        if (earnPanelTransform == null) return;

        // Создаём меш куба (примитив Unity)
        GameObject meshObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        meshObj.name = "EffectMesh";
        meshObj.transform.SetParent(transform, false);
        meshObj.transform.localPosition = Vector3.zero;
        meshObj.transform.localRotation = Quaternion.identity;

        // Удаляем коллайдер — он не нужен
        Collider col = meshObj.GetComponent<Collider>();
        if (col != null) Destroy(col);

        meshRenderer = meshObj.GetComponent<MeshRenderer>();

        // Создаём прозрачный материал
        effectMaterial = CreateTransparentMaterial();
        meshRenderer.material = effectMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        // Позиция и масштаб относительно EarnPanel (+0.03 по local Z выше; при rotation 180° — -0.03)
        float zOffset = Mathf.Abs(Mathf.DeltaAngle(180f, earnPanelTransform.eulerAngles.y)) < 90f ? -0.03f : 0.03f;
        Vector3 localOffset = new Vector3(0f, 0f, zOffset);
        transform.position = earnPanelTransform.position + earnPanelTransform.TransformDirection(localOffset);
        transform.rotation = earnPanelTransform.rotation;

        Vector3 panelLossyScale = earnPanelTransform.lossyScale;
        Vector3 absScale = new Vector3(Mathf.Abs(panelLossyScale.x), Mathf.Abs(panelLossyScale.y), Mathf.Abs(panelLossyScale.z));
        startScale = absScale * 1f;
        endScale = new Vector3(absScale.x * sizeMultiplierXZ, absScale.y * sizeMultiplierY, absScale.z * sizeMultiplierXZ);

        transform.localScale = startScale;
        elapsed = 0f;
    }

    /// <summary>
    /// Создаёт прозрачный материал для URP.
    /// Surface Type: Transparent, Alpha Blend.
    /// </summary>
    private Material CreateTransparentMaterial()
    {
        // Пробуем загрузить из Resources
        Material loaded = Resources.Load<Material>("game/Materials/EarnPanelEffect");
        if (loaded != null)
        {
            return new Material(loaded);
        }

        // Создаём в коде для URP Lit (Surface Type = Transparent)
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            urpLit = Shader.Find("Lit");
        }
        if (urpLit == null)
        {
            Debug.LogWarning("[EarnPanelCollectEffect] URP Lit shader не найден, используем Standard");
            urpLit = Shader.Find("Standard");
        }

        Material mat = new Material(urpLit);
        mat.name = "EarnPanelEffect_Generated";

        // URP Lit: Surface Type = Transparent
        if (mat.HasProperty("_Surface"))
            mat.SetFloat("_Surface", 1f); // 1 = Transparent
        if (mat.HasProperty("_Blend"))
            mat.SetFloat("_Blend", 0f);   // Alpha
        if (mat.HasProperty("_SrcBlend"))
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend"))
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite"))
            mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000; // Transparent queue

        mat.SetColor("_BaseColor", effectColor);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", effectColor);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");

        return mat;
    }

    private void Update()
    {
        if (meshRenderer == null || effectMaterial == null) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        // Увеличение размера (ease-out)
        float scaleT = 1f - Mathf.Pow(1f - t, 2f);
        transform.localScale = Vector3.Lerp(startScale, endScale, scaleT);

        // Затухание прозрачности
        Color c = effectColor;
        c.a = effectColor.a * (1f - t);
        effectMaterial.SetColor("_BaseColor", c);
        if (effectMaterial.HasProperty("_Color"))
            effectMaterial.SetColor("_Color", c);

        if (elapsed >= duration)
        {
            if (effectMaterial != null)
                Destroy(effectMaterial);
            Destroy(gameObject);
        }
    }
}
