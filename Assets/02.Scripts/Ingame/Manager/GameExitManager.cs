using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class GameExitManager : MonoBehaviour
{
    [SerializeField] private GameExitPopupUI _popup;
    [SerializeField] private UpgradeUI _upgradeUI;
    [SerializeField] private Clicker _clicker;

    private bool _wasClickerEnabled;
    private bool _isWorldInputBlocked;
    private readonly List<BackHandler> _backHandlers = new();

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

        if (_upgradeUI != null)
        {
            _upgradeUI.Opened += RegisterUpgradeHandler;
            _upgradeUI.Closed += UnregisterUpgradeHandler;
        }
    }

    private void Update()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
        if (StageManager.Instance != null && StageManager.Instance.IsTransitioning) return;

        if (TryHandleBack()) return;

        ShowPopup();
    }

    private void OnDestroy()
    {
        if (_popup != null)
        {
            _popup.CancelRequested -= HidePopup;
            _popup.ExitRequested -= ExitGame;
        }

        if (_upgradeUI != null)
        {
            _upgradeUI.Opened -= RegisterUpgradeHandler;
            _upgradeUI.Closed -= UnregisterUpgradeHandler;
        }

        _backHandlers.Clear();
        RestoreWorldInput();
    }

    public void RegisterBackHandler(object owner, Func<bool> tryClose)
    {
        if (owner == null)
        {
            throw new ArgumentNullException(nameof(owner));
        }

        if (tryClose == null)
        {
            throw new ArgumentNullException(nameof(tryClose));
        }

        UnregisterBackHandler(owner);
        _backHandlers.Add(new BackHandler(owner, tryClose));
    }

    public void UnregisterBackHandler(object owner)
    {
        if (owner == null) return;

        _backHandlers.RemoveAll(handler => ReferenceEquals(handler.Owner, owner));
    }

    private bool TryHandleBack()
    {
        while (_backHandlers.Count > 0)
        {
            int lastIndex = _backHandlers.Count - 1;
            BackHandler handler = _backHandlers[lastIndex];
            if (handler.TryClose()) return true;

            if (lastIndex < _backHandlers.Count &&
                ReferenceEquals(_backHandlers[lastIndex].Owner, handler.Owner))
            {
                _backHandlers.RemoveAt(lastIndex);
            }
        }

        return false;
    }

    private void ShowPopup()
    {
        if (_popup == null || _popup.IsVisible) return;

        BlockWorldInput();
        _popup.Show();
        RegisterBackHandler(_popup, TryHidePopup);
    }

    private void HidePopup()
    {
        if (_popup == null || !_popup.IsVisible) return;

        UnregisterBackHandler(_popup);
        _popup.Hide();
        RestoreWorldInput();
    }

    private bool TryHidePopup()
    {
        if (_popup == null || !_popup.IsVisible) return false;

        HidePopup();
        return true;
    }

    private void RegisterUpgradeHandler()
    {
        RegisterBackHandler(_upgradeUI, _upgradeUI.TryClose);
    }

    private void UnregisterUpgradeHandler()
    {
        UnregisterBackHandler(_upgradeUI);
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

    private sealed class BackHandler
    {
        public object Owner { get; }
        public Func<bool> TryClose { get; }

        public BackHandler(object owner, Func<bool> tryClose)
        {
            Owner = owner;
            TryClose = tryClose;
        }
    }

}
