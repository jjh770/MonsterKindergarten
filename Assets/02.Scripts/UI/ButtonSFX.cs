using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonSFX : MonoBehaviour
{
    [SerializeField] private EAudioSfx _cue = EAudioSfx.UIClick;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        _button.onClick.AddListener(PlayClickSound);
    }

    private void OnDestroy()
    {
        _button.onClick.RemoveListener(PlayClickSound);
    }

    private void PlayClickSound()
    {
        AudioManager.Instance?.PlaySFX(_cue);
    }
}
