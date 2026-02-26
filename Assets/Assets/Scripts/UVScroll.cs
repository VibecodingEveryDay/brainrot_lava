using UnityEngine;

/// <summary>
/// Двигает UV текстур материала (эффект скролла). Для лавы, воды и т.п.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class UVScroll : MonoBehaviour
{
    [SerializeField] private Vector2 scrollSpeed = new Vector2(0.1f, 0.05f);
    [SerializeField] private string texturePropertyName = "_BaseMap"; // URP: _BaseMap, Built-in: _MainTex
    
    private Material _material;
    private Vector2 _offset;

    void Start()
    {
        var renderer = GetComponent<Renderer>();
        _material = Application.isPlaying ? renderer.material : renderer.sharedMaterial;
        if (_material == null || !_material.HasProperty(texturePropertyName))
            texturePropertyName = "_MainTex"; // fallback
    }

    void Update()
    {
        if (_material == null) return;
        _offset += scrollSpeed * Time.deltaTime;
        _offset.x = _offset.x % 1f;
        _offset.y = _offset.y % 1f;
        _material.SetTextureOffset(texturePropertyName, _offset);
    }
}
