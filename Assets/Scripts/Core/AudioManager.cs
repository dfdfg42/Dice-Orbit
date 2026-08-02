using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Pool;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGM")]
    [SerializeField] private AudioMixerGroup bgmMixerGroup;
    [SerializeField] private float defaultBgmFadeTime = 0.35f;
    [Tooltip("지정하면 시작 시 자동으로 이 곡을 배경음악으로 재생(루프). 비우면 자동재생 안 함.")]
    [SerializeField] private AudioClip startupBgm;

    [Header("SFX")]
    [SerializeField] private AudioMixerGroup sfxMixerGroup;
    [SerializeField, Min(1)] private int sfxVoiceCount = 8;
    [SerializeField] private int maxSfxVoiceCount = 16;

    private readonly AudioSource[] bgmSources = new AudioSource[2];
    
    // 2D 전용 SFX 소스
    private AudioSource sfx2DSource;
    // 3D 전용 SFX 오브젝트 풀
    private ObjectPool<AudioSource> sfx3DPool;
    // 현재 활성화된(재생 중인) 3D SFX 소스들을 추적하여 볼륨 등 일괄 관리용
    private readonly List<AudioSource> active3DSources = new List<AudioSource>();

    private Coroutine bgmFadeRoutine;
    private int activeBgmSourceIndex = -1;
    private float bgmVolume = 1f;
    private float sfxVolume = 1f;

    public float BgmVolume => bgmVolume;
    public float SfxVolume => sfxVolume;

    /// <summary>
    /// 싱글톤 초기화 및 오디오 소스를 준비합니다. 씬 전환 시 파괴되지 않습니다.
    /// </summary>
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
        Ensure2DSfxSource();
        Initialize3DSfxPool();
    }

    /// <summary>
    /// 시작 배경음악이 지정돼 있으면 재생한다.
    /// </summary>
    private void Start()
    {
        if (startupBgm != null)
        {
            PlayBGM(startupBgm);
        }
    }

    /// <summary>
    /// 지정된 BGM 클립을 재생합니다. 다른 BGM이 재생 중이면 크로스페이드를 수행합니다.
    /// </summary>
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

    /// <summary>
    /// 현재 재생 중인 BGM을 정지합니다. 부드럽게 정지하려면 페이드 아웃 시간을 지정할 수 있습니다.
    /// </summary>
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

    /// <summary>
    /// 재생 중인 BGM을 일시 정지합니다.
    /// </summary>
    public void PauseBGM()
    {
        var currentSource = GetActiveBgmSource();
        if (currentSource != null && currentSource.isPlaying)
        {
            currentSource.Pause();
        }
    }

    /// <summary>
    /// 일시 정지된 BGM을 다시 재생합니다.
    /// </summary>
    public void ResumeBGM()
    {
        var currentSource = GetActiveBgmSource();
        if (currentSource != null)
        {
            currentSource.UnPause();
        }
    }

    /// <summary>
    /// 2D 전용 효과음을 재생합니다. 볼륨과 피치를 조정할 수 있습니다.
    /// </summary>
    public void PlaySFX(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null)
        {
            return;
        }

        Ensure2DSfxSource();

        sfx2DSource.pitch = pitch;
        sfx2DSource.PlayOneShot(clip, Mathf.Clamp01(volume) * sfxVolume);
    }

    /// <summary>
    /// 3D 공간상의 특정 위치에서 효과음을 재생합니다.
    /// </summary>
    public void PlaySFXAtPoint(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f, float spatialBlend = 1f)
    {
        if (clip == null)
        {
            return;
        }

        var source = sfx3DPool.Get();
        if (source == null)
        {
            Debug.LogWarning("활성화된 3D SFX 소스가 없습니다.");
            return;
        }

        source.transform.SetPositionAndRotation(position, Quaternion.identity);
        source.clip = clip;
        source.loop = false;
        source.pitch = pitch;
        source.volume = Mathf.Clamp01(volume) * sfxVolume;
        source.spatialBlend = Mathf.Clamp01(spatialBlend);
        source.outputAudioMixerGroup = sfxMixerGroup;
        source.Play();
        active3DSources.Add(source);

        StartCoroutine(Release3DSfxRoutine(source, clip.length));
    }

    /// <summary>
    /// 전체 BGM 볼륨을 설정합니다. 변경 시 현재 재생 중인 BGM에도 실시간으로 적용됩니다.
    /// </summary>
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

    /// <summary>
    /// 전체 SFX 볼륨을 설정합니다. 변경 시 현재 재생 중인 SFX 볼륨도 즉각적으로 업데이트됩니다.
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        var previousVolume = sfxVolume;
        sfxVolume = Mathf.Clamp01(volume);

        if (sfx2DSource != null)
        {
            sfx2DSource.volume = sfxVolume;
        }

        if (previousVolume <= 0f)
        {
            for (var i = 0; i < active3DSources.Count; i++)
            {
                if (active3DSources[i] != null && active3DSources[i].isPlaying)
                {
                    active3DSources[i].volume = sfxVolume;
                }
            }

            return;
        }

        var ratio = sfxVolume / previousVolume;
        for (var i = 0; i < active3DSources.Count; i++)
        {
            if (active3DSources[i] != null && active3DSources[i].isPlaying)
            {
                active3DSources[i].volume *= ratio;
            }
        }
    }

    /// <summary>
    /// BGM 재생을 위한 AudioSource들을 준비합니다 (크로스페이드를 위해 2개 사용).
    /// </summary>
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

    /// <summary>
    /// 2D 전용 SFX 소스를 준비합니다.
    /// </summary>
    private void Ensure2DSfxSource()
    {
        if (sfx2DSource != null)
        {
            return;
        }

        var child = new GameObject("SFX 2D Source");
        child.transform.SetParent(transform, false);

        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.outputAudioMixerGroup = sfxMixerGroup;
        source.volume = sfxVolume;

        sfx2DSource = source;
    }

    /// <summary>
    /// 3D SFX 재생을 위한 오브젝트 풀을 초기화합니다.
    /// </summary>
    private void Initialize3DSfxPool()
    {
        if (sfx3DPool != null)
        {
            return;
        }

        sfx3DPool = new ObjectPool<AudioSource>(
            CreatePooledObject,
            ActivatePoolObject,
            DeactivatePoolObject,
            null,
            false,
            sfxVoiceCount,
            maxSfxVoiceCount
        );
    }

    /// <summary>
    /// 풀에서 사용할 AudioSource를 생성합니다.
    /// </summary>
    private AudioSource CreatePooledObject()
    {
        var child = new GameObject("SFX 3D Source");
        child.transform.SetParent(transform, false);

        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 1f;
        source.outputAudioMixerGroup = sfxMixerGroup;

        return source;
    }

    /// <summary>
    /// 비활성화된 AudioSource가 활성화될 때 호출됩니다.
    /// </summary>
    private void ActivatePoolObject(AudioSource obj)
    {
        obj.gameObject.SetActive(true);
    }

    /// <summary>
    /// 활성화된 AudioSource가 비활성화될 때 호출됩니다.
    /// </summary>
    private void DeactivatePoolObject(AudioSource obj)
    {
        obj.Stop();
        obj.gameObject.SetActive(false);
        active3DSources.Remove(obj);
    }

    private IEnumerator Release3DSfxRoutine(AudioSource source, float delay)
    {
        yield return new WaitForSeconds(delay);
        
        if (source != null && source.gameObject.activeSelf)
        {
            sfx3DPool.Release(source);
        }
    }

    /// <summary>
    /// 현재 BGM을 재생 중인 AudioSource를 반환합니다.
    /// </summary>
    private AudioSource GetActiveBgmSource()
    {
        if (activeBgmSourceIndex < 0 || activeBgmSourceIndex >= bgmSources.Length)
        {
            return null;
        }

        return bgmSources[activeBgmSourceIndex];
    }

    /// <summary>
    /// 대기 중(다음 재생될)인 BGM AudioSource의 인덱스를 반환합니다.
    /// </summary>
    private int GetInactiveBgmSourceIndex()
    {
        if (activeBgmSourceIndex < 0)
        {
            return 0;
        }

        return activeBgmSourceIndex == 0 ? 1 : 0;
    }

    /// <summary>
    /// 현재 재생 중인 BGM AudioSource를 즉시 정지시킵니다.
    /// </summary>
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

    /// <summary>
    /// 이전 BGM에서 새 BGM으로 크로스페이드하는 코루틴입니다.
    /// </summary>
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

    /// <summary>
    /// BGM을 서서히 줄이며 정지시키는 코루틴입니다.
    /// </summary>
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

    /// <summary>
    /// 싱글톤 인스턴스 해제 용도입니다.
    /// </summary>
    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
