using System;
using UnityEngine;
using UnityEngine.UI;

public sealed class TutorialPresentation : IDisposable
{
    private readonly Canvas _tutorialCanvas;
    private readonly TutorialDialogueView _dialogueView;
    private TutorialDialogueLine[] _activeDialogue;
    private int _dialogueIndex;
    private Action _onDialogueComplete;
    private bool _keepSpotlightVisible;
    private TutorialDialoguePlacement _dialoguePlacement;
    private bool _isDisposed;

    public TutorialSpotlightView Spotlight { get; }

    public TutorialPresentation(
        Canvas parentCanvas,
        Canvas sortingReference,
        TutorialDialogueView dialoguePrefab,
        TutorialSpotlightView spotlightPrefab)
    {
        _tutorialCanvas = CreateTutorialCanvas(parentCanvas, sortingReference);
        _dialogueView = UnityEngine.Object.Instantiate(
            dialoguePrefab,
            _tutorialCanvas.transform);
        Spotlight = UnityEngine.Object.Instantiate(
            spotlightPrefab,
            _tutorialCanvas.transform);
        _dialogueView.NextRequested += OnNextRequested;
    }

    public void ShowDialogue(
        TutorialDialogueLine[] lines,
        Action onComplete,
        bool keepSpotlightVisible = false,
        TutorialDialoguePlacement placement = TutorialDialoguePlacement.Bottom)
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

        if (_activeDialogue == null || _activeDialogue.Length == 0)
        {
            FinishDialogue();
            return;
        }

        ShowCurrentLine();
    }

    public void Dispose()
    {
        if (_isDisposed) return;

        _isDisposed = true;
        _dialogueView.NextRequested -= OnNextRequested;
        _activeDialogue = null;
        _onDialogueComplete = null;
        UnityEngine.Object.Destroy(_tutorialCanvas.gameObject);
    }

    private void OnNextRequested()
    {
        if (_activeDialogue == null) return;

        _dialogueIndex++;
        if (_dialogueIndex < _activeDialogue.Length)
        {
            ShowCurrentLine();
            return;
        }

        FinishDialogue();
    }

    private void ShowCurrentLine()
    {
        TutorialDialogueLine line = _activeDialogue[_dialogueIndex];
        _dialogueView.Show(
            line.Speaker,
            line.Message,
            dimBackground: !_keepSpotlightVisible,
            placement: _dialoguePlacement);
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

    private static Canvas CreateTutorialCanvas(
        Canvas parentCanvas,
        Canvas sortingReference)
    {
        GameObject canvasObject = new GameObject(
            "TutorialCanvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(GraphicRaycaster));
        canvasObject.layer = parentCanvas.gameObject.layer;

        RectTransform rectTransform = (RectTransform)canvasObject.transform;
        rectTransform.SetParent(parentCanvas.transform, false);
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        Canvas reference = sortingReference != null
            ? sortingReference
            : parentCanvas;
        Canvas tutorialCanvas = canvasObject.GetComponent<Canvas>();
        tutorialCanvas.overrideSorting = true;
        tutorialCanvas.sortingLayerID = reference.sortingLayerID;
        tutorialCanvas.sortingOrder = reference.sortingOrder + 100;
        return tutorialCanvas;
    }
}
