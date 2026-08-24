using System;
using DG.Tweening;
using UnityEngine;

// 하늘 스테이지를 처음 여는 1회성 인트로 연출을 담당한다.
// 언제 시작할지는 StageManager가 판단하고, 이 클래스는 진행만 맡는다.
public sealed class SkyIntroDirector : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private StageUI _stageUI;
    [SerializeField] private TutorialDialogueView _dialoguePrefab;
    [SerializeField] private TutorialSpotlightView _guidePrefab;
    [SerializeField] private TutorialContent _tutorialContent;

    [Header("Intro")]
    [SerializeField, Min(0f)] private float _chargeDuration = 0.8f;

    private TutorialPresentation _presentation;
    private SlimeController _pendingTarget;
    private Sequence _chargeSequence;
    private bool _isStarted;

    public bool HasPendingTarget => _pendingTarget != null;
    public bool IsWaitingForStageButton { get; private set; }

    // 인트로가 하늘 전환을 요청한다. 두 번째 인자는 도착 후 콜백이다.
    public event Action<SlimeController, Action> SkyTransitionRequested;
    public event Action<bool> InteractionEnableRequested;

    private void Awake()
    {
        if (_canvas == null ||
            _stageUI == null ||
            _dialoguePrefab == null ||
            _guidePrefab == null ||
            _tutorialContent == null)
        {
            Debug.LogError("하늘 인트로 연출의 필수 참조가 비어 있습니다.", this);
            enabled = false;
        }
    }

    private void OnDestroy()
    {
        _chargeSequence?.Kill();
        _presentation?.Dispose();
    }

    public void Prepare(SlimeController target)
    {
        _pendingTarget = target;
    }

    public void Begin()
    {
        if (_pendingTarget == null || _isStarted) return;

        _isStarted = true;
        InteractionEnableRequested?.Invoke(false);
        _pendingTarget.PrepareStageTransfer();

        _presentation?.Dispose();
        _presentation = new TutorialPresentation(
            _canvas,
            _canvas,
            _dialoguePrefab,
            _guidePrefab);
        _presentation.ShowDialogue(
            _tutorialContent.GetDialogue(DialogueId.SkyIntro),
            PlayJourney);
    }

    public void HideSpotlight()
    {
        _presentation?.Spotlight.Hide();
    }

    public void Complete()
    {
        IsWaitingForStageButton = false;
        _presentation?.Dispose();
        _presentation = null;
        InteractionEnableRequested?.Invoke(true);
    }

    private void PlayJourney()
    {
        SlimeController target = _pendingTarget;
        if (target == null)
        {
            CompleteWithoutTarget();
            return;
        }

        target.PrepareStageTransfer();
        _chargeSequence?.Kill();
        _chargeSequence = DOTween.Sequence();
        _chargeSequence.Append(
            target.transform.DOPunchScale(
                new Vector3(0.25f, -0.18f, 0f),
                Mathf.Max(0.1f, _chargeDuration),
                5,
                0.7f));
        _chargeSequence.OnComplete(() =>
        {
            _chargeSequence = null;
            SkyTransitionRequested?.Invoke(target, OnArrived);
        });
    }

    private void OnArrived()
    {
        SlimeManager.Instance?.UpdateStageProgress(
            EGameStage.Sky,
            skyIntroCompleted: true);
        _pendingTarget = null;
        _isStarted = false;
        _stageUI.SetButtonVisible(true, true);

        RectTransform buttonTarget = _stageUI.ButtonTarget;
        if (_presentation == null || buttonTarget == null)
        {
            Complete();
            return;
        }

        IsWaitingForStageButton = true;
        _presentation.Spotlight.ShowUiTarget(
            _tutorialContent.StageButtonMessage,
            buttonTarget,
            SpotlightInteractionMode.PassThroughPrimary);
    }

    private void CompleteWithoutTarget()
    {
        SlimeManager.Instance?.UpdateStageProgress(
            EGameStage.Ground,
            skyIntroCompleted: true);
        _stageUI.SetButtonVisible(true, true);
        Complete();
    }
}
