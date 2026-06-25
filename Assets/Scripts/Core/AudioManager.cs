using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGM")]
    [SerializeField] private AudioMixerGroup bgmMixerGroup;
    [SerializeField] private float defaultBgmFadeTime = 0.35f;

    [Header("SFX")]
    [SerializeField] private AudioMixerGroup sfxMixerGroup;
    [SerializeField, Min(1)] private int sfxVoiceCount = 8;

    private readonly AudioSource[] bgmSources = new AudioSource[2];
    private readonly List<AudioSource> sfxSources = new List<AudioSource>();

    private Coroutine bgmFadeRoutine;
    private int activeBgmSourceIndex = -1;
    private float bgmVolume = 1f;
    private float sfxVolume = 1f;

    public float BgmVolume => bgmVolume;
    public float SfxVolume => sfxVolume;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureBgmSources();
        EnsureSfxSources();
    }

    public void PlayBGM(AudioClip clip, bool loop = true, float fadeTime = -1f)
    {
        if (clip == null)
        {
            return;
        }

        EnsureBgmSources();

        if (fadeTime < 0f)
        {
            fadeTime = defaultBgmFadeTime;
        }

        if (bgmFadeRoutine != null)
        {
            StopCoroutine(bgmFadeRoutine);
            bgmFadeRoutine = null;
        }

        var nextSourceIndex = GetInactiveBgmSourceIndex();
        var nextSource = bgmSources[nextSourceIndex];
        var currentSource = GetActiveBgmSource();

        if (currentSource != null && currentSource.clip == clip && currentSource.isPlaying)
        {
            currentSource.loop = loop;
            currentSource.volume = bgmVolume;
            return;
        }

        nextSource.clip = clip;
        nextSource.loop = loop;
        nextSource.volume = fadeTime <= 0f ? bgmVolume : 0f;
        nextSource.Play();

        if (fadeTime <= 0f || currentSource == null || !currentSource.isPlaying)
        {
            StopActiveBgmSource();
            nextSource.volume = bgmVolume;
            activeBgmSourceIndex = nextSourceIndex;
            return;
        }

        bgmFadeRoutine = StartCoroutine(CrossFadeBgmRoutine(currentSource, nextSource, fadeTime, nextSourceIndex));
    }

    public void StopBGM(float fadeOutTime = 0f)
    {
        var currentSource = GetActiveBgmSource();
        if (currentSource == null)
        {
            return;
        }

        if (bgmFadeRoutine != null)
        {
            StopCoroutine(bgmFadeRoutine);
            bgmFadeRoutine = null;
        }

        if (fadeOutTime <= 0f)
        {
            StopActiveBgmSource();
            return;
        }

        bgmFadeRoutine = StartCoroutine(FadeOutBgmRoutine(currentSource, fadeOutTime));
    }

    public void PauseBGM()
    {
        var currentSource = GetActiveBgmSource();
        if (currentSource != null && currentSource.isPlaying)
        {
            currentSource.Pause();
        }
    }

    public void ResumeBGM()
    {
        var currentSource = GetActiveBgmSource();
        if (currentSource != null)
        {
            currentSource.UnPause();
        }
    }

    public AudioSource PlaySFX(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null)
        {
            return null;
        }

        EnsureSfxSources();

        var source = GetAvailableSfxSource();
        source.clip = clip;
        source.loop = false;
        source.pitch = pitch;
        source.volume = Mathf.Clamp01(volume) * sfxVolume;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = sfxMixerGroup;
        source.Play();

        return source;
    }

    public AudioSource PlaySFXAtPoint(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f, float spatialBlend = 1f)
    {
        if (clip == null)
        {
            return null;
        }

        EnsureSfxSources();

        var source = GetAvailableSfxSource();
        source.transform.position = position;
        source.clip = clip;
        source.loop = false;
        source.pitch = pitch;
        source.volume = Mathf.Clamp01(volume) * sfxVolume;
        source.spatialBlend = Mathf.Clamp01(spatialBlend);
        source.outputAudioMixerGroup = sfxMixerGroup;
        source.Play();

        return source;
    }

    public void SetBGMVolume(float volume)
    {
        var previousVolume = bgmVolume;
        bgmVolume = Mathf.Clamp01(volume);

        if (previousVolume <= 0f)
        {
            for (var i = 0; i < bgmSources.Length; i++)
            {
                if (bgmSources[i] != null && bgmSources[i].isPlaying)
                {
                    bgmSources[i].volume = bgmVolume;
                }
            }

            return;
        }

        var ratio = bgmVolume / previousVolume;
        for (var i = 0; i < bgmSources.Length; i++)
        {
            if (bgmSources[i] != null && bgmSources[i].isPlaying)
            {
                bgmSources[i].volume *= ratio;
            }
        }
    }

    public void SetSFXVolume(float volume)
    {
        var previousVolume = sfxVolume;
        sfxVolume = Mathf.Clamp01(volume);

        if (previousVolume <= 0f)
        {
            for (var i = 0; i < sfxSources.Count; i++)
            {
                if (sfxSources[i] != null && sfxSources[i].isPlaying)
                {
                    sfxSources[i].volume = sfxVolume;
                }
            }

            return;
        }

        var ratio = sfxVolume / previousVolume;
        for (var i = 0; i < sfxSources.Count; i++)
        {
            if (sfxSources[i] != null && sfxSources[i].isPlaying)
            {
                sfxSources[i].volume *= ratio;
            }
        }
    }

    private void EnsureBgmSources()
    {
        for (var i = 0; i < bgmSources.Length; i++)
        {
            if (bgmSources[i] != null)
            {
                continue;
            }

            var child = new GameObject($"BGM Source {i}");
            child.transform.SetParent(transform, false);

            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = bgmMixerGroup;
            source.volume = bgmVolume;

            bgmSources[i] = source;
        }
    }

    private void EnsureSfxSources()
    {
        while (sfxSources.Count < sfxVoiceCount)
        {
            var index = sfxSources.Count;
            var child = new GameObject($"SFX Source {index}");
            child.transform.SetParent(transform, false);

            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.outputAudioMixerGroup = sfxMixerGroup;

            sfxSources.Add(source);
        }
    }

    private AudioSource GetActiveBgmSource()
    {
        if (activeBgmSourceIndex < 0 || activeBgmSourceIndex >= bgmSources.Length)
        {
            return null;
        }

        return bgmSources[activeBgmSourceIndex];
    }

    private int GetInactiveBgmSourceIndex()
    {
        if (activeBgmSourceIndex < 0)
        {
            return 0;
        }

        return activeBgmSourceIndex == 0 ? 1 : 0;
    }

    private void StopActiveBgmSource()
    {
        var currentSource = GetActiveBgmSource();
        if (currentSource != null)
        {
            currentSource.Stop();
            currentSource.clip = null;
        }

        activeBgmSourceIndex = -1;
    }

    private AudioSource GetAvailableSfxSource()
    {
        for (var i = 0; i < sfxSources.Count; i++)
        {
            if (!sfxSources[i].isPlaying)
            {
                return sfxSources[i];
            }
        }

        var source = sfxSources[0];
        source.Stop();
        return source;
    }

    private IEnumerator CrossFadeBgmRoutine(AudioSource fromSource, AudioSource toSource, float duration, int toIndex)
    {
        var fromStartVolume = fromSource.volume;
        var time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            var t = Mathf.Clamp01(time / duration);
            fromSource.volume = Mathf.Lerp(fromStartVolume, 0f, t);
            toSource.volume = Mathf.Lerp(0f, bgmVolume, t);
            yield return null;
        }

        fromSource.Stop();
        fromSource.clip = null;
        toSource.volume = bgmVolume;
        activeBgmSourceIndex = toIndex;
        bgmFadeRoutine = null;
    }

    private IEnumerator FadeOutBgmRoutine(AudioSource source, float duration)
    {
        var startVolume = source.volume;
        var time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            source.volume = Mathf.Lerp(startVolume, 0f, Mathf.Clamp01(time / duration));
            yield return null;
        }

        StopActiveBgmSource();
        bgmFadeRoutine = null;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
