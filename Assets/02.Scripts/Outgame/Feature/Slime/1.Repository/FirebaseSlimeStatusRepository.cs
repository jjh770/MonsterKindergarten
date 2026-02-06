using Cysharp.Threading.Tasks;
using Firebase.Auth;
using Firebase.Firestore;
using System;
using UnityEngine;

public class FirebaseSlimeStatusRepository : ISlimeStatusRepository
{
    private const string COLLECTION_NAME = "SlimeStatus";
    private FirebaseAuth _auth = FirebaseAuth.DefaultInstance;
    private FirebaseFirestore _db = FirebaseFirestore.DefaultInstance;

    public async UniTaskVoid Save(SlimeStatusSaveData saveData)
    {
        try
        {
            string email = _auth.CurrentUser.Email;
            saveData.LastSaveTime = DateTime.Now.ToString("o");
            await _db.Collection(COLLECTION_NAME).Document(email).SetAsync(saveData).AsUniTask();
            Debug.Log("SlimeStatus 저장 성공");
        }
        catch (Exception e)
        {
            Debug.LogError("SlimeStatus 저장 실패: " + e.Message);
        }
    }

    public async UniTask<SlimeStatusSaveData> Load()
    {
        Debug.Log("SlimeStatus 로드");

        try
        {
            string email = _auth.CurrentUser.Email;
            DocumentSnapshot snapshot = await _db.Collection(COLLECTION_NAME).Document(email).GetSnapshotAsync().AsUniTask();
            SlimeStatusSaveData data = snapshot.ConvertTo<SlimeStatusSaveData>();
            if (data != null)
            {
                Debug.Log("SlimeStatus 로드 성공");
                return data;
            }
            return SlimeStatusSaveData.Default;
        }
        catch (Exception e)
        {
            Debug.LogError("SlimeStatus 로드 실패: " + e.Message);
            return SlimeStatusSaveData.Default;
        }
    }
}
