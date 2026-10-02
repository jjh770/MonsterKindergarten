using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

// 메인 엔딩 각 화면의 시각 연출을 소유한다. 화면 계층은 씬에 작성되어 있고,
// 이 컴포넌트는 그 참조를 들고 보이고 사라지는 움직임만 만든다.
// 언제 재생할지와 게임 입력을 잠글지는 MainEndingUI가 결정한다.
public sealed class MainEndingPresentationView : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
{
    [Header("Root")]
    [Tooltip("엔딩 전체의 안전 영역 루트입니다. 노치 여백은 코드가 맞춥니다.")]
    [SerializeField] private RectTransform _safeAreaRoot;
    [SerializeField] private CanvasGroup _presentationCanvasGroup;
    [Tooltip("배경 전체를 덮는 버튼입니다. 길게 누르는 동안 연출이 빨라집니다.")]
    [SerializeField] private UnityEngine.UI.Button _backgroundButton;
    [SerializeField] private TextMeshProUGUI _advanceHint;
    [SerializeField] private CanvasGroup _advanceHintCanvasGroup;

    [Header("Class")]
    [SerializeField] private GameObject _classGroup;
    [SerializeField] private CanvasGroup _classCanvasGroup;
    [SerializeField] private RectTransform _slimeGrid;
    [SerializeField] private TextMeshProUGUI _classTitle;
    [Tooltip("일반 슬라임 20종의 그림입니다. 등급 순서대로 연결합니다.")]
    [SerializeField] private UnityEngine.UI.Image[] _slimeImages =
        new UnityEngine.UI.Image[SlimeStatusSaveData.NormalCollectionSize];
    [SerializeField] private SlimeOutlineImage[] _slimeOutlines =
        new SlimeOutlineImage[SlimeStatusSaveData.NormalCollectionSize];

    [Header("Credits")]
    [SerializeField] private GameObject _creditsGroup;
    [SerializeField] private CanvasGroup _creditsCanvasGroup;
    [SerializeField] private RectTransform _creditsContent;
    [SerializeField] private TextMeshProUGUI _creditsText;
    [SerializeField] private TextMeshProUGUI _creditsRightText;

    [Header("Teaser")]
    [SerializeField] private GameObject _teaserGroup;
    [SerializeField] private CanvasGroup _teaserCanvasGroup;
    [SerializeField] private UnityEngine.UI.Image[] _questionParticles;

    [Header("Celebration Effects")]
    [SerializeField] private UnityEngine.UI.Image[] _fireworkParticles;
    [SerializeField] private UnityEngine.UI.Image[] _petalParticles;

    [Header("Celebration")]
    [SerializeField] private GameObject _celebrationGroup;
    [SerializeField] private CanvasGroup _celebrationCanvasGroup;
    [SerializeField] private RectTransform _celebrationArtworkRect;
    [SerializeField] private UnityEngine.UI.Image _celebrationArtwork;
    [SerializeField] private TextMeshProUGUI _celebrationFallbackTitle;

    // 감사 인사 줄 높이의 하한. 그림 위 여백이 이보다 좁으면 이만큼은 확보한다.
    private const float ThanksTitleMinHeight = 160f;
    private Sprite _celebrationImageSprite;
    private UnityAction _holdStarted;
    private UnityAction _holdEnded;
    private UnityAction _advancePressed;
    private UnityEngine.UI.Image _backdropImage;
    private UnityEngine.UI.GridLayoutGroup _slimeGridLayout;
    private Color _backdropColor;
    private Tween _hintTween;
    private Tween[] _fireworkTweens;
    private Tween[] _questionTweens;
    private Tween[] _petalTweens;
    private Tween _celebrationZoomTween;
    private float _pointerDownAt;
    private Vector2 _classTitleAnchorMin;
    private Vector2 _classTitleAnchorMax;
    private Vector2 _classTitlePosition;
    private Vector2 _classTitleSize;
    private float _classTitleFontSize;
    private bool _classTitleAutoSizing;

