using UnityEngine;

// UiMessages 에셋을 씬에서 연결한다. 이 씬의 다른 컴포넌트가 문구를 읽기 전에 연결되도록 일찍 실행한다.
[DefaultExecutionOrder(-1000)]
public sealed class UiMessagesProvider : MonoBehaviour
{
    [SerializeField] private UiMessagesSO _messages;

    private void Awake()
    {
        if (_messages == null)
        {
            Debug.LogError("UiMessages 에셋 참조가 비어 있습니다.", this);
            return;
        }

        UiMessages.Use(_messages);
    }

    private void OnDestroy()
    {
        UiMessages.Release(_messages);
    }
}
