using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

// 뽑기 기계 그림과 그 위의 움직임을 맡는다. 티켓을 넣고, 캡슐이 섞이고, 하나가 배출구로 나오고,
// 눌러서 열면 등급 빛이 터진다. 무엇이 나오는지는 이미 정해져서 들어오므로 여기서는 보여 주기만 한다.
//
// 좌표는 모두 기계 루트의 로컬 좌표다. 투입구, 배출구, 놓이는 자리는 씬에 표시용 자식으로 두어서
// 코드에 이미지 픽셀 값을 적지 않는다. 그림을 바꾸면 그 표시만 옮기면 된다.
//
// 모든 대기는 취소되면 true를 돌려준다. 호출한 쪽이 그 값으로 연출을 접는다.
public sealed class GachaMachineView : MonoBehaviour
{
    // 등급이 높을수록 오래 뜸을 들이고 빛이 크고 번쩍인다. 특별한 결과는 여기에 무지개 색이 더해진다.
    private readonly struct OpenStyle
    {
        public readonly float ChargeSeconds;
        public readonly float GlowScale;
        public readonly int TwinkleCount;
        public readonly float FlashAlpha;
        public readonly float ShockwaveAlpha;
        public readonly float ShakeAmplitude;

        public OpenStyle(
            float chargeSeconds,
            float glowScale,
            int twinkleCount,
            float flashAlpha,
            float shockwaveAlpha,
            float shakeAmplitude)
        {
            ChargeSeconds = chargeSeconds;
            GlowScale = glowScale;
            TwinkleCount = twinkleCount;
            FlashAlpha = flashAlpha;
            ShockwaveAlpha = shockwaveAlpha;
            ShakeAmplitude = shakeAmplitude;
        }
    }

    [Header("Machine")]
    [SerializeField] private RectTransform _machineRoot;
    [SerializeField] private CanvasGroup _machineGroup;

    [Header("Capsules")]
    [SerializeField] private RectTransform _pileLayer;
    [SerializeField] private Image[] _pileCapsules;
    [Tooltip("티켓을 넣으면 아래로 떨어져 배출구로 나오는 캡슐의 _pileCapsules 번호입니다.")]
    [SerializeField, Min(0)] private int _chosenIndex;
    [SerializeField] private Sprite[] _capsuleSprites;
    [SerializeField] private Sprite _rainbowCapsuleSprite;

    [Header("Ticket")]
    [Tooltip("티켓이 날아오기 시작하는 곳입니다. 상단 바의 뽑기권 아이콘을 넣습니다.")]
    [SerializeField] private RectTransform _ticketSource;
    [SerializeField] private Image _ticketImage;
    [SerializeField] private RectTransform _ticketSlot;

    [Header("Outlet")]
    [SerializeField] private RectTransform _outlet;
    [SerializeField] private RectTransform _outletRest;
    [SerializeField] private Image _outletCapsule;
    [SerializeField] private Image _capsuleTop;
    [SerializeField] private Image _capsuleBottom;

    [Header("Light")]
    [SerializeField] private Image _glow;
    [SerializeField] private Image[] _twinkles;
    [SerializeField] private Image _shockwave;
    [SerializeField] private Image _flash;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float _appearDuration = 0.4f;
    [SerializeField, Min(0f)] private float _ticketFlyDuration = 0.55f;
    [SerializeField, Min(0f)] private float _shuffleDuration = 0.9f;
    [SerializeField, Min(0f)] private float _dropDuration = 0.45f;
    [SerializeField, Min(0f)] private float _rollOutDuration = 0.75f;
    [SerializeField, Min(0f)] private float _pickUpDuration = 0.5f;
    [SerializeField, Min(0f)] private float _splitDuration = 0.5f;

