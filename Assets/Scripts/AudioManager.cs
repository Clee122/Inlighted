using UnityEngine;

public class AudioManager : MonoBehaviour
{
    // The current scene exposes one AudioManager through Instance so gameplay
    // scripts can request sounds without needing individual Inspector references
    // to the manager on every Player component.
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource loopingSfxSource;

    [Header("Scene Music")]
    // Each scene can assign its own music here. This lets the Main Menu and
    // Main Level use the same AudioManager script without hard-coding tracks.
    [SerializeField] private AudioClip sceneMusic;

    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float sfxVolume = 1f;

    [Header("Debug")]
    [SerializeField] private bool showAudioDebugLogs = false;

    private void Awake()
    {
        // Only one AudioManager should control a scene. Rejecting a duplicate
        // prevents two managers from playing the same music or SFX together.
        if (
            Instance != null &&
            Instance != this
        )
        {
            Debug.LogWarning(
                "A second AudioManager was found in the scene and will be disabled."
            );

            gameObject.SetActive(false);
            return;
        }

        Instance = this;

        ConfigureAudioSources();
    }

    private void Start()
    {
        // Scene music starts after Awake has finished configuring the sources.
        // Leaving Scene Music empty is valid while final audio assets are pending.
        if (sceneMusic != null)
        {
            PlayMusic(sceneMusic);
        }
        else if (showAudioDebugLogs)
        {
            Debug.Log(
                "AudioManager has no Scene Music assigned."
            );
        }
    }

    private void ConfigureAudioSources()
    {
        if (musicSource != null)
        {
            // Music should continue until deliberately replaced or the scene ends.
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.volume = musicVolume;
        }
        else
        {
            Debug.LogWarning(
                "AudioManager Music Source has not been assigned."
            );
        }

        if (sfxSource != null)
        {
            // One-shot gameplay sounds are triggered manually at the exact
            // gameplay event rather than automatically when the scene begins.
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
            sfxSource.volume = sfxVolume;
        }
        else
        {
            Debug.LogWarning(
                "AudioManager SFX Source has not been assigned."
            );
        }

        if (loopingSfxSource != null)
        {
            // Walking and channeling need audio that can continue for an
            // unknown duration and stop immediately when their state ends.
            loopingSfxSource.loop = true;
            loopingSfxSource.playOnAwake = false;
            loopingSfxSource.volume = sfxVolume;
        }
        else
        {
            Debug.LogWarning(
                "AudioManager Looping SFX Source has not been assigned."
            );
        }
    }

    public void PlayMusic(
        AudioClip musicClip
    )
    {
        if (
            musicSource == null ||
            musicClip == null
        )
        {
            return;
        }

        // Restarting the same music unnecessarily could produce an audible
        // interruption, so an already-playing track is left alone.
        if (
            musicSource.clip == musicClip &&
            musicSource.isPlaying
        )
        {
            return;
        }

        musicSource.clip = musicClip;
        musicSource.volume = musicVolume;
        musicSource.Play();

        if (showAudioDebugLogs)
        {
            Debug.Log(
                "Music started: " +
                musicClip.name
            );
        }
    }

    public void StopMusic()
    {
        if (musicSource == null)
        {
            return;
        }

        musicSource.Stop();
        musicSource.clip = null;

        if (showAudioDebugLogs)
        {
            Debug.Log(
                "Music stopped."
            );
        }
    }

    public void PlaySFX(
        AudioClip audioClip
    )
    {
        if (
            sfxSource == null ||
            audioClip == null
        )
        {
            return;
        }

        // PlayOneShot allows short gameplay sounds such as Jump, Burst and
        // Hurt to overlap naturally instead of cutting one another off.
        sfxSource.PlayOneShot(
            audioClip,
            sfxVolume
        );

        if (showAudioDebugLogs)
        {
            Debug.Log(
                "One-shot SFX played: " +
                audioClip.name
            );
        }
    }

    public void StartLoopingSFX(
        AudioClip audioClip
    )
    {
        if (
            loopingSfxSource == null ||
            audioClip == null
        )
        {
            return;
        }

        // If this exact loop is already active there is no reason to restart it.
        // This is especially important for walking because movement is checked
        // continuously while the player holds a direction.
        if (
            loopingSfxSource.clip == audioClip &&
            loopingSfxSource.isPlaying
        )
        {
            return;
        }

        loopingSfxSource.Stop();
        loopingSfxSource.clip = audioClip;
        loopingSfxSource.volume = sfxVolume;
        loopingSfxSource.Play();

        if (showAudioDebugLogs)
        {
            Debug.Log(
                "Looping SFX started: " +
                audioClip.name
            );
        }
    }

    public void StopLoopingSFX(
        AudioClip audioClip
    )
    {
        if (loopingSfxSource == null)
        {
            return;
        }

        // A script should only stop the loop it originally requested. For
        // example, a walking-state update should not accidentally stop a
        // channel sound that has replaced the walking loop.
        if (
            audioClip != null &&
            loopingSfxSource.clip != audioClip
        )
        {
            return;
        }

        if (showAudioDebugLogs && loopingSfxSource.clip != null)
        {
            Debug.Log(
                "Looping SFX stopped: " +
                loopingSfxSource.clip.name
            );
        }

        loopingSfxSource.Stop();
        loopingSfxSource.clip = null;
    }

    public void SetMusicVolume(
        float newVolume
    )
    {
        // Keeping volume adjustment inside the manager gives the Settings scene
        // one consistent place to control music later.
        musicVolume =
            Mathf.Clamp01(
                newVolume
            );

        if (musicSource != null)
        {
            musicSource.volume =
                musicVolume;
        }
    }

    public void SetSFXVolume(
        float newVolume
    )
    {
        // One shared SFX value keeps gameplay sounds and looping sounds balanced
        // through the same future Settings control.
        sfxVolume =
            Mathf.Clamp01(
                newVolume
            );

        if (sfxSource != null)
        {
            sfxSource.volume =
                sfxVolume;
        }

        if (loopingSfxSource != null)
        {
            loopingSfxSource.volume =
                sfxVolume;
        }
    }

    public float GetMusicVolume()
    {
        return musicVolume;
    }

    public float GetSFXVolume()
    {
        return sfxVolume;
    }

    private void OnDestroy()
    {
        // Clearing the static reference allows the next scene's AudioManager
        // to become Instance without being mistaken for a duplicate.
        if (Instance == this)
        {
            Instance = null;
        }
    }
}