using Cysharp.Threading.Tasks;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class WebGetTextTest : MonoBehaviour
{
    // HTTP 프로토콜을 이용해서 웹 서버에게 데이터 작업을 요청할 수 있다.
    // 작업 요청은 크게 4가지 약속이 있음.
    // 1. 데이터 요청 : GET
    // 2. 데이터 전송 : POST
    // 3. 데이터 수정 : PUT
    // 4. 데이터 삭제 : DELETE

    private async void Start()
    {
        string result = await GetWebText("https://www.google.com/search?q=%EB%A9%94%EC%9D%B4%ED%94%8C%EC%8A%A4%ED%86%A0%EB%A6%AC&oq=%EB%A9%94%EC%9D%B4%ED%94%8C%EC%8A%A4%ED%86%A0%EB%A6%AC&gs_lcrp=EgZjaHJvbWUqDAgAECMYJxiABBiKBTIMCAAQIxgnGIAEGIoFMg8IARAuGBQYhwIYsQMYgAQyDAgCECMYJxiABBiKBTIMCAMQABgUGIcCGIAEMgcIBBAAGIAEMgcIBRAuGIAEMgYIBhBFGEEyBggHEAUYQNIBCDI4NDRqMGo3qAIAsAIA&sourceid=chrome&ie=UTF-8");
        Debug.Log(result);

        // 서버에게 데이터 요청 하는 작업은 비동기이므로 코루틴을 이용.
        StartCoroutine(GetText());
    }

    private async UniTask<string> GetWebText(string url)
    {
        var txt = (await UnityWebRequest.Get(url).SendWebRequest()).downloadHandler.text;
        return txt;
    }

    IEnumerator GetText()
    {
        // URL : 웹서버에게 어떤 자원(페이지/이미지/파일/데이터/API) 이 있는 위치를 가리키는 주소
        UnityWebRequest www = UnityWebRequest.Get("https://www.google.com/search?q=%EB%A9%94%EC%9D%B4%ED%94%8C%EC%8A%A4%ED%86%A0%EB%A6%AC&oq=%EB%A9%94%EC%9D%B4%ED%94%8C%EC%8A%A4%ED%86%A0%EB%A6%AC&gs_lcrp=EgZjaHJvbWUqDAgAECMYJxiABBiKBTIMCAAQIxgnGIAEGIoFMg8IARAuGBQYhwIYsQMYgAQyDAgCECMYJxiABBiKBTIMCAMQABgUGIcCGIAEMgcIBBAAGIAEMgcIBRAuGIAEMgYIBhBFGEEyBggHEAUYQNIBCDI4NDRqMGo3qAIAsAIA&sourceid=chrome&ie=UTF-8");
        yield return www.SendWebRequest();

        if (www.isNetworkError || www.isHttpError)
        {
            Debug.Log(www.error);
        }
        else
        {
            // Show results as text
            Debug.Log(www.downloadHandler.text);
        }
    }
}