    [Header("Look")]
    [Tooltip("캡슐을 열 때 커지는 배율입니다.")]
    [SerializeField, Min(1f)] private float _openScale = 2.4f;
    [Tooltip("캡슐 그림에서 이음선이 위에서 몇 퍼센트 아래에 있는지입니다. 반쪽으로 가를 때 씁니다.")]
    [SerializeField, Range(0.3f, 0.7f)] private float _seamFraction = 0.56f;
    [SerializeField, Min(0f)] private float _glowBaseScale = 1f;

    private const float WaitSfxFadeOutSeconds = 0.2f;

    private Vector2[] _pileHome;
    private Vector2 _rootRest;
    private Sprite _chosenSprite;
    private bool _isReady;

    private void Awake()
    {
        if (_machineRoot == null || _machineGroup == null || _pileLayer == null ||
            _pileCapsules == null || _pileCapsules.Length == 0 ||
            _chosenIndex >= _pileCapsules.Length ||
            _capsuleSprites == null || _capsuleSprites.Length == 0 ||
            _rainbowCapsuleSprite == null || _ticketImage == null || _ticketSlot == null ||
            _outlet == null || _outletRest == null || _outletCapsule == null ||
            _capsuleTop == null || _capsuleBottom == null || _glow == null ||
            _twinkles == null || _twinkles.Length == 0 || _shockwave == null || _flash == null)
        {
            Debug.LogError("뽑기 기계 연출의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _pileHome = new Vector2[_pileCapsules.Length];
        for (int i = 0; i < _pileCapsules.Length; i++)
        {
            _pileHome[i] = _pileCapsules[i].rectTransform.anchoredPosition;
        }

        _rootRest = _machineRoot.anchoredPosition;
        _capsuleTop.fillAmount = _seamFraction;
        _capsuleBottom.fillAmount = 1f - _seamFraction;
        _isReady = true;
    }

    // 새 연출을 시작하기 전에 모든 조각을 처음 모습으로 되돌린다. 결과가 특별하면 떨어지는 캡슐만
    // 무지개이고, 아니면 여섯 색 중 하나다. 색은 등급과 관계없이 정해서 겉모습으로 결과를 읽지 못하게 한다.
    public void Prepare(bool isSpecial)
    {
        if (!_isReady) return;

        _machineRoot.gameObject.SetActive(true);
        _machineRoot.anchoredPosition = _rootRest;
        _machineRoot.localScale = Vector3.one;
        _machineRoot.localRotation = Quaternion.identity;
        _machineGroup.alpha = 0f;

        _chosenSprite = isSpecial
            ? _rainbowCapsuleSprite
            : _capsuleSprites[UnityEngine.Random.Range(0, _capsuleSprites.Length)];

        for (int i = 0; i < _pileCapsules.Length; i++)
        {
            Image capsule = _pileCapsules[i];
            capsule.gameObject.SetActive(true);
            capsule.rectTransform.anchoredPosition = _pileHome[i];
            capsule.rectTransform.localRotation = Quaternion.identity;
            capsule.rectTransform.localScale = Vector3.one;
        }
        _pileCapsules[_chosenIndex].sprite = _chosenSprite;

        _ticketImage.gameObject.SetActive(false);
        _outletCapsule.sprite = _chosenSprite;
        _outletCapsule.gameObject.SetActive(false);
        _capsuleTop.sprite = _chosenSprite;
        _capsuleBottom.sprite = _chosenSprite;
        _capsuleTop.gameObject.SetActive(false);
        _capsuleBottom.gameObject.SetActive(false);
        _glow.gameObject.SetActive(false);
        _shockwave.gameObject.SetActive(false);
        SetImageAlpha(_flash, 0f);
        HideTwinkles();
    }

    public void Hide()
    {
        if (_machineRoot != null) _machineRoot.gameObject.SetActive(false);
    }

    public void SetMachineAlpha(float alpha)
    {
        if (_isReady) _machineGroup.alpha = Mathf.Clamp01(alpha);
    }

    public UniTask<bool> Appear(CancellationToken token)
    {
        return Run(_appearDuration, ratio =>
        {
            _machineGroup.alpha = ratio;
            float scale = Mathf.LerpUnclamped(0.86f, 1f, EaseOutBack(ratio));
            _machineRoot.localScale = Vector3.one * scale;
            _machineRoot.anchoredPosition = _rootRest + Vector2.down * (80f * (1f - ratio));
        }, token);
    }

    // 티켓을 기다리는 동안 캡슐 더미가 살짝 흔들려 살아 있게 한다.
    public void Idle(float elapsed)
    {
        for (int i = 0; i < _pileCapsules.Length; i++)
        {
            RectTransform rect = _pileCapsules[i].rectTransform;
            rect.anchoredPosition = _pileHome[i] +
                                    Vector2.up * (Mathf.Sin(elapsed * 1.8f + i * 0.9f) * 3f);
            rect.localRotation = Quaternion.Euler(
                0f, 0f, Mathf.Sin(elapsed * 1.3f + i) * 1.5f);
        }
    }

    public async UniTask<bool> InsertTicket(CancellationToken token)
    {
        AudioManager.Instance?.PlaySFX(EAudioSfx.UIClick);
        RectTransform ticket = _ticketImage.rectTransform;
        Vector2 slot = _ticketSlot.anchoredPosition;
        Vector2 start = _ticketSource != null
            ? (Vector2)_machineRoot.InverseTransformPoint(_ticketSource.position)
            : slot + Vector2.down * 500f;
        Vector2 control = new(
            (start.x + slot.x) * 0.5f,
            Mathf.Max(start.y, slot.y) + 160f);

        ticket.gameObject.SetActive(true);
        ticket.localScale = Vector3.one;
        ticket.localRotation = Quaternion.identity;
        ticket.anchoredPosition = start;
        SetImageAlpha(_ticketImage, 1f);

        bool cancelled = await Run(_ticketFlyDuration, ratio =>
        {
            float eased = Mathf.SmoothStep(0f, 1f, ratio);
            ticket.anchoredPosition = Bezier(start, control, slot, eased);
            ticket.localScale = Vector3.one * Mathf.Lerp(1f, 0.55f, eased);
            ticket.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, -14f, eased));
        }, token);
        if (cancelled) return true;

        // 투입구로 얇아지며 빨려 들어간다.
        cancelled = await Run(0.16f, ratio =>
        {
            ticket.localScale = new Vector3(0.55f, Mathf.Lerp(0.55f, 0f, ratio), 1f);
        }, token);
        ticket.gameObject.SetActive(false);
        if (cancelled) return true;

        return await Punch(0.04f, 0.28f, token);
    }

