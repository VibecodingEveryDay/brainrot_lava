using UnityEngine;

/// <summary>
/// Куб/объект с коллайдером: мяч от него отскакивает, игрок и всё остальное проходят сквозь (коллизия только с Ball).
/// Чтобы игрок не «вставал» на стену на несколько кадров: в инспекторе назначьте объектам стен слой WallBall до запуска сцены.
/// </summary>
[DefaultExecutionOrder(-200)]
public class WallBall : MonoBehaviour
{
    [Tooltip("Тег игрока (с ним коллизия отключается через IgnoreCollision)")]
    [SerializeField] private string playerTag = "Player";

    private static GameObject _cachedPlayer;
    private static Collider[] _cachedPlayerColliders;

    private void Awake()
    {
        int wallBallLayer = LayerMask.NameToLayer("WallBall");
        if (wallBallLayer >= 0)
            SetLayerRecursively(gameObject, wallBallLayer);
        IgnoreCollisionWithPlayer();
    }

    private static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        for (int i = 0; i < go.transform.childCount; i++)
            SetLayerRecursively(go.transform.GetChild(i).gameObject, layer);
    }

    private void IgnoreCollisionWithPlayer()
    {
        if (_cachedPlayer == null)
        {
            _cachedPlayer = GameObject.FindGameObjectWithTag(playerTag);
            if (_cachedPlayer == null) return;
            _cachedPlayerColliders = _cachedPlayer.GetComponentsInChildren<Collider>(true);
        }

        Collider[] myColliders = GetComponentsInChildren<Collider>(true);
        Collider[] playerColliders = _cachedPlayerColliders;
        if (playerColliders == null) return;

        for (int i = 0; i < myColliders.Length; i++)
        {
            if (myColliders[i] == null || !myColliders[i].enabled) continue;
            for (int j = 0; j < playerColliders.Length; j++)
            {
                if (playerColliders[j] == null || !playerColliders[j].enabled) continue;
                Physics.IgnoreCollision(myColliders[i], playerColliders[j], true);
            }
        }
    }
}
