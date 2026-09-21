using UnityEngine;

// 원형 진행도 Image의 공통 설정과 값 갱신만 담당한다.
public sealed class RadialProgressView : MonoBehaviour
{
    [SerializeField] private GameObject _track;
    [SerializeField] private UnityEngine.UI.Image _fill;

    private void Awake()
    {
        if (_track == null || _fill == null)
        {
            Debug.LogError("원형 진행도 표현의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _fill.raycastTarget = false;
        _fill.type = UnityEngine.UI.Image.Type.Filled;
        _fill.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
        _fill.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
        _fill.fillClockwise = true;
        _fill.fillAmount = 0f;
    }

    public void SetProgress(float progress01)
    {
        if (_fill != null)
        {
            _fill.fillAmount = Mathf.Clamp01(progress01);
        }
    }
}
