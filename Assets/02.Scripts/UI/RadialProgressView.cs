using UnityEngine;

// 원형 진행도 Image의 공통 설정과 값 갱신만 담당한다.
public sealed class RadialProgressView : MonoBehaviour
{
    [SerializeField] private GameObject _track;
    [SerializeField] private UnityEngine.UI.Image _fill;

    // 트랙에는 이미 Image가 있어 같은 오브젝트에 그래픽을 더 붙일 수 없다. 실행 중에
    // AddComponent하면 null이 돌아와 링이 한 번도 그려지지 않으므로 전용 자식으로 둔다.
    [Tooltip("링을 그리는 전용 오브젝트입니다. 비어 있으면 채우기 이미지로 대신 표시합니다.")]
    [SerializeField] private RingProgressGraphic _ring;

    private float _previousProgress;

    private void Awake()
    {
        if (_track == null || _fill == null)
        {
            Debug.LogError("원형 진행도 표현의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        UnityEngine.UI.Image trackImage = _track.GetComponent<UnityEngine.UI.Image>();
        Color trackColor = trackImage != null
            ? trackImage.color
            : new Color(0.18f, 0.12f, 0.07f, 0.72f);
        Color progressColor = _fill.color;

        if (_ring == null)
        {
            Debug.LogError("링 게이지 참조가 비어 있어 채우기 이미지로 표시합니다.", this);
            _fill.enabled = true;
            _fill.raycastTarget = false;
            _fill.type = UnityEngine.UI.Image.Type.Filled;
            _fill.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
            _fill.fillOrigin = (int)UnityEngine.UI.Image.Origin360.Top;
            _fill.fillClockwise = true;
            _fill.fillAmount = 0f;
            return;
        }

        if (trackImage != null) trackImage.enabled = false;
        _fill.enabled = false;
        _ring.Configure(trackColor, progressColor);
        _ring.SetProgress(0f);
    }

    public void SetProgress(float progress01)
    {
        float nextProgress = Mathf.Clamp01(progress01);
        if (_previousProgress > 0.95f && nextProgress < 0.1f)
        {
            _ring?.PlayCompletionPulse();
        }

        _previousProgress = nextProgress;
        _ring?.SetProgress(nextProgress);
    }
}
