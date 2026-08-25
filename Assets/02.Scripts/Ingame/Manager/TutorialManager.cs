using UnityEngine;

// 튜토리얼 실행권과 공용 프레젠테이션만 소유한다.
// 각 발동 조건과 단계 진행은 TutorialSequenceBase 구현체가 담당한다.
public sealed class TutorialManager : MonoBehaviour
{
    [SerializeField] private Canvas _canvas;
    [SerializeField] private GameObject _dialoguePresentationPrefab;
    [SerializeField] private TutorialContent _content;
    [SerializeField] private UpgradeUI _upgradeUI;

    private DialoguePresentation _presentation;
    private TutorialSequenceBase _activeSequence;

    public TutorialContent Content => _content;
    public DialoguePresentation Presentation => _presentation;

    // 튜토리얼이 입력을 소유하는 동안 다른 시스템이 기본 입력으로 되돌리지 못하게 한다.
    // StageManager.RefreshInteraction이 이 값을 존중한다.
    public static bool IsRunning { get; private set; }

    public bool TryBegin(TutorialSequenceBase sequence)
    {
        if (sequence == null ||
            (_activeSequence != null && _activeSequence != sequence))
        {
            return false;
        }

        if (!EnsurePresentation()) return false;

        _activeSequence = sequence;
        IsRunning = true;
        return true;
    }

    public void Complete(TutorialSequenceBase sequence)
    {
        if (_activeSequence != sequence) return;

        _presentation?.Spotlight.Hide();
        _activeSequence = null;
        IsRunning = false;
    }

    private bool EnsurePresentation()
    {
        if (_presentation != null) return true;

        if (_canvas == null || _dialoguePresentationPrefab == null ||
            _content == null)
        {
            Debug.LogError("튜토리얼 Canvas, 프리팹 또는 콘텐츠 에셋이 없습니다.", this);
            return false;
        }

        Canvas sortingReference = _upgradeUI != null
            ? _upgradeUI.GetComponentInParent<Canvas>()
            : _canvas;
        _presentation = new DialoguePresentation(
            _canvas,
            sortingReference,
            _dialoguePresentationPrefab);
        return true;
    }

    private void OnDestroy()
    {
        _presentation?.Dispose();
        _presentation = null;
        _activeSequence = null;
        IsRunning = false;
    }
}