    // MainEndingUI가 Awake에서 한 번 부른다. 참조가 비어 있으면 false를 돌려주고
    // 엔딩 전체가 꺼진다. 일부만 비어 있는 채로 재생하면 중간에 멈춘다.
    public bool Initialize(
        Sprite celebrationImageSprite,
        UnityAction holdStarted,
        UnityAction holdEnded,
        UnityAction advancePressed)
    {
        if (!HasRequiredReferences()) return false;

        _celebrationImageSprite = celebrationImageSprite;
        _holdStarted = holdStarted;
        _holdEnded = holdEnded;
        _advancePressed = advancePressed;
        _backdropImage = _backgroundButton.targetGraphic as UnityEngine.UI.Image;
        _slimeGridLayout = _slimeGrid.GetComponent<UnityEngine.UI.GridLayoutGroup>();
        _classTitleAnchorMin = _classTitle.rectTransform.anchorMin;
        _classTitleAnchorMax = _classTitle.rectTransform.anchorMax;
        _classTitlePosition = _classTitle.rectTransform.anchoredPosition;
        _classTitleSize = _classTitle.rectTransform.sizeDelta;
        _classTitleFontSize = _classTitle.fontSize;
        _classTitleAutoSizing = _classTitle.enableAutoSizing;
        if (_backdropImage != null)
        {
            _backdropColor = _backdropImage.color;
        }

        gameObject.SetActive(false);
        return true;
    }

    public void Dispose()
    {
        StopAllEffectTweens();
        _hintTween?.Kill();
        if (_slimeGridLayout != null) _slimeGridLayout.enabled = true;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        _pointerDownAt = Time.unscaledTime;
        _holdStarted?.Invoke();
    }
    public void OnPointerUp(PointerEventData eventData) => _holdEnded?.Invoke();
    public void OnPointerExit(PointerEventData eventData) => _holdEnded?.Invoke();
    public void OnPointerClick(PointerEventData eventData)
    {
        if (Time.unscaledTime - _pointerDownAt <= 0.28f)
            _advancePressed?.Invoke();
    }

    public void SetActive(bool active)
    {
        gameObject.SetActive(active);
    }

    public void RefreshSafeArea(RectTransform hostRoot)
    {
        if (hostRoot == null) return;

        SafeAreaInsets insets = SafeAreaUtility.GetInsets(hostRoot);
        _safeAreaRoot.offsetMin = new Vector2(insets.Left, insets.Bottom);
        _safeAreaRoot.offsetMax = new Vector2(-insets.Right, -insets.Top);
    }

    public void RefreshSlimeSprites(SlimeManager manager)
    {
        if (manager == null) return;

        for (int i = 0; i < _slimeImages.Length; i++)
        {
            ESlimeGrade grade = (ESlimeGrade)((int)ESlimeGrade.Grade1 + i);
            Sprite sprite = manager.Get(grade)?.SpecData.Sprite;
            _slimeOutlines[i].Apply(
                sprite,
                outlined: true,
                special: manager.IsSpecialDisplayedInDisplayRoom(grade));
        }
    }

    public void ResetVisuals()
    {
        if (_slimeGridLayout != null) _slimeGridLayout.enabled = true;
        if (_backdropImage != null) _backdropImage.color = _backdropColor;
        _presentationCanvasGroup.alpha = 0f;
        _classGroup.SetActive(false);
        _creditsGroup.SetActive(false);
        _teaserGroup.SetActive(false);
        _celebrationGroup.SetActive(false);
        SetAdvanceHint(false);
        StopAllEffectTweens();
    }

    public Tween BuildOpeningFade(float fadeDuration)
    {
        if (_backdropImage != null) _backdropImage.color = _backdropColor;
        _presentationCanvasGroup.alpha = 0f;
        return _presentationCanvasGroup
            .DOFade(1f, fadeDuration)
            .SetEase(Ease.OutQuad);
    }

