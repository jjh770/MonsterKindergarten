using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

// 화면 어디를 눌러도 눌린 자리에서 터지는 효과다. 동그란 원이 퍼져 나가고, 아주 작은 슬라임과
// 반짝이는 별이 흩뿌려지며 날아간다.
//
// 진짜 ParticleSystem이 아니라 UI 이미지를 직접 움직인다. HUD와 팝업이 Screen Space - Overlay
// 캔버스에 있어서, 파티클은 그 위에 그려지지 않기 때문이다. 이 효과는 가장 위 캔버스에 두고
// 입력은 받지 않는다(조각의 Raycast Target을 모두 끈다). 눌림 판정은 게임 입력과 따로 포인터를
// 직접 읽으므로, 튜토리얼이나 팝업이 입력을 막고 있어도 눌린 자리에는 효과가 나온다.
//
// 눌린 곳에 따라 양이 다르다. 원과 별은 어디를 눌러도 같고, 작은 슬라임만 슬라임을 누르면 가장 많이,
// 빈 땅은 적게, UI에서는 나오지 않아 버튼이 많은 화면이 어지러워지지 않는다. 세 경우의 양은
// 아래 Profile에서 따로 정한다.
//
// 빛은 가산 합성 이미지로 낸다. 오버레이 캔버스에는 2D 라이트도 후처리 블룸도 닿지 않아서, 눌린 자리의
// 섬광, 원의 번짐, 별마다 깔리는 후광을 따뜻한 노란빛 이미지로 뒤 화면에 더해 빛나 보이게 한다.
//
// 별과 슬라임은 원이 퍼지는 자리까지, 그리고 그 바깥으로 조금 더 나가 멈춘다. 속도와 중력이 아니라
// 도착할 거리를 정해 두고 그 거리까지 빠르게 나갔다가 느려지는 움직임이라, 원과 같은 범위에서 터진다.
//
// 조각은 씬에 둔 비활성 원본을 복제해 풀로 쓰고, 조각마다 DOTween을 만들지 않고 이 컴포넌트가
// 한 번에 움직인다. 연타해도 동시에 움직이는 조각 수에 상한을 둔다.
public sealed class ScreenTouchEffect : MonoBehaviour
{
    [SerializeField] private RectTransform _root;
    [Tooltip("퍼져 나가는 원 원본입니다. 씬에서는 비활성으로 둡니다.")]
    [SerializeField] private Image _ringTemplate;
    [Tooltip("반짝이는 별 원본입니다. 씬에서는 비활성으로 둡니다.")]
    [SerializeField] private Image _starTemplate;
    [Tooltip("작은 슬라임 원본입니다. 그림은 지금까지 만난 등급 중에서 무작위로 고릅니다.")]
    [SerializeField] private Image _slimeTemplate;
    [Tooltip("슬라임 데이터를 읽을 수 없을 때(로그인 화면) 대신 쓰는 그림입니다. 게임 중에는 쓰이지 않습니다.")]
    [SerializeField] private Sprite[] _slimeSprites;
    [Tooltip("빛 원본입니다(가운데가 밝은 둥근 그림, 가산 합성 머티리얼). 섬광과 별 후광이 함께 씁니다. 씬에서는 비활성으로 둡니다.")]
    [SerializeField] private Image _glowTemplate;
    [Tooltip("원의 번짐 원본입니다(흐릿하고 굵은 고리 그림, 가산 합성 머티리얼). 씬에서는 비활성으로 둡니다.")]
    [SerializeField] private Image _ringGlowTemplate;
    [Tooltip("슬라임을 눌렀는지 알아내는 데 씁니다. 비워 두면(로그인 화면) 슬라임은 없는 것으로 봅니다.")]
    [SerializeField] private Clicker _clicker;

