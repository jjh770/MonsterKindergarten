using Cysharp.Threading.Tasks;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class WebGetImageTest : MonoBehaviour
{
    [SerializeField] RawImage _myImage;

    private async void Start()
    {
        //StartCoroutine(GetTexture());
    }

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.A)) return;

        ChangeImage();
    }

    private async void ChangeImage()
    {
        // URL의 구성
        // 프로토콜   : http(s)://
        // 경로(주소) : place.dogs/300/200 (함수 이름)
        // 쿼리       : ?fit=contain&position=right (함수 매개변수)
        //            - ?로 시작하고 &로 구분한다.
        //            - fit : contain
        //            - position : right
        //            - 옵션인데 매번 다르므로 웹 서버 개발자와 의논하거나 문서를 읽고 파악해야함.
        _myImage.texture = await GetWebTexture("https://place.dog/300/300?fit=contain&position=right");
    }

    private async UniTask<Texture> GetWebTexture(string url)
    {
        try
        {
            var texture = ((DownloadHandlerTexture)((await UnityWebRequestTexture.GetTexture(url).SendWebRequest()).downloadHandler)).texture;
            return texture;
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
            return null;
        }
    }

    IEnumerator GetTexture()
    {
        UnityWebRequest www = UnityWebRequestTexture.GetTexture("https://yt3.googleusercontent.com/5Ft_OkHKiP9mz83nczLPgSnGdULpgqVOV1v2Tg7MVMtNd9eCifcdr_MftRf0M1xdOCAjmPNDVW8=s900-c-k-c0x00ffffff-no-rj");
        yield return www.SendWebRequest();

        if (www.isNetworkError || www.isHttpError)
        {
            Debug.Log(www.error);
        }
        else
        {
            Texture myTexture = ((DownloadHandlerTexture)www.downloadHandler).texture;
            _myImage.texture = myTexture;
        }
    }
}
