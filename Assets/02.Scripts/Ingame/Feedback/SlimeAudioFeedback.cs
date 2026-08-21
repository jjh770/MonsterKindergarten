using UnityEngine;

[RequireComponent(typeof(SlimeController))]
public sealed class SlimeAudioFeedback : MonoBehaviour
{
    [SerializeField] private AudioClip[] _levelUpSounds;
    [SerializeField] private AudioClip _landSound;
    [SerializeField, Min(0f)] private float _landSoundCooldown = 0.1f;

    private SlimeController _slimeController;

    private void Awake()
    {
        _slimeController = GetComponent<SlimeController>();
    }

    private void OnEnable()
    {
        _slimeController.OnPromoted += PlayLevelUpSound;
        _slimeController.OnLanded += PlayLandSound;
    }

    private void OnDisable()
    {
        if (_slimeController == null) return;

        _slimeController.OnPromoted -= PlayLevelUpSound;
        _slimeController.OnLanded -= PlayLandSound;
    }

    private void PlayLevelUpSound()
    {
        if (!_slimeController.IsCurrentStageActive ||
            AudioManager.Instance == null ||
            _levelUpSounds == null ||
            _levelUpSounds.Length == 0)
        {
            return;
        }

        int soundIndex = Random.Range(0, _levelUpSounds.Length);
        AudioManager.Instance.PlaySFX(_levelUpSounds[soundIndex]);
    }

    private void PlayLandSound()
    {
        if (!_slimeController.IsCurrentStageActive ||
            AudioManager.Instance == null ||
            _landSound == null)
        {
            return;
        }

        AudioManager.Instance.PlaySFXWithCooldown(
            _landSound,
            _landSoundCooldown);
    }
}
