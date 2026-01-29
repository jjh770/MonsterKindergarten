using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void RegisterVisibilityChangeEvent();
#endif

    public static AudioManager Instance;

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixerGroup _bgmMixerGroup;
    [SerializeField] private AudioMixerGroup _sfxMixerGroup;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource _bgmSource;
    [SerializeField] private AudioSource _sfxSource;

    [Header("BGM")]
    [SerializeField] private AudioClip _startBGM;

    [Header("Volume Settings")]
    [field: SerializeField, Range(0f, 1f)] public float MasterVolume { get; private set; } = 1f;
    [field: SerializeField, Range(0f, 1f)] public float BGMVolume { get; private set; } = 1f;
    [field: SerializeField, Range(0f, 1f)] public float SFXVolume { get; private set; } = 1f;

    private AudioMixer _audioMixer;
    private bool _isPaused;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeAudioSources();
    }

    private void Start()
    {
        ApplyVolumes();

        if (_startBGM != null)
        {
            PlayBGM(_startBGM);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        RegisterVisibilityChangeEvent();
#endif
    }

    private void ApplyVolumes()
    {
        if (_bgmSource != null)
        {
            _bgmSource.volume = BGMVolume * MasterVolume;
        }
        if (_sfxSource != null)
        {
            _sfxSource.volume = SFXVolume * MasterVolume;
        }
    }

    private void OnApplicationPause(bool pause)
    {
        HandlePause(pause);
    }


    // JavaScript visibilitychange 이벤트에서 SendMessage로 호출
    public void OnBrowserPause(int paused)
    {
        HandlePause(paused == 1);
    }

    private void HandlePause(bool pause)
    {
        if (pause == _isPaused) return;
        _isPaused = pause;

        if (pause)
        {
            if (_audioMixer != null)
            {
                _audioMixer.SetFloat("MasterVolume", VolumeToDecibel(0f));
            }
            else
            {
                if (_bgmSource != null) _bgmSource.volume = 0f;
                if (_sfxSource != null) _sfxSource.volume = 0f;
            }
        }
        else
        {
            if (_audioMixer != null)
            {
                _audioMixer.SetFloat("MasterVolume", VolumeToDecibel(MasterVolume));
            }
            ApplyVolumes();
        }
    }

    private void InitializeAudioSources()
    {
        if (_bgmSource == null)
        {
            var bgmObj = new GameObject("BGM Source");
            bgmObj.transform.SetParent(transform);
            _bgmSource = bgmObj.AddComponent<AudioSource>();
            _bgmSource.loop = true;
            _bgmSource.playOnAwake = false;
        }

        if (_sfxSource == null)
        {
            var sfxObj = new GameObject("SFX Source");
            sfxObj.transform.SetParent(transform);
            _sfxSource = sfxObj.AddComponent<AudioSource>();
            _sfxSource.loop = false;
            _sfxSource.playOnAwake = false;
        }

        if (_bgmMixerGroup != null)
        {
            _bgmSource.outputAudioMixerGroup = _bgmMixerGroup;
            _audioMixer = _bgmMixerGroup.audioMixer;
        }

        if (_sfxMixerGroup != null)
        {
            _sfxSource.outputAudioMixerGroup = _sfxMixerGroup;
            _audioMixer ??= _sfxMixerGroup.audioMixer;
        }
    }

    #region BGM

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null) return;

        _bgmSource.clip = clip;
        _bgmSource.Play();
    }

    public void StopBGM()
    {
        _bgmSource.Stop();
    }

    public void PauseBGM()
    {
        _bgmSource.Pause();
    }

    public void ResumeBGM()
    {
        _bgmSource.UnPause();
    }

    #endregion

    #region SFX

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;

        _sfxSource.PlayOneShot(clip, SFXVolume);
    }

    public void PlaySFX(AudioClip clip, float pitch)
    {
        if (clip == null) return;

        _sfxSource.pitch = pitch;
        _sfxSource.PlayOneShot(clip, SFXVolume);
        _sfxSource.pitch = 1f;
    }

    public void PlaySFXRandomPitch(AudioClip clip, float minPitch = 0.9f, float maxPitch = 1.1f)
    {
        if (clip == null) return;

        float randomPitch = Random.Range(minPitch, maxPitch);
        PlaySFX(clip, randomPitch);
    }

    #endregion

    #region Volume Control

    public void SetMasterVolume(float volume)
    {
        MasterVolume = Mathf.Clamp01(volume);
        if (_audioMixer != null)
        {
            _audioMixer.SetFloat("MasterVolume", VolumeToDecibel(MasterVolume));
        }
        ApplyVolumes();
    }

    public void SetBGMVolume(float volume)
    {
        BGMVolume = Mathf.Clamp01(volume);
        if (_audioMixer != null)
        {
            _audioMixer.SetFloat("BGMVolume", VolumeToDecibel(BGMVolume));
        }
        ApplyVolumes();
    }

    public void SetSFXVolume(float volume)
    {
        SFXVolume = Mathf.Clamp01(volume);
        if (_audioMixer != null)
        {
            _audioMixer.SetFloat("SFXVolume", VolumeToDecibel(SFXVolume));
        }
        ApplyVolumes();
    }

    private float VolumeToDecibel(float volume)
    {
        // 0 -> -80dB (무음), 1 -> 0dB (최대)
        return volume > 0 ? Mathf.Log10(volume) * 20f : -80f;
    }

    #endregion
}
