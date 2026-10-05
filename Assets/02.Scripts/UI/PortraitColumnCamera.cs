using UnityEngine;

// 월드를 그리는 카메라를 9:19.5 세로 영역 안으로 가둔다(PortraitColumn).
//
// Camera.rect를 줄이면 카메라의 가로세로비가 따라 바뀌어 세로 화면과 같은 월드가 보인다.
// 영역 밖은 이 카메라가 그리지 않으므로, 더 낮은 depth의 배경 카메라가 칠한다.
// 화면 크기가 바뀌는 순간(회전, 창 크기 변경, 에디터 해상도 변경)에 다시 맞춘다.
[RequireComponent(typeof(Camera))]
public sealed class PortraitColumnCamera : MonoBehaviour
{
    private Camera _camera;
    private Vector2Int _lastScreenSize;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        Apply();
    }

    private void OnEnable()
    {
        if (_camera != null) Apply();
    }

    private void Update()
    {
        if (_lastScreenSize.x == Screen.width && _lastScreenSize.y == Screen.height) return;

        Apply();
    }

    private void OnDisable()
    {
        if (_camera != null) _camera.rect = new Rect(0f, 0f, 1f, 1f);
    }

    private void Apply()
    {
        _lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        _camera.rect = PortraitColumn.GetViewportRect();
    }
}