    [Header("Amount by Target")]
    [Tooltip("슬라임을 눌렀을 때의 양입니다. 1이면 아래 기본 개수와 크기 그대로입니다.")]
    [SerializeField] private Profile _onSlime = new Profile(1f, 1f, 1f);
    [Tooltip("슬라임도 UI도 아닌 빈 곳을 눌렀을 때의 양입니다.")]
    [SerializeField] private Profile _onEmpty = new Profile(1f, 1f, 0.4f);
    [Tooltip("버튼과 팝업 같은 UI를 눌렀을 때의 양입니다. 작은 슬라임은 내지 않아 화면이 어지럽지 않게 합니다.")]
    [SerializeField] private Profile _onUi = new Profile(1f, 1f, 0f);

    [Header("Glow")]
    [SerializeField] private Color _glowColor = new Color(1f, 0.8f, 0.35f, 1f);
    [Tooltip("눌린 자리의 섬광 크기입니다.")]
    [SerializeField, Min(10f)] private float _flashSize = 420f;
    [SerializeField, Range(0f, 1f)] private float _flashStrength = 0.65f;
    [SerializeField, Min(0.05f)] private float _flashDuration = 0.28f;
    [Tooltip("별 크기에 곱해 별 뒤의 후광 크기를 정합니다.")]
    [SerializeField, Min(1f)] private float _haloSizeScale = 3f;
    [SerializeField, Range(0f, 1f)] private float _haloStrength = 0.4f;
    [Tooltip("원 번짐의 세기입니다. 0이면 번짐 없이 원만 그립니다.")]
    [SerializeField, Range(0f, 1f)] private float _ringGlowStrength = 0.6f;

    [Header("Ring")]
    [SerializeField, Min(10f)] private float _ringSize = 150f;
    [SerializeField, Min(0.1f)] private float _ringDuration = 0.4f;
    [SerializeField] private Color _ringColor = new Color(1f, 0.95f, 0.7f, 0.9f);

    [Header("Star")]
    [SerializeField, Range(0, 16)] private int _starCount = 7;
    [SerializeField] private Vector2 _starSizeRange = new Vector2(34f, 62f);
    [SerializeField] private Vector2 _starLifeRange = new Vector2(0.5f, 0.8f);
    [SerializeField] private Color[] _starColors =
    {
        new Color(1f, 0.92f, 0.45f, 1f),
        new Color(1f, 1f, 1f, 1f),
        new Color(1f, 0.78f, 0.88f, 1f),
    };

    [Header("Slime")]
    [SerializeField, Range(0, 12)] private int _slimeCount = 5;
    [SerializeField] private Vector2 _slimeSizeRange = new Vector2(56f, 84f);
    [SerializeField] private Vector2 _slimeLifeRange = new Vector2(0.7f, 1f);
    [Tooltip("날아가는 동안 도는 속도(도/초)의 최댓값입니다. 클수록 빙글빙글 돕니다.")]
    [SerializeField, Min(0f)] private float _slimeSpin = 90f;

    [Header("Spread")]
    [Tooltip("별과 작은 슬라임이 퍼져 멈추는 거리입니다. 원이 가장 크게 퍼진 반지름의 배수이고, 1이면 원의 가장자리, 1보다 크면 그 바깥입니다.")]
    [SerializeField] private Vector2 _spreadRange = new Vector2(1f, 1.25f);

    [Header("Limit")]
    [Tooltip("동시에 움직이는 조각 수의 상한입니다. 넘으면 새 효과를 건너뜁니다.")]
    [SerializeField, Min(10)] private int _maxActivePieces = 220;

    // 눌린 곳마다 기본 양에 곱하는 비율이다. 0이면 그 조각은 내지 않는다.
    [System.Serializable]
    private struct Profile
    {
        [Range(0f, 2f)] public float Ring;
        [Range(0f, 2f)] public float Star;
        [Range(0f, 2f)] public float Slime;

        public Profile(float ring, float star, float slime)
        {
            Ring = ring;
            Star = star;
            Slime = slime;
        }
    }

