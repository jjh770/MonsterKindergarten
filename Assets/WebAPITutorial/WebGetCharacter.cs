using Cysharp.Threading.Tasks;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class WebGetCharacter : MonoBehaviour
{
    [SerializeField] Button _button;
    [SerializeField] TMP_InputField _inputField;
    [SerializeField] RawImage _characterImage;
    [SerializeField] TextMeshProUGUI _characterNameText;
    [SerializeField] TextMeshProUGUI _characterLevelText;

    private const string API_KEY = "test_80ea05f33026262caa3774434ad1c27d93beb3ff17964fb12a84e9a900909f11efe8d04e6d233bd35cf2fabdeb93fb0d";
    private const string BASE_URL = "https://open.api.nexon.com/maplestory/v1";

    private string _characterName;

    private void Start()
    {
        _button.onClick.AddListener(() => GetCharacter());
    }

    private async void GetCharacter()
    {
        _characterName = _inputField.text;

        if (string.IsNullOrEmpty(_characterName))
        {
            Debug.Log("닉네임이 비어있습니다.");
            return;
        }
        string ocid = await GetCharacterOCID(_characterName);

        if (string.IsNullOrEmpty(ocid))
        {
            Debug.Log("캐릭터 정보를 찾을 수 없습니다.");
            return;
        }
        CharacterInfo characterInfoData = await GetCharacterInfo(ocid);

        if (characterInfoData == null)
        {
            Debug.Log("캐릭터 정보를 불러오는데 실패했습니다.");
            return;
        }
        _characterNameText.text = $"닉네임 : {characterInfoData.character_name}";
        _characterLevelText.text = $"레벨 : {characterInfoData.character_level}";

        if (!string.IsNullOrEmpty(characterInfoData.character_image))
        {
            _characterImage.texture = await GetCharacterImage(characterInfoData.character_image);
        }
    }

    private async UniTask<string> GetCharacterOCID(string characterName)
    {
        string ocidJson = await GetWebText($"{BASE_URL}/id?character_name={characterName}");
        CharacterOCID ocidData = JsonUtility.FromJson<CharacterOCID>(ocidJson);
        return ocidData?.ocid;
    }

    private async UniTask<CharacterInfo> GetCharacterInfo(string characterOcid)
    {
        string InfoJson = await GetWebText($"{BASE_URL}/character/basic?ocid={characterOcid}");
        CharacterInfo infoData = JsonUtility.FromJson<CharacterInfo>(InfoJson);
        return infoData;
    }

    private async UniTask<Texture2D> GetCharacterImage(string characterImage)
    {
        if (string.IsNullOrEmpty(characterImage))
            return null;

        try
        {
            using var request = UnityWebRequestTexture.GetTexture(characterImage);
            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"이미지 로드 실패: {request.error}");
                return null;
            }

            return DownloadHandlerTexture.GetContent(request);
        }
        catch (Exception e)
        {
            Debug.LogError($"이미지 로드 예외: {e.Message}");
            return null;
        }
    }

    private async UniTask<string> GetWebText(string url)
    {
        var request = UnityWebRequest.Get(url);
        request.SetRequestHeader("x-nxopen-api-key", API_KEY);

        await request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            return request.downloadHandler.text;
        }
        else
        {
            Debug.LogError($"Error: {request.error}");
            return null;
        }
    }

    [Serializable]
    private class CharacterOCID
    {
        public string ocid;
    }

    [Serializable]
    public class CharacterInfo
    {
        public string date;
        public string character_name;
        public string world_name;
        public string character_gender;
        public string character_class;
        public string character_class_level;
        public int character_level;
        public long character_exp;
        public string character_exp_rate;
        public string character_guild_name;
        public string character_image;
        public string character_date_create;
        public string access_flag;
        public string liberation_quest_clear;
    }
}
