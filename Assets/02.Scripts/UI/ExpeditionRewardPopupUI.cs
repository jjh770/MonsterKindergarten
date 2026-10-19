using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 탐험 보상을 받은 뒤 무엇을 얻었는지 보여 주는 팝업이다. 보상은 열기 전에 이미 지급되어 있고,
// 이 팝업은 보여 주기만 한다. 언제 열고 닫은 뒤 무엇을 할지는 ExpeditionUI가 정한다.
//
// 탐험 화면의 캔버스(ExpeditionOverlay, 50) 안에 둔다. 공용 팝업 캔버스(10)는 그 아래라 가려진다.
public sealed class ExpeditionRewardPopupUI : MonoBehaviour
{
    [Serializable]
    private sealed class DecorationIcon
    {
        public EPlaygroundObjectType Type;
        public Sprite Sprite;
    }

    [SerializeField] private GameObject _blocker;
    [SerializeField] private GameObject _panel;
    [SerializeField] private PopupMotion _motion;
    [SerializeField] private TextMeshProUGUI _titleText;
    [Header("Rows")]
    [SerializeField] private RectTransform _pointRow;
    [SerializeField] private RectTransform _pointIcon;
    [SerializeField] private TextMeshProUGUI _pointText;
    [SerializeField] private RectTransform _ticketRow;
    [SerializeField] private RectTransform _ticketIcon;
    [SerializeField] private TextMeshProUGUI _ticketText;
    [SerializeField] private RectTransform _decorationRow;
    [SerializeField] private Image _decorationImage;
    [SerializeField] private TextMeshProUGUI _decorationText;
    [SerializeField] private Button _confirmButton;
    [Tooltip("탐험에서 나오는 장식마다 팝업에 그릴 그림입니다.")]
    [SerializeField] private DecorationIcon[] _decorationIcons = Array.Empty<DecorationIcon>();

    [Header("Reveal")]
    [SerializeField, Min(0f)] private float _revealStartDelay = 0.2f;
    [SerializeField, Min(0f)] private float _revealInterval = 0.18f;
    [SerializeField, Min(0.05f)] private float _revealDuration = 0.35f;

    private Sequence _sequence;
    private Action<Vector3, Vector3> _confirmed;
    private bool _isReady;

    // 열려 있으면 탐험 화면이 닫히지 않게 막고, 뒤로 가기는 받기와 같게 처리한다.
    public bool IsVisible => _panel != null && _panel.activeSelf;

    private void Awake()
    {
        if (_blocker == null || _panel == null || _motion == null || _titleText == null ||
            _pointRow == null || _pointIcon == null || _pointText == null ||
            _ticketRow == null || _ticketIcon == null || _ticketText == null ||
            _decorationRow == null || _decorationImage == null || _decorationText == null ||
            _confirmButton == null)
        {
            Debug.LogError("탐험 보상 팝업의 필수 참조가 비어 있어요.", this);
            enabled = false;
            return;
        }

        _confirmButton.onClick.AddListener(Confirm);
        HideImmediate();
        _isReady = true;
    }

    private void OnDestroy()
    {
        _sequence?.Kill();
        if (_confirmButton != null) _confirmButton.onClick.RemoveListener(Confirm);
    }

