using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 화면을 덮었다 걷는 커튼. 씬 전환과 데이터 로딩에 같은 물건을 쓴다.
//
// 두 곳에 다른 컴포넌트를 두면 배경색과 페이드 시간을 손으로 맞춰야 하고, 하나만
// 어긋나도 씬이 바뀌는 순간 화면이 튄다. 프리팹 하나를 양쪽 씬에 두면 그 실수가
// 아예 생기지 않고, 전환 내내 같은 화면이 유지된다.
//
// 언제 걷을지는 이 컴포넌트가 정하지 않는다. 로그인 화면은 호출부가 직접 부르고,
// GameScene은 LoadingOverlayRevealer가 데이터 완료 신호를 받아 부른다.
//
// 컴포넌트는 항상 켜져 있는 부모에 두고 _root를 자식으로 둘 것. _root 자체에 붙이면
// 걷은 뒤 오브젝트가 꺼져 다시 덮을 수 없다.
public sealed class FadeCurtainUI : MonoBehaviour
{
    [Tooltip("화면을 덮는 패널입니다.")]
    [SerializeField] private GameObject _root;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField, Min(0f)] private float _fadeDuration = 0.3f;

    // 덮인 뒤 이만큼은 유지한다.
    //
    // 에디터는 로컬 저장이라 데이터 로드가 즉시 끝나 커튼이 깜빡이고 지나간다.
    // 기기에서 Firestore를 타면 반대로 길어진다. 최소 시간을 두면 두 경우가 같아 보인다.
    [SerializeField, Min(0f)] private float _minimumCoveredSeconds = 0.8f;

    [Tooltip("씬이 시작할 때 덮인 상태로 둡니다.")]
    [SerializeField] private bool _startCovered = true;

    // 커튼 위에는 아무것도 없다. 그래서 다른 캔버스와 숫자를 맞추지 않고 천장에 고정한다.
    //
    // 순서를 런타임에 계산하는 UI가 있다. DialoguePresentation은 기준 캔버스 + 100을
    // 쓰므로, 프리팹에 적어 둔 값은 기준이나 폭이 바뀌는 순간 조용히 추월당한다.
    // 천장을 넘으려면 상대도 천장으로 와야 하고, 그건 실수가 아니라 의도다.
    private const int TopmostSortingOrder = 32767; // Canvas.sortingOrder의 상한

    private float _coveredTime;
    private bool _isFading;

    // 진행 중인 전환은 새 요청에 자리를 내준다.
    //
    // 예전에는 _isFading이면 그냥 돌아갔는데, 호출부는 await로 기다리고 있으므로
    // 덮인 줄 알고 다음 단계로 갔다. 로그인이 빠른 에디터에서는 리빌이 끝나기 전에
    // 커버 요청이 와서 화면이 덮이지 않은 채 씬이 넘어갔다.
    private int _fadeGeneration;

    public bool IsReady => _root != null && _canvasGroup != null;
    public bool IsCovered { get; private set; }

    private void Awake()
    {
        ApplyTopmostSorting();

        if (!IsReady)
        {
            Debug.LogError("커튼의 패널 또는 CanvasGroup 참조가 비어 있습니다.", this);
            return;
        }

        SetCovered(_startCovered);
    }

    // 완전히 덮인 뒤에 돌아온다. 참조가 없으면 즉시 돌아와 호출부는 그대로 진행한다.
    //
    // 걷는 중에 불리면 그 자리에서 되돌아간다. 이미 덮였고 조용할 때만 할 일이 없다.
    public async UniTask CoverAsync()
    {
        if (!IsReady) return;
        if (IsCovered && !_isFading) return;

        int generation = ++_fadeGeneration;
        _isFading = true;
        _root.SetActive(true);
        _canvasGroup.blocksRaycasts = true;

        // 다른 요청에 밀렸으면 상태는 그쪽이 정한다.
        if (!await FadeTo(1f, generation)) return;

        if (this == null) return;
        IsCovered = true;
        _coveredTime = Time.unscaledTime;
        _isFading = false;
    }

    // 최소 유지 시간을 채운 뒤 걷는다.
    //
    // 덮는 중에 불리면 그 자리에서 되돌아간다. 이미 걷혔고 조용할 때만 할 일이 없다.
    public async UniTask RevealAsync()
    {
        if (!IsReady) return;
        if (!IsCovered && !_isFading) return;

        int generation = ++_fadeGeneration;
        _isFading = true;

        float remaining = _minimumCoveredSeconds - (Time.unscaledTime - _coveredTime);
        if (remaining > 0f)
        {
            // 씬이 내려가면 취소된다. 파괴된 오브젝트를 건드리지 않는다.
            await UniTask.Delay(
                    TimeSpan.FromSeconds(remaining),
                    DelayType.UnscaledDeltaTime,
                    cancellationToken: this.GetCancellationTokenOnDestroy())
                .SuppressCancellationThrow();

            // 기다리는 동안 덮으라는 요청이 왔을 수 있다.
            if (this == null || !IsReady || _fadeGeneration != generation) return;
        }

        if (this == null || !IsReady) return;

        if (!await FadeTo(0f, generation)) return;

        if (this == null || !IsReady) return;
        _canvasGroup.blocksRaycasts = false;
        _root.SetActive(false);
        IsCovered = false;
        _isFading = false;
    }

    // 커튼의 정렬 순서는 프리팹이 아니라 이 컴포넌트가 소유한다.
    //
    // overrideSorting을 함께 켠다. 중첩 캔버스는 이 값이 꺼져 있으면 sortingOrder를
    // 무시하고 부모를 따르므로, 프리팹을 다른 Canvas 아래로 옮기는 순간 최상단
    // 보장이 소리 없이 사라진다. 루트 캔버스에서는 켜 두어도 영향이 없다.
    private void ApplyTopmostSorting()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("커튼이 속한 Canvas를 찾지 못했습니다.", this);
            return;
        }

        canvas.overrideSorting = true;
        canvas.sortingOrder = TopmostSortingOrder;
    }

    private void SetCovered(bool covered)
    {
        IsCovered = covered;
        _canvasGroup.alpha = covered ? 1f : 0f;
        _canvasGroup.blocksRaycasts = covered;
        _root.SetActive(covered);
        if (covered) _coveredTime = Time.unscaledTime;
    }

    // 일시정지 연출과 무관해야 하므로 스케일되지 않은 시간을 쓴다.
    //
    // 목표까지 남은 거리에 비례해 시간을 잡는다. 전체 페이드는 _fadeDuration 그대로고,
    // 도중에 되돌아가는 경우만 짧아진다. 거의 다 걷힌 화면을 다시 덮는 데 처음부터
    // 덮는 것과 같은 시간을 쓰면, 보이는 변화 없이 그만큼 멈춰 있게 된다.
    //
    // 자기 세대가 밀려났으면 false를 돌려준다. 호출부는 상태를 건드리지 않고 물러난다.
    private async UniTask<bool> FadeTo(float target, int generation)
    {
        float start = _canvasGroup.alpha;
        float duration = _fadeDuration * Mathf.Abs(target - start);
        if (duration <= 0f)
        {
            _canvasGroup.alpha = target;
            return true;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            // 씬이 내려가는 중이거나 새 요청에 밀렸으면 멈춘다.
            if (_canvasGroup == null || _fadeGeneration != generation) return false;

            elapsed += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(
                start, target, Mathf.Clamp01(elapsed / duration));
            await UniTask.Yield();
        }

        if (_canvasGroup == null || _fadeGeneration != generation) return false;

        _canvasGroup.alpha = target;
        return true;
    }
}
