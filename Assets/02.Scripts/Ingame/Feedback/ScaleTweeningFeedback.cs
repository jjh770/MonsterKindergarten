using DG.Tweening;
using UnityEngine;

public class ScaleTweeningFeedback : MonoBehaviour, IFeedback
{
    private ClickTarget _owner;

    private void Awake()
    {
        _owner = GetComponent<ClickTarget>();
    }

    // 역할 : 스케일 트위닝 피드백에 대한 로직을 담당
    public void Play(ClickInfo clickInfo)
    {
        _owner.transform.DOKill();
        _owner.transform.DOScale(1.1f, 0.15f).OnComplete(() =>
        {
            _owner.transform.DOScale(1f, 0.15f);
        });
    }
}
