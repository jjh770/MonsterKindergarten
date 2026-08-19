using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Clicker))]
public sealed class GameExitManager : MonoBehaviour
{
    [SerializeField] private GameExitPopupUI _popup;
    [SerializeField] private UpgradeUI _upgradeUI;

    private Clicker _clicker;
    private bool _wasClickerEnabled;
    private bool _isWorldInputBlocked;

    private void Awake()
    {
        _clicker = GetComponent<Clicker>();
    }

    private void Start()
    {
        if (_popup == null)
        {
            Debug.LogError("게임 종료 팝업의 씬 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _popup.CancelRequested += HidePopup;
        _popup.ExitRequested += ExitGame;
        _popup.Hide();
    }

    private void Update()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;

        if (_popup != null && _popup.IsVisible)
        {
            HidePopup();
            return;
        }

        if (_upgradeUI != null && _upgradeUI.TryClose()) return;

        ShowPopup();
    }

    private void OnDestroy()
    {
        if (_popup != null)
        {
            _popup.CancelRequested -= HidePopup;
            _popup.ExitRequested -= ExitGame;
        }

        RestoreWorldInput();
    }

    private void ShowPopup()
    {
        if (_popup == null || _popup.IsVisible) return;

        BlockWorldInput();
        _popup.Show();
    }

    private void HidePopup()
    {
        if (_popup == null || !_popup.IsVisible) return;

        _popup.Hide();
        RestoreWorldInput();
    }

    private void BlockWorldInput()
    {
        if (_isWorldInputBlocked) return;

        _isWorldInputBlocked = true;

        if (_clicker != null)
        {
            _wasClickerEnabled = _clicker.enabled;
            _clicker.enabled = false;
        }
    }

    private void RestoreWorldInput()
    {
        if (!_isWorldInputBlocked) return;

        if (_clicker != null)
        {
            _clicker.enabled = _wasClickerEnabled;
        }

        _isWorldInputBlocked = false;
    }

    private void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

}
