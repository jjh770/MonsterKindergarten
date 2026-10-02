using System;
using UnityEngine;

public enum EAudioSfx
{
    UIClick,
    UpgradeBuy,
    InsufficientCurrency,
    FeatureUnlock,
    ShopPurchase,
    TicketCollect,
    OfflineRewardOpen,
    OfflineRewardCollect,
    OfflineRewardArrival,
    SlimeReaction,
    SlimePromote,
    SlimeLand,
    SlimeBounce,
    PlaygroundBumperHit,
    PlaygroundCannonLoad,
    PlaygroundCannonFire,
    DisplayRoomSlimeTransfer,
    LoginButton,
    AreaTransition,
    GachaWait,
    GachaResult,
}

[CreateAssetMenu(
    fileName = "GameAudioCatalog",
    menuName = "Monster Kindergarten/Game Audio Catalog")]
public sealed class GameAudioCatalogSO : ScriptableObject
{
    [Serializable]
    private sealed class ClipVolumeOverride
    {
        [SerializeField] private AudioClip _clip;
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;

        public AudioClip Clip => _clip;
        public float Volume => _volume;
    }

    [Serializable]
    private sealed class ThemeBgmBinding
    {
        [SerializeField] private EBackgroundTheme _theme;
        [SerializeField] private AudioClip[] _clips = Array.Empty<AudioClip>();

        public EBackgroundTheme Theme => _theme;
        public AudioClip[] Clips => _clips;
    }

    [Header("BGM")]
    [SerializeField] private AudioClip _loginBgm;
    [SerializeField] private AudioClip _startBgm;
    [SerializeField] private AudioClip[] _groundBgms = Array.Empty<AudioClip>();
    [SerializeField] private AudioClip[] _skyBgms = Array.Empty<AudioClip>();
    [SerializeField] private ThemeBgmBinding[] _additionalThemeBgms =
        Array.Empty<ThemeBgmBinding>();
    [SerializeField] private AudioClip[] _displayRoomBgms = Array.Empty<AudioClip>();

    [Header("Per-Clip Volume Overrides")]
    [SerializeField] private ClipVolumeOverride[] _volumeOverrides =
        Array.Empty<ClipVolumeOverride>();

    [Header("Common SFX")]
    [SerializeField] private AudioClip _uiClick;
    [SerializeField] private AudioClip _upgradeBuy;
    [SerializeField] private AudioClip _insufficientCurrency;
    [SerializeField] private AudioClip _featureUnlock;
    [SerializeField] private AudioClip _shopPurchase;
    [SerializeField] private AudioClip _ticketCollect;

    [Header("Transition SFX")]
    [SerializeField] private AudioClip _displayRoomSlimeTransfer;
    [SerializeField] private AudioClip _loginButton;
    [SerializeField] private AudioClip _areaTransition;

    [Header("Offline Reward SFX")]
    [SerializeField] private AudioClip _offlineRewardOpen;
    [SerializeField] private AudioClip _offlineRewardCollect;
    [SerializeField] private AudioClip _offlineRewardArrival;

    [Header("Gacha SFX")]
    [SerializeField] private AudioClip _gachaWait;
    [SerializeField] private AudioClip _gachaResult;

    [Header("Slime SFX")]
    [SerializeField] private AudioClip[] _slimeReactions = Array.Empty<AudioClip>();
    [SerializeField] private AudioClip[] _slimePromotes = Array.Empty<AudioClip>();
    [SerializeField] private AudioClip _slimeLand;
    [SerializeField] private AudioClip _slimeBounce;

    [Header("Playground SFX")]
    [SerializeField] private AudioClip _playgroundBumperHit;
    [SerializeField] private AudioClip _playgroundCannonLoad;
    [SerializeField] private AudioClip _playgroundCannonFire;

    public AudioClip LoginBgm => _loginBgm;
    public AudioClip StartBgm => _startBgm;

    public AudioClip GetRandomThemeBgm(EBackgroundTheme theme)
    {
        if (theme == EBackgroundTheme.Ground) return PickRandom(_groundBgms);
        if (theme == EBackgroundTheme.Sky) return PickRandom(_skyBgms);

        if (_additionalThemeBgms == null) return null;

        foreach (ThemeBgmBinding binding in _additionalThemeBgms)
        {
            if (binding != null && binding.Theme == theme)
            {
                return PickRandom(binding.Clips);
            }
        }

        return null;
    }

    public AudioClip GetRandomDisplayRoomBgm()
    {
        return PickRandom(_displayRoomBgms);
    }

    public float GetVolume(AudioClip clip)
    {
        if (clip == null || _volumeOverrides == null) return 1f;

        foreach (ClipVolumeOverride volumeOverride in _volumeOverrides)
        {
            if (volumeOverride != null && volumeOverride.Clip == clip)
            {
                return Mathf.Clamp01(volumeOverride.Volume);
            }
        }

        return 1f;
    }

    public AudioClip GetSfx(EAudioSfx cue)
    {
        return cue switch
        {
            EAudioSfx.UIClick => _uiClick,
            EAudioSfx.UpgradeBuy => _upgradeBuy,
            EAudioSfx.InsufficientCurrency => _insufficientCurrency,
            EAudioSfx.FeatureUnlock => _featureUnlock,
            EAudioSfx.ShopPurchase => _shopPurchase,
            EAudioSfx.TicketCollect => _ticketCollect,
            EAudioSfx.OfflineRewardOpen => _offlineRewardOpen,
            EAudioSfx.OfflineRewardCollect => _offlineRewardCollect,
            EAudioSfx.OfflineRewardArrival => _offlineRewardArrival,
            EAudioSfx.SlimeReaction => PickRandom(_slimeReactions),
            EAudioSfx.SlimePromote => PickRandom(_slimePromotes),
            EAudioSfx.SlimeLand => _slimeLand,
            EAudioSfx.SlimeBounce => _slimeBounce,
            EAudioSfx.PlaygroundBumperHit => _playgroundBumperHit,
            EAudioSfx.PlaygroundCannonLoad => _playgroundCannonLoad,
            EAudioSfx.PlaygroundCannonFire => _playgroundCannonFire,
            EAudioSfx.DisplayRoomSlimeTransfer => _displayRoomSlimeTransfer,
            EAudioSfx.LoginButton => _loginButton,
            EAudioSfx.AreaTransition => _areaTransition,
            EAudioSfx.GachaWait => _gachaWait,
            EAudioSfx.GachaResult => _gachaResult,
            _ => null,
        };
    }

    private static AudioClip PickRandom(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;

        int filled = 0;
        foreach (AudioClip clip in clips)
        {
            if (clip != null) filled++;
        }

        if (filled == 0) return null;

        int index = UnityEngine.Random.Range(0, filled);
        foreach (AudioClip clip in clips)
        {
            if (clip == null) continue;
            if (index == 0) return clip;
            index--;
        }

        return null;
    }
}