    public Tween BuildSlimeIntroduction(
        Vector2[] screenOrigins,
        float introHoldDuration,
        Action playArrivalSound,
        Action playFinaleSound)
    {
        _classGroup.SetActive(true);
        _classCanvasGroup.alpha = 1f;
        _classTitle.text = "모든 슬라임 친구들이\n유치원에 모였어요!";
        RestoreClassTitleLayout();
        _classTitle.rectTransform.localScale = Vector3.one * 0.72f;
        _slimeGrid.localScale = Vector3.one;

        Canvas.ForceUpdateCanvases();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(_slimeGrid);

        var targetPositions = new Vector2[_slimeImages.Length];
        for (int i = 0; i < _slimeImages.Length; i++)
        {
            targetPositions[i] = _slimeImages[i].rectTransform.anchoredPosition;
        }

        if (_slimeGridLayout != null) _slimeGridLayout.enabled = false;

        Sequence sequence = DOTween.Sequence();
        sequence.Insert(
            0f,
            _classTitle.rectTransform.DOScale(1f, 0.55f).SetEase(Ease.OutBack));
        for (int i = 0; i < _slimeImages.Length; i++)
        {
            UnityEngine.UI.Image image = _slimeImages[i];
            RectTransform rect = image.rectTransform;
            Vector2 startPosition = new Vector2(0f, -_safeAreaRoot.rect.height * 0.35f);
            if (screenOrigins != null && i < screenOrigins.Length)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _slimeGrid,
                    screenOrigins[i],
                    null,
                    out startPosition);
            }

