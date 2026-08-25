using System;
using DG.Tweening;
using UnityEngine;

// 스테이지 전환의 카메라, 오버레이, 배경, BGM 연출만 담당한다.
// 현재 스테이지 값과 저장 여부는 StageManager가 콜백으로 처리한다.
public sealed class StageTransitionPlayer : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Camera _camera;
    [SerializeField] private StageUI _stageUI;

    [Header("Stage Audio")]
    [SerializeField] private AudioClip _groundBgm;
    [SerializeField] private AudioClip _skyBgm;

    [Header("Camera Transition")]
    [SerializeField, Min(0.1f)] private float _cameraTransitionDuration = 1.2f;
    [SerializeField, Min(1f)] private float _cameraTravelDistance = 6f;

    [Header("Display Room Focus")]
    [SerializeField, Min(0.1f)] private float _displayRoomFocusDuration = 0.35f;
    [SerializeField, Min(0.1f)] private float _displayRoomFocusSize = 2.5f;
    [SerializeField, Min(0f)] private float _displayRoomFollowSpeed = 8f;

    [Header("Slime Transfer")]
    [SerializeField] private SlimeTransferSettings _skyTransfer = new()
    {
        Distance = 6f,
        Duration = 0.9f,
        Ease = Ease.InQuad,
    };
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
        if (_camera == null || _stageUI == null)
        {
            Debug.LogError("스테이지 전환 연출의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _cameraBasePosition = _camera.transform.position;
        _cameraBaseOrthographicSize = _camera.orthographicSize;
    }

    private void LateUpdate()
    {
        if (_displayRoomFocusTarget == null ||
            _focusSequence != null ||
            IsTransitioning)
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
    }

    public void FocusDisplayRoomSlime(SlimeController target, Action onComplete)
    {
        if (target == null)
        {
            onComplete?.Invoke();
            return;
        }

        _displayRoomFocusTarget = target;
        _focusSequence?.Kill();
        _focusSequence = DOTween.Sequence();
        _focusSequence.Join(
            _camera.transform
                .DOMove(GetDisplayRoomFocusPosition(target), _displayRoomFocusDuration)
                .SetEase(Ease.OutQuad));
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

    public void RestoreDisplayRoomFocus(Action onComplete = null)
    {
        _displayRoomFocusTarget = null;
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

    public void ApplyEnvironment(EGameStage stage, float crossFadeDuration)
    {
        _camera.backgroundColor = Color.white;

        if (crossFadeDuration < 0f || AudioManager.Instance == null) return;

        AudioManager.Instance.CrossFadeBGM(GetStageBgm(stage), crossFadeDuration);
    }

    // onStageSwitched는 화면이 가려진 중간 시점에, onCompleted는 연출이 끝난 뒤에 호출된다.
    public void Play(
        EGameStage targetStage,
        SlimeController travellingSlime,
        Action onStageSwitched,
        Action onCompleted)
    {
        IsTransitioning = true;
        _stageUI.SetButtonInteractable(false);
        _stageUI.BeginOverlay();

        float direction = targetStage == EGameStage.Sky ? 1f : -1f;
        float halfDuration = _cameraTransitionDuration * 0.5f;
        Vector2 slimeDestination = SpawnManager.Instance != null
            ? SpawnManager.Instance.GetRandomSpawnPosition()
            : Vector2.zero;
        Vector3 slimeStart = travellingSlime != null
            ? travellingSlime.transform.position
            : Vector3.zero;

        if (travellingSlime != null)
        {
            travellingSlime.PrepareStageTransfer();
        }

        AudioManager.Instance?.CrossFadeBGM(
            GetStageBgm(targetStage),
            _cameraTransitionDuration);

        _transitionSequence?.Kill();
        _transitionSequence = DOTween.Sequence();
        _transitionSequence.Join(
            _camera.transform.DOMoveY(
                _cameraBasePosition.y + direction * _cameraTravelDistance,
                halfDuration).SetEase(Ease.InQuad));
        _transitionSequence.Join(
            _stageUI.FadeOverlay(1f, halfDuration));

        if (travellingSlime != null)
        {
            _transitionSequence.Join(
                travellingSlime.transform.DOMoveY(
                    slimeStart.y + direction * _cameraTravelDistance,
                    halfDuration).SetEase(Ease.InQuad));
        }

        _transitionSequence.AppendCallback(() =>
        {
            onStageSwitched?.Invoke();
            ApplyEnvironment(targetStage, crossFadeDuration: -1f);

            Vector3 cameraPosition = _cameraBasePosition;
            cameraPosition.y -= direction * _cameraTravelDistance;
            _camera.transform.position = cameraPosition;

            if (travellingSlime != null)
            {
                travellingSlime.PrepareStageTransfer();
                travellingSlime.transform.position = new Vector3(
                    slimeDestination.x,
                    slimeDestination.y - direction * _cameraTravelDistance,
                    slimeStart.z);
            }
        });
        _transitionSequence.Append(
            _camera.transform.DOMoveY(
                _cameraBasePosition.y,
                halfDuration).SetEase(Ease.OutQuad));
        _transitionSequence.Join(
            _stageUI.FadeOverlay(0f, halfDuration));

        if (travellingSlime != null)
        {
            _transitionSequence.Join(
                travellingSlime.transform.DOMove(
                    new Vector3(
                        slimeDestination.x,
                        slimeDestination.y,
                        slimeStart.z),
                    halfDuration).SetEase(Ease.OutQuad));
        }

        _transitionSequence.OnComplete(() =>
        {
            _transitionSequence = null;
            _camera.transform.position = _cameraBasePosition;
            _stageUI.EndOverlay();
            IsTransitioning = false;
            _stageUI.SetButtonInteractable(true);
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
        _stageUI.SetButtonInteractable(false);
        _stageUI.BeginOverlay();

        float direction = targetSpace == EGameplaySpace.DisplayRoom ? 1f : -1f;
        float halfDuration = _cameraTransitionDuration * 0.5f;

        _transitionSequence?.Kill();
        _transitionSequence = DOTween.Sequence();
        _transitionSequence.Join(
            _camera.transform.DOMoveX(
                _cameraBasePosition.x + direction * _cameraTravelDistance,
                halfDuration).SetEase(Ease.InQuad));
        _transitionSequence.Join(
            _stageUI.FadeOverlay(1f, halfDuration));
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
            _stageUI.FadeOverlay(0f, halfDuration));
        _transitionSequence.OnComplete(() =>
        {
            _transitionSequence = null;
            _camera.transform.position = _cameraBasePosition;
            _stageUI.EndOverlay();
            IsTransitioning = false;
            _stageUI.SetButtonInteractable(true);
            onCompleted?.Invoke();
        });
    }

    // 하늘이 이미 열린 뒤의 일반 승급은 화면 전환 없이 슬라임만 올려 보낸다.
    public void PlayRegularSkyTransfer(SlimeController target, EGameStage currentStage)
    {
        if (target == null) return;

        target.PrepareStageTransfer();
        Vector3 startPosition = target.transform.position;
        Vector2 destination = SpawnManager.Instance != null
            ? SpawnManager.Instance.GetRandomSpawnPosition()
            : Vector2.zero;
        target.transform.DOMoveY(
                startPosition.y + _skyTransfer.Distance,
                Mathf.Min(_skyTransfer.Duration, _cameraTransitionDuration))
            .SetEase(_skyTransfer.Ease)
            .OnComplete(() =>
            {
                if (target == null) return;

                target.transform.position = new Vector3(
                    destination.x,
                    destination.y,
                    startPosition.z);
                target.SetStagePresentationActive(currentStage == EGameStage.Sky);
            });
    }

    // 장식장으로 보낼 때는 화면 전환 없이 슬라임만 옆으로 내보낸다.
    public Tween PlayDisplayRoomTransfer(SlimeController target, Action onComplete)
    {
        if (target == null) return null;

        target.PrepareStageTransfer();

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
        return position;
    }

    private void ResetDisplayRoomFocus()
    {
        _displayRoomFocusTarget = null;
        _focusSequence?.Kill();
        _focusSequence = null;
        _camera.transform.position = _cameraBasePosition;
        _camera.orthographicSize = _cameraBaseOrthographicSize;
    }

    private AudioClip GetStageBgm(EGameStage stage)
    {
        return stage == EGameStage.Ground ? _groundBgm : _skyBgm;
    }
}
