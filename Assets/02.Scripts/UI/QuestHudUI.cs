using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 상단 바 아래 오른쪽에 지금 퀘스트를 작게 보여 주고, 누르면 자세한 팝업(QuestPopupUI)을 연다.
// 이 컴포넌트는 늘 켜져 있는 오브젝트에 두고, 보이고 숨기는 것은 카드(_card)만 한다.
//
// 어떤 퀘스트인지는 저장된 값으로 매번 다시 계산한다(QuestProgress). 등급, 도감, 장식장, 탐험이 바뀌는
// 길이 여러 갈래라 신호를 하나라도 놓치면 지난 퀘스트가 남는다. 그래서 부스트 표시처럼 짧은 간격으로
// 확인하고, 바뀐 때만 글자를 고친다.
//
// 메인 튜토리얼을 마친 뒤 메인 필드에서만 보이고, 졸업식을 본 뒤에는 숨는다. 상단 HUD 안에 있어서
// 보내기·관찰 모드가 상단 바를 숨길 때 함께 숨는다. 자리는 고정이다. 광고 부스트 표시는 왼쪽 포인트
// 아래에 있어 겹치지 않는다.
public sealed class QuestHudUI : MonoBehaviour
{
    [SerializeField] private QuestTableSO _table;
    [SerializeField] private SlimeManager _slimeManager;
    [SerializeField] private GameplaySpaceManager _spaceManager;
    [SerializeField] private QuestPopupUI _popup;
    [SerializeField] private RectTransform _card;
    [SerializeField] private Button _cardButton;
    [SerializeField] private TMP_Text _headerText;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField, Min(0.05f)] private float _refreshInterval = 0.25f;

    private float _timer;
    private int _shownIndex = -1;
    private string _shownTitle;
    private Tween _punchTween;

    private void Awake()
    {
        if (_table == null || _slimeManager == null || _spaceManager == null || _popup == null ||
            _card == null || _cardButton == null || _headerText == null || _titleText == null)
        {
            Debug.LogError("퀘스트 HUD의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _card.gameObject.SetActive(false);
        _cardButton.onClick.AddListener(OnCardClicked);
    }

    private void OnDestroy()
    {
        _punchTween?.Kill();
        if (_cardButton != null) _cardButton.onClick.RemoveListener(OnCardClicked);
    }

    private void Update()
    {
        _timer += Time.unscaledDeltaTime;
        if (_timer < _refreshInterval) return;

        _timer = 0f;
        Refresh();
    }

    private void Refresh()
    {
        int index = CanShow()
            ? QuestProgress.FindCurrent(_table.Quests, QuestState.From(_slimeManager))
            : -1;
        if (index < 0)
        {
            _card.gameObject.SetActive(false);
            return;
        }

        QuestEntry quest = _table.Quests[index];
        string title = QuestPopupUI.FormatTitle(quest, QuestState.From(_slimeManager));
        bool wasShown = _card.gameObject.activeSelf;
        _card.gameObject.SetActive(true);
        if (index == _shownIndex && title == _shownTitle) return;

        // 앞 퀘스트를 끝내고 다음으로 넘어갈 때만 카드를 튕긴다. 처음 나타날 때와 진행도만 바뀔 때는 조용하다.
        bool advanced = wasShown && _shownIndex >= 0 && index != _shownIndex;
        _shownIndex = index;
        _shownTitle = title;
        _headerText.text = _table.FormatHeader(index);
        _titleText.text = title;
        if (advanced) PlayAdvance();
    }

    private bool CanShow()
    {
        return _slimeManager.IsInitialized &&
               !_slimeManager.IsMainEndingSeen &&
               TutorialProgress.IsCompleted(TutorialIds.Main) &&
               _spaceManager.IsMainFieldActive;
    }

    private void PlayAdvance()
    {
        _punchTween?.Kill(complete: true);
        _card.localScale = Vector3.one;
        _punchTween = _card.DOPunchScale(Vector3.one * 0.15f, 0.35f, 6, 0.5f)
            .SetUpdate(true)
            .OnComplete(() => _punchTween = null);
    }

    private void OnCardClicked()
    {
        if (_shownIndex < 0) return;

        _popup.Open(_shownIndex);
    }
}
