using UnityEngine;

/// <summary>
/// Маркер и обработчик мяча: при столкновении с игроком проигрывается звук и игрок отталкивается в -Z (без телепорта).
/// Повесь на префаб мяча вместе с Rigidbody и коллайдером.
/// </summary>
public class Ball : MonoBehaviour
{
    [Tooltip("Тег объекта игрока")]
    [SerializeField] private string playerTag = "Player";
    
    [Header("Sound")]
    [Tooltip("Звук, который проигрывается при столкновении мяча с игроком")]
    [SerializeField] private AudioClip hitPlayerClip;
    [Range(0f, 1f)]
    [Tooltip("Громкость звука столкновения с игроком (0-1, без дистанционного заглушения)")]
    [SerializeField] private float hitPlayerVolume = 1f;
    
    // Общий 2D-аудиоисточник для всех мячей, чтобы звук был таким же громким, как в инспекторе
    private static AudioSource sharedHitAudioSource;
    
    [Header("Отталкивание игрока")]
    [Tooltip("Сила отталкивания игрока в сторону отрицательного Z при столкновении с мячом")]
    [SerializeField] private float playerPushForce = 25f;
    
    [Header("Бот")]
    [Tooltip("Через сколько секунд после удара мячом удалять бота (и брейнрот в руках)")]
    [SerializeField] private float botDestroyDelay = 2f;
    
    private Rigidbody _rb;
    
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
    }
    
    private void OnCollisionEnter(Collision collision)
    {
        if (collision == null || collision.gameObject == null) return;
        
        if (collision.gameObject.CompareTag(playerTag))
        {
            HandleHitPlayer(collision.gameObject);
            return;
        }
        
        if (collision.gameObject.CompareTag("Bot"))
        {
            HandleHitBot(collision.gameObject);
            return;
        }
        
        // Отскок: инвертируем угловую скорость, чтобы мяч крутился в обратную сторону
        if (_rb != null)
            _rb.angularVelocity = -_rb.angularVelocity;
    }
    
    private void HandleHitPlayer(GameObject playerObject)
    {
        // Звук столкновения с игроком.
        if (hitPlayerClip != null)
        {
            if (sharedHitAudioSource == null)
            {
                AudioListener listener = FindFirstObjectByType<AudioListener>();
                GameObject host = listener != null ? listener.gameObject : gameObject;
                sharedHitAudioSource = host.GetComponent<AudioSource>();
                if (sharedHitAudioSource == null)
                {
                    sharedHitAudioSource = host.AddComponent<AudioSource>();
                    sharedHitAudioSource.playOnAwake = false;
                }
                sharedHitAudioSource.spatialBlend = 0f;
            }
            sharedHitAudioSource.PlayOneShot(hitPlayerClip, hitPlayerVolume);
        }
        
        TeleportManager tm = TeleportManager.Instance;
        if (tm != null)
        {
            tm.RemoveCarriedBrainrot();
            tm.ShowLoseText();
            tm.TeleportPlayerToHouseAfterDelay(1f);
        }
        
        // Отталкиваем игрока с большой силой в сторону отрицательного Z
        ThirdPersonController tpc = playerObject.GetComponent<ThirdPersonController>();
        if (tpc == null) tpc = playerObject.GetComponentInParent<ThirdPersonController>();
        if (tpc != null)
            tpc.AddKnockback(Vector3.back * playerPushForce);
    }
    
    private void HandleHitBot(GameObject botObject)
    {
        BotBehavior bot = botObject.GetComponent<BotBehavior>();
        if (bot == null) bot = botObject.GetComponentInParent<BotBehavior>();
        if (bot != null)
            bot.OnHitByBall(playerPushForce, botDestroyDelay);
    }
}
