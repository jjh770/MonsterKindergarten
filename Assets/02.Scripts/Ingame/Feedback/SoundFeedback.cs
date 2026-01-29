using UnityEngine;

public class SoundFeedback : MonoBehaviour, IFeedback
{
    [SerializeField] private AudioClip[] _clip;
    [SerializeField] private float _minPitch = 0.4f;
    [SerializeField] private float _maxPitch = 0.8f;

    public void Play(ClickInfo clickInfo)
    {
        if (clickInfo.ClickType == EClickType.Auto) return;

        if (AudioManager.Instance != null && _clip != null && _clip.Length > 0)
        {
            int randomNum = UnityEngine.Random.Range(0, _clip.Length);
            AudioManager.Instance.PlaySFXRandomPitch(_clip[randomNum], _minPitch, _maxPitch);
        }
    }
}
