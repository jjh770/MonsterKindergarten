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
    private const string SkyMessage = "새 친구가 하늘로 올라갔어요!";
    private const string GroundMessage = "새 친구가 땅으로 내려갔어요!";
    private const string PortalTapMessage = "포탈을 터치하세요";

    // 흰색은 UI Image에서 스프라이트 원본 RGB를 그대로 보여준다.
    // 결과 색은 포탈을 누른 뒤 Charge 단계부터 적용한다.
    private static readonly Color InitialColor = Color.white;
    private static readonly Color CommonColor = new(0.35f, 1f, 0.72f, 1f);
    private static readonly Color UncommonColor = new(0.32f, 0.78f, 1f, 1f);
    private static readonly Color RareColor = new(0.67f, 0.42f, 1f, 1f);
    private static readonly Color JackpotColor = new(1f, 0.76f, 0.22f, 1f);

    [SerializeField] private GameObject _root;
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private GameObject _reelViewport;
    [SerializeField] private Image _resultImage;
    [SerializeField] private ToastMessageUI _toast;
    [SerializeField] private Clicker _clicker;

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

    [Header("Look")]
    [SerializeField] private Color _silhouetteColor = Color.black;
    [SerializeField, Min(0f)] private float _emergeScale = 1.35f;
    [SerializeField, Min(0f)] private float _revealScale = 1f;
    [SerializeField, Min(0f)] private float _endScale = 0.25f;
    [SerializeField, Min(0f)] private float _nameSlideDistance = 180f;
    [SerializeField, Min(0f)] private float _moveArcRadius = 160f;
    [SerializeField, Min(0f)] private float _moveSpinTurns = 1f;
    [SerializeField, Min(0f)] private float _offScreenMargin = 300f;

    private RectTransform _resultRect;
    private RectTransform _resultNameRect;
    private Vector2 _resultNameRestPosition;
    private bool _isReady;
    private bool _isPlaying;
    private bool _portalTapped;

    public bool IsPlaying => _isPlaying;

    private void Awake()
    {
        if (_root == null || _canvasGroup == null || _resultImage == null ||
            _toast == null || _portalRoot == null || _portalRings == null ||
            _portalRings.Length == 0 || _portalCore == null ||
            _orbitSparks == null || _burstSparks == null ||
            _shockwave == null || _flashImage == null ||
            _portalButton == null || _tapPrompt == null ||
            _resultNameText == null || _arrivalEffectRoot == null ||
            _arrivalShockwave == null || _arrivalSparks == null)
        {
            Debug.LogError("가챠 포털 연출의 필수 참조가 비어 있습니다.", this);
            return;
        }

        _resultRect = _resultImage.transform as RectTransform;
        _resultNameRect = _resultNameText.transform as RectTransform;
        _isReady = _resultRect != null && _resultNameRect != null;
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
        EGameStage resultStage = GameStageRules.GetStage(target.Grade);
        bool isSameStage = StageManager.Instance != null &&
                           StageManager.Instance.CurrentStage == resultStage;

        target.SetStagePresentationActive(false);
        _clicker?.PushMode(this, ClickerInputMode.Blocked, ClickerInputPriority.Modal);
        bool isCancelled = await Present(
            target,
            rarity,
            isSameStage,
            resultStage,
            token);
        _clicker?.ReleaseMode(this);
        _isPlaying = false;

        if (isCancelled) return;
        if (isSameStage) target.SetStagePresentationActive(true);
        else _toast.Show(resultStage == EGameStage.Sky ? SkyMessage : GroundMessage);
        onCompleted?.Invoke();
    }

    private async UniTask<bool> Present(
        SlimeController target,
        EGachaRarity rarity,
        bool isSameStage,
        EGameStage resultStage,
        CancellationToken token)
    {
        Vector2 center = new(Screen.width * 0.5f, Screen.height * 0.5f);
        PreparePortal(center, target.Grade);
        _root.SetActive(true);

        if (await Fade(0f, 1f, _fadeDuration, token)) return true;
        if (await WaitForTap(token)) return true;

        Color resultColor = GetPortalColor(rarity);
        if (await Charge(resultColor, token)) return true;
        if (await Collapse(resultColor, token)) return true;
        if (await Burst(resultColor, token)) return true;
        if (await Emerge(GetSprite(target.Grade), resultColor, token)) return true;
        if (await Reveal(resultColor, token)) return true;
        if (await Wait(_holdDuration, token)) return true;

        Vector2 destination = isSameStage
            ? GetFieldScreenPosition(target, center)
            : GetOffScreenPosition(resultStage, center);
        if (await MoveTo(destination, token)) return true;
        if (isSameStage && await PlayArrivalEffect(destination, resultColor, token)) return true;
        if (await Fade(1f, 0f, _fadeDuration, token)) return true;

        CloseOverlay();
        return false;
    }

    private void PreparePortal(Vector2 center, ESlimeGrade grade)
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

        _resultRect.position = center;
        _resultRect.anchoredPosition += Vector2.down * 80f;
        _resultRect.localScale = Vector3.one * 0.2f;
        _resultImage.enabled = false;
        _resultImage.color = _silhouetteColor;

        _resultNameText.text = GetName(grade);
        _resultNameText.alpha = 0f;
        _resultNameText.gameObject.SetActive(true);
        _resultNameRect.anchoredPosition =
            _resultNameRestPosition + Vector2.right * _nameSlideDistance;

        _arrivalEffectRoot.gameObject.SetActive(false);
        _arrivalEffectRoot.localScale = Vector3.one;
        SetImageAlpha(_arrivalShockwave, 0f);
        HideArrivalSparks();

        SetPortalColor(InitialColor, 1f);
        SetImageAlpha(_shockwave, 0f);
        _shockwave.rectTransform.localScale = Vector3.one * 0.35f;
        SetImageAlpha(_flashImage, 0f);
        HideBurstSparks();
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
            Color current = Color.Lerp(InitialColor, color, ratio);
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
            AnimateOrbit(elapsed * 3f, color, Mathf.Lerp(1.45f, 0.45f, ratio));
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
            SetImageColor(_flashImage, color, inverse * 0.82f);
            SetImageColor(_shockwave, color, inverse * 0.9f);
            _shockwave.rectTransform.localScale =
                Vector3.one * Mathf.Lerp(0.35f, 2.15f, ratio);
            AnimateBurst(ratio, color);
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
        _resultImage.sprite = resultSprite;
        _resultImage.enabled = resultSprite != null;
        _resultImage.color = _silhouetteColor;
        Vector2 start = _resultRect.anchoredPosition;
        Vector2 end = start + Vector2.up * 80f;
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
            AnimateSustainedBurst(elapsed, resultColor);
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
            AnimateSustainedBurst(_emergeDuration + elapsed, resultColor);
        }

        _resultImage.color = Color.white;
        _resultRect.localScale = Vector3.one * _revealScale;
        _resultNameText.alpha = 1f;
        _resultNameRect.anchoredPosition = _resultNameRestPosition;
        _portalRoot.gameObject.SetActive(false);
        HideBurstSparks();
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
        int count = _burstSparks.Length;
        for (int i = 0; i < count; i++)
        {
            RectTransform spark = _burstSparks[i];
            if (spark == null) continue;
            float angle = Mathf.PI * 2f * i / Mathf.Max(1, count) + (i % 2) * 0.12f;
            float distance = Mathf.Lerp(25f, 330f + i % 5 * 22f, ratio);
            spark.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            spark.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg - 90f);
            spark.localScale = Vector3.one * Mathf.Lerp(1.35f, 0.25f, ratio);
            SetImageColor(spark.GetComponent<Image>(), color, 1f - ratio);
        }
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
            spark.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            spark.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg - 90f);
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

    private void HideBurstSparks()
    {
        foreach (RectTransform spark in _burstSparks)
        {
            if (spark == null) continue;
            spark.anchoredPosition = Vector2.zero;
            spark.localScale = Vector3.zero;
            SetImageAlpha(spark.GetComponent<Image>(), 0f);
        }
    }

    private void HideArrivalSparks()
    {
        foreach (RectTransform spark in _arrivalSparks)
        {
            if (spark == null) continue;
            spark.anchoredPosition = Vector2.zero;
            spark.localScale = Vector3.zero;
            SetImageAlpha(spark.GetComponent<Image>(), 0f);
        }
    }

    private void AnimateArrivalSparks(float ratio, Color color)
    {
        int count = _arrivalSparks.Length;
        for (int i = 0; i < count; i++)
        {
            RectTransform spark = _arrivalSparks[i];
            if (spark == null) continue;

            float angle = Mathf.PI * 2f * i / Mathf.Max(1, count) + (i % 2) * 0.16f;
            float distance = Mathf.Lerp(18f, 170f + i % 4 * 18f, ratio);
            spark.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            spark.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg - 90f);
            spark.localScale = Vector3.one * Mathf.Lerp(1.3f, 0.2f, ratio);
            SetImageColor(spark.GetComponent<Image>(), color, 1f - ratio);
        }
    }

    private void OnPortalTapped()
    {
        if (_isPlaying) _portalTapped = true;
    }

    private static Color GetPortalColor(EGachaRarity rarity)
    {
        return rarity switch
        {
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

    private static Sprite GetSprite(ESlimeGrade grade)
    {
        Slime slime = SlimeManager.Instance != null ? SlimeManager.Instance.Get(grade) : null;
        return slime?.SpecData?.Sprite;
    }

    private static string GetName(ESlimeGrade grade)
    {
        Slime slime = SlimeManager.Instance != null ? SlimeManager.Instance.Get(grade) : null;
        return slime?.SpecData?.Name ?? grade.ToString();
    }

    private static Vector2 GetFieldScreenPosition(SlimeController target, Vector2 fallback)
    {
        Camera camera = Camera.main;
        return camera != null ? (Vector2)camera.WorldToScreenPoint(target.transform.position) : fallback;
    }

    private Vector2 GetOffScreenPosition(EGameStage stage, Vector2 center)
    {
        float y = stage == EGameStage.Sky ? Screen.height + _offScreenMargin : -_offScreenMargin;
        return new Vector2(center.x, y);
    }

    private async UniTask<bool> MoveTo(Vector2 destination, CancellationToken token)
    {
        Vector2 start = _resultRect.position;
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
            _resultRect.position = Vector2.Lerp(start, destination, ratio) + orbit;
            _resultRect.localScale = Vector3.one * Mathf.Lerp(startScale, _endScale, ratio);
            _resultRect.localRotation = Quaternion.Euler(
                0f,
                0f,
                -360f * _moveSpinTurns * ratio);
            _resultNameText.alpha = 1f - Mathf.Clamp01(ratio * 2.5f);
            _resultNameRect.anchoredPosition =
                _resultNameRestPosition + Vector2.left * _nameSlideDistance * ratio;
        }


        _resultRect.position = destination;
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
        _arrivalEffectRoot.position = destination;
        _arrivalEffectRoot.localScale = Vector3.one;
        _arrivalEffectRoot.gameObject.SetActive(true);
        HideArrivalSparks();
        float elapsed = 0f;

        while (elapsed < _arrivalDuration)
        {
            if (await NextFrame(token)) return true;
            elapsed += Time.unscaledDeltaTime;
            float ratio = Normalized(elapsed, _arrivalDuration);
            float inverse = 1f - ratio;
            _arrivalShockwave.rectTransform.localScale =
                Vector3.one * Mathf.Lerp(0.35f, 2.1f, ratio);
            SetImageColor(_arrivalShockwave, color, inverse * 0.85f);
            AnimateArrivalSparks(ratio, color);
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
