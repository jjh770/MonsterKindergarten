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

    [Tooltip("배경을 교차시켜 바꾸는 쪽입니다. 비우면 화면을 덮는 방식으로 바뀝니다.")]
    [SerializeField] private BackgroundMove _backgroundMove;

    [Tooltip("두 배경이 부드럽게 교차되는 데 걸리는 시간입니다.")]
    [FormerlySerializedAs("_themeSlideDuration")]
    [SerializeField, Min(0.1f)] private float _themeDissolveDuration = 0.8f;

    [Header("Camera Transition")]
    [SerializeField, Min(0.1f)] private float _cameraTransitionDuration = 1.2f;
    [SerializeField, Min(1f)] private float _cameraTravelDistance = 6f;

    [Tooltip("장식장에서 쓰는 화면 크기입니다. 메인 필드보다 키우면 같은 화면에 더 넓은 방이 들어갑니다.")]
    [SerializeField, Min(0.1f)] private float _displayRoomOrthographicSize = 6.5f;

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
    private EGameplaySpace _currentSpace = EGameplaySpace.MainField;
    private Sequence _transitionSequence;
    private Sequence _focusSequence;

    // 테마에서 마지막으로 뽑은 곡. 장식장에 다녀와도 듣던 곡으로 돌아가려면
    // 무엇을 듣고 있었는지 기억해야 한다.
    private AudioClip _currentThemeBgm;
    private SlimeController _displayRoomFocusTarget;

    public bool IsTransitioning { get; private set; }

    // 공간마다 쉬는 자리의 화면 크기가 다르다. 확대·복귀·경계 계산이 모두 이 값을
    // 기준으로 삼아야 장식장에서 확대했다가 돌아올 때 메인 필드 크기로 튀지 않는다.
    private float BaseOrthographicSize => _currentSpace == EGameplaySpace.DisplayRoom
        ? _displayRoomOrthographicSize
        : _cameraBaseOrthographicSize;

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
                    Mathf.Min(BaseOrthographicSize, _displayRoomFocusSize),
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
                        BaseOrthographicSize,
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
                        BaseOrthographicSize,
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
                .DOOrthoSize(BaseOrthographicSize, _displayRoomFocusDuration)
                .SetEase(Ease.OutQuad));
        _focusSequence.OnComplete(() =>
        {
            _focusSequence = null;
            _camera.transform.position = _cameraBasePosition;
            _camera.orthographicSize = BaseOrthographicSize;
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

        // 배경만 부드럽게 교차시킨다. 화면을 덮지 않으므로 슬라임은 계속 보이고,
        // 장소 이동이 아니라 환경이 바뀌는 것으로 읽힌다.
        if (_backgroundMove != null &&
            _backgroundMove.TryPlayThemeDissolve(
                targetTheme,
                _themeDissolveDuration,
                () => EndBackgroundThemeDissolve(onCompleted)))
        {
            AudioManager.Instance?.CrossFadeBGM(
                GetThemeBgm(targetTheme),
                _themeDissolveDuration);

            // 상태와 BGM은 디졸브와 함께 간다. 가려지는 순간이 없으므로 중간까지
            // 기다리지 않는다.
            onThemeSwitched?.Invoke();
            ApplyEnvironment(targetTheme, crossFadeDuration: -1f);
            return;
        }

        // 밀 자리가 없을 때(장식장을 보는 중 등)는 화면을 덮었다 걷는 방식으로 바꾼다.
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

    // 디졸브에는 덮개가 없어 걷어 낼 것도 없다. 입력만 돌려준다.
    private void EndBackgroundThemeDissolve(Action onCompleted)
    {
        IsTransitioning = false;
        _backgroundThemeUI.SetButtonInteractable(true);
        onCompleted?.Invoke();
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

        // 공간이 바뀌는 동안 음악도 함께 넘어간다.
        //
        // 나올 때는 듣던 곡으로 돌아간다. 여기서 다시 뽑으면 장식장을 잠깐 들여다본
        // 것만으로 테마 음악이 갈린다. 장식장 곡이 비어 있으면 CrossFadeBGM이
        // 아무것도 하지 않아 현재 테마 곡이 그대로 이어진다.
        AudioManager.Instance?.CrossFadeBGM(
            targetSpace == EGameplaySpace.DisplayRoom
                ? AudioManager.Instance.GetRandomDisplayRoomBgm()
                : _currentThemeBgm,
            _cameraTransitionDuration);

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
            // 화면이 완전히 덮인 순간이라 카메라를 그냥 갈아 끼워도 보이지 않는다.
            // 공간마다 화면 크기가 다르므로 위치와 함께 여기서 맞춘다.
            //
            // 알리기 전에 바꾼다. 배경은 카메라 높이에 맞춰 크기를 다시 잡는데,
            // 순서가 뒤바뀌면 예전 높이로 계산해 화면보다 짧은 배경이 깔린다.
            _currentSpace = targetSpace;
            _camera.orthographicSize = BaseOrthographicSize;

            Vector3 cameraPosition = _cameraBasePosition;
            cameraPosition.x -= direction * _cameraTravelDistance;
            _camera.transform.position = cameraPosition;

            onSpaceSwitched?.Invoke();
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
            BaseOrthographicSize - _camera.orthographicSize);
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
        _camera.orthographicSize = BaseOrthographicSize;
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

    // 테마를 실제로 바꿀 때만 불린다. 같은 테마를 다시 고르면 호출부가 먼저
    // 돌려보내므로, 여기서 뽑을 때마다 곡이 갈리는 걱정은 하지 않아도 된다.
    private AudioClip GetThemeBgm(EBackgroundTheme theme)
    {
        AudioClip picked = AudioManager.Instance?.GetRandomThemeBgm(theme);

        // 장식장에서 나올 때 되돌아갈 곡이다. 뽑지 못했으면 이전 기억을 지우지
        // 않는다. 지우면 장식장에서 나올 때 돌아갈 곳이 없어진다.
        if (picked != null) _currentThemeBgm = picked;

        return picked;
    }

}
