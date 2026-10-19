using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 하나만 뽑았을 때 화면 가운데에서 결과 슬라임이 솟고, 이름과 함께 드러나고, 필드로 날아가 도착하는
// 모습을 맡는다. 언제 무엇을 보일지는 GachaResultDirector가 정하고, 여기서는 받은 대로 움직인다.
//
// 감독과 같은 오브젝트에 붙는다. 그림들은 닫힐 때 꺼지는 연출 화면 아래에 있지만 이 컴포넌트는 꺼지지
// 않아서, 화면을 열기 전에 비워 둘 수 있다. 좌표는 모두 결과 그림의 부모(연출 화면) 기준이다.
public sealed class GachaSingleResult : MonoBehaviour
{
    // 파편이 퍼지는 모양이다. 거리는 캔버스 좌표 단위이고, 끝 거리는 파편마다
    // EndSpread씩 EndSpreadSteps 칸으로 엇갈려 한 줄로 늘어서지 않게 한다.
    private struct BurstShape
    {
        public float AngleJitter;
        public float StartDistance;
        public float EndDistance;
        public float EndSpread;
        public int EndSpreadSteps;
        public float StartScale;
        public float EndScale;
    }

    private static readonly BurstShape ArrivalBurstShape = new()
    {
        AngleJitter = 0.16f,
        StartDistance = 18f,
        EndDistance = 170f,
        EndSpread = 18f,
        EndSpreadSteps = 4,
        StartScale = 1.3f,
        EndScale = 0.2f,
    };

    [SerializeField] private Image _resultImage;
    [SerializeField] private SlimeOutlineImage _resultOutline;
    [SerializeField] private GachaResultAura _resultAura;
    [SerializeField] private TMP_Text _resultNameText;
    [SerializeField] private RectTransform _arrivalEffectRoot;
    [SerializeField] private RectTransform[] _arrivalSparks;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float _emergeDuration = 0.45f;
    [SerializeField, Min(0f)] private float _revealDuration = 0.6f;
    [SerializeField, Min(0f)] private float _arrivalDuration = 0.45f;

    [Header("Look")]
    [SerializeField, Min(0f)] private float _emergeScale = 1.35f;
    [SerializeField, Min(0f)] private float _revealScale = 1f;
    [SerializeField, Min(0f)] private float _endScale = 0.25f;
    [SerializeField, Min(0f)] private float _nameSlideDistance = 180f;
    // 결과 슬라임이 솟아오르는 세로 이동 거리. 캔버스 좌표(anchoredPosition) 단위다.
    [SerializeField, Min(0f)] private float _emergeRise = 80f;

    private RectTransform _resultRect;
    private RectTransform _resultNameRect;
    private Vector2 _resultNameRestPosition;
    private bool _isSpecial;
    // 결과 그림이 다 드러난 뒤에만 무지개 광택을 입힌다. 드러나는 중에는 실루엣 색이 먼저다.
    private bool _isShimmering;
    private bool _isReady;

    // 결과 그림의 부모다. 연출 화면의 좌표는 모두 이것 기준이다.
    public RectTransform OverlayRect =>
        _resultImage != null ? _resultImage.rectTransform.parent as RectTransform : null;

    // 필드에 도착할 때의 그림 크기다. 여러 개를 뽑았을 때 자리의 그림도 이 크기에 맞춰 도착한다.
    public float ArrivalWidth => _resultImage.rectTransform.rect.width * _endScale;

