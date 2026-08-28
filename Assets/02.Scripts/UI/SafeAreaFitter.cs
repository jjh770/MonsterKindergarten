using UnityEngine;

// 자식들을 세이프에어리어 안쪽으로 밀어 넣는 영역.
//
// 화면 가장자리에 붙는 UI마다 인셋을 따로 계산하면 일부만 반영돼 노치 기기에서
// 정렬이 어긋난다. 영역 하나가 여백을 잡고 자식은 그 안에서 평소 좌표를 쓴다.
//
// 위치를 옮기는 연출이 붙은 오브젝트에는 걸지 않는다. 늘어난 사각형에서는
// offsetMin/offsetMax와 anchoredPosition이 같은 값을 공유하므로 서로 덮어쓴다.
// HudVisibility가 움직이는 HUD 루트라면 이 컴포넌트는 그 자식에 둔다.
[RequireComponent(typeof(RectTransform))]
public sealed class SafeAreaFitter : MonoBehaviour
{
    // 화면 전체를 덮는 기준 사각형. 비워 두면 부모에서 찾는다.
    [SerializeField] private RectTransform _referenceRect;

    private RectTransform _rectTransform;
    private bool _isInitialized;

    private void Awake()
    {
        _rectTransform = transform as RectTransform;
        if (_rectTransform == null) return;

        if (_referenceRect == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            _referenceRect = canvas != null
                ? canvas.rootCanvas.transform as RectTransform
                : null;
        }

        if (_referenceRect == null)
        {
            Debug.LogError("세이프에어리어 기준 사각형을 찾지 못했습니다.", this);
            enabled = false;
            return;
        }

        _isInitialized = true;
        Apply();
    }

    private void OnEnable()
    {
        if (_isInitialized) Apply();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (_isInitialized) Apply();
    }

    // 기기에 따라 앱을 다시 열 때 세이프에어리어가 달라질 수 있다.
    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && _isInitialized) Apply();
    }

    private void OnApplicationPause(bool isPaused)
    {
        if (!isPaused && _isInitialized) Apply();
    }

    private void Apply()
    {
        SafeAreaInsets insets = SafeAreaUtility.GetInsets(_referenceRect);
        _rectTransform.offsetMin = new Vector2(insets.Left, insets.Bottom);
        _rectTransform.offsetMax = new Vector2(-insets.Right, -insets.Top);
    }

}
