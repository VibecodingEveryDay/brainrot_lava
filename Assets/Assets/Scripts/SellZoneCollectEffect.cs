using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

/// <summary>
/// Исчезающий эффект при продаже брейнрота в SellZone: полупрозрачный блок над зоной,
/// анимация 500ms (увеличение + затухание), как у EarnPanel.
/// </summary>
public class SellZoneCollectEffect : MonoBehaviour
{
    [SerializeField] private float duration = 0.5f;
    [SerializeField] [Range(1.2f, 1.8f)] private float sizeMultiplierXZ = 1.45f;
    [SerializeField] [Range(1.2f, 3f)] private float sizeMultiplierY = 2.2f;
    [SerializeField] private Color effectColor = new Color(1f, 0.85f, 0.2f, 0.35f);

    private MeshRenderer meshRenderer;
    private Material effectMaterial;
    private float elapsed;
    private Vector3 startScale;
    private Vector3 endScale;

    public void Init(Transform zoneTransform)
    {
        if (zoneTransform == null) return;

        GameObject meshObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        meshObj.name = "SellZoneEffectMesh";
        meshObj.transform.SetParent(transform, false);
        meshObj.transform.localPosition = Vector3.zero;
        meshObj.transform.localRotation = Quaternion.identity;

        Collider col = meshObj.GetComponent<Collider>();
        if (col != null) Destroy(col);

        meshRenderer = meshObj.GetComponent<MeshRenderer>();
        effectMaterial = CreateTransparentMaterial();
        meshRenderer.material = effectMaterial;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        transform.position = zoneTransform.position;
        transform.rotation = zoneTransform.rotation;

        Vector3 zoneLossyScale = zoneTransform.lossyScale;
        Vector3 absScale = new Vector3(Mathf.Abs(zoneLossyScale.x), Mathf.Abs(zoneLossyScale.y), Mathf.Abs(zoneLossyScale.z));
        startScale = absScale * 1f;
        endScale = new Vector3(absScale.x * sizeMultiplierXZ, absScale.y * sizeMultiplierY, absScale.z * sizeMultiplierXZ);

        transform.localScale = startScale;
        elapsed = 0f;
    }

    private Material CreateTransparentMaterial()
    {
        Material loaded = Resources.Load<Material>("game/Materials/EarnPanelEffect");
        if (loaded != null)
        {
            Material instance = new Material(loaded);
            instance.SetColor("_BaseColor", effectColor);
            if (instance.HasProperty("_Color")) instance.SetColor("_Color", effectColor);
            return instance;
        }

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");

        Material mat = new Material(urpLit);
        mat.name = "SellZoneEffect_Generated";
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        mat.SetColor("_BaseColor", effectColor);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", effectColor);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        return mat;
    }

    private void Update()
    {
        if (meshRenderer == null || effectMaterial == null) return;

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);
        float scaleT = 1f - Mathf.Pow(1f - t, 2f);
        transform.localScale = Vector3.Lerp(startScale, endScale, scaleT);

        Color c = effectColor;
        c.a = effectColor.a * (1f - t);
        effectMaterial.SetColor("_BaseColor", c);
        if (effectMaterial.HasProperty("_Color")) effectMaterial.SetColor("_Color", c);

        if (elapsed >= duration)
        {
            if (effectMaterial != null) Destroy(effectMaterial);
            Destroy(gameObject);
        }
    }
}
