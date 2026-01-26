using UnityEngine;

public class SoundFeedback : MonoBehaviour, IFeedback
{
    private AudioSource _audio;

    private void Awake()
    {
        _audio = GetComponent<AudioSource>();
    }
    public void Play(ClickInfo clickInfo)
    {
        if (clickInfo.ClickType == EClickType.Auto) return;

        _audio.pitch = UnityEngine.Random.Range(0.4f, 0.8f);
        _audio.Play();
    }
}
