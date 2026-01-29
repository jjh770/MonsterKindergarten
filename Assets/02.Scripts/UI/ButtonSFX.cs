using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonSFX : MonoBehaviour
{
    [SerializeField] private AudioClip _clickSound;

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
        if (AudioManager.Instance != null && _clickSound != null)
        {
            AudioManager.Instance.PlaySFX(_clickSound);
        }
    }
}
