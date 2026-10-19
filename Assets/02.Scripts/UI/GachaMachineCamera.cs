using System;
using UnityEngine;

// 뽑기 화면의 가짜 카메라다. 기계와 캡슐 층을 감싼 MachineCamera 오브젝트에 붙어, 그 크기와 위치와
// 기울기를 바꿔 확대, 초점 이동, 흔들림을 흉내 낸다. 월드 카메라는 쓰지 않는다. 이 화면은 Overlay
// 캔버스라 카메라의 영향을 받지 않고, 월드 카메라는 DisplayRoomCameraController와 PortraitColumnCamera가 맡는다.
//
// 언제 어디를 얼마나 당길지는 연출 순서를 아는 GachaMachineView가 정한다. 여기서는 받은 목표로
// 움직이고, 흔들림을 그 위에 얹을 뿐이다. 좌표는 모두 이 오브젝트의 부모 기준이다.
[RequireComponent(typeof(RectTransform))]
public sealed class GachaMachineCamera : MonoBehaviour
{
    [Tooltip("초점이 화면 가운데로 끌려오는 정도입니다. 1이면 초점을 제자리에 둔 채 확대하고, 클수록 초점이 가운데로 옵니다.")]
    [SerializeField, Min(0f)] private float _pull = 1.35f;

    private RectTransform _rect;

    // 확대와 초점은 목표를 향해 Update가 움직이고, 흔들림은 그 위에 얹힌다.
    private float _zoom = 1f;
    private Vector2 _focus;
    private float _pullNow;
    private float _fromZoom;
    private float _toZoom;
    private Vector2 _fromFocus;
    private Vector2 _toFocus;
    private float _fromPull;
    private float _toPull;
    private float _elapsed;
    private float _duration;
    private Func<float, float> _ease;
    private bool _moving;
    private float _holdShake;
    private float _burstShake;
    private float _burstSeconds;
    private float _burstElapsed;

    private void Awake()
    {
        _rect = (RectTransform)transform;
        _pullNow = _pull;
    }

    private void Update()
    {
        float delta = Time.unscaledDeltaTime;
        if (_moving)
        {
            _elapsed += delta;
            float ratio = _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);
            float eased = _ease(ratio);
            _zoom = Mathf.LerpUnclamped(_fromZoom, _toZoom, eased);
            _focus = Vector2.LerpUnclamped(_fromFocus, _toFocus, eased);
            _pullNow = Mathf.LerpUnclamped(_fromPull, _toPull, eased);
            if (ratio >= 1f) _moving = false;
        }

        float shake = _holdShake;
        if (_burstElapsed < _burstSeconds)
        {
            _burstElapsed += delta;
            float left = 1f - Mathf.Clamp01(_burstElapsed / _burstSeconds);
            shake = Mathf.Max(shake, _burstShake * left * left);
        }

        Apply(shake);
    }

    // focus를 향해 zoom배로 확대한다. pull을 주지 않으면 인스펙터의 기본값을 쓴다.
    public void MoveTo(float zoom, Vector2 focus, float duration, Func<float, float> ease, float pull = -1f)
    {
        _fromZoom = _zoom;
        _fromFocus = _focus;
        _fromPull = _pullNow;
        _toZoom = zoom;
        _toFocus = focus;
        _toPull = pull < 0f ? _pull : pull;
        _elapsed = 0f;
        _duration = duration;
        _ease = ease;
        _moving = true;
    }

    // 끄기 전까지 계속 떨린다. 0이면 멈춘다.
    public void SetHoldShake(float amplitude)
    {
        _holdShake = amplitude;
    }

    // 한 번 크게 흔들리고 잦아든다.
    public void Burst(float amplitude, float seconds)
    {
        if (amplitude <= 0f) return;

        _burstShake = amplitude;
        _burstSeconds = seconds;
        _burstElapsed = 0f;
    }

    public void ResetView()
    {
        _moving = false;
        _zoom = 1f;
        _focus = Vector2.zero;
        _pullNow = _pull;
        _holdShake = 0f;
        _burstShake = 0f;
        _burstSeconds = 0f;
        _burstElapsed = 0f;
        Apply(0f);
    }

    // 초점이 제자리에 머물도록 확대한 뒤, _pullNow만큼 초점을 화면 가운데로 끌어온다.
    private void Apply(float shake)
    {
        Vector2 pan = -_focus * ((_zoom - 1f) * _pullNow);
        Vector2 offset = Vector2.zero;
        float roll = 0f;
        if (shake > 0f)
        {
            float time = Time.unscaledTime * 38f;
            offset = new Vector2(
                Mathf.PerlinNoise(time, 0.17f) - 0.5f,
                Mathf.PerlinNoise(0.53f, time) - 0.5f) * (2f * shake);
            roll = (Mathf.PerlinNoise(time, 0.91f) - 0.5f) * shake * 0.08f;
        }

        _rect.localScale = Vector3.one * _zoom;
        _rect.anchoredPosition = pan + offset;
        _rect.localRotation = Quaternion.Euler(0f, 0f, roll);
    }
}