    // 원이 퍼지는 동안 커지는 비율의 끝값이다. Simulate의 Lerp와 같아야 별이 원 가장자리에서 멈춘다.
    private const float RingMaxScale = 1.25f;

    private enum PieceKind
    {
        Ring,
        Star,
        Slime,
        Flash,
        Halo,
        RingGlow,
    }

    private sealed class Piece
    {
        public PieceKind Kind;
        public Image Image;
        public RectTransform Rect;
        public Vector2 Origin;
        public Vector2 Position;
        // 별과 슬라임이 향하는 방향과 멈추는 거리. 원은 쓰지 않는다.
        public Vector2 Direction;
        public float Distance;
        public float Spin;
        public float Rotation;
        public float Age;
        public float Life;
        public float Size;
        public Color Color;
        // 세기를 줄이는 비율. 후광과 번짐처럼 본체보다 옅게 그리는 조각이 쓴다.
        public float Strength = 1f;
    }

    private readonly List<Piece> _active = new();
    private readonly List<RaycastResult> _uiHits = new();
    private UiImagePool _ringPool;
    private UiImagePool _starPool;
    private UiImagePool _slimePool;
    private UiImagePool _glowPool;
    private UiImagePool _ringGlowPool;

    private void Awake()
    {
        if (_root == null || _ringTemplate == null || _starTemplate == null || _slimeTemplate == null ||
            _glowTemplate == null || _ringGlowTemplate == null)
        {
            Debug.LogError("터치 효과의 필수 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _ringPool = new UiImagePool(_ringTemplate, _root, 4);
        _starPool = new UiImagePool(_starTemplate, _root, _starCount * 4);
        _slimePool = new UiImagePool(_slimeTemplate, _root, _slimeCount * 4);
        _glowPool = new UiImagePool(_glowTemplate, _root, 2 + _starCount * 3);
        _ringGlowPool = new UiImagePool(_ringGlowTemplate, _root, 3);
    }

    private void OnDisable()
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Release(_active[i]);
        }

        _active.Clear();
    }

    private void Update()
    {
        PlayPressedThisFrame();
        Simulate(Time.unscaledDeltaTime);
    }

