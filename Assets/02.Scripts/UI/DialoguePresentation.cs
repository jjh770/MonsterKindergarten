using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class DialoguePresentation : IDisposable
{
    private readonly GameObject _presentationObject;
    private readonly TutorialDialogueView _dialogueView;
    private IReadOnlyList<DialogueLine> _activeDialogue;
    private int _dialogueIndex;
    private Action _onDialogueComplete;
    private bool _keepSpotlightVisible;
    private DialoguePlacement _dialoguePlacement;
    private bool _isDisposed;

    public TutorialSpotlightView Spotlight { get; }

    public DialoguePresentation(
        Canvas parentCanvas,
        Canvas sortingReference,
        GameObject presentationPrefab)
    {
        _presentationObject = UnityEngine.Object.Instantiate(
            presentationPrefab,
            parentCanvas.transform);

        Canvas dialogueCanvas = _presentationObject.GetComponent<Canvas>();
        _dialogueView = _presentationObject.GetComponentInChildren<TutorialDialogueView>(true);
        Spotlight = _presentationObject.GetComponentInChildren<TutorialSpotlightView>(true);
        if (dialogueCanvas == null || _dialogueView == null || Spotlight == null)
        {
            UnityEngine.Object.Destroy(_presentationObject);
            throw new InvalidOperationException(
                "DialoguePresentation 프리팹의 필수 컴포넌트가 없습니다.");
        }

        ConfigureSorting(dialogueCanvas, sortingReference ?? parentCanvas);
        _dialogueView.NextRequested += OnNextRequested;
    }

    public void ShowDialogue(
        IReadOnlyList<DialogueLine> lines,
        Action onComplete,
        bool keepSpotlightVisible = false,
        DialoguePlacement placement = DialoguePlacement.Bottom)
    {
        _keepSpotlightVisible = keepSpotlightVisible;
        _dialoguePlacement = placement;
        if (!keepSpotlightVisible)
        {
            Spotlight.Hide();
        }

        _activeDialogue = lines;
        _dialogueIndex = 0;
        _onDialogueComplete = onComplete;

        if (_activeDialogue == null || _activeDialogue.Count == 0)
        {
            FinishDialogue();
            return;
        }

        ShowCurrentLine();
    }

    // 진행 중인 대화를 끝맺음 콜백 없이 접는다. 시퀀스가 도중에 물러날 때 쓴다. 접지 않으면
    // 튜토리얼은 끝났는데 대화창만 화면에 남는다.
    public void CancelDialogue()
    {
        _dialogueView.Hide();
        _activeDialogue = null;
        _dialogueIndex = 0;
        _onDialogueComplete = null;
        _keepSpotlightVisible = false;
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        _dialogueView.NextRequested -= OnNextRequested;
        _activeDialogue = null;
        _onDialogueComplete = null;
        UnityEngine.Object.Destroy(_presentationObject);
    }

    private void OnNextRequested()
    {
        if (_activeDialogue == null) return;

        _dialogueIndex++;
        if (_dialogueIndex < _activeDialogue.Count)
        {
            ShowCurrentLine();
            return;
        }

        FinishDialogue();
    }

    private void ShowCurrentLine()
    {
        DialogueLine line = _activeDialogue[_dialogueIndex];

        // 구멍 가까이에 놓는 배치는 지금 비추는 구멍이 있어야 한다. 없으면 뷰가 아래쪽으로 돌아간다.
        Rect? spotlightRect = null;
        if (_dialoguePlacement == DialoguePlacement.NearSpotlight &&
            Spotlight.TryGetHoleWorldRect(out Rect holeRect))
        {
            spotlightRect = holeRect;
        }

        _dialogueView.Show(
            line.Speaker,
            line.Message,
            dimBackground: !_keepSpotlightVisible,
            placement: _dialoguePlacement,
            spotlightWorldRect: spotlightRect);
    }

    private void FinishDialogue()
    {
        Action onComplete = _onDialogueComplete;

        _dialogueView.Hide();
        _activeDialogue = null;
        _dialogueIndex = 0;
        _onDialogueComplete = null;
        _keepSpotlightVisible = false;

        onComplete?.Invoke();
    }

    private static void ConfigureSorting(Canvas dialogueCanvas, Canvas reference)
    {
        dialogueCanvas.overrideSorting = true;
        dialogueCanvas.sortingLayerID = reference.sortingLayerID;
        dialogueCanvas.sortingOrder = reference.sortingOrder + 100;
    }
}
