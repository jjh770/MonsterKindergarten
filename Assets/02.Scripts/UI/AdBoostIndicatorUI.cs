using TMPro;
using UnityEngine;

// 포인트 부스트가 켜져 있는 동안 남은 시간을 상단에 보여 준다. 꺼져 있으면 숨는다.
// 이 컴포넌트는 늘 켜져 있는 오브젝트에 두고, 보이고 숨기는 것은 알약(_pill)만 한다.
public sealed class AdBoostIndicatorUI : MonoBehaviour
{
    [SerializeField] private AdRewardService _adRewardService;
    [SerializeField] private GameObject _pill;
    [SerializeField] private TMP_Text _label;
    [SerializeField, Min(0.05f)] private float _refreshInterval = 0.25f;

    private float _timer;
    private int _shownSeconds = -1;

    private void Start()
    {
        if (_adRewardService == null || _pill == null || _label == null)
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
            _shownSeconds = -1;
            _pill.SetActive(false);
            return;
        }

        if (seconds == _shownSeconds && _pill.activeSelf) return;

        _shownSeconds = seconds;
        _label.text = $"포인트 {_adRewardService.Table.PointBoostMultiplier:0.#}배  {seconds / 60}:{seconds % 60:00}";
        _pill.SetActive(true);
    }
}