    // 캡슐이 섞이고, 하나가 바닥으로 떨어져 가려지고, 배출구에서 굴러 나온다.
    public async UniTask<bool> Dispense(CancellationToken token)
    {
        AudioManager.Instance?.PlayLoopingSFX(EAudioSfx.GachaWait);
        bool cancelled = await Run(_shuffleDuration, ratio =>
        {
            float elapsed = ratio * _shuffleDuration;
            float envelope = 0.25f + 0.75f * Mathf.Sin(ratio * Mathf.PI);
            for (int i = 0; i < _pileCapsules.Length; i++)
            {
                RectTransform rect = _pileCapsules[i].rectTransform;
                rect.anchoredPosition = _pileHome[i] + new Vector2(
                    Mathf.Sin(elapsed * 17f + i * 1.7f) * 16f * envelope,
                    Mathf.Abs(Mathf.Sin(elapsed * 13f + i * 2.3f)) * 26f * envelope);
                rect.localRotation = Quaternion.Euler(
                    0f, 0f, Mathf.Sin(elapsed * 11f + i) * 22f * envelope);
            }

            _machineRoot.anchoredPosition = _rootRest + new Vector2(
                Mathf.Sin(elapsed * 61f) * 3f * envelope, 0f);
        }, token);
        AudioManager.Instance?.StopLoopingSFX(WaitSfxFadeOutSeconds);
        if (cancelled) return true;

        _machineRoot.anchoredPosition = _rootRest;
        if (await Settle(token)) return true;
        if (await DropChosen(token)) return true;
        AudioManager.Instance?.PlaySFX(EAudioSfx.SlimeLand);
        if (await Punch(0.03f, 0.25f, token)) return true;
        return await RollOut(token);
    }

