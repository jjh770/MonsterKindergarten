using DG.Tweening;
using UnityEngine;

public class ScaleTweeningFeedback : MonoBehaviour, IFeedback
{
    private Slime _owner;
    private Tween _scaleTween;
    private void Awake()
    {
        _owner = GetComponent<Slime>();
    }

    // 역할 : 스케일 트위닝 피드백에 대한 로직을 담당
    public void Play(ClickInfo clickInfo)
    {
        _scaleTween?.Kill();
        _owner.transform.localScale = Vector3.one;
        _scaleTween = _owner.transform.DOPunchScale(Vector3.one * 0.5f, 0.5f, 10, 1);
    }

    private void OnDisable()
    {
        // 비활성화 시 Tween 정리 (오브젝트 풀링 대응)
        _scaleTween?.Kill();
        _scaleTween = null;
    }

    private void OnDestroy()
    {
        // 파괴 시에도 안전하게 정리
        _scaleTween?.Kill();
        _scaleTween = null;
    }
}
