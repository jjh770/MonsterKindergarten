using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

// 배경 테마와 게임플레이 공간 전환 연출을 담당한다.
public sealed class GameplayTransitionPlayer : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Camera _camera;
    [FormerlySerializedAs("_stageUI")]
    [SerializeField] private BackgroundThemeUI _backgroundThemeUI;
    // 전환 중 화면을 덮는 판. 이 연출의 소유물이라 여기서 직접 다룬다.
    [SerializeField] private Image _transitionOverlay;

    [Header("Background Audio")]
    [SerializeField] private AudioClip _groundBgm;
    [SerializeField] private AudioClip _skyBgm;

    [Header("Camera Transition")]
    [SerializeField, Min(0.1f)] private float _cameraTransitionDuration = 1.2f;
    [SerializeField, Min(1f)] private float _cameraTravelDistance = 6f;

    [Header("Display Room Focus")]
    [SerializeField, Min(0.1f)] private float _displayRoomFocusDuration = 0.35f;
    [SerializeField, Min(0.1f)] private float _displayRoomFocusSize = 2.5f;
    [SerializeField, Min(0.1f)] private float _displayRoomObservationSize = 2.1f;
    [SerializeField, Min(0f)] private float _displayRoomFollowSpeed = 8f;

    [Header("Slime Transfer")]
    [SerializeField] private SlimeTransferSettings _displayRoomTransfer = new()
    {
        Distance = 6f,
        Duration = 0.45f,
        Ease = Ease.InBack,
    };

    private Vector3 _cameraBasePosition;
    private float _cameraBaseOrthographicSize;
    private Sequence _transitionSequence;
    private Sequence _focusSequence;
    private SlimeController _displayRoomFocusTarget;

    public bool IsTransitioning { get; private set; }

    private void Awake()
    {
        if (_camera == null || _backgroundThemeUI == null)
        {
            Debug.LogError("전환 연출의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _cameraBasePosition = _camera.transform.position;
        _cameraBaseOrthographicSize = _camera.orthographicSize;

        // 오버레이는 전환 중에만 입력을 막는다. 씬에는 켜진 채 저장되므로 여기서 내린다.
        if (_transitionOverlay != null)
        {
            _transitionOverlay.raycastTarget = false;
            SetOverlayAlpha(0f);
        }
    }

    private void LateUpdate()
    {
        if (_displayRoomFocusTarget == null || IsTransitioning)
        {
            return;
        }

        Vector3 destination = GetDisplayRoomFocusPosition(_displayRoomFocusTarget);
        float followAmount = 1f - Mathf.Exp(-_displayRoomFollowSpeed * Time.deltaTime);
        _camera.transform.position = Vector3.Lerp(
            _camera.transform.position,
            destination,
            followAmount);
    }

    private void OnDestroy()
    {
        _transitionSequence?.Kill();
        _focusSequence?.Kill();
        ClearDisplayRoomFocusTarget();
    }

    public void FocusDisplayRoomSlime(SlimeController target, Action onComplete)
    {
        if (target == null)
        {
            onComplete?.Invoke();
            return;
        }

        if (_displayRoomFocusTarget != null &&
            _displayRoomFocusTarget != target)
        {
            _displayRoomFocusTarget.SetDisplayRoomCameraFocus(false);
        }

        _displayRoomFocusTarget = target;
        target.SetDisplayRoomCameraFocus(true);
        _focusSequence?.Kill();
        _focusSequence = DOTween.Sequence();
        _focusSequence.Join(
            _camera
                .DOOrthoSize(
                    Mathf.Min(_cameraBaseOrthographicSize, _displayRoomFocusSize),
                    _displayRoomFocusDuration)
                .SetEase(Ease.OutQuad));
        _focusSequence.OnComplete(() =>
        {
            _focusSequence = null;
            onComplete?.Invoke();
        });
    }

    public void BeginDisplayRoomObservation(Action onComplete = null)
    {
        if (_displayRoomFocusTarget == null)
        {
            onComplete?.Invoke();
            return;
        }

        _focusSequence?.Kill();
        _focusSequence = DOTween.Sequence();
        _focusSequence.Join(
            _camera
                .DOOrthoSize(
                    Mathf.Min(
                        _cameraBaseOrthographicSize,
                        _displayRoomObservationSize),
                    _displayRoomFocusDuration)
                .SetEase(Ease.OutQuad));
        _focusSequence.OnComplete(() =>
        {
            _focusSequence = null;
            onComplete?.Invoke();
        });
    }

    public void EndDisplayRoomObservation(Action onComplete = null)
    {
        if (_displayRoomFocusTarget == null)
        {
            onComplete?.Invoke();
            return;
        }

        _focusSequence?.Kill();
        _focusSequence = DOTween.Sequence();
        _focusSequence.Join(
            _camera
                .DOOrthoSize(
                    Mathf.Min(
                        _cameraBaseOrthographicSize,
                        _displayRoomFocusSize),
                    _displayRoomFocusDuration)
                .SetEase(Ease.OutQuad));
        _focusSequence.OnComplete(() =>
        {
            _focusSequence = null;
            onComplete?.Invoke();
        });
    }

    public void RestoreDisplayRoomFocus(Action onComplete = null)
    {
        ClearDisplayRoomFocusTarget();
        _focusSequence?.Kill();
        _focusSequence = DOTween.Sequence();
        _focusSequence.Join(
            _camera.transform
                .DOMove(_cameraBasePosition, _displayRoomFocusDuration)
                .SetEase(Ease.OutQuad));
        _focusSequence.Join(
            _camera
                .DOOrthoSize(_cameraBaseOrthographicSize, _displayRoomFocusDuration)
                .SetEase(Ease.OutQuad));
        _focusSequence.OnComplete(() =>
        {
            _focusSequence = null;
            _camera.transform.position = _cameraBasePosition;
            _camera.orthographicSize = _cameraBaseOrthographicSize;
            onComplete?.Invoke();
        });
    }

    public void ApplyEnvironment(EBackgroundTheme theme, float crossFadeDuration)
    {
        _camera.backgroundColor = Color.white;

        if (crossFadeDuration < 0f || AudioManager.Instance == null) return;

        AudioManager.Instance.CrossFadeBGM(GetThemeBgm(theme), crossFadeDuration);
    }

    // onThemeSwitched는 화면이 가려진 중간 시점에, onCompleted는 연출이 끝난 뒤에 호출된다.
    public void PlayBackgroundTheme(
        EBackgroundTheme targetTheme,
        Action onThemeSwitched,
        Action onCompleted)
    {
        IsTransitioning = true;
        _backgroundThemeUI.SetButtonInteractable(false);
        BeginOverlay();

        float halfDuration = _cameraTransitionDuration * 0.5f;

        AudioManager.Instance?.CrossFadeBGM(
            GetThemeBgm(targetTheme),
            _cameraTransitionDuration);

        _transitionSequence?.Kill();
        _transitionSequence = DOTween.Sequence();
        _transitionSequence.Join(
            FadeOverlay(1f, halfDuration));

        _transitionSequence.AppendCallback(() =>
        {
            onThemeSwitched?.Invoke();
            ApplyEnvironment(targetTheme, crossFadeDuration: -1f);
        });
        _transitionSequence.Append(
            FadeOverlay(0f, halfDuration));

        _transitionSequence.OnComplete(() =>
        {
            _transitionSequence = null;
            _camera.transform.position = _cameraBasePosition;
            EndOverlay();
            IsTransitioning = false;
            _backgroundThemeUI.SetButtonInteractable(true);
            onCompleted?.Invoke();
        });
    }

    public void PlaySpace(
        EGameplaySpace targetSpace,
        Action onSpaceSwitched,
        Action onCompleted)
    {
        if (IsTransitioning) return;

        ResetDisplayRoomFocus();

        IsTransitioning = true;
        _backgroundThemeUI.SetButtonInteractable(false);
        BeginOverlay();

        float direction = targetSpace == EGameplaySpace.DisplayRoom ? 1f : -1f;
        float halfDuration = _cameraTransitionDuration * 0.5f;

        _transitionSequence?.Kill();
        _transitionSequence = DOTween.Sequence();
        _transitionSequence.Join(
            _camera.transform.DOMoveX(
                _cameraBasePosition.x + direction * _cameraTravelDistance,
                halfDuration).SetEase(Ease.InQuad));
        _transitionSequence.Join(
            FadeOverlay(1f, halfDuration));
        _transitionSequence.AppendCallback(() =>
        {
            onSpaceSwitched?.Invoke();

            Vector3 cameraPosition = _cameraBasePosition;
            cameraPosition.x -= direction * _cameraTravelDistance;
            _camera.transform.position = cameraPosition;
        });
        _transitionSequence.Append(
            _camera.transform.DOMoveX(
                _cameraBasePosition.x,
                halfDuration).SetEase(Ease.OutQuad));
        _transitionSequence.Join(
            FadeOverlay(0f, halfDuration));
        _transitionSequence.OnComplete(() =>
        {
            _transitionSequence = null;
            _camera.transform.position = _cameraBasePosition;
            EndOverlay();
            IsTransitioning = false;
            _backgroundThemeUI.SetButtonInteractable(true);
            onCompleted?.Invoke();
        });
    }

    // 장식장으로 보낼 때는 화면 전환 없이 슬라임만 옆으로 내보낸다.
    public Tween PlayDisplayRoomTransfer(SlimeController target, Action onComplete)
    {
        if (target == null) return null;

        target.PreparePresentationTransfer();

        float direction = target.transform.position.x >= 0f ? 1f : -1f;
        return target.transform
            .DOMoveX(
                target.transform.position.x + direction * _displayRoomTransfer.Distance,
                _displayRoomTransfer.Duration)
            .SetEase(_displayRoomTransfer.Ease)
            .OnComplete(() => onComplete?.Invoke());
    }

    private Vector3 GetDisplayRoomFocusPosition(SlimeController target)
    {
        if (target == null) return _cameraBasePosition;

        Vector3 position = target.transform.position;
        position.z = _cameraBasePosition.z;

        // 확대된 화면의 세로 범위를 기본 화면 안에 가둔다.
        // 위아래 벽까지 따라가면 배경 바깥의 여백이 드러난다.
        // 가로는 배경이 기본 화면보다 넓어 가두지 않는다.
        float verticalMargin = Mathf.Max(
            0f,
            _cameraBaseOrthographicSize - _camera.orthographicSize);
        position.y = Mathf.Clamp(
            position.y,
            _cameraBasePosition.y - verticalMargin,
            _cameraBasePosition.y + verticalMargin);
        return position;
    }

    // 추적 대상을 놓을 때 인터폴레이션도 함께 되돌린다.
    // ?.는 참조 null만 보므로 파괴된 오브젝트를 거르지 못한다.
    // Unity의 == 오버로드를 타도록 명시적으로 비교한다.
    private void ClearDisplayRoomFocusTarget()
    {
        if (_displayRoomFocusTarget != null)
        {
            _displayRoomFocusTarget.SetDisplayRoomCameraFocus(false);
        }

        _displayRoomFocusTarget = null;
    }

    private void ResetDisplayRoomFocus()
    {
        ClearDisplayRoomFocusTarget();
        _focusSequence?.Kill();
        _focusSequence = null;
        _camera.transform.position = _cameraBasePosition;
        _camera.orthographicSize = _cameraBaseOrthographicSize;
    }

    // 전환 시작 시 오버레이를 최상단으로 올리고 입력을 막는다.
    private void BeginOverlay()
    {
        if (_transitionOverlay == null) return;

        _transitionOverlay.transform.SetAsLastSibling();
        _transitionOverlay.raycastTarget = true;
        SetOverlayAlpha(0f);
    }

    private void EndOverlay()
    {
        if (_transitionOverlay == null) return;

        _transitionOverlay.raycastTarget = false;
    }

    private Tween FadeOverlay(float alpha, float duration)
    {
        return _transitionOverlay != null
            ? _transitionOverlay.DOFade(alpha, duration)
            : null;
    }

    private void SetOverlayAlpha(float alpha)
    {
        Color color = _transitionOverlay.color;
        color.a = alpha;
        _transitionOverlay.color = color;
    }

    private AudioClip GetThemeBgm(EBackgroundTheme theme)
    {
        return theme == EBackgroundTheme.Ground ? _groundBgm : _skyBgm;
    }
}
