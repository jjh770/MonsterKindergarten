using System;
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

    // 튜토리얼이 도는 동안에는 다른 전면 안내를 띄우면 안 된다. 스포트라이트와
    // 팝업이 각자 전체 화면 입력을 막아 어느 쪽도 진행할 수 없게 된다.
    // 씬 밖에서도 봐야 하므로 정적으로 노출한다.
    public static bool IsRunning { get; private set; }
    public static event Action Finished;

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
        Finished?.Invoke();
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

        // 씬이 내려갈 때 실행 상태가 남으면 다음 씬에서 안내가 영영 미뤄진다.
        // 구독자도 함께 파괴되는 시점이므로 완료 알림은 보내지 않는다.
        if (_activeSequence != null) IsRunning = false;
        _activeSequence = null;
    }
}
