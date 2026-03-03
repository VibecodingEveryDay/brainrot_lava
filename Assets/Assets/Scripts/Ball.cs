using UnityEngine;

/// <summary>
/// Маркер и обработчик мяча: при столкновении с игроком вызывается проигрыш (UI, телепорт на базу, удаление брейнрота и всех мячей).
/// Повесь на префаб мяча вместе с Rigidbody и коллайдером.
/// </summary>
public class Ball : MonoBehaviour
{
    [Tooltip("Тег объекта игрока")]
    [SerializeField] private string playerTag = "Player";

    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.gameObject == null) return;
        if (!collision.gameObject.CompareTag(playerTag)) return;

        TeleportManager tm = TeleportManager.Instance;
        if (tm != null)
        {
            tm.OnPlayerHitByBall();
        }
    }
}
