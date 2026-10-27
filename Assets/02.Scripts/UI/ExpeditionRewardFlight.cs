using UnityEngine;

// 탐험 보상의 포인트와 뽑기권 아이콘이 상단 바로 날아가는 연출이다. 보상은 이미 지급돼 있고,
// 숫자와 저장은 건드리지 않는다.
//
// 무엇을 몇 장, 어느 아이콘으로 보내고 닿을 때 어떤 소리를 낼지만 정한다. 흩어졌다가 날아가 앉는
// 움직임은 다른 보상과 같은 UiIconFlight가 맡는다. 같은 아이콘을 튕기는 연출이 한곳을 거쳐야
// 아이콘 크기가 어긋나지 않는다.
//
// 상단 바는 탐험 화면이 열린 동안 숨겨지므로 화면을 닫은 뒤에 부른다. 상단 바가 내려오는 동안
// 날아가도 UiIconFlight가 매 프레임 아이콘의 위치를 다시 읽어 내려앉는다. 출발점은 탐험 화면의
// 보상 팝업이 준 위치이고, 두 캔버스 모두 Screen Space Overlay라 그 위치를 그대로 쓴다.
public sealed class ExpeditionRewardFlight : MonoBehaviour
{
    [Tooltip("상단 바로 날려 보내는 공용 비행 연출입니다.")]
    [SerializeField] private UiIconFlight _iconFlight;
    [SerializeField] private RectTransform _pointTarget;
    [SerializeField] private RectTransform _ticketTarget;
    [SerializeField] private Sprite _pointSprite;
    [SerializeField] private Sprite _ticketSprite;
    [SerializeField, Min(1)] private int _pointCount = 12;
    [SerializeField, Min(1)] private int _maxTicketCount = 8;
    [SerializeField, Min(1f)] private float _iconSize = 90f;

    private const float ArrivalSoundCooldown = .06f;

    private void Awake()
    {
        if (_iconFlight == null || _pointTarget == null || _ticketTarget == null ||
            _pointSprite == null || _ticketSprite == null)
        {
            Debug.LogError("탐험 보상 날아가기의 필수 참조가 비어 있어요.", this);
            enabled = false;
        }
    }

    public void Play(Vector3 pointFrom, Vector3 ticketFrom, int tickets)
    {
        if (!enabled) return;

        var size = new Vector2(_iconSize, _iconSize);
        _iconFlight.PlayBundle(
            pointFrom,
            _pointSprite,
            size,
            _pointTarget,
            _pointCount,
            index => PlayArrivalSound(index, EAudioSfx.OfflineRewardArrival),
            null);
        if (tickets > 0)
        {
            _iconFlight.PlayBundle(
                ticketFrom,
                _ticketSprite,
                size,
                _ticketTarget,
                Mathf.Min(tickets, _maxTicketCount),
                index => PlayArrivalSound(index, EAudioSfx.TicketCollect),
                null);
        }
    }

    // 첫 장은 보상의 소리를 내고, 뒤따르는 장은 짧은 소리를 겹치지 않게 흩뿌린다.
    private static void PlayArrivalSound(int index, EAudioSfx firstSound)
    {
        if (AudioManager.Instance == null) return;

        if (index == 0)
        {
            AudioManager.Instance.PlaySFX(firstSound);
            return;
        }

        AudioManager.Instance.PlaySFXRandomPitchWithCooldown(EAudioSfx.TicketCollect, ArrivalSoundCooldown);
    }
}
