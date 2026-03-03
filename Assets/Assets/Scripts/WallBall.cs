using UnityEngine;

/// <summary>
/// Куб/объект с коллайдером: мяч от него отскакивает, игрок проходит сквозь (коллизия с игроком отключена).
/// </summary>
public class WallBall : MonoBehaviour
{
    [Tooltip("Тег игрока (с ним коллизия отключается)")]
    [SerializeField] private string playerTag = "Player";

    private static GameObject _cachedPlayer;
    private static Collider[] _cachedPlayerColliders;

    private void Start()
    {
        IgnoreCollisionWithPlayer();
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
