using UnityEngine;
using UnityEngine.UI;

// 넓은 화면에서 캔버스의 배율이 세로 영역에 맞게 정해지도록 한다(PortraitColumn).
//
// 기준 해상도 1080x2340은 9:19.5라서 높이에 맞추면 영역의 폭이 정확히 1080 단위가 된다.
// 평소처럼 가로와 세로를 반반 섞으면 넓은 화면에서 배율이 가로 기준으로 커져 UI가 영역 밖으로 번진다.
// 영역을 적용하지 않는 화면에서는 씬에 적어 둔 값을 그대로 쓴다.
[RequireComponent(typeof(CanvasScaler))]
public sealed class PortraitColumnCanvasScaler : MonoBehaviour
{
    private CanvasScaler _scaler;
    private float _authoredMatch;
    private Vector2Int _lastScreenSize;

    private void Awake()
    {
        _scaler = GetComponent<CanvasScaler>();
        _authoredMatch = _scaler.matchWidthOrHeight;
        Apply();
    }

    private void Update()
    {
        if (_lastScreenSize.x == Screen.width && _lastScreenSize.y == Screen.height) return;

        Apply();
    }

    private void OnDestroy()
    {
        if (_scaler != null) _scaler.matchWidthOrHeight = _authoredMatch;
    }

    private void Apply()
    {
        _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        _scaler.matchWidthOrHeight = PortraitColumn.IsActive ? 1f : _authoredMatch;
    }
}