    // confirmed는 팝업이 다 닫힌 뒤 포인트와 뽑기권 아이콘이 있던 화면 위치를 받는다. 보상이 날아갈 출발점이다.
    // 팝업을 쓸 수 없으면 바로 부른다. 보상은 이미 지급됐으므로 연출을 못 보여 줘도 막을 이유가 없다.
    public void Show(string destinationName, ExpeditionReward reward, bool alreadyOwned,
        Action<Vector3, Vector3> confirmed)
    {
        if (!_isReady || reward == null)
        {
            confirmed?.Invoke(transform.position, transform.position);
            return;
        }

        _sequence?.Kill();
        _confirmed = confirmed;
        _titleText.text = string.Format(UiMessages.ExpeditionReturned, destinationName);
        _pointText.text = $"+{new Currency(reward.Points)}";
        _ticketText.text = $"+{reward.GachaTickets}장";
        _ticketRow.gameObject.SetActive(reward.GachaTickets > 0);

        bool hasDecoration = reward.Decoration.HasValue;
        _decorationRow.gameObject.SetActive(hasDecoration);
        if (hasDecoration)
        {
            EPlaygroundObjectType type = reward.Decoration.Value;
            _decorationImage.sprite = FindDecorationSprite(type);
            _decorationImage.enabled = _decorationImage.sprite != null;
            _decorationText.text = string.Format(
                alreadyOwned ? UiMessages.ExpeditionDecorationOwned : UiMessages.ExpeditionDecorationFound,
                DecorationName(type));
        }

        _blocker.SetActive(true);
        _panel.SetActive(true);
        _confirmButton.interactable = true;
        PlaySound(EAudioSfx.OfflineRewardOpen);

        // 줄마다 작은 크기에서 튀어 나온다. 처음 얻은 장식은 마지막에 한 번 더 들썩이며 효과음을 낸다.
        _sequence = DOTween.Sequence().SetUpdate(true);
        _sequence.Join(_motion.PlayOpen());
        float time = _revealStartDelay;
        foreach (RectTransform row in new[] { _pointRow, _ticketRow, _decorationRow })
        {
            if (!row.gameObject.activeSelf) continue;

            row.localScale = Vector3.zero;
            _sequence.Insert(time, row.DOScale(Vector3.one, _revealDuration).SetEase(Ease.OutBack));
            _sequence.InsertCallback(time, () => PlaySound(EAudioSfx.SlimeBounce));
            time += _revealInterval;
        }

        if (hasDecoration && !alreadyOwned)
        {
            _sequence.InsertCallback(time, () => PlaySound(EAudioSfx.ShopPurchase));
            _sequence.Insert(time, _decorationImage.rectTransform.DOPunchScale(Vector3.one * .3f, .45f, 6, .5f));
        }

        _sequence.OnComplete(() => _sequence = null);
    }

    public void Confirm()
    {
        if (!IsVisible || !_confirmButton.interactable) return;

        _confirmButton.interactable = false;
        _sequence?.Kill();
        PlaySound(EAudioSfx.OfflineRewardCollect);

        // 줄이 아직 튀어 나오는 중이면 다 보이게 한다. 출발점은 닫히기 전의 아이콘 자리다.
        foreach (RectTransform row in new[] { _pointRow, _ticketRow, _decorationRow }) row.localScale = Vector3.one;
        Vector3 pointFrom = _pointIcon.position;
        Vector3 ticketFrom = _ticketIcon.position;

        Action<Vector3, Vector3> confirmed = _confirmed;
        _confirmed = null;
        _sequence = DOTween.Sequence().SetUpdate(true);
        _sequence.Append(_motion.PlayClose());
        _sequence.OnComplete(() =>
        {
            _sequence = null;
            HideImmediate();
            confirmed?.Invoke(pointFrom, ticketFrom);
        });
    }

    // 화면이 강제로 닫힐 때 쓴다. 보상은 이미 지급돼 있어 날아가는 연출만 건너뛴다.
    public void HideImmediate()
    {
        _sequence?.Kill();
        _sequence = null;
        _confirmed = null;
        if (_panel != null) _panel.SetActive(false);
        if (_blocker != null) _blocker.SetActive(false);
    }

    private Sprite FindDecorationSprite(EPlaygroundObjectType type)
    {
        foreach (DecorationIcon icon in _decorationIcons)
        {
            if (icon != null && icon.Type == type) return icon.Sprite;
        }

        return null;
    }

    public static string DecorationName(EPlaygroundObjectType type) => type switch
    {
        EPlaygroundObjectType.FlowerPatch => "꽃밭",
        EPlaygroundObjectType.FlowerCushion => "꽃 방석",
        EPlaygroundObjectType.FlowerBasket => "꽃바구니",
        EPlaygroundObjectType.FireflyLantern => "반딧불 랜턴",
        _ => "장식",
    };

    private static void PlaySound(EAudioSfx cue) => AudioManager.Instance?.PlaySFX(cue);
}
