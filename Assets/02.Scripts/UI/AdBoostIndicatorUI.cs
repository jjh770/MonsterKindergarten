using DG.Tweening;
using TMPro;
using UnityEngine;

// 포인트 부스트가 켜져 있는 동안 남은 시간을 상단에 보여 준다. 꺼져 있으면 숨는다.
// 이 컴포넌트는 늘 켜져 있는 오브젝트에 두고, 보이고 숨기는 것은 알약(_pill)만 한다.
public sealed class AdBoostIndicatorUI : MonoBehaviour
{
    [SerializeField] private AdRewardService _adRewardService;
    [SerializeField] private GameObject _pill;
    [Tooltip("알약이 나타나고 사라지는 연출입니다. 공용 팝업 연출을 쓰며 알약 자신을 움직입니다.")]
    [SerializeField] private PopupMotion _pillMotion;
    [SerializeField] private TMP_Text _label;
    [SerializeField, Min(0.05f)] private float _refreshInterval = 0.25f;

    private float _timer;
    private int _shownSeconds = -1;
    private bool _isHiding;

    private void Start()
    {
        if (_adRewardService == null || _pill == null || _pillMotion == null || _label == null)
        {
            Debug.LogError("부스트 표시의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _pill.SetActive(false);
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
        int seconds = Mathf.CeilToInt(_adRewardService.GetPointBoostRemainingSeconds());
        if (seconds <= 0)
        {
            Hide();
            return;
        }

        if (seconds == _shownSeconds && _pill.activeSelf && !_isHiding) return;

        _shownSeconds = seconds;
        _label.text = $"포인트 {_adRewardService.Table.PointBoostMultiplier:0.#}배 적용  {seconds / 60}:{seconds % 60:00}";
        Show();
    }

    // 켜져 있으면 문구만 갱신하고, 꺼져 있거나 사라지는 중이면 다시 나타낸다.
    private void Show()
    {
        if (_pill.activeSelf && !_isHiding) return;

        _isHiding = false;
        _pill.SetActive(true);
        _pillMotion.PlayOpen();
    }

    // 연출이 끝난 뒤에 끈다. 사라지는 동안 시간이 다시 생기면 Show가 이어받는다.
    private void Hide()
    {
        _shownSeconds = -1;
        if (!_pill.activeSelf || _isHiding) return;

        _isHiding = true;
        _pillMotion.PlayClose().OnComplete(() =>
        {
            if (!_isHiding) return;

            _isHiding = false;
            _pill.SetActive(false);
        });
    }
}