    // 캡슐을 누르라고 알려 주는 흔들림이다. 배출구 앞에 놓인 캡슐이 통통거리고 은은히 빛난다.
    public void IdleCapsule(float elapsed)
    {
        RectTransform capsule = _outletCapsule.rectTransform;
        capsule.localScale = Vector3.one * (1f + Mathf.Sin(elapsed * 3.2f) * 0.04f);
        capsule.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * 5f) * 5f);

        _glow.gameObject.SetActive(true);
        _glow.rectTransform.anchoredPosition = capsule.anchoredPosition;
        _glow.rectTransform.localScale = Vector3.one * (_glowBaseScale * 0.55f);
        SetImageColor(_glow, Color.white, 0.22f + Mathf.Sin(elapsed * 3.2f) * 0.1f);
    }

    // 캡슐을 집어 화면 가운데로 옮기고, 흔들다가, 반쪽으로 갈라 빛을 터뜨린다.
    public async UniTask<bool> Open(EGachaRarity rarity, Color color, CancellationToken token)
    {
        OpenStyle style = GetStyle(rarity);
        bool rainbow = rarity == EGachaRarity.Special;
        RectTransform capsule = _outletCapsule.rectTransform;
        Vector2 rest = capsule.anchoredPosition;
        Vector2 open = GetOpenPoint();

        Quaternion restRotation = capsule.localRotation;
        float restScale = capsule.localScale.x;

        bool cancelled = await Run(_pickUpDuration, ratio =>
        {
            float eased = Mathf.SmoothStep(0f, 1f, ratio);
            capsule.anchoredPosition = Vector2.Lerp(rest, open, eased) +
                                       Vector2.up * (Mathf.Sin(ratio * Mathf.PI) * 70f);
            capsule.localScale = Vector3.one *
                                 Mathf.LerpUnclamped(restScale, _openScale, EaseOutBack(ratio));
            capsule.localRotation = Quaternion.Slerp(restRotation, Quaternion.identity, eased);
            _glow.rectTransform.anchoredPosition = capsule.anchoredPosition;
        }, token);
        if (cancelled) return true;

        _glow.gameObject.SetActive(true);
        cancelled = await Run(style.ChargeSeconds, ratio =>
        {
            float elapsed = ratio * style.ChargeSeconds;
            float amplitude = Mathf.Lerp(2f, style.ShakeAmplitude, ratio * ratio);
            capsule.anchoredPosition = open + new Vector2(
                Mathf.Sin(elapsed * 55f) * amplitude,
                Mathf.Cos(elapsed * 47f) * amplitude * 0.6f);
            capsule.localRotation = Quaternion.Euler(
                0f, 0f, Mathf.Sin(elapsed * 40f) * amplitude * 0.9f);
            capsule.localScale = Vector3.one * (_openScale * (1f + 0.1f * ratio * ratio));

            Color tint = rainbow ? RainbowTint.Pure() : color;
            _glow.rectTransform.anchoredPosition = open;
            _glow.rectTransform.localScale = Vector3.one *
                                             (_glowBaseScale * Mathf.Lerp(0.7f, style.GlowScale * 0.8f, ratio));
            SetImageColor(_glow, tint, Mathf.Lerp(0.25f, 0.9f, ratio));
        }, token);
        if (cancelled) return true;

        // 가르는 순간: 통짜 캡슐을 감추고 같은 자리에 반쪽 둘을 놓는다.
        AudioManager.Instance?.PlaySFX(EAudioSfx.FeatureUnlock);
        float scale = capsule.localScale.x;
        capsule.gameObject.SetActive(false);
        PlaceHalf(_capsuleTop, open, scale);
        PlaceHalf(_capsuleBottom, open, scale);
        PrepareShockwave(style);
        float flashPeak = style.FlashAlpha;

        cancelled = await Run(_splitDuration, ratio =>
        {
            float outward = 1f - (1f - ratio) * (1f - ratio);
            float fade = 1f - ratio * ratio;
            Color tint = rainbow ? RainbowTint.Pure() : color;

            _capsuleTop.rectTransform.anchoredPosition = open + new Vector2(-34f, 170f) * outward;
            _capsuleTop.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 20f * outward);
            _capsuleBottom.rectTransform.anchoredPosition = open + new Vector2(34f, -170f) * outward;
            _capsuleBottom.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -20f * outward);
            SetImageAlpha(_capsuleTop, fade);
            SetImageAlpha(_capsuleBottom, fade);

            _glow.rectTransform.localScale = Vector3.one *
                                             (_glowBaseScale * Mathf.Lerp(style.GlowScale * 0.8f, style.GlowScale * 1.25f, outward));
            SetImageColor(_glow, tint, 1f - ratio);

            AnimateTwinkleBurst(open, style.TwinkleCount, ratio, tint);
            if (flashPeak > 0f) SetImageColor(_flash, tint, flashPeak * (1f - ratio));
            if (style.ShockwaveAlpha > 0f)
            {
                _shockwave.rectTransform.anchoredPosition = open;
                _shockwave.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.35f, 2.4f, outward);
                SetImageColor(_shockwave, tint, style.ShockwaveAlpha * (1f - ratio));
            }
        }, token);

        _capsuleTop.gameObject.SetActive(false);
        _capsuleBottom.gameObject.SetActive(false);
        _glow.gameObject.SetActive(false);
        _shockwave.gameObject.SetActive(false);
        SetImageAlpha(_flash, 0f);
        HideTwinkles();
        return cancelled;
    }

    // 슬라임이 솟아오르는 동안 빛이 계속 새로 퍼지게 한다. 한 번 터지고 끝나면 허전하다.
    public void Sustain(float elapsed, Color color)
    {
        Vector2 center = GetOpenPoint();
        int count = _twinkles.Length;
        for (int i = 0; i < count; i++)
        {
            Image twinkle = _twinkles[i];
            if (twinkle == null) continue;

            float phase = Mathf.Repeat(elapsed * 1.5f + (float)i / count, 1f);
            float angle = Mathf.PI * 2f * i / count + elapsed * 0.45f;
            float distance = Mathf.Lerp(60f, 320f + i % 3 * 30f, phase);
            float alpha = Mathf.Sin(phase * Mathf.PI);
            twinkle.gameObject.SetActive(true);
            twinkle.rectTransform.anchoredPosition = center +
                                                     new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            twinkle.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.1f, alpha);
            twinkle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, elapsed * 90f + i * 30f);
            SetImageColor(twinkle, Color.Lerp(Color.white, color, 0.6f), alpha * 0.85f);
        }
    }

    public void EndSustain()
    {
        HideTwinkles();
    }

    private async UniTask<bool> Settle(CancellationToken token)
    {
        Vector2[] from = new Vector2[_pileCapsules.Length];
        for (int i = 0; i < from.Length; i++)
        {
            from[i] = _pileCapsules[i].rectTransform.anchoredPosition;
        }

        return await Run(0.15f, ratio =>
        {
            for (int i = 0; i < _pileCapsules.Length; i++)
            {
                RectTransform rect = _pileCapsules[i].rectTransform;
                rect.anchoredPosition = Vector2.Lerp(from[i], _pileHome[i], ratio);
                rect.localRotation = Quaternion.Slerp(rect.localRotation, Quaternion.identity, ratio);
            }
        }, token);
    }

    // 고른 캡슐이 살짝 뛰었다가 바닥 아래로 떨어진다. 그림 아래쪽 틀이 가려 주므로 따로 지우지 않는다.
    private async UniTask<bool> DropChosen(CancellationToken token)
    {
        RectTransform chosen = _pileCapsules[_chosenIndex].rectTransform;
        // 떨어지는 동안 이웃 캡슐에 가리지 않도록 더미의 맨 앞으로 꺼낸다.
        chosen.SetAsLastSibling();
        Vector2 from = chosen.anchoredPosition;
        Vector2 top = from + Vector2.up * 38f;
        float fallY = -_pileLayer.rect.height * 0.5f - chosen.rect.height;

        bool cancelled = await Run(0.18f, ratio =>
        {
            float eased = 1f - (1f - ratio) * (1f - ratio);
            chosen.anchoredPosition = Vector2.Lerp(from, top, eased);
            chosen.localRotation = Quaternion.Euler(0f, 0f, 12f * eased);
        }, token);
        if (cancelled) return true;

        cancelled = await Run(_dropDuration, ratio =>
        {
            float eased = ratio * ratio;
            chosen.anchoredPosition = new Vector2(top.x, Mathf.Lerp(top.y, fallY, eased));
            chosen.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(12f, 200f, eased));
        }, token);
        chosen.gameObject.SetActive(false);
        return cancelled;
    }

    // 배출구 안쪽에서 작게 나타나 앞으로 튀어나오며 굴러 놓일 자리에 선다.
    private async UniTask<bool> RollOut(CancellationToken token)
    {
        RectTransform capsule = _outletCapsule.rectTransform;
        Vector2 start = _outlet.anchoredPosition;
        Vector2 end = _outletRest.anchoredPosition;
        capsule.gameObject.SetActive(true);
        capsule.anchoredPosition = start;
        capsule.localScale = Vector3.one * 0.55f;
        SetImageAlpha(_outletCapsule, 0f);
        AudioManager.Instance?.PlaySFX(EAudioSfx.SlimeBounce);

        return await Run(_rollOutDuration, ratio =>
        {
            float slide = Mathf.SmoothStep(0f, 1f, ratio);
            capsule.anchoredPosition = new Vector2(
                Mathf.Lerp(start.x, end.x, slide),
                Mathf.Lerp(start.y, end.y, EaseOutBounce(ratio)));
            capsule.localScale = Vector3.one *
                                 Mathf.LerpUnclamped(0.55f, 1f, EaseOutBack(Mathf.Min(1f, ratio * 1.6f)));
            capsule.localRotation = Quaternion.Euler(0f, 0f, -360f * (1f - slide));
            SetImageAlpha(_outletCapsule, Mathf.Clamp01(ratio / 0.15f));
        }, token);
    }

    // 기계 전체가 쿵 하고 눌렸다 돌아오는 작은 반응이다.
    private async UniTask<bool> Punch(float amount, float duration, CancellationToken token)
    {
        try
        {
            return await Run(duration, ratio =>
            {
                float scale = 1f - Mathf.Sin(ratio * Mathf.PI) * amount * (1f - ratio * 0.5f);
                _machineRoot.localScale = new Vector3(1f + (1f - scale) * 0.5f, scale, 1f);
            }, token);
        }
        finally
        {
            _machineRoot.localScale = Vector3.one;
        }
    }

    private void PlaceHalf(Image half, Vector2 position, float scale)
    {
        half.gameObject.SetActive(true);
        half.rectTransform.anchoredPosition = position;
        half.rectTransform.localScale = Vector3.one * scale;
        half.rectTransform.localRotation = Quaternion.identity;
        SetImageAlpha(half, 1f);
    }

    private void PrepareShockwave(OpenStyle style)
    {
        _shockwave.gameObject.SetActive(style.ShockwaveAlpha > 0f);
        SetImageAlpha(_shockwave, 0f);
    }

    private void AnimateTwinkleBurst(Vector2 center, int count, float ratio, Color color)
    {
        int length = _twinkles.Length;
        for (int i = 0; i < length; i++)
        {
            Image twinkle = _twinkles[i];
            if (twinkle == null) continue;

            if (i >= count)
            {
                twinkle.gameObject.SetActive(false);
                continue;
            }

            float angle = Mathf.PI * 2f * i / Mathf.Max(1, count) + (i % 2) * 0.25f;
            float distance = Mathf.Lerp(90f, 300f + i % 3 * 60f, 1f - (1f - ratio) * (1f - ratio));
            twinkle.gameObject.SetActive(true);
            twinkle.rectTransform.anchoredPosition = center +
                                                     new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            twinkle.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.1f, 0.2f, ratio);
            twinkle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ratio * 120f + i * 25f);
            SetImageColor(twinkle, Color.Lerp(Color.white, color, 0.6f), 1f - ratio);
        }
    }

    private void HideTwinkles()
    {
        foreach (Image twinkle in _twinkles)
        {
            if (twinkle != null) twinkle.gameObject.SetActive(false);
        }
    }

    // 기계 루트가 아니라 연출 루트(결과 슬라임이 솟는 화면 가운데)를 기계 로컬 좌표로 바꾼다.
    // 기계를 어디에 놓아도 캡슐이 열리는 자리와 슬라임이 나타나는 자리가 어긋나지 않는다.
    private Vector2 GetOpenPoint()
    {
        RectTransform parent = _machineRoot.parent as RectTransform;
        if (parent == null) return Vector2.zero;

        Vector3 world = parent.TransformPoint(parent.rect.center);
        return _machineRoot.InverseTransformPoint(world);
    }

    private static OpenStyle GetStyle(EGachaRarity rarity)
    {
        return rarity switch
        {
            EGachaRarity.Special => new OpenStyle(1.2f, 2.1f, 12, 0.7f, 0.9f, 16f),
            EGachaRarity.Jackpot => new OpenStyle(1.05f, 1.9f, 12, 0.6f, 0.85f, 15f),
            EGachaRarity.Rare => new OpenStyle(0.8f, 1.55f, 9, 0.35f, 0.7f, 12f),
            EGachaRarity.Uncommon => new OpenStyle(0.6f, 1.25f, 7, 0f, 0f, 9f),
            _ => new OpenStyle(0.45f, 1f, 5, 0f, 0f, 7f),
        };
    }

    // 마지막 프레임은 항상 정확히 1로 한 번 호출해서 끝 상태가 어긋나지 않게 한다.
    private static async UniTask<bool> Run(
        float duration,
        Action<float> step,
        CancellationToken token)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow())
            {
                return true;
            }

            elapsed += Time.unscaledDeltaTime;
            step(Mathf.Clamp01(elapsed / duration));
        }

        step(1f);
        return false;
    }

    private static Vector2 Bezier(Vector2 a, Vector2 control, Vector2 b, float t)
    {
        float inverse = 1f - t;
        return inverse * inverse * a + 2f * inverse * t * control + t * t * b;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    private static float EaseOutBounce(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;
        if (t < 1f / d1) return n1 * t * t;
        if (t < 2f / d1) return n1 * (t -= 1.5f / d1) * t + 0.75f;
        if (t < 2.5f / d1) return n1 * (t -= 2.25f / d1) * t + 0.9375f;
        return n1 * (t -= 2.625f / d1) * t + 0.984375f;
    }

    private static void SetImageColor(Image image, Color color, float alpha)
    {
        if (image != null)
        {
            image.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        }
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null) return;

        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }
}
