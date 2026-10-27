using System.Text;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

// 퀘스트 카드를 누르면 열리는 팝업. 무엇을 하면 되는지와 끝냈을 때 열리거나 받는 것을 보여 준다.
// 도감 효과 팝업(CollectionBonusUI)과 같은 판과 같은 방식으로 열고 닫는다. 바깥을 누르거나 뒤로 가기,
// 닫기 버튼으로 닫히고, 열린 동안은 필드 입력을 막는다.
public sealed class QuestPopupUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private QuestTableSO _table;
    [SerializeField] private SlimeManager _slimeManager;
    [SerializeField] private GameObject _popupRoot;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private PopupMotion _panelMotion;
    [SerializeField] private Button _closeButton;
    [SerializeField] private TMP_Text _headerText;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _bodyText;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private GameExitManager _gameExitManager;
    [SerializeField, Min(0f)] private float _fadeDuration = 0.15f;

    private const string ReachedColor = "#4F8A3B";

    private Tween _fadeTween;
    private bool _isOpen;
    private bool _isClosing;

    private void Start()
    {
        if (_table == null || _slimeManager == null || _popupRoot == null || _panel == null ||
            _canvasGroup == null || _closeButton == null || _headerText == null || _titleText == null ||
            _bodyText == null || _clicker == null || _gameExitManager == null)
        {
            Debug.LogError("퀘스트 팝업의 필수 씬 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _popupRoot.SetActive(false);
        _closeButton.onClick.AddListener(Close);
    }

    private void OnDestroy()
    {
        _fadeTween?.Kill();
        _closeButton?.onClick.RemoveListener(Close);
        _clicker?.ReleaseMode(this);
        _gameExitManager?.UnregisterBackHandler(this);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_isOpen || _isClosing) return;
        if (RectTransformUtility.RectangleContainsScreenPoint(
                _panel,
                eventData.position,
                eventData.pressEventCamera))
        {
            return;
        }

        TryClose();
    }

    public void Open(int index)
    {
        if (!enabled || _isOpen || _isClosing) return;
        if (index < 0 || index >= _table.Quests.Count) return;

        _isOpen = true;
        transform.SetAsLastSibling();
        _popupRoot.SetActive(true);
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;
        Fill(index);
        _clicker.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Modal);
        _gameExitManager.RegisterBackHandler(this, TryClose);

        _fadeTween?.Kill();
        _fadeTween = _canvasGroup
            .DOFade(1f, _fadeDuration)
            .SetUpdate(true)
            .OnComplete(() => _fadeTween = null);
        _panelMotion?.PlayOpen();
    }

    private void Close()
    {
        TryClose();
    }

    private bool TryClose()
    {
        if (!_isOpen) return false;
        if (_isClosing) return true;

        _isClosing = true;
        _canvasGroup.interactable = false;
        _gameExitManager.UnregisterBackHandler(this);
        _clicker.ReleaseMode(this);

        _fadeTween?.Kill();
        _panelMotion?.PlayClose();
        _fadeTween = _canvasGroup
            .DOFade(0f, _fadeDuration)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                _fadeTween = null;
                _popupRoot.SetActive(false);
                _isOpen = false;
                _isClosing = false;
            });
        return true;
    }

    private void Fill(int index)
    {
        QuestEntry quest = _table.Quests[index];
        QuestState state = QuestState.From(_slimeManager);
        _headerText.text = _table.FormatHeader(index);
        _titleText.text = FormatTitle(quest, state);

        var builder = new StringBuilder(quest.Description);
        builder.Append("\n\n<b>보상</b>");
        foreach (QuestReward reward in quest.Rewards)
        {
            if (reward == null) continue;

            builder.Append("\n<color=").Append(ReachedColor).Append('>')
                .Append(GetRewardLabel(reward.Kind))
                .Append("</color>");
            if (!string.IsNullOrEmpty(reward.Detail))
            {
                builder.Append("\n<size=88%>").Append(reward.Detail).Append("</size>");
            }
        }

        _bodyText.text = builder.ToString();
    }

    // HUD와 팝업이 같은 제목을 쓴다. 하나씩 채우는 퀘스트는 뒤에 진행도를 붙인다.
    public static string FormatTitle(QuestEntry quest, QuestState state)
    {
        if (!QuestProgress.ShowsProgress(quest.Goal)) return quest.Title;

        int current = Mathf.Min(QuestProgress.GetCurrentValue(quest.Goal, state), quest.Target);
        return $"{quest.Title} ({current}/{quest.Target})";
    }

    private static string GetRewardLabel(EQuestRewardKind kind) => kind switch
    {
        EQuestRewardKind.NewFeature => "새 기능 열림",
        EQuestRewardKind.PointBonus => "포인트 획득량 증가 (보너스)",
        EQuestRewardKind.GachaTickets => "뽑기권",
        EQuestRewardKind.ExpeditionGift => "탐험 보상",
        EQuestRewardKind.Graduation => "졸업식",
        _ => string.Empty,
    };
}