    private void Awake()
    {
        if (_resultImage == null || _resultOutline == null || _resultAura == null ||
            _resultNameText == null || _arrivalEffectRoot == null || _arrivalSparks == null ||
            OverlayRect == null)
        {
            Debug.LogError("뽑기 1회 결과 연출의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _resultRect = _resultImage.rectTransform;
        _resultNameRect = _resultNameText.rectTransform;
        _resultNameRestPosition = _resultNameRect.anchoredPosition;
        _isReady = true;
    }

    private void Update()
    {
        if (_isShimmering) _resultImage.color = RainbowTint.Shimmer();
    }

    // 지난번 연출의 흔적이 남지 않게 감춘다.
    public void ResetAll()
    {
        if (!_isReady) return;

        _resultAura.Hide();
        _resultOutline.Apply(null, false, false);
        _resultImage.enabled = false;
        _resultNameText.alpha = 0f;
        _resultNameText.gameObject.SetActive(true);
        _arrivalEffectRoot.gameObject.SetActive(false);
        _isShimmering = false;
    }

    public void StopShimmer()
    {
        _isShimmering = false;
    }

    // center 아래에 작게 숨겨 두고 이름을 옆으로 비켜 둔다. 캡슐이 열리면 Emerge로 솟는다.
    public void Prepare(Vector2 center, string name, bool isSpecial)
    {
        if (!_isReady) return;

        _isSpecial = isSpecial;
        _isShimmering = false;
        _resultRect.anchoredPosition = center + Vector2.down * _emergeRise;
        _resultRect.localScale = Vector3.one * 0.2f;
        _resultImage.enabled = false;
        _resultImage.color = Color.white;

        _resultNameText.text = name;
        _resultNameText.alpha = 0f;
        _resultNameText.gameObject.SetActive(true);
        _resultNameRect.anchoredPosition =
            _resultNameRestPosition + Vector2.right * _nameSlideDistance;

        _arrivalEffectRoot.gameObject.SetActive(false);
        _arrivalEffectRoot.localScale = Vector3.one;
        HideSparks();
    }

    // 슬라임이 커지며 솟는다. onFrame은 매 프레임 솟기 시작부터 흐른 시간을 받는다. 그동안 기계의 빛을
    // 이어 가게 하는 데 쓴다.
    public async UniTask<bool> Emerge(
        Sprite sprite,
        EGachaRarity rarity,
        Action<float> onFrame,
        CancellationToken token)
    {
        _resultAura.Show(rarity);
        _resultOutline.Apply(sprite, sprite != null, _isSpecial);
        _resultImage.enabled = sprite != null;
        _resultImage.color = Color.white;
        Vector2 start = _resultRect.anchoredPosition;
        Vector2 end = start + Vector2.up * _emergeRise;
        float elapsed = 0f;

        while (elapsed < _emergeDuration)
        {
            if (await NextFrame(token)) return true;
            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.SmoothStep(0f, 1f, Normalized(elapsed, _emergeDuration));
            _resultRect.anchoredPosition = Vector2.Lerp(start, end, ratio);
            _resultRect.localScale = Vector3.one * Mathf.Lerp(0.2f, _emergeScale, ratio);
            onFrame?.Invoke(elapsed);
        }

        return false;
    }

    // 반짝이며 제 크기로 돌아오고 이름이 미끄러져 들어온다. onFrame은 Emerge에 이어 흐른 시간을 받는다.
    public async UniTask<bool> Reveal(Action<float> onFrame, CancellationToken token)
    {
        float elapsed = 0f;
        while (elapsed < _revealDuration)
        {
            if (await NextFrame(token)) return true;
            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.SmoothStep(0f, 1f, Normalized(elapsed, _revealDuration));
            float sparkle = Mathf.Sin(ratio * Mathf.PI * 3f) * (1f - ratio) * 0.08f;
            float nameRatio = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.12f, 0.72f, ratio));
            _resultRect.localScale = Vector3.one *
                                     (Mathf.Lerp(_emergeScale, _revealScale, ratio) + sparkle);
            _resultNameText.alpha = nameRatio;
            _resultNameRect.anchoredPosition = Vector2.Lerp(
                _resultNameRestPosition + Vector2.right * _nameSlideDistance,
                _resultNameRestPosition,
                nameRatio);
            onFrame?.Invoke(_emergeDuration + elapsed);
        }

        _resultImage.color = Color.white;
        _isShimmering = _isSpecial;
        _resultRect.localScale = Vector3.one * _revealScale;
        _resultNameText.alpha = 1f;
        _resultNameRect.anchoredPosition = _resultNameRestPosition;
        return false;
    }

    public void PlayTrail(Color color)
    {
        _resultAura.PlayTrail(color, _isSpecial);
    }

