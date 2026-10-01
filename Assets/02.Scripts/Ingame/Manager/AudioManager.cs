using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
public class AudioManager : MonoBehaviour
{
    private const string BgmVolumeKey = "Audio_BGMVolume";
    private const string SfxVolumeKey = "Audio_SFXVolume";
    public static AudioManager Instance { get; private set; }

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixerGroup _bgmMixerGroup;
    [SerializeField] private AudioMixerGroup _sfxMixerGroup;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource _bgmSource;
    [SerializeField] private AudioSource _secondaryBgmSource;
    [SerializeField] private AudioSource _sfxSource;
    [SerializeField] private AudioSource _loopingSfxSource;

    [Header("Audio Catalog")]
    [SerializeField] private GameAudioCatalogSO _catalog;

    [Header("Volume Settings")]
    [field: SerializeField, Range(0f, 1f)] public float MasterVolume { get; private set; } = 1f;
    [field: SerializeField, Range(0f, 1f)] public float BGMVolume { get; private set; } = 1f;
    [field: SerializeField, Range(0f, 1f)] public float SFXVolume { get; private set; } = 1f;

    private AudioMixer _audioMixer;
    private bool _isPaused;
    private AudioSource _activeBgmSource;
    private AudioSource _inactiveBgmSource;
    private Coroutine _bgmFadeCoroutine;
    private Coroutine _loopingSfxFadeCoroutine;
    private float _primaryBgmWeight = 1f;
    private float _secondaryBgmWeight;
    private float _loopingSfxWeight = 1f;
    private readonly Dictionary<AudioClip, float> _lastSfxPlayedTimes =
        new Dictionary<AudioClip, float>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BGMVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, BGMVolume));
        SFXVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, SFXVolume));
        InitializeAudioSources();
    }

    private void Start()
    {
        ApplyVolumes();

        AudioClip initialBgm = SceneManager.GetActiveScene().name == "LoginScene"
            ? _catalog?.LoginBgm
            : _catalog?.StartBgm;
        if (initialBgm != null)
        {
            PlayBGM(initialBgm);
        }
    }

    private void ApplyVolumes()
    {
        if (_audioMixer != null)
        {
            // MainMixer의 실제 노출 이름. 믹서와 소스에 음량을 중복 적용하지 않는다.
            _audioMixer.SetFloat("Master", VolumeToDecibel(_isPaused ? 0f : MasterVolume));
            _audioMixer.SetFloat("BGM", VolumeToDecibel(BGMVolume));
            _audioMixer.SetFloat("SFX", VolumeToDecibel(SFXVolume));
        }
        ApplyBgmVolumes();

        ApplySfxVolumes();
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause) SaveVolumeSettings();
        HandlePause(pause);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SaveVolumeSettings();
            Instance = null;
        }
    }


    private void HandlePause(bool pause)
    {
        if (pause == _isPaused) return;
        _isPaused = pause;

        ApplyVolumes();
    }

    private void InitializeAudioSources()
    {
        if (_bgmSource == null ||
            _secondaryBgmSource == null ||
            _sfxSource == null ||
            _loopingSfxSource == null ||
            _catalog == null)
        {
            Debug.LogError("AudioManager의 필수 참조가 비어 있습니다.", this);
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
            _loopingSfxSource.outputAudioMixerGroup = _sfxMixerGroup;
            _audioMixer ??= _sfxMixerGroup.audioMixer;
        }

        _activeBgmSource = _bgmSource;
        _inactiveBgmSource = _secondaryBgmSource;
    }

    #region BGM

    public void PlayLoginBgm(float crossFadeDuration = 0.35f)
    {
        CrossFadeBGM(_catalog?.LoginBgm, crossFadeDuration);
    }

    private void PlayBGM(AudioClip clip)
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
        float volume = _audioMixer != null ? 1f : (_isPaused ? 0f : BGMVolume * MasterVolume);
        if (_bgmSource != null)
        {
            _bgmSource.volume = volume * _primaryBgmWeight * GetClipVolume(_bgmSource.clip);
        }

        if (_secondaryBgmSource != null)
        {
            _secondaryBgmSource.volume =
                volume * _secondaryBgmWeight * GetClipVolume(_secondaryBgmSource.clip);
        }
    }

    #endregion

    #region SFX

    public AudioClip GetRandomThemeBgm(EBackgroundTheme theme)
    {
        return _catalog != null ? _catalog.GetRandomThemeBgm(theme) : null;
    }

    public AudioClip GetRandomDisplayRoomBgm()
    {
        return _catalog != null ? _catalog.GetRandomDisplayRoomBgm() : null;
    }

    public void PlaySFX(EAudioSfx cue)
    {
        AudioClip clip = GetSfx(cue);
        PlaySFX(clip, GetClipVolume(clip));
    }

    public void PlaySFXWithCooldown(EAudioSfx cue, float cooldown)
    {
        AudioClip clip = GetSfx(cue);
        PlaySFXWithCooldown(clip, cooldown, GetClipVolume(clip));
    }

    public void PlaySFXRandomPitch(
        EAudioSfx cue,
        float minPitch = 0.9f,
        float maxPitch = 1.1f)
    {
        AudioClip clip = GetSfx(cue);
        PlaySFXRandomPitch(clip, minPitch, maxPitch, GetClipVolume(clip));
    }

    public void PlaySFXRandomPitchWithCooldown(
        EAudioSfx cue,
        float cooldown,
        float minPitch = 0.9f,
        float maxPitch = 1.1f)
    {
        AudioClip clip = GetSfx(cue);
        if (clip == null) return;

        float currentTime = Time.unscaledTime;
        if (_lastSfxPlayedTimes.TryGetValue(clip, out float lastPlayedTime) &&
            currentTime - lastPlayedTime < cooldown) return;

        _lastSfxPlayedTimes[clip] = currentTime;
        PlaySFXRandomPitch(clip, minPitch, maxPitch, GetClipVolume(clip));
    }

    public void PlaySFXRandomPitchSequence(
        EAudioSfx cue,
        int count,
        float interval,
        float minPitch = 0.9f,
        float maxPitch = 1.1f)
    {
        AudioClip clip = GetSfx(cue);
        if (clip == null || count <= 0) return;

        StartCoroutine(PlaySfxRandomPitchSequenceRoutine(
            clip,
            count,
            Mathf.Max(0f, interval),
            minPitch,
            maxPitch,
            GetClipVolume(clip)));
    }

    public void PlayLoopingSFX(EAudioSfx cue)
    {
        AudioClip clip = GetSfx(cue);
        if (clip == null) return;

        StopLoopingSfxFade();
        _loopingSfxSource.Stop();
        _loopingSfxSource.clip = clip;
        _loopingSfxSource.loop = true;
        _loopingSfxWeight = 1f;
        ApplySfxVolumes();
        _loopingSfxSource.Play();
    }

    public void StopLoopingSFX(float fadeDuration = 0f)
    {
        StopLoopingSfxFade();

        if (!_loopingSfxSource.isPlaying) return;
        if (fadeDuration <= 0f)
        {
            StopLoopingSfxNow();
            return;
        }

        _loopingSfxFadeCoroutine = StartCoroutine(
            FadeOutLoopingSfxRoutine(fadeDuration));
    }

    private AudioClip GetSfx(EAudioSfx cue)
    {
        return _catalog != null ? _catalog.GetSfx(cue) : null;
    }

    private float GetClipVolume(AudioClip clip)
    {
        return _catalog != null ? _catalog.GetVolume(clip) : 1f;
    }

    private void PlaySFX(AudioClip clip, float volumeScale)
    {
        if (clip == null) return;

        _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    private void PlaySFXWithCooldown(
        AudioClip clip,
        float cooldown,
        float volumeScale)
    {
        if (clip == null) return;

        float currentTime = Time.unscaledTime;
        if (_lastSfxPlayedTimes.TryGetValue(clip, out float lastPlayedTime) &&
            currentTime - lastPlayedTime < cooldown)
        {
            return;
        }

        _lastSfxPlayedTimes[clip] = currentTime;
        _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
    }

    private void PlaySFXWithPitch(AudioClip clip, float pitch, float volumeScale)
    {
        if (clip == null) return;

        _sfxSource.pitch = pitch;
        _sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        _sfxSource.pitch = 1f;
    }

    private void PlaySFXRandomPitch(
        AudioClip clip,
        float minPitch,
        float maxPitch,
        float volumeScale)
    {
        if (clip == null) return;

        float randomPitch = Random.Range(minPitch, maxPitch);
        PlaySFXWithPitch(clip, randomPitch, volumeScale);
    }

    private IEnumerator PlaySfxRandomPitchSequenceRoutine(
        AudioClip clip,
        int count,
        float interval,
        float minPitch,
        float maxPitch,
        float volumeScale)
    {
        for (int i = 0; i < count; i++)
        {
            PlaySFXRandomPitch(clip, minPitch, maxPitch, volumeScale);
            if (i + 1 < count && interval > 0f)
            {
                yield return new WaitForSecondsRealtime(interval);
            }
        }
    }

    private IEnumerator FadeOutLoopingSfxRoutine(float duration)
    {
        float startWeight = _loopingSfxWeight;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (!_isPaused)
            {
                elapsed += Time.unscaledDeltaTime;
            }

            _loopingSfxWeight = Mathf.Lerp(
                startWeight,
                0f,
                Mathf.Clamp01(elapsed / duration));
            ApplySfxVolumes();
            yield return null;
        }

        StopLoopingSfxNow();
        _loopingSfxFadeCoroutine = null;
    }

    private void StopLoopingSfxFade()
    {
        if (_loopingSfxFadeCoroutine == null) return;

        StopCoroutine(_loopingSfxFadeCoroutine);
        _loopingSfxFadeCoroutine = null;
    }

    private void StopLoopingSfxNow()
    {
        _loopingSfxSource.Stop();
        _loopingSfxSource.clip = null;
        _loopingSfxWeight = 1f;
        ApplySfxVolumes();
    }

    private void ApplySfxVolumes()
    {
        float volume = _audioMixer != null
            ? 1f
            : (_isPaused ? 0f : SFXVolume * MasterVolume);

        if (_sfxSource != null)
        {
            _sfxSource.volume = volume;
        }

        if (_loopingSfxSource != null)
        {
            _loopingSfxSource.volume =
                volume * _loopingSfxWeight * GetClipVolume(_loopingSfxSource.clip);
        }
    }

    #endregion

    #region Volume Control

    public void SetBGMVolume(float volume)
    {
        BGMVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(BgmVolumeKey, BGMVolume);
        ApplyVolumes();
    }

    public void SetSFXVolume(float volume)
    {
        SFXVolume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(SfxVolumeKey, SFXVolume);
        ApplyVolumes();
    }

    public void SaveVolumeSettings()
    {
        PlayerPrefs.Save();
    }

    private float VolumeToDecibel(float volume)
    {
        // 0 -> -80dB (무음), 1 -> 0dB (최대)
        return volume > 0 ? Mathf.Log10(volume) * 20f : -80f;
    }

    #endregion
}
