using System;
using DG.Tweening;
using UnityEngine;

// 화면을 독점하는 일시 모드의 공통 수명 주기다.
//
// 서랍, 월드 입력, 뒤로가기, HUD는 모두 이 세션 자신을 소유자로 등록한다.
// 바깥 UI가 이미 같은 자원을 쓰고 있어도 모드가 끝날 때 자기 요청만 해제하므로
// 튜토리얼 잠금이나 정보창 입력 차단을 먼저 풀지 않는다.
public sealed class GameplayModeSession : IDisposable
{
    private readonly UpgradeUI _upgradeUI;
    private readonly Clicker _clicker;
    private readonly GameExitManager _gameExitManager;
    private readonly HudVisibility _hudVisibility;
    private readonly EHudParts _hiddenHudParts;
    private readonly GameObject _modeRoot;
    private readonly CanvasGroup _fadeTarget;
    private readonly float _duration;
    private readonly float _activeAlpha;
    private readonly float _inactiveAlpha;
    private readonly bool _manageRaycasts;

    private Tween _fadeTween;
    private bool _isActive;

    public bool IsTransitioning => _fadeTween != null;

    public GameplayModeSession(
        UpgradeUI upgradeUI,
        Clicker clicker,
        GameExitManager gameExitManager,
        HudVisibility hudVisibility,
        EHudParts hiddenHudParts,
        GameObject modeRoot,
        CanvasGroup fadeTarget,
        float duration,
        float activeAlpha = 1f,
        float inactiveAlpha = 0f,
        bool manageRaycasts = true)
    {
        _upgradeUI = upgradeUI;
        _clicker = clicker;
        _gameExitManager = gameExitManager;
        _hudVisibility = hudVisibility;
        _hiddenHudParts = hiddenHudParts;
        _modeRoot = modeRoot;
        _fadeTarget = fadeTarget;
        _duration = Mathf.Max(0f, duration);
        _activeAlpha = activeAlpha;
        _inactiveAlpha = inactiveAlpha;
        _manageRaycasts = manageRaycasts;
    }

    public void Enter(
        ClickerInputMode inputMode,
        ClickerInputPriority inputPriority,
        Func<bool> tryClose,
        bool bringRootToFront = false,
        Action onCompleted = null)
    {
        if (_isActive) return;

        _isActive = true;
        _upgradeUI.PushStandDown(this);
        _clicker.PushMode(this, inputMode, inputPriority);
        _gameExitManager.RegisterBackHandler(this, tryClose);
        _hudVisibility.PushHide(this, _hiddenHudParts);

        _modeRoot.SetActive(true);
        if (bringRootToFront)
        {
            _modeRoot.transform.SetAsLastSibling();
        }

        _fadeTarget.alpha = _inactiveAlpha;
        if (_manageRaycasts)
        {
            _fadeTarget.blocksRaycasts = true;
        }

        FadeTo(_activeAlpha, onCompleted);
    }

    public void SetInputMode(
        ClickerInputMode inputMode,
        ClickerInputPriority inputPriority)
    {
        if (!_isActive) return;

        _clicker.PushMode(this, inputMode, inputPriority);
    }

    public void Exit(
        bool animated = true,
        bool deactivateRootImmediately = false,
        Action onCompleted = null)
    {
        if (!_isActive)
        {
            ResetPresentation();
            onCompleted?.Invoke();
            return;
        }

        _isActive = false;
        ReleaseOwnership(animated);

        if (_manageRaycasts)
        {
            _fadeTarget.blocksRaycasts = false;
        }

        if (deactivateRootImmediately)
        {
            _modeRoot.SetActive(false);
        }

        if (!animated || _duration <= 0f)
        {
            KillFade();
            _fadeTarget.alpha = _inactiveAlpha;
            _modeRoot.SetActive(false);
            onCompleted?.Invoke();
            return;
        }

        FadeTo(
            _inactiveAlpha,
            () =>
            {
                _modeRoot.SetActive(false);
                onCompleted?.Invoke();
            });
    }

    public void ResetPresentation()
    {
        _isActive = false;
        ReleaseOwnership(animated: false);
        KillFade();
        if (_fadeTarget != null)
        {
            _fadeTarget.alpha = _inactiveAlpha;
            if (_manageRaycasts)
            {
                _fadeTarget.blocksRaycasts = false;
            }
        }

        if (_modeRoot != null)
        {
            _modeRoot.SetActive(false);
        }
    }

    public void Dispose()
    {
        ResetPresentation();
    }

    private void ReleaseOwnership(bool animated)
    {
        if (_gameExitManager != null)
        {
            _gameExitManager.UnregisterBackHandler(this);
        }

        if (_clicker != null)
        {
            _clicker.ReleaseMode(this);
        }

        if (_upgradeUI != null)
        {
            _upgradeUI.ReleaseStandDown(this, animated);
        }

        if (_hudVisibility != null)
        {
            _hudVisibility.Release(this, animated);
        }
    }

    private void FadeTo(float alpha, Action onCompleted)
    {
        KillFade();
        if (_duration <= 0f)
        {
            _fadeTarget.alpha = alpha;
            onCompleted?.Invoke();
            return;
        }

        _fadeTween = _fadeTarget
            .DOFade(alpha, _duration)
            .OnComplete(() =>
            {
                _fadeTween = null;
                onCompleted?.Invoke();
            });
    }

    private void KillFade()
    {
        _fadeTween?.Kill();
        _fadeTween = null;
    }
}
