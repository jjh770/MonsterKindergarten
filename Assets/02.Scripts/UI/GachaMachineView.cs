using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

// 뽑기 기계 그림과 그 위의 움직임을 맡는다. 티켓을 넣고, 캡슐이 섞이고, 캡슐이 배출구로 나오고,
// 눌러서 열면 등급 빛이 터진다. 무엇이 나오는지는 이미 정해져서 들어오므로 여기서는 보여 주기만 한다.
//
// 한 번에 1개 또는 여러 개(최대 _capsules 길이)를 뽑는다. 1개는 배출구 앞에서 기다리다 눌리면 화면
// 가운데로 올라와 열리고, 여러 개는 각자 정해진 자리(_slotMarkers)로 굴러가 그 자리에서 열린다.
//
// 기계 그림(_machineRoot)과 캡슐 층(_capsuleLayer)은 같은 자리에 겹쳐 놓은 형제다. 기계는 흐려지거나
// 눌려도 그 위의 캡슐과 빛은 그대로여야 하므로 한 CanvasGroup 아래에 두지 않는다. 좌표는 모두 이 둘이
// 공유하는 로컬 좌표다. 투입구, 배출구, 놓이는 자리는 씬에 표시용 자식으로 두어서 코드에 이미지
// 픽셀 값을 적지 않는다. 그림을 바꾸면 그 표시만 옮기면 된다.
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
        public readonly float HitStopSeconds;
        public readonly float BurstShake;

        public OpenStyle(
            float chargeSeconds,
            float glowScale,
            int twinkleCount,
            float flashAlpha,
            float shockwaveAlpha,
            float shakeAmplitude,
            float hitStopSeconds,
            float burstShake)
        {
            ChargeSeconds = chargeSeconds;
            GlowScale = glowScale;
            TwinkleCount = twinkleCount;
            FlashAlpha = flashAlpha;
            ShockwaveAlpha = shockwaveAlpha;
            ShakeAmplitude = shakeAmplitude;
            HitStopSeconds = hitStopSeconds;
            BurstShake = burstShake;
        }
    }

    // 캡슐 한 개와 그것을 가를 때 쓰는 위쪽·아래쪽 반쪽이다.
    [Serializable]
    private sealed class CapsuleSlot
    {
        public Image Capsule;
        public Image Top;
        public Image Bottom;
    }

    [Header("Machine")]
    [SerializeField] private RectTransform _machineRoot;
    [SerializeField] private CanvasGroup _machineGroup;
    [Tooltip("기계와 캡슐 층을 함께 감싸는 가짜 카메라입니다. 이 오브젝트의 크기와 위치를 움직여 확대, 이동, 흔들림을 만듭니다.")]
    [SerializeField] private RectTransform _camera;
    [Tooltip("캡슐, 반쪽, 빛, 티켓이 있는 층입니다. 기계 그림과 같은 자리에 겹쳐 둡니다.")]
    [SerializeField] private RectTransform _capsuleLayer;

    [Header("Pile")]
    [SerializeField] private RectTransform _pileLayer;
    [SerializeField] private Image[] _pileCapsules;
    [Tooltip("뽑는 순서대로 바닥으로 떨어지는 _pileCapsules 번호입니다. _capsules와 같은 길이로 둡니다.")]
    [SerializeField] private int[] _dropIndices;
    [Tooltip("하나만 뽑을 때 빠진 자리를 메우는 캡슐의 _pileCapsules 번호입니다. 앞 번호부터 차례로 바로 앞 번호의 자리로 굴러 내려갑니다.")]
    [SerializeField] private int[] _refillChain;
    [SerializeField] private Sprite[] _capsuleSprites;
    [SerializeField] private Sprite _rainbowCapsuleSprite;

    [Header("Ticket")]
    [SerializeField] private Image _ticketImage;
    [SerializeField] private RectTransform _ticketSlot;
    [Tooltip("튀어나올 때 버튼 그림 크기의 몇 배까지 커지는지입니다.")]
    [SerializeField, Min(1f)] private float _ticketPopScale = 1.25f;
    [Tooltip("투입구에 닿을 때 버튼 그림 크기의 몇 배인지입니다.")]
    [SerializeField, Min(0.1f)] private float _ticketEndScale = 0.5f;
    [Tooltip("티켓이 옆으로 출렁이는 폭입니다. 캔버스 단위이고 투입구에 가까울수록 줄어듭니다.")]
    [SerializeField, Min(0f)] private float _flutterSway = 80f;
    [Tooltip("날아가는 동안 좌우로 출렁이는 횟수입니다.")]
    [SerializeField, Min(0f)] private float _flutterWaves = 2.5f;
    [Tooltip("진자처럼 기울어지는 최대 각도입니다.")]
    [SerializeField, Min(0f)] private float _flutterTilt = 30f;
    [Tooltip("날아가는 동안 앞뒤로 뒤집히는 횟수입니다. 뭉치는 뒤집지 않습니다.")]
    [SerializeField, Min(0f)] private float _flutterFlips = 2f;
    [SerializeField, Min(0.01f)] private float _trailInterval = 0.04f;
    [SerializeField, Min(0.05f)] private float _trailLife = 0.45f;

    [Header("Outlet")]
    [SerializeField] private RectTransform _outlet;
    [SerializeField] private CapsuleSlot[] _capsules;
    [Tooltip("여러 개를 뽑을 때 캡슐이 놓이는 자리입니다. 연출 루트에 둔 표시이고 _capsules와 같은 순서입니다.")]
    [SerializeField] private RectTransform[] _slotMarkers;
    [Tooltip("여러 개를 뽑을 때 자리에 놓인 캡슐의 크기 배율입니다.")]
    [SerializeField, Min(0.1f)] private float _slotCapsuleScale = 1.25f;
    [Tooltip("하나만 뽑을 때 화면 가운데에 놓인 캡슐의 크기 배율입니다.")]
    [SerializeField, Min(0.1f)] private float _singleCapsuleScale = 1.8f;

    [Header("Light")]
    [SerializeField] private Image _glow;
    [SerializeField] private Image[] _twinkles;
    [SerializeField] private Image _shockwave;
    [SerializeField] private Image _flash;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float _appearDuration = 0.4f;
    [SerializeField, Min(0f)] private float _ticketPopDuration = 0.2f;
    [SerializeField, Min(0f)] private float _ticketFlyDuration = 0.9f;
    [SerializeField, Min(0f)] private float _shuffleDuration = 0.9f;
    [SerializeField, Min(0f)] private float _dropDuration = 0.45f;
    [SerializeField, Min(0f)] private float _rollOutDuration = 0.75f;
    [SerializeField, Min(0f)] private float _splitDuration = 0.5f;
    [Tooltip("여러 개를 뽑을 때 캡슐이 하나씩 떨어지는 간격입니다.")]
    [SerializeField, Min(0.05f)] private float _multiDropStagger = 0.22f;
    [Tooltip("여러 개를 뽑을 때 제자리에서 열리는 캡슐의 뜸 들이는 시간 배율입니다.")]
    [SerializeField, Range(0.2f, 1f)] private float _multiChargeRatio = 0.6f;

    [Header("Camera")]
    [Tooltip("초점이 화면 가운데로 끌려오는 정도입니다. 1이면 초점을 제자리에 둔 채 확대하고, 클수록 초점이 가운데로 옵니다.")]
    [SerializeField, Min(0f)] private float _cameraPull = 1.35f;
    [SerializeField, Min(1f)] private float _ticketZoom = 1.12f;
    [SerializeField, Min(1f)] private float _shuffleZoom = 1.32f;
    [SerializeField, Min(1f)] private float _chargeZoom = 1.45f;
    [Tooltip("여러 개를 뽑을 때 캡슐을 하나 열면서 당기는 확대 배율입니다. 캡슐이 자기 자리에 머물도록 초점을 제자리에 둡니다.")]
    [SerializeField, Min(1f)] private float _multiChargeZoom = 1.25f;

    [Header("Look")]
    [Tooltip("캡슐을 열 때 커지는 배율입니다.")]
    [SerializeField, Min(1f)] private float _openScale = 2.4f;
    [Tooltip("캡슐 그림에서 이음선이 위에서 몇 퍼센트 아래에 있는지입니다. 반쪽으로 가를 때 씁니다.")]
    [SerializeField, Range(0.3f, 0.7f)] private float _seamFraction = 0.56f;
    [SerializeField, Min(0f)] private float _glowBaseScale = 1f;
    [Tooltip("여러 개를 뽑아 바닥이 비면 남은 캡슐이 한 줄만큼 내려앉는 거리입니다.")]
    [SerializeField, Min(0f)] private float _collapseDrop = 85f;

    private const float WaitSfxFadeOutSeconds = 0.2f;
    private const float RefillSeconds = 0.4f;
    private const float RefillStagger = 0.18f;
    private const float PileHop = 38f;

    private float[] _trailBirth;
    private Vector2[] _trailPosition;
    private int _trailNext;

    private Vector2[] _pileHome;
    private Vector2[] _pileAuthoredHome;
    private int[] _pileSiblingIndex;
    private Vector2 _rootRest;

    // 한 번의 뽑기 상태.
    private int _count;
    private Sprite[] _chosenSprites;
    private bool[] _opened;
    private Vector2[] _slotLocal;
    private Vector2[] _capsuleRest;
    private Button[] _capsuleButtons;
    private int _tappedIndex = -1;

    // 카메라 상태. 확대와 초점은 목표를 향해 Update가 움직이고, 흔들림은 그 위에 얹힌다.
    private float _cameraZoom = 1f;
    private Vector2 _cameraFocus;
    private float _cameraPullNow;
    private float _cameraFromZoom;
    private float _cameraToZoom;
    private Vector2 _cameraFromFocus;
    private Vector2 _cameraToFocus;
    private float _cameraFromPull;
    private float _cameraToPull;
    private float _cameraElapsed;
    private float _cameraDuration;
    private Func<float, float> _cameraEase;
    private bool _cameraMoving;
    private float _holdShake;
    private float _burstShake;
    private float _burstShakeSeconds;
    private float _burstShakeElapsed;
    private bool _isReady;

    public int Count => _count;

    private void Awake()
    {
        if (_machineRoot == null || _machineGroup == null || _camera == null ||
            _capsuleLayer == null || _pileLayer == null ||
            _pileCapsules == null || _pileCapsules.Length == 0 ||
            _capsuleSprites == null || _capsuleSprites.Length == 0 ||
            _rainbowCapsuleSprite == null || _ticketImage == null || _ticketSlot == null ||
            _outlet == null || !IsValidCapsules() ||
            !IsValidDropIndices() || !IsValidRefillChain() || _glow == null ||
            _twinkles == null || _twinkles.Length == 0 || _shockwave == null || _flash == null)
        {
            Debug.LogError("뽑기 기계 연출의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _pileHome = new Vector2[_pileCapsules.Length];
        _pileAuthoredHome = new Vector2[_pileCapsules.Length];
        _pileSiblingIndex = new int[_pileCapsules.Length];
        for (int i = 0; i < _pileCapsules.Length; i++)
        {
            _pileAuthoredHome[i] = _pileCapsules[i].rectTransform.anchoredPosition;
            _pileSiblingIndex[i] = _pileCapsules[i].transform.GetSiblingIndex();
        }

        int slots = _capsules.Length;
        _chosenSprites = new Sprite[slots];
        _opened = new bool[slots];
        _slotLocal = new Vector2[slots];
        _capsuleRest = new Vector2[slots];
        _capsuleButtons = new Button[slots];
        for (int i = 0; i < slots; i++)
        {
            int index = i;
            _capsuleButtons[i] = _capsules[i].Capsule.GetComponent<Button>();
            if (_capsuleButtons[i] != null)
            {
                _capsuleButtons[i].onClick.AddListener(() => _tappedIndex = index);
            }

            _capsules[i].Top.fillAmount = _seamFraction;
            _capsules[i].Bottom.fillAmount = 1f - _seamFraction;
        }

        _trailBirth = new float[_twinkles.Length];
        _trailPosition = new Vector2[_twinkles.Length];
        _rootRest = _machineRoot.anchoredPosition;
        _cameraPullNow = _cameraPull;
        _isReady = true;
    }

    // 새 연출을 시작하기 전에 모든 조각을 처음 모습으로 되돌린다.
    public void Prepare()
    {
        if (!_isReady) return;

        _machineRoot.gameObject.SetActive(true);
        _machineRoot.anchoredPosition = _rootRest;
        _machineRoot.localScale = Vector3.one;
        _machineRoot.localRotation = Quaternion.identity;
        _machineGroup.alpha = 0f;

        _count = 0;
        _tappedIndex = -1;
        ResetCamera();
        for (int i = 0; i < _pileCapsules.Length; i++)
        {
            // 지난번에 캡슐이 메운 자리와 더미 맨 앞으로 꺼냈던 순서를 처음대로 돌린다.
            _pileHome[i] = _pileAuthoredHome[i];
            Image capsule = _pileCapsules[i];
            capsule.transform.SetSiblingIndex(_pileSiblingIndex[i]);
            capsule.gameObject.SetActive(true);
            capsule.rectTransform.anchoredPosition = _pileHome[i];
            capsule.rectTransform.localRotation = Quaternion.identity;
            capsule.rectTransform.localScale = Vector3.one;
        }

        for (int i = 0; i < _capsules.Length; i++)
        {
            _opened[i] = false;
            _capsules[i].Capsule.gameObject.SetActive(false);
            _capsules[i].Top.gameObject.SetActive(false);
            _capsules[i].Bottom.gameObject.SetActive(false);
            if (_capsuleButtons[i] != null) _capsuleButtons[i].interactable = false;
        }

        _ticketImage.gameObject.SetActive(false);
        _glow.gameObject.SetActive(false);
        _shockwave.gameObject.SetActive(false);
        SetImageAlpha(_flash, 0f);
        HideTwinkles();
    }

    // 결과가 정해진 뒤 불린다. 특별한 결과면 그 캡슐만 무지개이고, 아니면 여섯 색 중 하나다.
    // 색은 등급과 관계없이 정해서 겉모습으로 결과를 읽지 못하게 한다.
    public void BeginPull(IReadOnlyList<bool> isSpecial)
    {
        if (!_isReady) return;

        _count = Mathf.Min(isSpecial.Count, _capsules.Length);
        for (int i = 0; i < _count; i++)
        {
            Sprite sprite = isSpecial[i]
                ? _rainbowCapsuleSprite
                : _capsuleSprites[UnityEngine.Random.Range(0, _capsuleSprites.Length)];
            _chosenSprites[i] = sprite;
            _pileCapsules[_dropIndices[i]].sprite = sprite;
            _capsules[i].Capsule.sprite = sprite;
            _capsules[i].Top.sprite = sprite;
            _capsules[i].Bottom.sprite = sprite;
            // 하나만 뽑으면 화면 가운데, 여러 개를 뽑으면 정해 둔 자리다.
            _slotLocal[i] = _count > 1 && i < _slotMarkers.Length && _slotMarkers[i] != null
                ? (Vector2)_capsuleLayer.InverseTransformPoint(_slotMarkers[i].position)
                : GetOpenPoint();
        }
    }

    public void Hide()
    {
        if (_machineRoot != null) _machineRoot.gameObject.SetActive(false);
    }

    public void SetMachineAlpha(float alpha)
    {
        if (_isReady) _machineGroup.alpha = Mathf.Clamp01(alpha);
    }

    public UniTask<bool> FadeMachineTo(float target, float seconds, CancellationToken token)
    {
        float from = _machineGroup.alpha;
        return Run(seconds, ratio => _machineGroup.alpha = Mathf.Lerp(from, target, ratio), token);
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

    // 버튼을 기다리는 동안 캡슐 더미가 살짝 흔들려 살아 있게 한다.
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

    // 버튼의 티켓 그림이 튀어나와 바람에 날리듯 날아간다. 곡선 위에서 옆으로 출렁이고, 진자처럼 기울고,
    // 앞뒤로 뒤집히다가 투입구에 가까워질수록 모두 잦아들어 정확히 슬롯에 눕는다. 지나간 자리에는 반짝이가
    // 흩날린다. 뭉치(asStack)는 뒤집히지 않고, 빨려 들어갈 때 기계가 다섯 번 흔들린다.
    // onLaunched는 튀어나오기를 마치고 날아가기 시작하는 순간에 불린다. 원래 그림을 감추는 데 쓴다.
    public async UniTask<bool> InsertTicket(
        RectTransform source,
        Sprite sprite,
        bool asStack,
        Action onLaunched,
        CancellationToken token)
    {
        AudioManager.Instance?.PlaySFX(EAudioSfx.UIClick);
        RectTransform ticket = _ticketImage.rectTransform;
        Vector2 slot = _ticketSlot.anchoredPosition;
        Vector2 start = source != null
            ? (Vector2)_capsuleLayer.InverseTransformPoint(source.position)
            : slot + Vector2.down * 500f;
        Vector2 control = new(
            (start.x + slot.x) * 0.5f,
            Mathf.Max(start.y, slot.y) + 160f);
        float flips = asStack ? 0f : _flutterFlips;
        float sway = asStack ? _flutterSway * 0.6f : _flutterSway;

        _ticketImage.sprite = sprite;
        ticket.sizeDelta = source != null ? source.rect.size : new Vector2(110f, 110f);
        ticket.gameObject.SetActive(true);
        ticket.localScale = Vector3.one;
        ticket.localRotation = Quaternion.identity;
        ticket.anchoredPosition = start;
        SetImageAlpha(_ticketImage, 1f);

        // 버튼에서 위로 톡 튀어나오며 커진다.
        bool cancelled = await Run(_ticketPopDuration, ratio =>
        {
            float pop = EaseOutBack(ratio);
            ticket.anchoredPosition = start + Vector2.up * (pop * 36f);
            ticket.localScale = Vector3.one * Mathf.LerpUnclamped(1f, _ticketPopScale, pop);
            ticket.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(ratio * Mathf.PI) * -12f);
        }, token);
        if (cancelled) return true;

        onLaunched?.Invoke();
        Vector2 flightStart = ticket.anchoredPosition;
        control = new Vector2(
            (flightStart.x + slot.x) * 0.5f,
            Mathf.Max(flightStart.y, slot.y) + 160f);
        MoveCamera(_ticketZoom, ToCamera(_ticketSlot.anchoredPosition), _ticketFlyDuration, OutExpo);

        ResetTrail();
        float nextEmit = 0f;
        cancelled = await Run(_ticketFlyDuration, ratio =>
        {
            float elapsed = ratio * _ticketFlyDuration;
            float eased = Mathf.SmoothStep(0f, 1f, ratio);
            float calm = 1f - eased;

            // 곡선의 접선에 수직인 쪽으로 출렁인다. 잦아드는 정도는 제곱으로 줄여 끝에서 확실히 멈춘다.
            Vector2 tangent = 2f * calm * (control - flightStart) + 2f * eased * (slot - control);
            Vector2 side = tangent.sqrMagnitude > 0.0001f
                ? new Vector2(-tangent.y, tangent.x).normalized
                : Vector2.right;
            float phase = eased * _flutterWaves * Mathf.PI * 2f;
            ticket.anchoredPosition = Bezier(flightStart, control, slot, eased) +
                                      side * (Mathf.Sin(phase) * sway * calm * calm);
            ticket.localRotation = Quaternion.Euler(
                0f, 0f, Mathf.Sin(phase + 0.8f) * _flutterTilt * calm);

            // 앞뒤로 뒤집힐 때 폭이 좁아졌다 돌아온다. 끝으로 갈수록 뒤집힘은 멎고 정면을 향한다.
            float baseScale = Mathf.Lerp(_ticketPopScale, _ticketEndScale, eased);
            float flip = Mathf.Abs(Mathf.Cos(eased * flips * Mathf.PI));
            float settle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1f, eased));
            float width = flips <= 0f
                ? 1f
                : Mathf.Lerp(Mathf.Lerp(0.25f, 1f, flip), 1f, settle);
            ticket.localScale = new Vector3(baseScale * width, baseScale, 1f);

            while (elapsed >= nextEmit)
            {
                EmitTrail(ticket.anchoredPosition, nextEmit);
                nextEmit += _trailInterval;
            }

            UpdateTrail(elapsed);
        }, token);
        if (cancelled)
        {
            HideTwinkles();
            return true;
        }

        // 투입구로 얇아지며 빨려 들어간다.
        cancelled = await Run(0.16f, ratio =>
        {
            ticket.localScale = new Vector3(
                _ticketEndScale,
                Mathf.Lerp(_ticketEndScale, 0f, ratio),
                1f);
            UpdateTrail(_ticketFlyDuration + ratio * 0.16f);
        }, token);
        ticket.gameObject.SetActive(false);
        HideTwinkles();
        if (cancelled) return true;

        if (!asStack) return await Punch(0.04f, 0.28f, token);

        // 뭉치는 한 장씩 들어가듯 기계가 다섯 번 연달아 흔들린다.
        for (int i = 0; i < 5; i++)
        {
            AudioManager.Instance?.PlaySFX(EAudioSfx.UIClick);
            BurstShake(4f + i * 2f, 0.14f);
            if (await Punch(0.03f + i * 0.004f, 0.13f, token)) return true;
        }

        return false;
    }

    // 캡슐이 섞이고, 뽑힌 만큼 바닥으로 떨어져 가려지고, 배출구에서 굴러 나온다.
    public async UniTask<bool> Dispense(CancellationToken token)
    {
        float shuffleSeconds = _count > 1 ? _shuffleDuration * 1.3f : _shuffleDuration;
        AudioManager.Instance?.PlayLoopingSFX(EAudioSfx.GachaWait);
        MoveCamera(_shuffleZoom, ToCamera(_pileLayer.anchoredPosition), 0.45f, OutExpo);
        _holdShake = _count > 1 ? 6f : 4f;
        bool cancelled = await Run(shuffleSeconds, ratio =>
        {
            float elapsed = ratio * shuffleSeconds;
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
        _holdShake = 0f;
        if (cancelled) return true;

        _machineRoot.anchoredPosition = _rootRest;
        if (await Settle(token)) return true;

        return _count == 1
            ? await DispenseOne(token)
            : await DispenseMany(token);
    }

    // 캡슐을 눌러야 한다고 알려 주는 흔들림이다. 가끔씩만 좌우로 뛰고 흔들려서, 계속 떨리는 것보다
    // 눈에 띄고 거슬리지 않는다. 캡슐마다 주기가 달라 한꺼번에 움직이지 않는다.
    public void IdleCapsules(float elapsed)
    {
        for (int i = 0; i < _count; i++)
        {
            if (_opened[i] || !_capsules[i].Capsule.gameObject.activeSelf) continue;

            RectTransform rect = _capsules[i].Capsule.rectTransform;
            float period = 2.4f + i * 0.45f;
            float time = Mathf.Repeat(elapsed + i * 0.9f, period);
            float amplitude = time < 0.7f ? 1f - time / 0.7f : 0f;
            rect.anchoredPosition = _capsuleRest[i] + new Vector2(
                Mathf.Sin(time * 30f) * amplitude * 8f,
                Mathf.Abs(Mathf.Sin(time * 17f)) * amplitude * 12f);
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(time * 34f) * amplitude * 9f);
        }

        // 하나만 뽑을 때는 캡슐 뒤가 은은히 빛나 시선을 끈다.
        if (_count == 1 && !_opened[0])
        {
            _glow.gameObject.SetActive(true);
            _glow.rectTransform.anchoredPosition = _capsuleRest[0];
            _glow.rectTransform.localScale = Vector3.one * (_glowBaseScale * 0.9f);
            SetImageColor(_glow, Color.white, 0.22f + Mathf.Sin(elapsed * 3.2f) * 0.1f);
        }
    }

    public bool TryConsumeCapsuleTap(out int index)
    {
        index = _tappedIndex;
        _tappedIndex = -1;
        return index >= 0;
    }

    // 캡슐 하나를 여는 동안 다른 캡슐이 눌리지 않게 하고, 끝나면 아직 안 연 캡슐만 다시 누르게 한다.
    public void SetCapsulesInteractable(bool interactable)
    {
        for (int i = 0; i < _count; i++)
        {
            if (_capsuleButtons[i] == null) continue;

            _capsuleButtons[i].interactable = interactable && !_opened[i];
        }

        if (!interactable) _tappedIndex = -1;
    }

    // 캡슐을 흔들다가 반쪽으로 갈라 빛을 터뜨린다. 하나만 뽑을 때는 먼저 화면 가운데로 집어 올리고,
    // 여러 개를 뽑을 때는 자기 자리에서 바로 연다. 카메라는 캡슐이 제자리에 머물도록 초점을 고정한다.
    public async UniTask<bool> Open(
        int index,
        EGachaRarity rarity,
        Color color,
        CancellationToken token)
    {
        OpenStyle style = GetStyle(rarity);
        bool rainbow = rarity == EGachaRarity.Special;
        bool isMulti = _count > 1;
        CapsuleSlot slot = _capsules[index];
        RectTransform capsule = slot.Capsule.rectTransform;
        Vector2 open = _capsuleRest[index];
        float baseScale = isMulti ? _slotCapsuleScale : _singleCapsuleScale;
        float chargeSeconds = isMulti ? style.ChargeSeconds * _multiChargeRatio : style.ChargeSeconds;
        float chargeZoom = isMulti ? _multiChargeZoom : _chargeZoom;
        bool cancelled;

        // 캡슐은 이미 자기 자리에 있다. 집어 올리지 않고 그 자리에서 흔들다가 가른다.
        capsule.localRotation = Quaternion.identity;
        _glow.gameObject.SetActive(true);

        // 모이는 동안 카메라가 점점 빨려 들어가고 화면이 떨린다. 마지막에 가장 가파르게 당겨진다.
        MoveCamera(chargeZoom, ToCamera(open), chargeSeconds, InExpo, 1f);
        cancelled = await Run(chargeSeconds, ratio =>
        {
            float elapsed = ratio * chargeSeconds;
            _holdShake = Mathf.Lerp(0f, style.ShakeAmplitude * 0.5f, ratio * ratio);
            float amplitude = Mathf.Lerp(2f, style.ShakeAmplitude, ratio * ratio);
            capsule.anchoredPosition = open + new Vector2(
                Mathf.Sin(elapsed * 55f) * amplitude,
                Mathf.Cos(elapsed * 47f) * amplitude * 0.6f);
            capsule.localRotation = Quaternion.Euler(
                0f, 0f, Mathf.Sin(elapsed * 40f) * amplitude * 0.9f);
            capsule.localScale = Vector3.one * (baseScale * (1f + 0.1f * ratio * ratio));

            Color tint = rainbow ? RainbowTint.Pure() : color;
            _glow.rectTransform.anchoredPosition = open;
            _glow.rectTransform.localScale = Vector3.one *
                                             (_glowBaseScale * Mathf.Lerp(0.7f, style.GlowScale * 0.8f, ratio));
            SetImageColor(_glow, tint, Mathf.Lerp(0.25f, 0.9f, ratio));
        }, token);
        if (cancelled) return true;

        // 좋은 결과일수록 터지기 직전에 한 박자 멈춘다. 멈춘 동안 화면은 가장 당겨진 채 정지한다.
        _holdShake = 0f;
        if (style.HitStopSeconds > 0f && await Pause(style.HitStopSeconds, token)) return true;

        // 가르는 순간: 통짜 캡슐을 감추고 같은 자리에 반쪽 둘을 놓는다.
        AudioManager.Instance?.PlaySFX(EAudioSfx.FeatureUnlock);
        MoveCamera(1f, Vector2.zero, 0.45f, EaseOutBack, 1f);
        BurstShake(style.BurstShake, 0.55f);
        float scale = capsule.localScale.x;
        capsule.gameObject.SetActive(false);
        PlaceHalf(slot.Top, open, scale);
        PlaceHalf(slot.Bottom, open, scale);
        PrepareShockwave(style);
        float flashPeak = style.FlashAlpha;

        cancelled = await Run(_splitDuration, ratio =>
        {
            float outward = 1f - (1f - ratio) * (1f - ratio);
            float fade = 1f - ratio * ratio;
            Color tint = rainbow ? RainbowTint.Pure() : color;
            float spread = isMulti ? 0.6f : 1f;

            slot.Top.rectTransform.anchoredPosition = open + new Vector2(-34f, 170f) * (outward * spread);
            slot.Top.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 20f * outward);
            slot.Bottom.rectTransform.anchoredPosition = open + new Vector2(34f, -170f) * (outward * spread);
            slot.Bottom.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -20f * outward);
            SetImageAlpha(slot.Top, fade);
            SetImageAlpha(slot.Bottom, fade);

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

        slot.Top.gameObject.SetActive(false);
        slot.Bottom.gameObject.SetActive(false);
        _glow.gameObject.SetActive(false);
        _shockwave.gameObject.SetActive(false);
        SetImageAlpha(_flash, 0f);
        HideTwinkles();
        _opened[index] = true;
        return cancelled;
    }

    // 슬라임이 솟아오르는 동안 빛이 계속 새로 퍼지게 한다. 한 번 터지고 끝나면 허전하다.
    // 하나만 뽑을 때만 쓴다. 여러 개를 뽑을 때는 빛 조각을 열리는 캡슐이 쓰고 있다.
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

    // 하나만 뽑을 때: 쿵 하고 떨어지면 빈자리를 위 캡슐이 메운다. 그 뒤 캡슐이 배출구에서 화면 가운데로
    // 날아오는 동안 기계는 뒤로 물러나고, 카메라는 처음 시점으로 돌아와 캡슐만 남는다.
    private async UniTask<bool> DispenseOne(CancellationToken token)
    {
        if (await DropPileCapsule(_dropIndices[0], 0.18f, _dropDuration, token)) return true;
        AudioManager.Instance?.PlaySFX(EAudioSfx.SlimeLand);

        MoveCamera(1f, Vector2.zero, 0.6f, InOutQuint, 1f);
        BurstShake(10f, 0.3f);
        (bool punchCancelled, bool refillCancelled) = await UniTask.WhenAll(
            Punch(0.03f, 0.25f, token),
            Refill(token));
        if (punchCancelled || refillCancelled) return true;

        (bool rollCancelled, bool fadeCancelled) = await UniTask.WhenAll(
            RollOut(0, _slotLocal[0], token),
            FadeMachineTo(0f, 0.6f, token));
        return rollCancelled || fadeCancelled;
    }

    // 여러 개를 뽑을 때: 아래 칸 캡슐부터 하나씩 연달아 떨어져 각자 자리로 굴러 나오고, 바닥이 빈 더미는
    // 한 줄만큼 내려앉는다. 카메라는 캡슐이 놓이는 자리가 어긋나지 않게 처음 시점으로 돌아온다.
    private async UniTask<bool> DispenseMany(CancellationToken token)
    {
        MoveCamera(1f, Vector2.zero, 0.6f, InOutQuint, 1f);

        UniTask<bool>[] drops = new UniTask<bool>[_count];
        for (int i = 0; i < _count; i++)
        {
            drops[i] = DropAndRollOut(i, token);
        }

        UniTask<bool> collapse = CollapsePile(token);
        bool[] dropResults = await UniTask.WhenAll(drops);
        bool collapseCancelled = await collapse;
        if (collapseCancelled) return true;

        foreach (bool dropCancelled in dropResults)
        {
            if (dropCancelled) return true;
        }

        return false;
    }

    private async UniTask<bool> DropAndRollOut(int index, CancellationToken token)
    {
        float delay = index * _multiDropStagger;
        if (delay > 0f && await Pause(delay, token)) return true;

        if (await DropPileCapsule(_dropIndices[index], 0.1f, 0.3f, token)) return true;
        AudioManager.Instance?.PlaySFX(EAudioSfx.SlimeLand);
        BurstShake(6f, 0.2f);
        return await RollOut(index, _slotLocal[index], token);
    }

    // 남은 캡슐이 비워진 바닥으로 한 줄씩 내려앉는다. 마지막 캡슐이 떨어진 뒤에 시작한다.
    private async UniTask<bool> CollapsePile(CancellationToken token)
    {
        float wait = (_count - 1) * _multiDropStagger + 0.4f;
        if (await Pause(wait, token)) return true;

        List<int> remaining = new();
        for (int i = 0; i < _pileCapsules.Length; i++)
        {
            bool removed = false;
            for (int k = 0; k < _count; k++)
            {
                if (_dropIndices[k] == i) removed = true;
            }

            if (!removed) remaining.Add(i);
        }

        Vector2[] from = new Vector2[remaining.Count];
        Vector2[] to = new Vector2[remaining.Count];
        for (int i = 0; i < remaining.Count; i++)
        {
            int index = remaining[i];
            from[i] = _pileCapsules[index].rectTransform.anchoredPosition;
            to[i] = _pileHome[index] + Vector2.down * _collapseDrop;
            _pileHome[index] = to[i];
        }

        return await Run(0.5f, ratio =>
        {
            for (int i = 0; i < remaining.Count; i++)
            {
                RectTransform rect = _pileCapsules[remaining[i]].rectTransform;
                float local = Mathf.Clamp01(ratio * 1.25f - i * 0.04f);
                rect.anchoredPosition = new Vector2(
                    Mathf.Lerp(from[i].x, to[i].x, Mathf.SmoothStep(0f, 1f, local)),
                    Mathf.Lerp(from[i].y, to[i].y, EaseOutBounce(local)));
                rect.localRotation = Quaternion.Slerp(
                    rect.localRotation, Quaternion.identity, local);
            }
        }, token);
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
    private async UniTask<bool> DropPileCapsule(
        int pileIndex,
        float hopSeconds,
        float fallSeconds,
        CancellationToken token)
    {
        RectTransform chosen = _pileCapsules[pileIndex].rectTransform;
        // 떨어지는 동안 이웃 캡슐에 가리지 않도록 더미의 맨 앞으로 꺼낸다.
        chosen.SetAsLastSibling();
        Vector2 from = chosen.anchoredPosition;
        Vector2 top = from + Vector2.up * PileHop;
        float fallY = -_pileLayer.rect.height * 0.5f - chosen.rect.height;

        bool cancelled = await Run(hopSeconds, ratio =>
        {
            float eased = 1f - (1f - ratio) * (1f - ratio);
            chosen.anchoredPosition = Vector2.Lerp(from, top, eased);
            chosen.localRotation = Quaternion.Euler(0f, 0f, 12f * eased);
        }, token);
        if (cancelled) return true;

        cancelled = await Run(fallSeconds, ratio =>
        {
            float eased = ratio * ratio;
            chosen.anchoredPosition = new Vector2(top.x, Mathf.Lerp(top.y, fallY, eased));
            chosen.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(12f, 200f, eased));
        }, token);
        chosen.gameObject.SetActive(false);
        return cancelled;
    }

    // 배출구 안쪽에서 작게 나타나 호를 그리며 굴러 놓일 자리까지 날아가 선다.
    private async UniTask<bool> RollOut(
        int index,
        Vector2 end,
        CancellationToken token)
    {
        RectTransform capsule = _capsules[index].Capsule.rectTransform;
        Vector2 start = _outlet.anchoredPosition;
        float restScale = _count > 1 ? _slotCapsuleScale : _singleCapsuleScale;
        float seconds = _rollOutDuration * 0.8f;
        capsule.gameObject.SetActive(true);
        capsule.anchoredPosition = start;
        capsule.localScale = Vector3.one * 0.55f;
        SetImageAlpha(_capsules[index].Capsule, 0f);
        AudioManager.Instance?.PlaySFX(EAudioSfx.SlimeBounce);

        bool cancelled = await Run(seconds, ratio =>
        {
            float slide = Mathf.SmoothStep(0f, 1f, ratio);
            float y = Mathf.Lerp(start.y, end.y, slide) + Mathf.Sin(ratio * Mathf.PI) * 140f;
            capsule.anchoredPosition = new Vector2(Mathf.Lerp(start.x, end.x, slide), y);
            capsule.localScale = Vector3.one *
                                 Mathf.LerpUnclamped(0.55f, restScale, EaseOutBack(Mathf.Min(1f, ratio * 1.6f)));
            capsule.localRotation = Quaternion.Euler(0f, 0f, -360f * (1f - slide));
            SetImageAlpha(_capsules[index].Capsule, Mathf.Clamp01(ratio / 0.15f));
        }, token);

        _capsuleRest[index] = end;
        if (_capsuleButtons[index] != null && !cancelled)
        {
            _capsuleButtons[index].interactable = true;
        }

        return cancelled;
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

    // 떨어진 캡슐이 비운 자리를 위의 캡슐이 굴러 내려와 메운다. 앞 번호부터 차례로 바로 앞 번호의 자리로 내려가서
    // 맨 위의 한 칸만 비는 자연스러운 더미가 된다.
    private UniTask<bool> Refill(CancellationToken token)
    {
        int count = _refillChain.Length;
        if (count == 0) return UniTask.FromResult(false);

        Vector2[] from = new Vector2[count];
        Vector2[] to = new Vector2[count];
        Vector2 hole = _pileHome[_dropIndices[0]];
        for (int i = 0; i < count; i++)
        {
            int index = _refillChain[i];
            from[i] = _pileCapsules[index].rectTransform.anchoredPosition;
            to[i] = hole;
            hole = _pileHome[index];
            _pileHome[index] = to[i];
        }

        float total = RefillSeconds + RefillStagger * (count - 1);
        return Run(total, ratio =>
        {
            float elapsed = ratio * total;
            for (int i = 0; i < count; i++)
            {
                float local = Mathf.Clamp01((elapsed - i * RefillStagger) / RefillSeconds);
                RectTransform rect = _pileCapsules[_refillChain[i]].rectTransform;
                rect.anchoredPosition = new Vector2(
                    Mathf.Lerp(from[i].x, to[i].x, Mathf.SmoothStep(0f, 1f, local)),
                    Mathf.Lerp(from[i].y, to[i].y, EaseOutBounce(local)));
                rect.localRotation = Quaternion.Euler(
                    0f, 0f, Mathf.Sin(local * Mathf.PI) * (i % 2 == 0 ? 12f : -12f));
            }
        }, token);
    }

    private bool IsValidCapsules()
    {
        if (_capsules == null || _capsules.Length == 0) return false;

        foreach (CapsuleSlot slot in _capsules)
        {
            if (slot == null || slot.Capsule == null || slot.Top == null || slot.Bottom == null)
            {
                return false;
            }
        }

        return _slotMarkers != null && _slotMarkers.Length >= _capsules.Length;
    }

    private bool IsValidDropIndices()
    {
        if (_dropIndices == null || _capsules == null || _dropIndices.Length != _capsules.Length)
        {
            return false;
        }

        for (int i = 0; i < _dropIndices.Length; i++)
        {
            if (_dropIndices[i] < 0 || _dropIndices[i] >= _pileCapsules.Length) return false;

            for (int j = 0; j < i; j++)
            {
                if (_dropIndices[j] == _dropIndices[i]) return false;
            }
        }

        return true;
    }

    private bool IsValidRefillChain()
    {
        if (_refillChain == null) return false;

        foreach (int index in _refillChain)
        {
            if (index < 0 || index >= _pileCapsules.Length) return false;

            foreach (int dropped in _dropIndices)
            {
                if (index == dropped) return false;
            }
        }

        return true;
    }

    // 카메라 좌표계는 기계 루트가 놓인 자리를 원점으로 한다. 기계 로컬 점을 그 좌표로 옮긴다.
    private Vector2 ToCamera(Vector2 machineLocal)
    {
        return _rootRest + machineLocal;
    }

    private void MoveCamera(
        float zoom,
        Vector2 focus,
        float duration,
        Func<float, float> ease,
        float pull = -1f)
    {
        _cameraFromZoom = _cameraZoom;
        _cameraFromFocus = _cameraFocus;
        _cameraFromPull = _cameraPullNow;
        _cameraToZoom = zoom;
        _cameraToFocus = focus;
        _cameraToPull = pull < 0f ? _cameraPull : pull;
        _cameraElapsed = 0f;
        _cameraDuration = duration;
        _cameraEase = ease;
        _cameraMoving = true;
    }

    private void BurstShake(float amplitude, float seconds)
    {
        if (amplitude <= 0f) return;

        _burstShake = amplitude;
        _burstShakeSeconds = seconds;
        _burstShakeElapsed = 0f;
    }

    private void ResetCamera()
    {
        _cameraMoving = false;
        _cameraZoom = 1f;
        _cameraFocus = Vector2.zero;
        _cameraPullNow = _cameraPull;
        _holdShake = 0f;
        _burstShake = 0f;
        _burstShakeSeconds = 0f;
        _burstShakeElapsed = 0f;
        ApplyCamera(0f);
    }

    private void Update()
    {
        if (!_isReady) return;

        float delta = Time.unscaledDeltaTime;
        if (_cameraMoving)
        {
            _cameraElapsed += delta;
            float ratio = _cameraDuration <= 0f ? 1f : Mathf.Clamp01(_cameraElapsed / _cameraDuration);
            float eased = _cameraEase(ratio);
            _cameraZoom = Mathf.LerpUnclamped(_cameraFromZoom, _cameraToZoom, eased);
            _cameraFocus = Vector2.LerpUnclamped(_cameraFromFocus, _cameraToFocus, eased);
            _cameraPullNow = Mathf.LerpUnclamped(_cameraFromPull, _cameraToPull, eased);
            if (ratio >= 1f) _cameraMoving = false;
        }

        float shake = _holdShake;
        if (_burstShakeElapsed < _burstShakeSeconds)
        {
            _burstShakeElapsed += delta;
            float left = 1f - Mathf.Clamp01(_burstShakeElapsed / _burstShakeSeconds);
            shake = Mathf.Max(shake, _burstShake * left * left);
        }

        ApplyCamera(shake);
    }

    // 초점이 제자리에 머물도록 확대한 뒤, _cameraPullNow만큼 초점을 화면 가운데로 끌어온다.
    private void ApplyCamera(float shake)
    {
        Vector2 pan = -_cameraFocus * ((_cameraZoom - 1f) * _cameraPullNow);
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

        _camera.localScale = Vector3.one * _cameraZoom;
        _camera.anchoredPosition = pan + offset;
        _camera.localRotation = Quaternion.Euler(0f, 0f, roll);
    }

    private static async UniTask<bool> Pause(float seconds, CancellationToken token) =>
        await UniTask.Delay(TimeSpan.FromSeconds(seconds), DelayType.UnscaledDeltaTime,
            cancellationToken: token).SuppressCancellationThrow();

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

    private void ResetTrail()
    {
        for (int i = 0; i < _trailBirth.Length; i++)
        {
            _trailBirth[i] = -999f;
        }

        _trailNext = 0;
    }

    private void EmitTrail(Vector2 position, float time)
    {
        int index = _trailNext;
        _trailNext = (_trailNext + 1) % _twinkles.Length;
        _trailBirth[index] = time;
        _trailPosition[index] = position + new Vector2(
            UnityEngine.Random.Range(-28f, 28f),
            UnityEngine.Random.Range(-28f, 28f));
    }

    // 반짝이는 태어난 자리에 남아 천천히 아래로 가라앉으며 작아지고 옅어진다.
    private void UpdateTrail(float time)
    {
        for (int i = 0; i < _twinkles.Length; i++)
        {
            Image twinkle = _twinkles[i];
            if (twinkle == null) continue;

            float age = time - _trailBirth[i];
            if (age < 0f || age > _trailLife)
            {
                twinkle.gameObject.SetActive(false);
                continue;
            }

            float life = age / _trailLife;
            twinkle.gameObject.SetActive(true);
            twinkle.rectTransform.anchoredPosition = _trailPosition[i] + Vector2.down * (life * 40f);
            twinkle.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.9f, 0.15f, life);
            twinkle.rectTransform.localRotation = Quaternion.Euler(0f, 0f, life * 160f + i * 30f);
            SetImageColor(twinkle, new Color(1f, 0.95f, 0.65f), (1f - life) * 0.9f);
        }
    }

    private void HideTwinkles()
    {
        foreach (Image twinkle in _twinkles)
        {
            if (twinkle != null) twinkle.gameObject.SetActive(false);
        }
    }

    // 카메라가 움직이지 않을 때 화면 가운데에 오는 캡슐 층 로컬 좌표다. 카메라는 이 점으로 돌아와 멈추므로
    // 캡슐이 열리는 자리와 슬라임이 솟는 자리가 어긋나지 않는다.
    private Vector2 GetOpenPoint()
    {
        return -_rootRest;
    }

    private static OpenStyle GetStyle(EGachaRarity rarity)
    {
        return rarity switch
        {
            EGachaRarity.Special => new OpenStyle(1.2f, 2.1f, 12, 0.7f, 0.9f, 16f, 0.12f, 36f),
            EGachaRarity.Jackpot => new OpenStyle(1.05f, 1.9f, 12, 0.6f, 0.85f, 15f, 0.1f, 30f),
            EGachaRarity.Rare => new OpenStyle(0.8f, 1.55f, 9, 0.35f, 0.7f, 12f, 0.06f, 18f),
            EGachaRarity.Uncommon => new OpenStyle(0.6f, 1.25f, 7, 0f, 0f, 9f, 0f, 8f),
            _ => new OpenStyle(0.45f, 1f, 5, 0f, 0f, 7f, 0f, 0f),
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

    private static float OutExpo(float t)
    {
        return t >= 1f ? 1f : 1f - Mathf.Pow(2f, -10f * t);
    }

    private static float InExpo(float t)
    {
        return t <= 0f ? 0f : Mathf.Pow(2f, 10f * t - 10f);
    }

    private static float InOutQuint(float t)
    {
        return t < 0.5f
            ? 16f * t * t * t * t * t
            : 1f - Mathf.Pow(-2f * t + 2f, 5f) * 0.5f;
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