    // 손가락이 닿은 프레임마다 그 자리에서 효과를 낸다. 터치스크린이 있으면 모든 손가락을 보고,
    // 없으면(에디터) 마우스를 본다.
    private void PlayPressedThisFrame()
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            foreach (TouchControl touch in touchscreen.touches)
            {
                if (touch.press.wasPressedThisFrame)
                {
                    Play(touch.position.ReadValue());
                }
            }

            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            Play(mouse.position.ReadValue());
        }
    }

    // screenPosition은 화면 좌표(픽셀)다. 오버레이 캔버스라 카메라는 쓰지 않는다.
    public void Play(Vector2 screenPosition)
    {
        if (!enabled || _active.Count >= _maxActivePieces) return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _root, screenPosition, null, out Vector2 origin))
        {
            return;
        }

        Profile profile = GetProfile(screenPosition);
        EmitFlash(origin);
        if (profile.Ring > 0f)
        {
            EmitRing(origin, profile.Ring);
        }

        int stars = Mathf.RoundToInt(_starCount * profile.Star);
        for (int i = 0; i < stars; i++)
        {
            EmitStar(origin, profile.Ring);
        }

        int slimes = Mathf.RoundToInt(_slimeCount * profile.Slime);
        for (int i = 0; i < slimes; i++)
        {
            EmitSlime(origin, profile.Ring);
        }
    }

    // UI가 가장 위에 있으니 먼저 보고, 없으면 슬라임, 둘 다 아니면 빈 곳이다.
    private Profile GetProfile(Vector2 screenPosition)
    {
        if (IsOverUi(screenPosition))
        {
            return _onUi;
        }

        if (_clicker != null && _clicker.HasSelectionTargetAt(screenPosition))
        {
            return _onSlime;
        }

        return _onEmpty;
    }

    private bool IsOverUi(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return false;

        var pointerData = new PointerEventData(eventSystem) { position = screenPosition };
        _uiHits.Clear();
        eventSystem.RaycastAll(pointerData, _uiHits);
        return _uiHits.Count > 0;
    }

    private void EmitRing(Vector2 origin, float scale)
    {
        Piece piece = Begin(PieceKind.Ring, _ringPool, origin, _ringSize * scale, _ringDuration, _ringColor);
        piece.Image.sprite = _ringTemplate.sprite;

        if (_ringGlowStrength > 0f)
        {
            // 번짐은 원보다 굵게 그려 가장자리가 빛으로 번져 보이게 한다. 크기와 시간은 원과 같다.
            Piece glow = Begin(
                PieceKind.RingGlow, _ringGlowPool, origin, _ringSize * scale, _ringDuration, _glowColor);
            glow.Strength = _ringGlowStrength;
        }
    }

    // 눌린 자리에서 번쩍하고 사라지는 빛이다. 가장 먼저 그려 별과 슬라임 뒤에 깔린다.
    private void EmitFlash(Vector2 origin)
    {
        if (_flashStrength <= 0f) return;

        Piece piece = Begin(PieceKind.Flash, _glowPool, origin, _flashSize, _flashDuration, _glowColor);
        piece.Strength = _flashStrength;
    }

    private void EmitStar(Vector2 origin, float ringScale)
    {
        Color color = _starColors.Length > 0
            ? _starColors[Random.Range(0, _starColors.Length)]
            : Color.white;
        Piece piece = Begin(
            PieceKind.Star,
            _starPool,
            origin,
            Random.Range(_starSizeRange.x, _starSizeRange.y),
            Random.Range(_starLifeRange.x, _starLifeRange.y),
            color);
        Scatter(piece, ringScale);
        piece.Spin = Random.Range(-240f, 240f);
        piece.Rotation = Random.Range(0f, 90f);

        if (_haloStrength > 0f)
        {
            // 후광은 별과 같은 길을 같은 시간 동안 따라간다. 색은 별 색을 노란빛에 섞어 정한다.
            Piece halo = Begin(
                PieceKind.Halo,
                _glowPool,
                origin,
                piece.Size * _haloSizeScale,
                piece.Life,
                Color.Lerp(_glowColor, color, 0.35f));
            halo.Direction = piece.Direction;
            halo.Distance = piece.Distance;
            halo.Strength = _haloStrength;
        }
    }

    private void EmitSlime(Vector2 origin, float ringScale)
    {
        Piece piece = Begin(
            PieceKind.Slime,
            _slimePool,
            origin,
            Random.Range(_slimeSizeRange.x, _slimeSizeRange.y),
            Random.Range(_slimeLifeRange.x, _slimeLifeRange.y),
            Color.white);
        Sprite sprite = PickSlimeSprite();
        if (sprite != null)
        {
            piece.Image.sprite = sprite;
        }

        Scatter(piece, ringScale);
        piece.Spin = Random.Range(-_slimeSpin, _slimeSpin);
        piece.Rotation = Random.Range(-15f, 15f);
    }

    // 지금까지 만난(최고 등급까지의) 슬라임 중 하나의 그림을 고른다. 등급 그림은 슬라임 스펙에서
    // 읽으므로 그림이나 등급이 바뀌어도 이 목록을 따로 고칠 일이 없다. 데이터가 없으면(로그인 화면)
    // 직렬화된 대체 목록에서 고른다.
    private Sprite PickSlimeSprite()
    {
        SlimeManager manager = SlimeManager.Instance;
        if (manager != null && manager.IsInitialized)
        {
            int highest = (int)manager.HighestGrade;
            Slime slime = manager.Get((ESlimeGrade)Random.Range(1, highest + 1));
            if (slime != null && slime.SpecData.Sprite != null)
            {
                return slime.SpecData.Sprite;
            }
        }

        return _slimeSprites != null && _slimeSprites.Length > 0
            ? _slimeSprites[Random.Range(0, _slimeSprites.Length)]
            : null;
    }

    // 사방 아무 방향으로, 원이 가장 크게 퍼진 반지름의 _spreadRange배 거리까지 보낸다.
    // 원의 가장 큰 크기는 EmitRing과 Simulate가 정한 Lerp 끝값(1.25배)과 같다.
    private void Scatter(Piece piece, float ringScale)
    {
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        piece.Direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        float ringRadius = _ringSize * ringScale * RingMaxScale * 0.5f;
        piece.Distance = ringRadius * Random.Range(_spreadRange.x, _spreadRange.y);
    }

    private Piece Begin(
        PieceKind kind,
        UiImagePool pool,
        Vector2 origin,
        float size,
        float life,
        Color color)
    {
        Image image = pool.Rent();
        var piece = new Piece
        {
            Kind = kind,
            Image = image,
            Rect = image.rectTransform,
            Origin = origin,
            Position = origin,
            Life = life,
            Size = size,
            Color = color,
        };

        // 풀에서 돌려받은 조각은 이전 값이 남아 있으므로 크기, 색, 위치를 모두 새로 정한다.
        image.raycastTarget = false;
        image.color = color;
        piece.Rect.sizeDelta = new Vector2(size, size);
        piece.Rect.anchoredPosition = origin;
        _active.Add(piece);
        return piece;
    }

    private void Simulate(float deltaTime)
    {
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            Piece piece = _active[i];
            piece.Age += deltaTime;
            if (piece.Age >= piece.Life)
            {
                Release(piece);
                _active.RemoveAt(i);
                continue;
            }

            float t = piece.Age / piece.Life;
            piece.Rotation += piece.Spin * deltaTime;
            if (piece.Kind == PieceKind.Star || piece.Kind == PieceKind.Slime || piece.Kind == PieceKind.Halo)
            {
                // 빠르게 터져 나갔다가 도착하며 느려진다.
                float travelled = 1f - (1f - t) * (1f - t) * (1f - t);
                piece.Position = piece.Origin + piece.Direction * (piece.Distance * travelled);
            }

            float scale;
            float alpha;
            if (piece.Kind == PieceKind.Ring || piece.Kind == PieceKind.RingGlow)
            {
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                scale = Mathf.Lerp(0.25f, RingMaxScale, eased);
                alpha = Mathf.Pow(1f - t, 1.5f);
            }
            else if (piece.Kind == PieceKind.Flash)
            {
                // 빠르게 커지며 번쩍하고, 이후 급히 옅어진다.
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);
                scale = Mathf.Lerp(0.35f, 1f, eased);
                alpha = (1f - t) * (1f - t);
            }
            else
            {
                // 처음 15% 동안 커지며 튀어나오고, 이후 줄어들며 끝에서 옅어진다.
                scale = t < 0.15f
                    ? Mathf.Lerp(0.2f, 1f, t / 0.15f)
                    : Mathf.Lerp(1f, 0.35f, (t - 0.15f) / 0.85f);
                alpha = t < 0.6f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.6f) / 0.4f);
            }

            Color color = piece.Color;
            color.a *= alpha * piece.Strength;
            piece.Image.color = color;
            piece.Rect.anchoredPosition = piece.Position;
            piece.Rect.localScale = Vector3.one * scale;
            piece.Rect.localRotation = Quaternion.Euler(0f, 0f, piece.Rotation);
        }
    }

    private void Release(Piece piece)
    {
        UiImagePool pool = piece.Kind switch
        {
            PieceKind.Ring => _ringPool,
            PieceKind.Star => _starPool,
            PieceKind.Slime => _slimePool,
            PieceKind.RingGlow => _ringGlowPool,
            _ => _glowPool,
        };
        pool?.Return(piece.Image);
    }
}
