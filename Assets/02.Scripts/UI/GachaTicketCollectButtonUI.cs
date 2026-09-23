using UnityEngine;
using UnityEngine.UI;

// 도감 12종 달성 뒤 열리는 일괄 회수 버튼이다. 노출 조건은 GachaHudVisibility가,
// 실제 티켓 상태와 회수 순서는 GachaTicketField가 맡고 이 컴포넌트는 둘을 연결한다.
public sealed class GachaTicketCollectButtonUI : MonoBehaviour
{
    [SerializeField] private GachaTicketField _ticketField;
    [SerializeField] private Button _button;

    public RectTransform ButtonTarget => _button != null
        ? _button.transform as RectTransform
        : null;

    private void Awake()
    {
        if (_ticketField == null || _button == null)
        {
            Debug.LogError("티켓 회수 버튼의 필수 참조가 비어 있습니다.", this);
            enabled = false;
        }
    }

    private void Start()
    {
        if (!enabled) return;

        _button.onClick.AddListener(OnClick);
        _ticketField.CollectionStateChanged += Refresh;
        Refresh();
    }

    private void OnEnable()
    {
        if (enabled)
        {
            Refresh();
        }
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnClick);
        }

        if (_ticketField != null)
        {
            _ticketField.CollectionStateChanged -= Refresh;
        }
    }

    private void OnClick()
    {
        _ticketField.TryCollectAll();
        Refresh();
    }

    private void Refresh()
    {
        if (_button == null || _ticketField == null) return;

        _button.interactable = _ticketField.CanCollectAll;
    }
}
