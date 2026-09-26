using UnityEngine;

[RequireComponent(typeof(SlimeController))]
public sealed class SlimeAudioFeedback : MonoBehaviour
{
    [SerializeField, Min(0f)] private float _landSoundCooldown = 0.1f;

    [Header("Bump")]
    [Tooltip("장식장에서 슬라임끼리 부딪힐 때 나는 소리입니다.")]
    [SerializeField, Min(0f)] private float _bumpSoundCooldown = 0.08f;

    [Tooltip("이보다 느리게 스치면 소리를 내지 않습니다.")]
    [SerializeField, Min(0f)] private float _bumpMinimumSpeed = 0.6f;

    private SlimeController _slimeController;

    private void Awake()
    {
        _slimeController = GetComponent<SlimeController>();
    }

    private void OnEnable()
    {
        _slimeController.OnPromoted += PlayLevelUpSound;
        _slimeController.OnLanded += PlayLandSound;
        _slimeController.OnBumped += PlayBumpSound;
    }

    private void OnDisable()
    {
        if (_slimeController == null) return;

        _slimeController.OnPromoted -= PlayLevelUpSound;
        _slimeController.OnLanded -= PlayLandSound;
        _slimeController.OnBumped -= PlayBumpSound;
    }

    private void PlayLevelUpSound()
    {
        if (!_slimeController.IsMainFieldActive ||
            AudioManager.Instance == null)
        {
            return;
        }

        AudioManager.Instance.PlaySFX(EAudioSfx.SlimePromote);
    }

    // IsMainFieldActive를 묻지 않는다. 슬라임끼리 부딪히는 일은 장식장에서만
    // 일어나므로, 그 조건을 걸면 정작 필요한 자리에서 전부 걸러진다.
    private void PlayBumpSound(float impactSpeed)
    {
        if (impactSpeed < _bumpMinimumSpeed ||
            AudioManager.Instance == null)
        {
            return;
        }

        AudioManager.Instance.PlaySFXWithCooldown(
            EAudioSfx.SlimeBump,
            _bumpSoundCooldown);
    }

    private void PlayLandSound()
    {
        if (!_slimeController.IsMainFieldActive ||
            AudioManager.Instance == null)
        {
            return;
        }

        AudioManager.Instance.PlaySFXWithCooldown(
            EAudioSfx.SlimeLand,
            _landSoundCooldown);
    }
}
