using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 가챠권을 쓰는 버튼 하나만 담당한다. 실행 순서는 GachaService가 정한다.
//
// 언제 보일지는 GachaHudVisibility가 정한다. 여기서는 개수 표기와 클릭만 다룬다.
//
// 티켓이 없어도 버튼을 흐리게 두지 않는다. 흐린 버튼은 왜 못 쓰는지 알려주지 않고,
// 가챠권이라는 것이 있다는 사실 자체가 아직 낯선 시점이라 안내가 필요하다.
//
// PullSucceeded는 뽑은 순간이 아니라 연출이 끝난 뒤에 발화한다. 튜토리얼이 이 신호를
// 받아 결과 슬라임을 가리키는데, 연출 도중에는 그 슬라임이 숨겨져 있기 때문이다.
public sealed class GachaButtonUI : MonoBehaviour
{
    private const string NoTicketMessage =
        "슬라임이 떨어뜨리는 가챠권을 모아보세요.";

    private const string NoRoomMessage =
        "유치원이 가득 찼어요.\n슬라임을 합쳐 자리를 만들어 주세요.";

    [SerializeField] private Button _button;
    [SerializeField] private TMP_Text _countLabel;
    [SerializeField] private ToastMessageUI _toast;

    [Tooltip("비워 두면 연출 없이 결과가 바로 필드에 나타납니다.")]
    [SerializeField] private GachaResultDirector _resultDirector;

    // 스포트라이트가 버튼을 가리킬 때 필요하다.
    public RectTransform ButtonTarget => _button != null
        ? _button.transform as RectTransform
        : null;
    public event Action<SlimeController> PullSucceeded;

    private void Awake()
    {
        if (_button == null || _countLabel == null || _toast == null)
        {
            Debug.LogError("가챠 버튼의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _button.onClick.AddListener(OnButtonClicked);
        GameManager.OnAllDataInitialized += Refresh;
        Refresh();
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnButtonClicked);
        }

        GameManager.OnAllDataInitialized -= Refresh;

        if (CurrencyManager.Instance != null)
        {
            CurrencyManager.Instance.OnDataChanged -= OnCurrencyChanged;
        }
    }

    private void OnCurrencyChanged(ECurrencyType type, Currency amount)
    {
        if (type != ECurrencyType.GachaTicket) return;

        Refresh();
    }

    private void OnButtonClicked()
    {
        EGachaFailure failure = GachaService.TryPull(out SlimeController spawned);
        Refresh();

        if (failure == EGachaFailure.None && spawned != null)
        {
            if (_resultDirector != null)
            {
                _resultDirector.Play(spawned, () => PullSucceeded?.Invoke(spawned));
            }
            else
            {
                PullSucceeded?.Invoke(spawned);
            }
        }

        switch (failure)
        {
            case EGachaFailure.NoTicket:
                _toast.Show(NoTicketMessage);
                break;
            case EGachaFailure.NoRoom:
                _toast.Show(NoRoomMessage);
                break;
        }
    }

    private void Refresh()
    {
        TrySubscribeCurrency();

        if (CurrencyManager.Instance == null) return;

        double count = (double)CurrencyManager.Instance.Get(ECurrencyType.GachaTicket);
        _countLabel.text = count.ToString("0");
    }

    // 재화 이벤트는 인스턴스 이벤트라 매니저가 준비된 뒤에야 붙일 수 있다.
    // 중복 구독을 막기 위해 붙이기 전에 뗀다. 같은 대상이어도 여러 번 붙고
    // 그만큼 여러 번 불린다.
    private void TrySubscribeCurrency()
    {
        if (CurrencyManager.Instance == null) return;

        CurrencyManager.Instance.OnDataChanged -= OnCurrencyChanged;
        CurrencyManager.Instance.OnDataChanged += OnCurrencyChanged;
    }
}
