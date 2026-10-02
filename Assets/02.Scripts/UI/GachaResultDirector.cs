using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 이미 생성되고 저장된 가챠 결과를 포털로 공개한 뒤 필드로 넘긴다.
// 포털 색은 뽑을 때 확정된 가중치 희귀도를 표현할 뿐, 터치 시 결과를 다시 뽑지 않는다.
public sealed class GachaResultDirector : MonoBehaviour
{
    private const string PortalTapMessage = "포탈을 터치하세요";

    // 흰색은 UI Image에서 스프라이트 원본 RGB를 그대로 보여준다.
    // 결과 색은 포탈을 누른 뒤 Charge 단계부터 적용한다.
    private static readonly Color InitialColor = Color.white;
    private static readonly Color CommonColor = new(0.35f, 1f, 0.72f, 1f);
    private static readonly Color UncommonColor = new(0.32f, 0.78f, 1f, 1f);
    private static readonly Color RareColor = new(0.67f, 0.42f, 1f, 1f);
    private static readonly Color JackpotColor = new(1f, 0.76f, 0.22f, 1f);

    private const string SpecialSubtitle = "뭔가 특별해 보여요...!";
    // 특별한 결과는 터지는 순간의 빛과 충격파를 더 세게 준다.
    private const float SpecialBurstBoost = 1.35f;

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

    private static readonly BurstShape PortalBurstShape = new()
    {
        AngleJitter = 0.12f,
        StartDistance = 25f,
        EndDistance = 330f,
        EndSpread = 22f,
        EndSpreadSteps = 5,
        StartScale = 1.35f,
        EndScale = 0.25f,
    };

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

    [SerializeField] private GameObject _root;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private GameObject _reelViewport;
    [SerializeField] private Image _resultImage;
    [SerializeField] private Clicker _clicker;
    [SerializeField] private SlimeManager _slimeManager;

    [Header("Portal")]
    [SerializeField] private RectTransform _portalRoot;
    [SerializeField] private Image[] _portalRings;
    [SerializeField] private Image _portalCore;
    [SerializeField] private RectTransform[] _orbitSparks;
    [SerializeField] private RectTransform[] _burstSparks;
    [SerializeField] private Image _shockwave;
    [SerializeField] private Image _flashImage;
    [SerializeField] private Button _portalButton;
    [SerializeField] private TMP_Text _tapPrompt;
    [SerializeField] private TMP_Text _resultNameText;
    [SerializeField] private RectTransform _arrivalEffectRoot;
    [SerializeField] private Image _arrivalShockwave;
    [SerializeField] private RectTransform[] _arrivalSparks;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float _fadeDuration = 0.25f;
    [SerializeField, Min(0f)] private float _chargeDuration = 0.35f;
    [SerializeField, Min(0f)] private float _collapseDuration = 0.2f;
    [SerializeField, Min(0f)] private float _burstDuration = 0.22f;
    [SerializeField, Min(0f)] private float _emergeDuration = 0.45f;
    [SerializeField, Min(0f)] private float _revealDuration = 0.6f;
    [SerializeField, Min(0f)] private float _holdDuration = 0.7f;
    [SerializeField, Min(0.01f)] private float _moveDuration = 0.5f;
    [SerializeField, Min(0f)] private float _arrivalDuration = 0.45f;

    [Header("Audio")]
    [SerializeField, Min(0f)] private float _waitSfxFadeOutDuration = 0.75f;

    [Header("Look")]
    [SerializeField] private Color _silhouetteColor = Color.black;
    [SerializeField, Min(0f)] private float _emergeScale = 1.35f;
    [SerializeField, Min(0f)] private float _revealScale = 1f;
    [SerializeField, Min(0f)] private float _endScale = 0.25f;
    [SerializeField, Min(0f)] private float _nameSlideDistance = 180f;
    [SerializeField, Min(0f)] private float _moveArcRadius = 160f;
    [SerializeField, Min(0f)] private float _moveSpinTurns = 1f;

    // 결과 슬라임이 솟아오르는 세로 이동 거리. 캔버스 좌표(anchoredPosition) 단위다.
    [SerializeField, Min(0f)] private float _emergeRise = 80f;

    private RectTransform _resultRect;
    private RectTransform _resultParentRect;
    private RectTransform _resultNameRect;
    private Vector2 _resultNameRestPosition;
    private bool _isReady;
    private bool _isPlaying;
    private bool _portalTapped;
    private bool _isSpecialResult;
    // 결과 그림이 다 드러난 뒤에만 무지개 광택을 입힌다. 드러나는 중에는 실루엣 색이 먼저다.
    private bool _isResultImageShimmering;