    // destination(연출 화면 좌표)으로 날아가는 동안 이름은 반대로 미끄러지며 사라진다.
    public async UniTask<bool> MoveTo(Vector2 destination, GachaResultFlight flight, CancellationToken token)
    {
        Vector2 start = _resultRect.anchoredPosition;
        float startScale = _resultRect.localScale.x;
        float elapsed = 0f;
        while (elapsed < flight.Duration)
        {
            if (await NextFrame(token)) return true;
            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.SmoothStep(0f, 1f, Normalized(elapsed, flight.Duration));
            flight.Apply(_resultRect, start, destination, startScale, _endScale, ratio);
            _resultNameText.alpha = 1f - Mathf.Clamp01(ratio * 2.5f);
            _resultNameRect.anchoredPosition =
                _resultNameRestPosition + Vector2.left * _nameSlideDistance * ratio;
        }

        _resultRect.anchoredPosition = destination;
        _resultRect.localScale = Vector3.one * _endScale;
        _resultRect.localRotation = Quaternion.identity;
        _resultNameText.alpha = 0f;
        return false;
    }

    // 도착한 자리에서 빛과 파편이 터지고 그림이 한 번 튄다.
    public async UniTask<bool> Arrive(Vector2 destination, Color color, CancellationToken token)
    {
        _resultAura.PlayBurst(color, _isSpecial);

        // 도착 이펙트 루트는 결과 이미지와 같은 부모를 공유하므로 로컬 좌표를 그대로 쓴다.
        _arrivalEffectRoot.anchoredPosition = destination;
        _arrivalEffectRoot.localScale = Vector3.one;
        _arrivalEffectRoot.gameObject.SetActive(true);
        HideSparks();
        float elapsed = 0f;

        while (elapsed < _arrivalDuration)
        {
            if (await NextFrame(token)) return true;
            elapsed += Time.unscaledDeltaTime;
            float ratio = Normalized(elapsed, _arrivalDuration);
            AnimateSparkBurst(_arrivalSparks, ArrivalBurstShape, ratio, Tint(color));
            _resultRect.localScale = Vector3.one *
                                     (_endScale * (1f + Mathf.Sin(ratio * Mathf.PI) * GachaResultFlight.ArrivalPulse));
        }

        _resultRect.localScale = Vector3.one * _endScale;
        _arrivalEffectRoot.gameObject.SetActive(false);
        return false;
    }

    // 특별한 결과는 정해진 색 대신 색상환을 도는 색을 쓴다.
    private Color Tint(Color color)
    {
        return _isSpecial ? RainbowTint.Pure() : color;
    }

    // 바깥으로 퍼지며 작아지고 옅어지는 파편이다. 거리, 크기, 엇갈림은 BurstShape이 든다.
    private static void AnimateSparkBurst(
        RectTransform[] sparks,
        in BurstShape shape,
        float ratio,
        Color color)
    {
        int count = sparks.Length;
        for (int i = 0; i < count; i++)
        {
            RectTransform spark = sparks[i];
            if (spark == null) continue;

            float angle = Mathf.PI * 2f * i / Mathf.Max(1, count) +
                          (i % 2) * shape.AngleJitter;
            float endDistance = shape.EndDistance + i % shape.EndSpreadSteps * shape.EndSpread;
            SetRadial(spark, angle, Mathf.Lerp(shape.StartDistance, endDistance, ratio));
            spark.localScale = Vector3.one * Mathf.Lerp(shape.StartScale, shape.EndScale, ratio);
            SetImageColor(spark.GetComponent<Image>(), color, 1f - ratio);
        }
    }

    // 중심에서 angle(라디안) 방향으로 distance만큼 떨어진 자리에 놓고 바깥을 향해 세운다.
    // 스프라이트가 위쪽을 보고 그려져 있어서 90도를 뺀다.
    private static void SetRadial(RectTransform spark, float angle, float distance)
    {
        spark.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        spark.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg - 90f);
    }

    private void HideSparks()
    {
        foreach (RectTransform spark in _arrivalSparks)
        {
            if (spark == null) continue;
            spark.anchoredPosition = Vector2.zero;
            spark.localScale = Vector3.zero;
            SetImageAlpha(spark.GetComponent<Image>(), 0f);
        }
    }

    private static void SetImageColor(Image image, Color color, float alpha)
    {
        if (image != null)
            image.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null) return;
        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }

    private static float Normalized(float elapsed, float duration) =>
        duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);

    private static async UniTask<bool> NextFrame(CancellationToken token) =>
        await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
}
