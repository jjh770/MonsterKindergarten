using UnityEngine;
using UnityEngine.UI;

// UI 이미지의 스프라이트를 일정 간격으로 바꿔 애니메이션처럼 보이게 한다.
//
// 슬라임 본체는 Animator와 오버라이드 컨트롤러를 쓰지만, 로딩 화면의 이미지 한 장을
// 위해 컨트롤러를 만들 필요는 없다.
//
// 재생과 정지를 OnEnable / OnDisable에 맞춰 두면, 로딩 오버레이가 꺼질 때 알아서
// 멈춘다. 부모를 끄는 쪽에서 따로 챙길 것이 없다.
[RequireComponent(typeof(Image))]
public sealed class SpriteSequenceUI : MonoBehaviour
{
    [SerializeField] private Image _image;
    [Tooltip("순서대로 반복 재생합니다.")]
    [SerializeField] private Sprite[] _frames;
    [SerializeField, Min(0.01f)] private float _secondsPerFrame = 0.12f;

    private float _elapsed;
    private int _index;

    private void Reset()
    {
        _image = GetComponent<Image>();
    }

    private void Awake()
    {
        if (_image == null) _image = GetComponent<Image>();
    }

    private void OnEnable()
    {
        _elapsed = 0f;
        _index = 0;
        Apply();
    }

    private void Update()
    {
        if (_frames == null || _frames.Length <= 1) return;

        // 로딩 중에는 timeScale이 어떻든 돌아야 한다.
        _elapsed += Time.unscaledDeltaTime;
        if (_elapsed < _secondsPerFrame) return;

        _elapsed -= _secondsPerFrame;
        _index = (_index + 1) % _frames.Length;
        Apply();
    }

    private void Apply()
    {
        if (_image == null || _frames == null || _frames.Length == 0) return;

        Sprite frame = _frames[_index];
        if (frame != null) _image.sprite = frame;
    }
}