    public bool IsPlaying => _isPlaying;

    private void Awake()
    {
        if (_root == null || _canvasGroup == null || _resultImage == null ||
            _portalRoot == null || _portalRings == null ||
            _portalRings.Length == 0 || _portalCore == null ||
            _orbitSparks == null || _burstSparks == null ||
            _shockwave == null || _flashImage == null ||
            _portalButton == null || _tapPrompt == null ||
            _resultNameText == null || _arrivalEffectRoot == null ||
            _arrivalShockwave == null || _arrivalSparks == null ||
            _slimeManager == null)
        {
            Debug.LogError("가챠 포털 연출의 필수 참조가 비어 있습니다.", this);
            return;
        }

        _resultRect = _resultImage.transform as RectTransform;
        _resultParentRect = _resultRect != null
            ? _resultRect.parent as RectTransform
            : null;
        _resultNameRect = _resultNameText.transform as RectTransform;
        _isReady = _resultRect != null && _resultParentRect != null && _resultNameRect != null;
        if (_resultNameRect != null)
        {
            _resultNameRestPosition = _resultNameRect.anchoredPosition;
        }
        _portalButton.onClick.AddListener(OnPortalTapped);
        _root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_portalButton != null) _portalButton.onClick.RemoveListener(OnPortalTapped);
        AudioManager.Instance?.StopLoopingSFX();
        _clicker?.ReleaseMode(this);
    }

    public void Play(
        SlimeController target,
        EGachaRarity rarity,
        Action onCompleted)
    {
        if (!_isReady || _isPlaying || target == null)
        {
            onCompleted?.Invoke();
            return;
        }

        PlayAsync(target, rarity, onCompleted).Forget();
    }

    private async UniTaskVoid PlayAsync(
        SlimeController target,
        EGachaRarity rarity,
        Action onCompleted)
    {
        _isPlaying = true;
        CancellationToken token = this.GetCancellationTokenOnDestroy();
        target.SetLocationPresentationActive(false);
        _clicker?.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Modal);
        bool isCancelled = false;
        try
        {
            isCancelled = await Present(
                target,
                rarity,
                token);
        }
        finally
        {
            AudioManager.Instance?.StopLoopingSFX();
            _clicker?.ReleaseMode(this);
            _isResultImageShimmering = false;
            _isPlaying = false;
        }

        if (isCancelled) return;
        target.SetLocationPresentationActive(true);
        onCompleted?.Invoke();
    }

    private async UniTask<bool> Present(
        SlimeController target,
        EGachaRarity rarity,
        CancellationToken token)
    {
        // 포털·결과물은 결과 이미지의 부모 캔버스 로컬 좌표로만 움직인다. 화면 픽셀을
        // 그대로 position에 넣으면 CanvasScaler가 켜진 해상도에서 좌표계가 어긋난다.
        _isSpecialResult = rarity == EGachaRarity.Special;
        _isResultImageShimmering = false;
        Vector2 center = GetLocalCenter();
        PreparePortal(center, target.Grade);
        _root.SetActive(true);
        AudioManager.Instance?.PlayLoopingSFX(EAudioSfx.GachaWait);

        if (await Fade(0f, 1f, _fadeDuration, token)) return true;
        if (await WaitForTap(token)) return true;

        Color resultColor = GetPortalColor(rarity);
        if (await Charge(resultColor, token)) return true;
        if (await Collapse(resultColor, token)) return true;
        if (await Burst(resultColor, token)) return true;
        if (await Emerge(GetSprite(target.Grade), resultColor, token)) return true;
        if (await Reveal(resultColor, token)) return true;
        if (await Wait(_holdDuration, token)) return true;

        Vector2 destination = GetFieldLocalPosition(target, center);
        AudioManager.Instance?.PlaySFX(EAudioSfx.GachaResult);
        if (await MoveTo(destination, token)) return true;
        if (await PlayArrivalEffect(destination, resultColor, token)) return true;
        if (await Fade(1f, 0f, _fadeDuration, token)) return true;

        CloseOverlay();
        return false;
    }

    private void PreparePortal(Vector2 centerLocal, ESlimeGrade grade)
    {
        _portalTapped = false;
        _canvasGroup.alpha = 0f;
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;
        _reelViewport?.SetActive(false);
        _portalRoot.anchoredPosition = Vector2.zero;
        _portalRoot.localScale = Vector3.one;
        _portalRoot.gameObject.SetActive(true);
        _portalButton.interactable = true;
        _tapPrompt.gameObject.SetActive(true);
        _tapPrompt.text = PortalTapMessage;
        _tapPrompt.alpha = 1f;

        _resultRect.anchoredPosition = centerLocal + Vector2.down * _emergeRise;
        _resultRect.localScale = Vector3.one * 0.2f;
        _resultImage.enabled = false;
        _resultImage.color = _silhouetteColor;

        _resultNameText.text = _isSpecialResult
            ? GetName(grade) + "\n<size=65%>" + SpecialSubtitle + "</size>"
            : GetName(grade);
        _resultNameText.alpha = 0f;
        _resultNameText.gameObject.SetActive(true);
        _resultNameRect.anchoredPosition =
            _resultNameRestPosition + Vector2.right * _nameSlideDistance;

        _arrivalEffectRoot.gameObject.SetActive(false);
        _arrivalEffectRoot.localScale = Vector3.one;
        SetImageAlpha(_arrivalShockwave, 0f);
        HideSparks(_arrivalSparks);

        SetPortalColor(InitialColor, 1f);
        SetImageAlpha(_shockwave, 0f);
        _shockwave.rectTransform.localScale = Vector3.one * 0.35f;
        SetImageAlpha(_flashImage, 0f);
        HideSparks(_burstSparks);
    }

    private async UniTask<bool> WaitForTap(CancellationToken token)
    {
        float elapsed = 0f;
        while (!_portalTapped)
        {
            if (await NextFrame(token)) return true;
            elapsed += Time.unscaledDeltaTime;
            Idle(elapsed);
        }

        _portalButton.interactable = false;
        _tapPrompt.gameObject.SetActive(false);
        return false;
    }

    private void Idle(float elapsed)
    {
        _portalRoot.localScale = Vector3.one *
                                 (1f + Mathf.Sin(elapsed * 2.4f) * 0.035f);
        RotateRings(elapsed * 26f);
        SetImageColor(_portalCore, InitialColor,
            0.55f + Mathf.Sin(elapsed * 3.2f) * 0.12f);
        _tapPrompt.alpha = 0.68f + Mathf.Sin(elapsed * 3f) * 0.22f;
        AnimateOrbit(elapsed, InitialColor, 1f);
    }

    private async UniTask<bool> Charge(Color color, CancellationToken token)
    {
        float elapsed = 0f;
        while (elapsed < _chargeDuration)
        {
            if (await NextFrame(token)) return true;
            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.SmoothStep(0f, 1f, Normalized(elapsed, _chargeDuration));
            Color current = Color.Lerp(InitialColor, Tint(color), ratio);
            SetPortalColor(current, 1f);
            RotateRings(elapsed * Mathf.Lerp(90f, 360f, ratio));
            AnimateOrbit(elapsed * 2.2f, current, Mathf.Lerp(1f, 1.45f, ratio));
            _portalRoot.localScale = Vector3.one * Mathf.Lerp(1f, 1.12f, ratio);
        }
        return false;
    }

    private async UniTask<bool> Collapse(Color color, CancellationToken token)
    {
        Vector3 startScale = _portalRoot.localScale;
        float elapsed = 0f;
        while (elapsed < _collapseDuration)
        {
            if (await NextFrame(token)) return true;
            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.SmoothStep(0f, 1f, Normalized(elapsed, _collapseDuration));
            _portalRoot.localScale = Vector3.Lerp(startScale, Vector3.one * 0.58f, ratio);
            RotateRings(360f + elapsed * 720f);
            AnimateOrbit(elapsed * 3f, Tint(color), Mathf.Lerp(1.45f, 0.45f, ratio));
        }
        return false;
    }

    private async UniTask<bool> Burst(Color color, CancellationToken token)
    {
        float elapsed = 0f;
        Vector2 portalStart = _portalRoot.anchoredPosition;
        while (elapsed < _burstDuration)
        {
            if (await NextFrame(token)) return true;
            elapsed += Time.unscaledDeltaTime;
            float ratio = Normalized(elapsed, _burstDuration);
            float inverse = 1f - ratio;
            _portalRoot.localScale = Vector3.one * Mathf.Lerp(0.58f, 1.28f, ratio);
            _portalRoot.anchoredPosition = portalStart + new Vector2(
                Mathf.Sin(elapsed * 135f) * 11f * inverse,
                Mathf.Cos(elapsed * 117f) * 8f * inverse);
            SetImageColor(_flashImage, Tint(color), inverse * 0.82f * BurstBoost);
            SetImageColor(_shockwave, Tint(color), inverse * 0.9f * BurstBoost);
            _shockwave.rectTransform.localScale =
                Vector3.one * Mathf.Lerp(0.35f, 2.15f, ratio);
            AnimateBurst(ratio, Tint(color));
        }

        _portalRoot.anchoredPosition = portalStart;
        SetImageAlpha(_flashImage, 0f);
        SetImageAlpha(_shockwave, 0f);
        return false;
    }

    private async UniTask<bool> Emerge(
        Sprite resultSprite,
        Color resultColor,
        CancellationToken token)
    {
        AudioManager.Instance?.PlaySFX(EAudioSfx.FeatureUnlock);
        _resultImage.sprite = resultSprite;
        _resultImage.enabled = resultSprite != null;
        _resultImage.color = _silhouetteColor;
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
            _portalRoot.localScale = Vector3.one * Mathf.Lerp(1.28f, 0.72f, ratio);
            SetPortalAlpha(1f - ratio * 0.45f);
            RotateRings(720f + elapsed * 250f);
            AnimateSustainedBurst(elapsed, Tint(resultColor));
        }
        return false;
    }

    private async UniTask<bool> Reveal(Color resultColor, CancellationToken token)
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
            float portalCollapse = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.68f, 1f, ratio));
            _resultImage.color = Color.Lerp(_silhouetteColor, Color.white, ratio);
            _resultRect.localScale = Vector3.one *
                                     (Mathf.Lerp(_emergeScale, _revealScale, ratio) + sparkle);
            _resultNameText.alpha = nameRatio;
            _resultNameRect.anchoredPosition = Vector2.Lerp(
                _resultNameRestPosition + Vector2.right * _nameSlideDistance,
                _resultNameRestPosition,
                nameRatio);
            _portalRoot.localScale = Vector3.one * Mathf.Lerp(0.72f, 0f, portalCollapse);
            SetPortalAlpha(0.55f * (1f - portalCollapse));
            RotateRings(970f + elapsed * 210f);
            AnimateSustainedBurst(_emergeDuration + elapsed, Tint(resultColor));
        }

        _resultImage.color = Color.white;
        _isResultImageShimmering = _isSpecialResult;
        _resultRect.localScale = Vector3.one * _revealScale;
        _resultNameText.alpha = 1f;
        _resultNameRect.anchoredPosition = _resultNameRestPosition;
        _portalRoot.gameObject.SetActive(false);
        HideSparks(_burstSparks);
        return false;
    }

    private void RotateRings(float angle)
    {
        for (int i = 0; i < _portalRings.Length; i++)
        {
            Image ring = _portalRings[i];
            if (ring == null) continue;
            float direction = i % 2 == 0 ? 1f : -1f;
            ring.rectTransform.localRotation = Quaternion.Euler(
                0f, 0f, angle * direction * (1f + i * 0.24f));
        }
    }

    private void AnimateOrbit(float elapsed, Color color, float intensity)
    {
        int count = _orbitSparks.Length;
        for (int i = 0; i < count; i++)
        {
            RectTransform spark = _orbitSparks[i];
            if (spark == null) continue;
            float angle = elapsed * (0.75f + i % 3 * 0.14f) +
                          Mathf.PI * 2f * i / Mathf.Max(1, count);
            float radius = (205f + i % 4 * 18f) * intensity;
            spark.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            spark.localScale = Vector3.one *
                               ((0.7f + Mathf.Sin(elapsed * 4f + i) * 0.25f) * intensity);
            SetImageColor(spark.GetComponent<Image>(), color,
                Mathf.Clamp01(0.75f * intensity));
        }
    }

    private void AnimateBurst(float ratio, Color color)
    {
        AnimateSparkBurst(_burstSparks, PortalBurstShape, ratio, color);
    }

    // 바깥으로 퍼지며 작아지고 옅어지는 파편이다. 포털이 터질 때와 도착할 때가 같은 수식을
    // 쓰고 거리, 크기, 엇갈림만 다르다. 그 차이는 BurstShape이 든다.
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

    // 슬라임이 모습을 드러내는 동안 한 번 터지고 끝나지 않도록 각 파티클의
    // 진행도를 엇갈려 반복한다. 포털이 접히기 직전까지 계속 새 빛이 나온다.
    private void AnimateSustainedBurst(float elapsed, Color color)
    {
        int count = _burstSparks.Length;
        for (int i = 0; i < count; i++)
        {
            RectTransform spark = _burstSparks[i];
            if (spark == null) continue;

            float phase = Mathf.Repeat(elapsed * 1.7f + (float)i / Mathf.Max(1, count), 1f);
            float angle = Mathf.PI * 2f * i / Mathf.Max(1, count) + elapsed * 0.45f;
            float distance = Mathf.Lerp(45f, 350f + i % 4 * 22f, phase);
            float alpha = Mathf.Sin(phase * Mathf.PI);
            SetRadial(spark, angle, distance);
            spark.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.2f, alpha);
            SetImageColor(spark.GetComponent<Image>(), color, alpha * 0.9f);
        }
    }

    private void SetPortalColor(Color color, float alpha)
    {
        for (int i = 0; i < _portalRings.Length; i++)
            SetImageColor(_portalRings[i], color, alpha * (0.75f + i * 0.06f));
        SetImageColor(_portalCore, color, alpha * 0.62f);
        foreach (RectTransform spark in _orbitSparks)
            if (spark != null) SetImageColor(spark.GetComponent<Image>(), color, alpha);
    }

    private void SetPortalAlpha(float alpha)
    {
        foreach (Image ring in _portalRings) SetImageAlpha(ring, alpha);
        SetImageAlpha(_portalCore, alpha * 0.65f);
        foreach (RectTransform spark in _orbitSparks)
            if (spark != null) SetImageAlpha(spark.GetComponent<Image>(), alpha);
    }

    private void HideSparks(RectTransform[] sparks)
    {
        foreach (RectTransform spark in sparks)
        {
            if (spark == null) continue;
            spark.anchoredPosition = Vector2.zero;
            spark.localScale = Vector3.zero;
            SetImageAlpha(spark.GetComponent<Image>(), 0f);
        }
    }

    private void AnimateArrivalSparks(float ratio, Color color)
    {
        AnimateSparkBurst(_arrivalSparks, ArrivalBurstShape, ratio, color);
    }

    private void OnPortalTapped()
    {
        if (!_isPlaying || _portalTapped) return;

        _portalTapped = true;
        AudioManager.Instance?.StopLoopingSFX(_waitSfxFadeOutDuration);
    }

    private float BurstBoost => _isSpecialResult ? SpecialBurstBoost : 1f;

    // 특별한 결과는 정해진 색 대신 색상환을 도는 색을 쓴다. 나머지는 그대로 돌려준다.
    private Color Tint(Color color)
    {
        if (!_isSpecialResult) return color;

        return RainbowTint.Pure();
    }

    private void Update()
    {
        if (!_isResultImageShimmering) return;

        _resultImage.color = RainbowTint.Shimmer();
    }

    private static Color GetPortalColor(EGachaRarity rarity)
    {
        return rarity switch
        {
            // 색은 Tint가 프레임마다 무지개로 덮는다. 여기서는 시작 색만 정한다.
            EGachaRarity.Special => Color.white,
            EGachaRarity.Jackpot => JackpotColor,
            EGachaRarity.Rare => RareColor,
            EGachaRarity.Uncommon => UncommonColor,
            _ => CommonColor
        };
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

    private Sprite GetSprite(ESlimeGrade grade)
    {
        Slime slime = _slimeManager.Get(grade);
        return slime?.SpecData?.Sprite;
    }

    private string GetName(ESlimeGrade grade)
    {
        Slime slime = _slimeManager.Get(grade);
        return slime?.SpecData?.Name ?? grade.ToString();
    }

    // 부모 rect의 중앙 로컬 좌표. pivot이 가운데가 아니어도 맞도록 rect.center를 쓴다.
    private Vector2 GetLocalCenter()
    {
        return _resultParentRect != null ? _resultParentRect.rect.center : Vector2.zero;
    }

    // 슬라임의 월드 위치를 화면 좌표로 옮긴 뒤 결과 이미지 부모의 로컬 좌표로 되돌린다.
    // 정상 파일들이 쓰는 표준 경로(WorldToScreenPoint -> ScreenPointToLocalPointInRectangle)다.
    private Vector2 GetFieldLocalPosition(SlimeController target, Vector2 fallbackLocal)
    {
        Camera camera = Camera.main;
        if (camera == null || _resultParentRect == null) return fallbackLocal;

        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
            camera, target.transform.position);

        // 오버레이 캔버스면 카메라를 넘기지 않는다. 카메라 캔버스면 그 캔버스 카메라를 쓴다.
        Camera uiCamera = ResolveUiCamera();
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _resultParentRect, screenPoint, uiCamera, out Vector2 local)
            ? local
            : fallbackLocal;
    }

    private Camera ResolveUiCamera()
    {
        Canvas canvas = _resultParentRect != null
            ? _resultParentRect.GetComponentInParent<Canvas>()
            : null;
        if (canvas == null) return null;

        return canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;
    }

    // destination은 결과 이미지 부모의 로컬 좌표다. 시작점도 anchoredPosition을 써서
    // 두 끝점이 같은 좌표계 위에 놓이게 한다.
    private async UniTask<bool> MoveTo(Vector2 destination, CancellationToken token)
    {
        Vector2 start = _resultRect.anchoredPosition;
        Vector2 path = destination - start;
        Vector2 forward = path.sqrMagnitude > 0.01f ? path.normalized : Vector2.up;
        Vector2 perpendicular = new(-forward.y, forward.x);
        float startScale = _resultRect.localScale.x;
        float elapsed = 0f;
        while (elapsed < _moveDuration)
        {
            if (await NextFrame(token)) return true;
            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.SmoothStep(0f, 1f, Normalized(elapsed, _moveDuration));
            float angle = ratio * Mathf.PI * 2f;
            float envelope = Mathf.Sin(ratio * Mathf.PI);
            Vector2 orbit =
                (perpendicular * Mathf.Sin(angle) +
                 forward * (1f - Mathf.Cos(angle)) * 0.45f) *
                (_moveArcRadius * envelope);
            _resultRect.anchoredPosition = Vector2.Lerp(start, destination, ratio) + orbit;
            _resultRect.localScale = Vector3.one * Mathf.Lerp(startScale, _endScale, ratio);
            _resultRect.localRotation = Quaternion.Euler(
                0f,
                0f,
                -360f * _moveSpinTurns * ratio);
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

    private async UniTask<bool> PlayArrivalEffect(
        Vector2 destination,
        Color color,
        CancellationToken token)
    {
        // 도착 이펙트 루트는 결과 이미지와 같은 부모를 공유하므로 로컬 좌표를 그대로 쓴다.
        _arrivalEffectRoot.anchoredPosition = destination;
        _arrivalEffectRoot.localScale = Vector3.one;
        _arrivalEffectRoot.gameObject.SetActive(true);
        HideSparks(_arrivalSparks);
        float elapsed = 0f;

        while (elapsed < _arrivalDuration)
        {
            if (await NextFrame(token)) return true;
            elapsed += Time.unscaledDeltaTime;
            float ratio = Normalized(elapsed, _arrivalDuration);
            float inverse = 1f - ratio;
            _arrivalShockwave.rectTransform.localScale =
                Vector3.one * Mathf.Lerp(0.35f, 2.1f, ratio);
            SetImageColor(_arrivalShockwave, Tint(color), inverse * 0.85f);
            AnimateArrivalSparks(ratio, Tint(color));
            _resultRect.localScale = Vector3.one *
                                     (_endScale * (1f + Mathf.Sin(ratio * Mathf.PI) * 0.28f));
        }

        _resultRect.localScale = Vector3.one * _endScale;
        _arrivalEffectRoot.gameObject.SetActive(false);
        return false;
    }

    private async UniTask<bool> Fade(float from, float to, float duration, CancellationToken token)
    {
        if (duration <= 0f)
        {
            _canvasGroup.alpha = to;
            return false;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (await NextFrame(token)) return true;
            elapsed += Time.unscaledDeltaTime;
            _canvasGroup.alpha = Mathf.Lerp(from, to, Normalized(elapsed, duration));
        }
        _canvasGroup.alpha = to;
        return false;
    }

    private static async UniTask<bool> Wait(float seconds, CancellationToken token) =>
        await UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.UnscaledDeltaTime,
            cancellationToken: token).SuppressCancellationThrow();

    private void CloseOverlay()
    {
        _portalButton.interactable = false;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
        _root.SetActive(false);
    }

    private static async UniTask<bool> NextFrame(CancellationToken token) =>
        await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
}
