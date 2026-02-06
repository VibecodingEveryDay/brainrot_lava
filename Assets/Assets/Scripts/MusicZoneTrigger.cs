using UnityEngine;

/// <summary>
/// Триггер зоны боя. Вешается на объект с Collider (isTrigger).
/// При входе игрока — музыка fight, при выходе — lobby.
/// </summary>
[RequireComponent(typeof(Collider))]
public class MusicZoneTrigger : MonoBehaviour
{
    [Tooltip("Тег игрока")]
    [SerializeField] private string playerTag = "Player";
    
    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
            col.isTrigger = true;
    }
    
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (MusicManager.Instance != null)
            MusicManager.Instance.SetPlayerInFightZone(true);
    }
    
    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        if (MusicManager.Instance != null)
            MusicManager.Instance.SetPlayerInFightZone(false);
    }
}
