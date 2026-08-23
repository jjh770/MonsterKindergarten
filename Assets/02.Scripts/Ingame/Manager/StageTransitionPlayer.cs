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
    [SerializeField] private SpriteRenderer _skyBackgroundRenderer;

    [Header("Stage Audio")]
    [SerializeField] private AudioClip _groundBgm;
    [SerializeField] private AudioClip _skyBgm;

    [Header("Transition")]
    [SerializeField, Min(0.1f)] private float _transitionDuration = 1.2f;
    [SerializeField, Min(1f)] private float _cameraTravelDistance = 6f;

    private Vector3 _cameraBasePosition;
    private Sequence _transitionSequence;

    public bool IsTransitioning { get; private set; }

    private void Awake()
    {
        if (_camera == null || _stageUI == null || _skyBackgroundRenderer == null)
        {
            Debug.LogError("스테이지 전환 연출의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _cameraBasePosition = _camera.transform.position;
    }

    private void OnDestroy()
    {
        _transitionSequence?.Kill();
    }

    public void ApplyEnvironment(EGameStage stage, float crossFadeDuration)
    {
        _skyBackgroundRenderer.enabled = true;
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
        float halfDuration = _transitionDuration * 0.5f;
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
            _transitionDuration);

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
                startPosition.y + _cameraTravelDistance,
                Mathf.Min(0.9f, _transitionDuration))
            .SetEase(Ease.InQuad)
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

    private AudioClip GetStageBgm(EGameStage stage)
    {
        return stage == EGameStage.Ground ? _groundBgm : _skyBgm;
    }
}