            image.color = new Color(1f, 1f, 1f, 0f);
            rect.anchoredPosition = startPosition;
            rect.localRotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? -12f : 12f);
            rect.localScale = new Vector3(0.72f, 0.38f, 1f);
            float delay = i * 0.09f;
            sequence.Insert(delay, image.DOFade(1f, 0.14f));
            sequence.Insert(
                delay,
                rect.DOJumpAnchorPos(targetPositions[i], 155f, 1, 0.66f)
                    .SetEase(Ease.OutQuart));
            sequence.Insert(
                delay,
                rect.DORotate(Vector3.zero, 0.48f).SetEase(Ease.OutBack));
            sequence.Insert(
                delay,
                rect.DOScale(1f, 0.58f)
                    .SetEase(Ease.OutBack));

            float landingTime = delay + 0.6f;
            sequence.InsertCallback(landingTime, () => playArrivalSound?.Invoke());
            sequence.Insert(
                landingTime,
                rect.DOPunchScale(new Vector3(0.28f, -0.16f, 0f), 0.34f, 7, 0.8f));
            sequence.Insert(
                landingTime,
                rect.DOPunchAnchorPos(new Vector2(0f, -18f), 0.3f, 6, 0.7f));

            if ((i + 1) % 4 == 0)
            {
                sequence.Insert(
                    landingTime,
                    _slimeGrid.DOPunchScale(Vector3.one * 0.055f, 0.34f, 6, 0.75f));
            }
        }

        float finalLandingTime = (_slimeImages.Length - 1) * 0.09f + 0.62f;
        sequence.InsertCallback(finalLandingTime, () => playFinaleSound?.Invoke());
        sequence.Insert(
            finalLandingTime,
            _classTitle.rectTransform
                .DOPunchScale(Vector3.one * 0.16f, 0.48f, 6, 0.7f));
        sequence.Insert(
            finalLandingTime,
            _slimeGrid.DOPunchScale(Vector3.one * 0.09f, 0.5f, 7, 0.75f));
        sequence.Insert(
            finalLandingTime,
            _slimeGrid.DOPunchRotation(new Vector3(0f, 0f, 3.5f), 0.55f, 10, 0.8f));
        if (_backdropImage != null)
        {
            Color flash = new Color(1f, 0.86f, 0.45f, _backdropColor.a);
            sequence.Insert(finalLandingTime, _backdropImage.DOColor(flash, 0.12f));
            sequence.Insert(
                finalLandingTime + 0.12f,
                _backdropImage.DOColor(_backdropColor, 0.55f).SetEase(Ease.OutQuad));
        }

        sequence.AppendInterval(introHoldDuration);
        sequence.OnComplete(() =>
        {
            if (_slimeGridLayout != null) _slimeGridLayout.enabled = true;
            _slimeGrid.localScale = Vector3.one;
        });
        return sequence;
    }

    public Tween HideSlimeIntroduction(float fadeDuration)
    {
        return _classCanvasGroup.DOFade(0f, fadeDuration).OnComplete(() =>
        {
            _classGroup.SetActive(false);
        });
    }

    // 감사 인사와 함께 축전 그림과 꽃잎이 나온다. 별 터지는 효과는 SafeAreaRoot 안에 있어서
    // CelebrationGroup보다 앞에 그려지므로 그림 위로 터진다.
    public Tween BuildThanks(float fadeDuration)
    {
        _classGroup.SetActive(true);
        _classCanvasGroup.alpha = 0f;
        _slimeGrid.gameObject.SetActive(false);
        ShowCelebration();

        _classTitle.text = "모든 순간을 함께해 주셔서\n진심으로 감사합니다!";
        PlaceThanksTitleAboveArtwork();
        _classTitle.enableAutoSizing = false;
        _classTitle.fontSize = 60f;
        _classTitle.alignment = TextAlignmentOptions.Center;
        _classTitle.rectTransform.localScale = Vector3.one * 0.78f;

        Sequence sequence = DOTween.Sequence();
        sequence.Append(_classCanvasGroup.DOFade(1f, fadeDuration));
        sequence.Join(_celebrationCanvasGroup.DOFade(1f, fadeDuration)
            .SetEase(Ease.OutQuad));
        sequence.Join(_classTitle.rectTransform.DOScale(1f, 0.55f).SetEase(Ease.OutBack));
        sequence.Append(_classTitle.rectTransform.DOPunchScale(
            Vector3.one * 0.1f, 0.5f, 5, 0.65f));
        sequence.OnComplete(StartFireworkEffects);
        return sequence;
    }

    public Tween HideThanks(float fadeDuration)
    {
        KillTweens(ref _fireworkTweens);
        Deactivate(_fireworkParticles);

        Sequence sequence = DOTween.Sequence();
        sequence.Append(_classCanvasGroup.DOFade(0f, fadeDuration));
        sequence.Join(_celebrationCanvasGroup.DOFade(0f, fadeDuration));
        sequence.OnComplete(() =>
        {
            _slimeGrid.gameObject.SetActive(true);
            _classGroup.SetActive(false);
            RestoreClassTitleLayout();
            StopCelebration();
        });
        return sequence;
    }

    public Tween BuildCredits(
        string leftText,
        string rightText,
        float riseDuration)
    {
        _creditsText.text = leftText;
        _creditsRightText.text = rightText;
        _creditsGroup.SetActive(true);
        _creditsCanvasGroup.alpha = 0f;

        _creditsContent.anchoredPosition = new Vector2(
            0f,
            -_safeAreaRoot.rect.height * 0.42f);
        Sequence sequence = DOTween.Sequence();
        sequence.Append(_creditsCanvasGroup.DOFade(1f, 0.3f));
        sequence.Join(_creditsContent.DOAnchorPosY(
            0f,
            riseDuration).SetEase(Ease.OutCubic));
        return sequence;
    }

    public Tween HideCredits(float fadeDuration)
    {
        return _creditsCanvasGroup.DOFade(0f, fadeDuration)
            .OnComplete(() => _creditsGroup.SetActive(false));
    }

    public Tween BuildSpecialSlimeTeaser(float fadeDuration)
    {
        StopCelebration();
        _teaserGroup.SetActive(true);
        _teaserCanvasGroup.alpha = 0f;

        Sequence sequence = DOTween.Sequence();
        sequence.Append(_teaserCanvasGroup.DOFade(1f, fadeDuration));
        sequence.OnComplete(StartQuestionEffects);
        return sequence;
    }

    // 축전 그림과 꽃잎. 감사 인사 화면에서 함께 보인다.
    private void ShowCelebration()
    {
        bool hasArtwork = _celebrationImageSprite != null;
        _celebrationGroup.SetActive(true);
        _celebrationCanvasGroup.alpha = 0f;
        _celebrationArtwork.sprite = _celebrationImageSprite;
        _celebrationArtwork.color = hasArtwork
            ? Color.white
            : new Color(0.22f, 0.16f, 0.08f, 1f);
        _celebrationFallbackTitle.gameObject.SetActive(!hasArtwork);
        _celebrationArtworkRect.localScale = Vector3.one;

        StartPetalEffects();
        _celebrationZoomTween?.Kill();
        _celebrationZoomTween = _celebrationArtworkRect
            .DOScale(1.04f, 6f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void StopCelebration()
    {
        KillTweens(ref _fireworkTweens);
        Deactivate(_fireworkParticles);
        KillTweens(ref _petalTweens);
        Deactivate(_petalParticles);
        _celebrationZoomTween?.Kill();
        _celebrationZoomTween = null;
        _celebrationGroup.SetActive(false);
    }

    // 그림은 화면 위아래에 여백을 두고 놓여 있다. 감사 인사는 그 위쪽 여백에 놓아 그림을
    // 가리지 않게 한다. 노치 때문에 여백 높이가 기기마다 달라 그림의 실제 위치에서 계산한다.
    private void PlaceThanksTitleAboveArtwork()
    {
        Canvas.ForceUpdateCanvases();
        var corners = new Vector3[4];
        _celebrationArtworkRect.GetWorldCorners(corners);
        float artworkTop = _safeAreaRoot.InverseTransformPoint(corners[1]).y;
        float safeTop = _safeAreaRoot.rect.yMax;
        float height = Mathf.Max(ThanksTitleMinHeight, safeTop - artworkTop);

        RectTransform title = _classTitle.rectTransform;
        title.anchorMin = new Vector2(0.5f, 1f);
        title.anchorMax = new Vector2(0.5f, 1f);
        title.sizeDelta = new Vector2(
            Mathf.Max(760f, _safeAreaRoot.rect.width - 120f),
            height);
        title.anchoredPosition = new Vector2(0f, -height * 0.5f);
    }

    public Tween BuildClose(float fadeDuration)
    {
        KillTweens(ref _petalTweens);
        KillTweens(ref _questionTweens);
        return _presentationCanvasGroup.DOFade(0f, fadeDuration);
    }

    public void SetAdvanceHint(bool visible)
    {
        _hintTween?.Kill();
        _hintTween = null;
        _advanceHint.gameObject.SetActive(visible);
        if (!visible) return;

        _advanceHint.text = "화면을 터치해 계속";
        _advanceHintCanvasGroup.alpha = 0.35f;
        _hintTween = _advanceHintCanvasGroup.DOFade(1f, 0.8f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetUpdate(true);
    }

    private void StartFireworkEffects()
    {
        KillTweens(ref _fireworkTweens);
        _fireworkTweens = new Tween[_fireworkParticles.Length];
        int burstSize = 6;
        for (int i = 0; i < _fireworkParticles.Length; i++)
        {
            UnityEngine.UI.Image image = _fireworkParticles[i];
            RectTransform rect = image.rectTransform;
            int particleIndex = i % burstSize;
            int burstIndex = i / burstSize;
            float angle = particleIndex * (360f / burstSize) + burstIndex * 31f;
            Vector2 center = burstIndex switch
            {
                0 => new Vector2(-280f, 210f),
                1 => new Vector2(270f, 80f),
                _ => new Vector2(0f, -170f),
            };
            Vector2 destination = center + new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad)) * (300f + particleIndex * 14f);
            Color color = Color.HSVToRGB((i * 0.11f) % 1f, 0.55f, 1f);

            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.AppendInterval(burstIndex * 0.55f);
            sequence.AppendCallback(() =>
            {
                image.gameObject.SetActive(true);
                image.color = new Color(color.r, color.g, color.b, 0f);
                rect.anchoredPosition = center;
                rect.localScale = Vector3.one * 0.12f;
            });
            sequence.Append(image.DOFade(1f, 0.08f));
            sequence.Join(rect.DOScale(0.72f + particleIndex * 0.06f, 0.22f)
                .SetEase(Ease.OutBack));
            sequence.Append(rect.DOAnchorPos(destination, 0.82f).SetEase(Ease.OutCubic));
            sequence.Join(rect.DOScale(0.18f, 0.82f).SetEase(Ease.InQuad));
            sequence.Join(image.DOFade(0f, 0.82f).SetEase(Ease.InQuad));
            sequence.Join(rect.DORotate(new Vector3(0f, 0f, 360f), 0.82f,
                RotateMode.FastBeyond360));
            sequence.AppendInterval(0.55f);
            sequence.SetLoops(-1, LoopType.Restart);
            _fireworkTweens[i] = sequence;
        }
    }

    private void StartQuestionEffects()
    {
        KillTweens(ref _questionTweens);
        _questionTweens = new Tween[_questionParticles.Length];
        for (int i = 0; i < _questionParticles.Length; i++)
        {
            PlayQuestionParticle(i, i * 0.32f);
        }
    }

    private void PlayQuestionParticle(int index, float initialDelay = 0f)
    {
        if (!_teaserGroup.activeInHierarchy ||
            index < 0 || index >= _questionParticles.Length)
        {
            return;
        }

        UnityEngine.UI.Image image = _questionParticles[index];
        RectTransform rect = image.rectTransform;
        Vector2 start = new Vector2(
            UnityEngine.Random.Range(-340f, 340f),
            UnityEngine.Random.Range(-330f, 80f));
        float angle = UnityEngine.Random.Range(-38f, 38f);
        float distance = UnityEngine.Random.Range(330f, 540f);
        Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.up;
        Vector2 destination = start + direction * distance;
        float duration = UnityEngine.Random.Range(1.7f, 2.5f);
        float scale = UnityEngine.Random.Range(0.24f, 0.4f);
        float rotation = UnityEngine.Random.Range(-35f, 35f);

        Sequence sequence = DOTween.Sequence().SetUpdate(true);
        sequence.AppendInterval(initialDelay);
        sequence.AppendCallback(() =>
        {
            image.gameObject.SetActive(true);
            image.color = new Color(1f, 1f, 1f, 0f);
            rect.anchoredPosition = start;
            rect.localScale = Vector3.one * (scale * 0.55f);
            rect.localRotation = Quaternion.identity;
        });
        sequence.Append(image.DOFade(0.62f, 0.24f));
        sequence.Join(rect.DOScale(scale, 0.34f).SetEase(Ease.OutBack));
        sequence.Append(rect.DOAnchorPos(destination, duration).SetEase(Ease.OutSine));
        sequence.Join(rect.DORotate(new Vector3(0f, 0f, rotation), duration)
            .SetEase(Ease.InOutSine));
        sequence.Join(image.DOFade(0f, duration).SetEase(Ease.InQuad));
        sequence.AppendInterval(UnityEngine.Random.Range(0.25f, 0.7f));
        sequence.OnComplete(() =>
        {
            image.gameObject.SetActive(false);
            _questionTweens[index] = null;
            PlayQuestionParticle(index);
        });
        _questionTweens[index] = sequence;
    }

    private void RestoreClassTitleLayout()
    {
        _classTitle.rectTransform.anchorMin = _classTitleAnchorMin;
        _classTitle.rectTransform.anchorMax = _classTitleAnchorMax;
        _classTitle.rectTransform.anchoredPosition = _classTitlePosition;
        _classTitle.rectTransform.sizeDelta = _classTitleSize;
        _classTitle.fontSize = _classTitleFontSize;
        _classTitle.enableAutoSizing = _classTitleAutoSizing;
        _classTitle.alignment = TextAlignmentOptions.Center;
    }

    private void StartPetalEffects()
    {
        KillTweens(ref _petalTweens);
        _petalTweens = new Tween[_petalParticles.Length];
        for (int i = 0; i < _petalParticles.Length; i++)
        {
            UnityEngine.UI.Image image = _petalParticles[i];
            RectTransform rect = image.rectTransform;
            float x = Mathf.Lerp(-430f, 430f,
                _petalParticles.Length <= 1 ? 0.5f : i / (float)(_petalParticles.Length - 1));
            float delay = (i % 7) * 0.32f;
            float duration = 3.2f + (i % 4) * 0.45f;
            Color petalColor = i % 2 == 0
                ? new Color(1f, 0.72f, 0.82f, 0.86f)
                : new Color(1f, 0.9f, 0.68f, 0.86f);
            Sequence sequence = DOTween.Sequence().SetUpdate(true);
            sequence.AppendInterval(delay);
            sequence.AppendCallback(() =>
            {
                image.gameObject.SetActive(true);
                image.color = petalColor;
                rect.anchoredPosition = new Vector2(x, 980f);
                rect.localScale = new Vector3(0.55f, 1f, 1f);
            });
            sequence.Append(rect.DOAnchorPos(
                new Vector2(x + (i % 2 == 0 ? 150f : -150f), -980f),
                duration).SetEase(Ease.Linear));
            sequence.Join(rect.DORotate(new Vector3(0f, 0f, 540f), duration,
                RotateMode.FastBeyond360).SetEase(Ease.Linear));
            sequence.Join(image.DOFade(0.15f, duration).SetEase(Ease.InQuad));
            sequence.SetLoops(-1, LoopType.Restart);
            _petalTweens[i] = sequence;
        }
    }

    private void StopAllEffectTweens()
    {
        KillTweens(ref _fireworkTweens);
        KillTweens(ref _questionTweens);
        KillTweens(ref _petalTweens);
        _celebrationZoomTween?.Kill();
        _celebrationZoomTween = null;
        Deactivate(_fireworkParticles);
        Deactivate(_questionParticles);
        Deactivate(_petalParticles);
    }

    private static void Deactivate(UnityEngine.UI.Image[] images)
    {
        if (images == null) return;
        foreach (UnityEngine.UI.Image image in images)
        {
            if (image != null) image.gameObject.SetActive(false);
        }
    }

    private static void KillTweens(ref Tween[] tweens)
    {
        if (tweens != null)
        {
            foreach (Tween tween in tweens) tween?.Kill();
        }
        tweens = null;
    }

    private bool HasRequiredReferences()
    {
        bool hasReferences = _safeAreaRoot != null &&
                             _presentationCanvasGroup != null &&
                             _backgroundButton != null &&
                             _advanceHint != null &&
                             _advanceHintCanvasGroup != null &&
                             _classGroup != null &&
                             _classCanvasGroup != null &&
                             _slimeGrid != null &&
                             _classTitle != null &&
                             _slimeOutlines != null &&
                             _slimeOutlines.Length == SlimeStatusSaveData.NormalCollectionSize &&
                             _creditsGroup != null &&
                             _creditsCanvasGroup != null &&
                             _creditsContent != null &&
                             _creditsText != null &&
                             _creditsRightText != null &&
                             _teaserGroup != null &&
                             _teaserCanvasGroup != null &&
                             _celebrationGroup != null &&
                             _celebrationCanvasGroup != null &&
                             _celebrationArtworkRect != null &&
                             _celebrationArtwork != null &&
                             _celebrationFallbackTitle != null &&
                             _questionParticles != null &&
                             _fireworkParticles != null &&
                             _petalParticles != null &&
                             _slimeImages != null &&
                             _slimeImages.Length == SlimeStatusSaveData.NormalCollectionSize;

        for (int i = 0; hasReferences && i < _slimeImages.Length; i++)
        {
            hasReferences = _slimeImages[i] != null && _slimeOutlines[i] != null;
        }

        if (!hasReferences)
        {
            Debug.LogError("메인 엔딩 연출의 필수 참조가 비어 있습니다.", this);
        }

        return hasReferences;
    }
}
