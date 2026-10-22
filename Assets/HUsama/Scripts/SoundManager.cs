using UnityEngine;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource sfxSource;      // One-shot sounds ke liye
    [SerializeField] private AudioSource loopSfxSource;  // Mixing loop ke liye

    [Header("Generic Interaction Sounds")]
    public AudioClip itemPickupSound;    // Mouse click hold
    public AudioClip itemReleaseSound;   // Shelf par wapis drop
    public AudioClip successSparkleSound;// Task complete sparkles
    public AudioClip slideWhooshSound;   // Bowl/Pan slide out

    private void Awake()
    {
        if (Instance != null && Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // AudioSources auto setup agar inspector se assign na hon
        if (sfxSource == null)
            sfxSource = gameObject.AddComponent<AudioSource>();

        if (loopSfxSource == null)
        {
            loopSfxSource = gameObject.AddComponent<AudioSource>();
            loopSfxSource.loop = true;
        }
    }

    // 1. Instant sound play karne ke liye (Click, Drop, Pour, Sparkle)
    public void PlaySound(AudioClip clip, float volume = 1f)
    {
        if (clip != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(clip, volume);
        }
    }

    // 2. Continuous mixing sound (Finger chalne par play, rukne par pause)
    public void PlayLoopingSound(AudioClip clip, float volume = 1f)
    {
        if (clip == null || loopSfxSource == null) return;

        if (loopSfxSource.clip != clip)
        {
            loopSfxSource.clip = clip;
        }

        loopSfxSource.volume = volume;
        if (!loopSfxSource.isPlaying)
            loopSfxSource.Play();
    }

    public void StopLoopingSound()
    {
        if (loopSfxSource != null && loopSfxSource.isPlaying)
            loopSfxSource.Stop();
    }
}
