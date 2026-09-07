using System.Text;
using TMPro;
using UnityEngine;

// 문구 뒤의 점 개수를 순환시켜 진행 중임을 보인다.
//
// 트윈이 아니라 문자열을 바꾸는 일이라 DOTween을 쓰지 않는다.
// 재생과 정지는 OnEnable / OnDisable에 맡긴다. 로딩 오버레이가 꺼지면 함께 멈춘다.
[RequireComponent(typeof(TextMeshProUGUI))]
public sealed class LoadingDotsText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _text;
    [Tooltip("점을 붙일 앞부분입니다.")]
    [SerializeField] private string _baseText = "Loading";
    [SerializeField, Min(1)] private int _maxDots = 3;
    [SerializeField, Min(0.05f)] private float _secondsPerDot = 0.35f;
    [Tooltip("켜면 남는 자리를 공백으로 채워 글자 폭이 변하지 않습니다.")]
    [SerializeField] private bool _keepWidth = true;

    private readonly StringBuilder _builder = new StringBuilder();
    private float _elapsed;
    private int _dotCount = 1;

    private void Reset()
    {
        _text = GetComponent<TextMeshProUGUI>();
    }

    private void Awake()
    {
        if (_text == null) _text = GetComponent<TextMeshProUGUI>();
    }

    private void OnEnable()
    {
        _elapsed = 0f;
        _dotCount = 1;
        Apply();
    }

    private void Update()
    {
        // 로딩 중에는 timeScale이 어떻든 돌아야 한다.
        _elapsed += Time.unscaledDeltaTime;
        if (_elapsed < _secondsPerDot) return;

        _elapsed -= _secondsPerDot;
        _dotCount = _dotCount % _maxDots + 1;
        Apply();
    }

    private void Apply()
    {
        if (_text == null) return;

        _builder.Clear();
        _builder.Append(_baseText);
        _builder.Append('.', _dotCount);

        // 가운데 정렬이면 점이 늘 때마다 문구 전체가 흔들린다. 남는 자리를 채워 막는다.
        if (_keepWidth) _builder.Append(' ', _maxDots - _dotCount);

        _text.text = _builder.ToString();
    }
}
