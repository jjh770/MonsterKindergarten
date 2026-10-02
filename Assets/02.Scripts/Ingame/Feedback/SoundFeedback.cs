using UnityEngine;

public class SoundFeedback : MonoBehaviour, IFeedback
{
    [SerializeField] private float _minPitch = 0.4f;
    [SerializeField] private float _maxPitch = 0.8f;

    public void Play(ClickInfo clickInfo)
    {
        if (clickInfo.ClickType == EClickType.Auto) return;

        AudioManager.Instance?.PlaySFXRandomPitch(
            EAudioSfx.SlimeReaction,
            _minPitch,
            _maxPitch);
    }
}
