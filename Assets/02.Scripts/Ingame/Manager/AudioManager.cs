using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
public class AudioManager : MonoBehaviour
{
#if UNITY_WEBGL 
    [DllImport("__Internal")]
    private static extern void RegisterVisibilityChangeEvent();
#endif

    public static AudioManager Instance;

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixerGroup _bgmMixerGroup;
    [SerializeField] private AudioMixerGroup _sfxMixerGroup;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource _bgmSource;
    [SerializeField] private AudioSource _secondaryBgmSource;
    [SerializeField] private AudioSource _sfxSource;

    [Header("BGM")]
    [SerializeField] private AudioClip _startBGM;

    [Header("Volume Settings")]
    [field: SerializeField, Range(0f, 1f)] public float MasterVolume { get; private set; } = 1f;
    [field: SerializeField, Range(0f, 1f)] public float BGMVolume { get; private set; } = 1f;
    [field: SerializeField, Range(0f, 1f)] public float SFXVolume { get; private set; } = 1f;

    private AudioMixer _audioMixer;
    private bool _isPaused;
    private AudioSource _activeBgmSource;
    private AudioSource _inactiveBgmSource;
    private Coroutine _bgmFadeCoroutine;
    private float _primaryBgmWeight = 1f;
    private float _secondaryBgmWeight;
    private readonly Dictionary<AudioClip, float> _lastSfxPlayedTimes =
        new Dictionary<AudioClip, float>();

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
        ApplyBgmVolumes();

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
                if (_secondaryBgmSource != null) _secondaryBgmSource.volume = 0f;
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
        if (_bgmSource == null ||
            _secondaryBgmSource == null ||
            _sfxSource == null)
        {
            Debug.LogError("AudioManager의 AudioSource 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        if (_bgmMixerGroup != null)
        {
            _bgmSource.outputAudioMixerGroup = _bgmMixerGroup;
            _secondaryBgmSource.outputAudioMixerGroup = _bgmMixerGroup;
            _audioMixer = _bgmMixerGroup.audioMixer;
        }

        if (_sfxMixerGroup != null)
        {
            _sfxSource.outputAudioMixerGroup = _sfxMixerGroup;
            _audioMixer ??= _sfxMixerGroup.audioMixer;
        }

        _activeBgmSource = _bgmSource;
        _inactiveBgmSource = _secondaryBgmSource;
    }

    #region BGM

    public void PlayBGM(AudioClip clip)
    {
        if (clip == null) return;

        StopBgmFade();
        _activeBgmSource.clip = clip;
        _activeBgmSource.Play();
        _inactiveBgmSource.Stop();
        SetBgmWeight(_activeBgmSource, 1f);
        SetBgmWeight(_inactiveBgmSource, 0f);
        ApplyBgmVolumes();
    }

    public void CrossFadeBGM(AudioClip clip, float duration)
    {
        if (clip == null) return;
        if (_activeBgmSource.clip == clip && _activeBgmSource.isPlaying) return;

        StopBgmFade();
        _bgmFadeCoroutine = StartCoroutine(CrossFadeBgmRoutine(
            clip,
            Mathf.Max(0f, duration)));
    }

    public void StopBGM()
    {
        StopBgmFade();
        _bgmSource.Stop();
        _secondaryBgmSource.Stop();
    }

    public void PauseBGM()
    {
        _bgmSource.Pause();
        _secondaryBgmSource.Pause();
    }

    public void ResumeBGM()
    {
        _bgmSource.UnPause();
        _secondaryBgmSource.UnPause();
    }

    private IEnumerator CrossFadeBgmRoutine(AudioClip clip, float duration)
    {
        AudioSource previousSource = _activeBgmSource;
        AudioSource nextSource = _inactiveBgmSource;
        nextSource.clip = clip;
        nextSource.Play();
        SetBgmWeight(nextSource, 0f);

        if (duration <= 0f)
        {
            SetBgmWeight(previousSource, 0f);
            SetBgmWeight(nextSource, 1f);
        }
        else
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!_isPaused)
                {
                    elapsed += Time.unscaledDeltaTime;
                }

                float progress = Mathf.Clamp01(elapsed / duration);
                SetBgmWeight(previousSource, 1f - progress);
                SetBgmWeight(nextSource, progress);
                ApplyBgmVolumes();
                yield return null;
            }
        }

        previousSource.Stop();
        _activeBgmSource = nextSource;
        _inactiveBgmSource = previousSource;
        SetBgmWeight(_activeBgmSource, 1f);
        SetBgmWeight(_inactiveBgmSource, 0f);
        ApplyBgmVolumes();
        _bgmFadeCoroutine = null;
    }

    private void StopBgmFade()
    {
        if (_bgmFadeCoroutine == null) return;

        StopCoroutine(_bgmFadeCoroutine);
        _bgmFadeCoroutine = null;
    }

    private void SetBgmWeight(AudioSource source, float weight)
    {
        if (source == _bgmSource)
        {
            _primaryBgmWeight = weight;
        }
        else if (source == _secondaryBgmSource)
        {
            _secondaryBgmWeight = weight;
        }
    }

    private void ApplyBgmVolumes()
    {
        if (_bgmSource != null)
        {
            _bgmSource.volume = BGMVolume * MasterVolume * _primaryBgmWeight;
        }

        if (_secondaryBgmSource != null)
        {
            _secondaryBgmSource.volume =
                BGMVolume * MasterVolume * _secondaryBgmWeight;
        }
    }

    #endregion

    #region SFX

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;

        _sfxSource.PlayOneShot(clip, SFXVolume);
    }

    public void PlaySFXWithCooldown(AudioClip clip, float cooldown)
    {
        if (clip == null) return;

        float currentTime = Time.unscaledTime;
        if (_lastSfxPlayedTimes.TryGetValue(clip, out float lastPlayedTime) &&
            currentTime - lastPlayedTime < cooldown)
        {
            return;
        }

        _lastSfxPlayedTimes[clip] = currentTime;
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
