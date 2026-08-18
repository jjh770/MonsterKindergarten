using System;
using UnityEngine;

[RequireComponent(typeof(Clicker))]
public sealed class TutorialManager : MonoBehaviour
{
    private enum TutorialStep
    {
        None,
        Dialogue,
        Click,
        Drag,
        Complete,
    }

    [Serializable]
    private struct DialogueLine
    {
        [SerializeField] private string _speaker;
        [TextArea(2, 4)]
        [SerializeField] private string _message;

        public string Speaker => _speaker;
        public string Message => _message;

        public DialogueLine(string speaker, string message)
        {
            _speaker = speaker;
            _message = message;
        }
    }

    [Header("Guide UI")]
    [SerializeField] private Canvas _canvas;
    [SerializeField] private TutorialDialogueView _dialoguePrefab;
    [SerializeField] private TutorialSpotlightView _guidePrefab;
    [SerializeField] private string _clickMessage = "슬라임을 터치해 보세요!";
    [SerializeField] private string _dragMessage = "슬라임을 드래그해 보세요!";

    [Header("Dialogue")]
    [SerializeField] private DialogueLine[] _introductionDialogue =
    {
        new DialogueLine("선생님", "어서 와! 이곳은 몬스터 유치원이야."),
        new DialogueLine("선생님", "먼저 눈앞의 슬라임과 인사해 볼까?"),
    };

    private Clicker _clicker;
    private AutoClicker _autoClicker;
    private TutorialDialogueView _dialogueUI;
    private TutorialSpotlightView _guideUI;
    private SlimeController _tutorialSlime;
    private TutorialStep _step;
    private DialogueLine[] _activeDialogue;
    private int _dialogueIndex;
    private Action _onDialogueComplete;

    private void Awake()
    {
        _clicker = GetComponent<Clicker>();
        _autoClicker = FindFirstObjectByType<AutoClicker>();
    }

    private void Start()
    {
        _clicker.TargetClicked += OnTargetClicked;
        _clicker.TargetDragCompleted += OnTargetDragCompleted;

        if (SpawnManager.Instance == null) return;

        SpawnManager.Instance.OnTutorialSlimeReady += Begin;

        if (SpawnManager.Instance.TutorialSlime != null)
        {
            Begin(SpawnManager.Instance.TutorialSlime);
        }
    }

    private void OnDestroy()
    {
        if (_clicker != null)
        {
            _clicker.TargetClicked -= OnTargetClicked;
            _clicker.TargetDragCompleted -= OnTargetDragCompleted;
            _clicker.SetInputMode(true, true);
        }

        if (SpawnManager.Instance != null)
        {
            SpawnManager.Instance.OnTutorialSlimeReady -= Begin;
            SpawnManager.Instance.SetSpawningPaused(false);
        }

        _autoClicker?.SetPaused(false);
        _onDialogueComplete = null;
        _activeDialogue = null;

        if (_dialogueUI != null)
        {
            _dialogueUI.NextRequested -= OnDialogueNextRequested;
            Destroy(_dialogueUI.gameObject);
        }

        if (_guideUI != null)
        {
            Destroy(_guideUI.gameObject);
        }
    }

    private void Begin(SlimeController tutorialSlime)
    {
        if (tutorialSlime == null || _step != TutorialStep.None) return;
        if (_canvas == null || _dialoguePrefab == null || _guidePrefab == null)
        {
            Debug.LogError("튜토리얼 Canvas, 대화창 또는 스포트라이트 프리팹이 없습니다.");
            tutorialSlime.SetMovementLocked(false);
            return;
        }

        _tutorialSlime = tutorialSlime;
        _dialogueUI = Instantiate(_dialoguePrefab, _canvas.transform);
        _dialogueUI.NextRequested += OnDialogueNextRequested;
        _guideUI = Instantiate(_guidePrefab, _canvas.transform);

        SpawnManager.Instance.SetSpawningPaused(true);
        _autoClicker?.SetPaused(true);
        ShowDialogue(_introductionDialogue, ShowClickStep);
    }

    private void ShowDialogue(DialogueLine[] lines, Action onComplete)
    {
        _step = TutorialStep.Dialogue;
        _clicker.SetInputMode(false, false);
        _guideUI.Hide();

        _activeDialogue = lines;
        _dialogueIndex = 0;
        _onDialogueComplete = onComplete;

        if (_activeDialogue == null || _activeDialogue.Length == 0)
        {
            FinishDialogue();
            return;
        }

        ShowCurrentDialogueLine();
    }

    private void OnDialogueNextRequested()
    {
        if (_step != TutorialStep.Dialogue || _activeDialogue == null) return;

        _dialogueIndex++;
        if (_dialogueIndex < _activeDialogue.Length)
        {
            ShowCurrentDialogueLine();
            return;
        }

        FinishDialogue();
    }

    private void ShowCurrentDialogueLine()
    {
        DialogueLine line = _activeDialogue[_dialogueIndex];
        _dialogueUI.Show(line.Speaker, line.Message);
    }

    private void FinishDialogue()
    {
        Action onComplete = _onDialogueComplete;

        _dialogueUI.Hide();
        _activeDialogue = null;
        _dialogueIndex = 0;
        _onDialogueComplete = null;

        onComplete?.Invoke();
    }

    private void ShowClickStep()
    {
        _step = TutorialStep.Click;
        _clicker.SetInputMode(true, false, _tutorialSlime);
        _guideUI.Show(_clickMessage, _tutorialSlime.transform);
    }

    private void OnTargetClicked(SlimeController target)
    {
        if (_step != TutorialStep.Click || target != _tutorialSlime) return;

        _step = TutorialStep.Drag;
        _clicker.SetInputMode(false, true, _tutorialSlime);
        _guideUI.Show(_dragMessage, _tutorialSlime.transform);
    }

    private void OnTargetDragCompleted(SlimeController target)
    {
        if (_step != TutorialStep.Drag || target != _tutorialSlime) return;

        Complete();
    }

    private void Complete()
    {
        _step = TutorialStep.Complete;
        _guideUI.Hide();
        _tutorialSlime.SetMovementLocked(false);
        _clicker.SetInputMode(true, true);
        SpawnManager.Instance.SetSpawningPaused(false);
        _autoClicker?.SetPaused(false);
    }
}
