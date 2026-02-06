using UnityEngine;
using System.Collections;

/// <summary>
/// Менеджер музыки. Управляет фоновой музыкой в игре.
/// Singleton — доступен через MusicManager.Instance
/// </summary>
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }
    
    [Header("Music Clips")]
    [Tooltip("Музыка для лобби/дома")]
    [SerializeField] private AudioClip lobbyMusic;
    
    [Tooltip("Музыка для боя")]
    [SerializeField] private AudioClip fightMusic;
    
    [Header("Settings")]
    [Tooltip("Громкость музыки (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 0.5f;
    
    [Tooltip("Время плавного перехода между треками (секунды)")]
    [SerializeField] private float crossfadeDuration = 1.5f;
    
    [Tooltip("Зацикливать музыку")]
    [SerializeField] private bool loop = true;
    
    // Два AudioSource для плавного перехода (crossfade)
    private AudioSource audioSourceA;
    private AudioSource audioSourceB;
    private bool isPlayingA = true;
    
    private Coroutine crossfadeCoroutine;
    private Coroutine zoneSwitchCoroutine;
    
    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(transform.root.gameObject); // Музыка продолжает играть между сценами (root для работы с дочерними объектами)
        
        // Создаём два AudioSource для crossfade
        audioSourceA = gameObject.AddComponent<AudioSource>();
        audioSourceB = gameObject.AddComponent<AudioSource>();
        
        SetupAudioSource(audioSourceA);
        SetupAudioSource(audioSourceB);
    }
    
    private void Start()
    {
        // Вне ZoneCollider — lobby, в ZoneCollider — fight (переключает TowerZoneTrigger)
        PlayLobbyMusic();
        StartCoroutine(PreloadFightMusicCoroutine());
    }
    
    /// <summary>
    /// Подгружает музыку боя при старте (прогрев на неактивном источнике), чтобы при первом входе в зону не было просадки FPS.
    /// Ждём завершения crossfade lobby, чтобы не перезаписать играющий источник.
    /// </summary>
    private IEnumerator PreloadFightMusicCoroutine()
    {
        if (fightMusic == null) yield break;
        yield return new WaitForSeconds(crossfadeDuration + 0.2f);
        AudioSource inactive = isPlayingA ? audioSourceB : audioSourceA;
        inactive.clip = fightMusic;
        inactive.volume = 0f;
        inactive.Play();
        yield return null;
        inactive.Pause();
    }
    
    /// <summary>
    /// Вызывается TowerZoneTrigger: игрок вошёл (true) или вышел (false) из зоны боя.
    /// Переключение отложено на следующий кадр, чтобы не вызывать просадку FPS в кадр входа в триггер.
    /// </summary>
    public void SetPlayerInFightZone(bool inZone)
    {
        if (zoneSwitchCoroutine != null)
            StopCoroutine(zoneSwitchCoroutine);
        zoneSwitchCoroutine = StartCoroutine(SetZoneDelayedCoroutine(inZone));
    }
    
    private IEnumerator SetZoneDelayedCoroutine(bool inZone)
    {
        yield return null;
        if (inZone)
            PlayFightMusic();
        else
            PlayLobbyMusic();
        zoneSwitchCoroutine = null;
    }
    
    private void SetupAudioSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = loop;
        source.volume = 0f;
        source.spatialBlend = 0f; // 2D звук (не зависит от позиции)
    }
    
    /// <summary>
    /// Воспроизводит музыку лобби
    /// </summary>
    public void PlayLobbyMusic()
    {
        if (lobbyMusic != null)
        {
            CrossfadeTo(lobbyMusic);
        }
    }
    
    /// <summary>
    /// Воспроизводит музыку боя
    /// </summary>
    public void PlayFightMusic()
    {
        if (fightMusic != null)
        {
            CrossfadeTo(fightMusic);
        }
    }
    
    /// <summary>
    /// Плавный переход к новому треку
    /// </summary>
    private void CrossfadeTo(AudioClip newClip)
    {
        if (crossfadeCoroutine != null)
        {
            StopCoroutine(crossfadeCoroutine);
        }
        
        crossfadeCoroutine = StartCoroutine(CrossfadeCoroutine(newClip));
    }
    
    private IEnumerator CrossfadeCoroutine(AudioClip newClip)
    {
        AudioSource fadeOut = isPlayingA ? audioSourceA : audioSourceB;
        AudioSource fadeIn = isPlayingA ? audioSourceB : audioSourceA;
        
        // Если уже играет этот же трек — ничего не делаем
        if (fadeOut.clip == newClip && fadeOut.isPlaying)
        {
            yield break;
        }
        
        // Запускаем новый трек
        fadeIn.clip = newClip;
        fadeIn.volume = 0f;
        fadeIn.Play();
        
        float elapsed = 0f;
        float startVolumeOut = fadeOut.volume;
        
        while (elapsed < crossfadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / crossfadeDuration;
            
            fadeOut.volume = Mathf.Lerp(startVolumeOut, 0f, t);
            fadeIn.volume = Mathf.Lerp(0f, musicVolume, t);
            
            yield return null;
        }
        
        // Финальные значения
        fadeOut.volume = 0f;
        fadeOut.Stop();
        fadeIn.volume = musicVolume;
        
        isPlayingA = !isPlayingA;
        crossfadeCoroutine = null;
    }
    
    /// <summary>
    /// Устанавливает громкость музыки
    /// </summary>
    public void SetVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        
        // Обновляем громкость активного источника
        AudioSource active = isPlayingA ? audioSourceA : audioSourceB;
        if (active.isPlaying)
        {
            active.volume = musicVolume;
        }
    }
    
    /// <summary>
    /// Останавливает музыку с плавным затуханием
    /// </summary>
    public void StopMusic()
    {
        if (crossfadeCoroutine != null)
        {
            StopCoroutine(crossfadeCoroutine);
        }
        
        StartCoroutine(FadeOutCoroutine());
    }
    
    private IEnumerator FadeOutCoroutine()
    {
        AudioSource active = isPlayingA ? audioSourceA : audioSourceB;
        float startVolume = active.volume;
        float elapsed = 0f;
        
        while (elapsed < crossfadeDuration)
        {
            elapsed += Time.deltaTime;
            active.volume = Mathf.Lerp(startVolume, 0f, elapsed / crossfadeDuration);
            yield return null;
        }
        
        active.Stop();
        active.volume = 0f;
    }
}
