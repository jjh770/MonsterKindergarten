using UnityEngine;

// 클라우드 저장이 막힌 사실을 플레이어에게 알린다. GameScene 전용이다.
//
// 플레이어가 고칠 수 있는 문제가 아니므로 조치를 요구하지 않는다. 대신 하지 말아야
// 할 것을 알린다. 이 상태에서 앱을 지우거나 기기를 바꾸면 진행도가 사라지기 때문이다.
//
// "네트워크를 확인하라"고 하지 않는다. 연결이 끊긴 동안의 쓰기는 Firestore SDK가
// 큐에 담아 두었다가 알아서 밀어 올리므로 이 알림 자체가 뜨지 않는다. 이 알림이 뜬다는
// 것은 연결은 멀쩡한데 서버가 거부했다는 뜻이라, 연결을 확인하라는 안내는 멀쩡한 것을
// 의심하게 만든다.
//
// 한 번만 띄우면 놓치기 쉬워 상태가 풀릴 때까지 되풀이한다. 되풀이가 성립하려면
// 성공 시 상태가 풀려야 하고, 그건 CloudSaveGuard.ReportSuccess가 맡는다.
//
// 이벤트를 구독하지 않고 상태를 직접 본다. 실패와 복구가 모두 상태 하나로 드러나고,
// 로그인 화면의 Clear까지 같은 값으로 반영되기 때문이다.
public sealed class CloudSaveFailureNotifier : MonoBehaviour
{
    [SerializeField] private ToastMessageUI _toast;

    [Tooltip("평소 토스트보다 오래 띄웁니다. 읽고 판단할 내용이라 짧으면 놓칩니다.")]
    [SerializeField, Min(0f)] private float _displaySeconds = 5f;

    [Tooltip("실패가 계속되는 동안 이 간격으로 다시 알립니다.")]
    [SerializeField, Min(1f)] private float _repeatSeconds = 10f;

    private const string Message =
        "진행도가 이 기기에만 저장되고 있어요.\n" +
        "앱을 지우거나 기기를 바꾸면 복구할 수 없어요.";

    private bool _wasFailed;
    private float _elapsed;

    private void Update()
    {
        if (!CloudSaveGuard.HasFailed)
        {
            _wasFailed = false;
            return;
        }

        // 실패로 막 바뀐 순간에는 기다리지 않고 바로 알린다.
        if (!_wasFailed)
        {
            _wasFailed = true;
            _elapsed = 0f;
            Notify();
            return;
        }

        // 일시정지 연출과 무관해야 하므로 스케일되지 않은 시간을 쓴다.
        _elapsed += Time.unscaledDeltaTime;
        if (_elapsed < _repeatSeconds) return;

        _elapsed = 0f;
        Notify();
    }

    private void Notify()
    {
        if (_toast == null)
        {
            Debug.LogError("알림에 쓸 토스트 참조가 비어 있습니다.", this);
            enabled = false;
            return;
        }

        _toast.Show(Message, _displaySeconds);
    }
}
