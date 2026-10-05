using UnityEngine;

// 가로로 늘어난 패널의 폭을 세로 영역(PortraitColumn) 안으로 줄인다.
//
// 하단 패널은 화면 폭 전체로 늘어나도록 앵커가 걸려 있어서, 넓은 화면에서는 HUD 버튼과
// 어긋나게 양 끝까지 번진다. 씬에 적어 둔 sizeDelta.x를 기준으로 영역 밖만큼 좌우를 똑같이
// 덜어 낸다. 좌우가 같은 양이라 패널을 위아래로 움직이는 anchoredPosition 연출과 부딪히지 않는다.
[RequireComponent(typeof(RectTransform))]
public sealed class PortraitColumnPanelWidth : MonoBehaviour
{
    private RectTransform _rect;
    private Canvas _rootCanvas;
    private float _authoredSizeDeltaX;
    private Vector2Int _lastScreenSize;
    private float _lastScale;
    private bool _isInitialized;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _authoredSizeDeltaX = _rect.sizeDelta.x;
        Canvas canvas = GetComponentInParent<Canvas>();
        _rootCanvas = canvas != null ? canvas.rootCanvas : null;
        _isInitialized = _rootCanvas != null;

        if (!_isInitialized)
        {
            Debug.LogError("패널 폭을 맞출 캔버스를 찾지 못했습니다.", this);
            enabled = false;
            return;
        }

        Apply();
    }

    private void OnEnable()
    {
        if (_isInitialized) Apply();
    }

    private void Update()
    {
        // 캔버스 배율은 화면 크기가 바뀐 뒤 한 프레임 늦게 갱신된다. 크기만 보면 예전 배율로 한 번 맞추고
        // 끝나므로, 배율이 바뀐 것도 함께 본다.
        bool screenChanged = _lastScreenSize.x != Screen.width || _lastScreenSize.y != Screen.height;
        if (!screenChanged && Mathf.Approximately(_lastScale, _rootCanvas.scaleFactor)) return;

        Apply();
    }

    private void OnDestroy()
    {
        if (_rect != null && _isInitialized)
        {
            Vector2 size = _rect.sizeDelta;
            size.x = _authoredSizeDeltaX;
            _rect.sizeDelta = size;
        }
    }

    private void Apply()
    {
        _lastScreenSize = new Vector2Int(Screen.width, Screen.height);

        // 캔버스 단위 = 화면 픽셀 / 배율. 영역 밖 한쪽의 폭을 캔버스 단위로 바꾼다.
        float scale = _rootCanvas.scaleFactor;
        _lastScale = scale;
        float marginUnits = scale > 0f ? PortraitColumn.GetPixelRect().xMin / scale : 0f;

        Vector2 size = _rect.sizeDelta;
        size.x = _authoredSizeDeltaX - marginUnits * 2f;
        _rect.sizeDelta = size;
    }
}
