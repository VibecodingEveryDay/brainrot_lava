using UnityEngine;

/// <summary>
/// Зона старта/финиша (объект с Collider Is Trigger). Отслеживает вход/выход игрока и уведомляет TeleportManager и MusicManager.
/// </summary>
public class StartFinishZone : MonoBehaviour
{
    [Tooltip("Тег игрока")]
    [SerializeField] private string playerTag = "Player";

    private void OnTriggerEnter(Collider other)
    {
        if (other == null) return;

        // Мяч, попавший в зону — удаляем
        Ball ball = other.GetComponent<Ball>();
        if (ball == null && other.attachedRigidbody != null)
            ball = other.attachedRigidbody.GetComponent<Ball>();
        if (ball != null)
        {
            Destroy(ball.gameObject);
            return;
        }

        if (other.CompareTag("Bot"))
        {
            BotCarryController botCarry = other.GetComponent<BotCarryController>();
            if (botCarry == null) botCarry = other.GetComponentInParent<BotCarryController>();
            if (botCarry != null && botCarry.GetCurrentCarriedObject() != null)
            {
                BrainrotObject br = botCarry.GetCurrentCarriedObject();
                Destroy(br.gameObject);
                Destroy(botCarry.GetCarrierTransform().gameObject);
            }
            return;
        }

        if (!other.CompareTag(playerTag)) return;

        if (TeleportManager.Instance != null)
            TeleportManager.Instance.SetPlayerInStartFinishZone(true);

        if (MusicManager.Instance != null)
            MusicManager.Instance.SetPlayerInFightZone(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null || !other.CompareTag(playerTag)) return;

        if (TeleportManager.Instance != null)
        {
            TeleportManager.Instance.SetPlayerInStartFinishZone(false);

            PlayerCarryController carry = other.GetComponent<PlayerCarryController>();
            if (carry == null) carry = other.GetComponentInParent<PlayerCarryController>();
            BrainrotObject brainrot = carry != null ? carry.GetCurrentCarriedObject() : null;
            if (brainrot != null)
                TeleportManager.Instance.OnPlayerExitedStartFinishWithBrainrot(brainrot.GetObjectName());
        }

        if (MusicManager.Instance != null)
            MusicManager.Instance.SetPlayerInFightZone(false);
    }
}
