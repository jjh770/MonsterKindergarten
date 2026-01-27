using UnityEngine;

public class SoundFeedback : MonoBehaviour, IFeedback
{
    [SerializeField] private AudioClip[] _clip;
    [SerializeField] private float _minPitch = 0.4f;
    [SerializeField] private float _maxPitch = 0.8f;

    private int _randomNum = 0;

    public void Play(ClickInfo clickInfo)
    {
        if (clickInfo.ClickType == EClickType.Auto) return;

        if (AudioManager.Instance != null && _clip != null)
        {
            _randomNum = UnityEngine.Random.Range(0, _clip.Length);
            AudioManager.Instance.PlaySFXRandomPitch(_clip[_randomNum], _minPitch, _maxPitch);
        }
    }
}
