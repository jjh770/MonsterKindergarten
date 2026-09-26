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
    SlimeBump,
    PlaygroundBumperHit,
    PlaygroundCannonLoad,
    PlaygroundCannonFire,
}

[CreateAssetMenu(
    fileName = "GameAudioCatalog",
    menuName = "Monster Kindergarten/Game Audio Catalog")]
public sealed class GameAudioCatalogSO : ScriptableObject
{
    [Serializable]
    private sealed class ThemeBgmBinding
    {
        [SerializeField] private EBackgroundTheme _theme;
        [SerializeField] private AudioClip[] _clips = Array.Empty<AudioClip>();

        public EBackgroundTheme Theme => _theme;
        public AudioClip[] Clips => _clips;
    }

    [Header("BGM")]
    [SerializeField] private AudioClip _startBgm;
    [SerializeField] private AudioClip[] _groundBgms = Array.Empty<AudioClip>();
    [SerializeField] private AudioClip[] _skyBgms = Array.Empty<AudioClip>();
    [SerializeField] private ThemeBgmBinding[] _additionalThemeBgms =
        Array.Empty<ThemeBgmBinding>();
    [SerializeField] private AudioClip[] _displayRoomBgms = Array.Empty<AudioClip>();

    [Header("Common SFX")]
    [SerializeField] private AudioClip _uiClick;
    [SerializeField] private AudioClip _upgradeBuy;
    [SerializeField] private AudioClip _insufficientCurrency;
    [SerializeField] private AudioClip _featureUnlock;
    [SerializeField] private AudioClip _shopPurchase;
    [SerializeField] private AudioClip _ticketCollect;

    [Header("Offline Reward SFX")]
    [SerializeField] private AudioClip _offlineRewardOpen;
    [SerializeField] private AudioClip _offlineRewardCollect;
    [SerializeField] private AudioClip _offlineRewardArrival;

    [Header("Slime SFX")]
    [SerializeField] private AudioClip[] _slimeReactions = Array.Empty<AudioClip>();
    [SerializeField] private AudioClip[] _slimePromotes = Array.Empty<AudioClip>();
    [SerializeField] private AudioClip _slimeLand;
    [SerializeField] private AudioClip _slimeBump;

    [Header("Playground SFX")]
    [SerializeField] private AudioClip _playgroundBumperHit;
    [SerializeField] private AudioClip _playgroundCannonLoad;
    [SerializeField] private AudioClip _playgroundCannonFire;

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
            EAudioSfx.SlimeBump => _slimeBump,
            EAudioSfx.PlaygroundBumperHit => _playgroundBumperHit,
            EAudioSfx.PlaygroundCannonLoad => _playgroundCannonLoad,
            EAudioSfx.PlaygroundCannonFire => _playgroundCannonFire,
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
