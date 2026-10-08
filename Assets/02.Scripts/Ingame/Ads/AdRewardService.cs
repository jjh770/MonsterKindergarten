using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 보상형 광고를 한 번 보여 주고 결과를 알린다. 광고를 어떻게 보여 주는지는 IAdService가, 보상을 얼마나 어떻게
// 지급하는지는 보상 신호를 받는 쪽이 맡고, 여기서는 광고가 겹치지 않게 하고 보상 신호를 한 번만 내보낸다.
//
// 플랫폼 분기는 CreateAdService 한 곳에 있다. 에디터는 가짜 광고를 쓰고, 그 밖의 빌드는 AdMob을 붙일 때까지
// 광고가 없는 것으로 다룬다. 에디터의 가짜 광고가 기기 동작을 대신하지 않게 하려는 것이다.
public sealed class AdRewardService : MonoBehaviour
{
    [SerializeField] private AdRewardTableSO _table;

    private IAdService _adService;
    private bool _isRequesting;

    public AdRewardTableSO Table => _table;
    public bool IsReady => _adService != null && _adService.IsReady;
    public bool IsShowingAd => _adService != null && _adService.IsShowing;

    // 에디터에서 중도 닫기와 불러오기 실패를 흉내 낼 때 가짜 광고를 꺼내 쓴다.
    public IAdService AdService => _adService;

    public event Action<EAdPlacement> RewardEarned;
    public event Action ReadyChanged;

    private void Awake()
    {
        if (_table == null)
        {
            Debug.LogError("광고 보상 표가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _adService = CreateAdService();
    }

    private void Start()
    {
        if (!enabled) return;

        _adService.ReadyChanged += OnReadyChanged;
        _adService.Preload();
    }

    private void OnDestroy()
    {
        if (_adService != null)
        {
            _adService.ReadyChanged -= OnReadyChanged;
        }
    }

    public async UniTask<EAdShowResult> ShowAsync(EAdPlacement placement)
    {
        if (!enabled || _adService == null) return EAdShowResult.NotReady;
        if (_isRequesting) return EAdShowResult.Busy;

        _isRequesting = true;
        try
        {
            EAdShowResult result = await _adService.ShowRewardedAsync(destroyCancellationToken);
            if (result == EAdShowResult.Rewarded)
            {
                RewardEarned?.Invoke(placement);
            }

            return result;
        }
        finally
        {
            _isRequesting = false;
        }
    }

    private void OnReadyChanged() => ReadyChanged?.Invoke();

    private static IAdService CreateAdService()
    {
#if UNITY_EDITOR
        return new FakeAdService();
#else
        return new NullAdService();
#endif
    }
}
