#if !UNITY_WEBGL || UNITY_EDITOR

using Cysharp.Threading.Tasks;
using Firebase.Auth;
using Firebase.Firestore;
using System;
using UnityEngine;

public class FirebaseCurrencyRepository : IRepository<CurrencySaveData>
{
    private string CURRENCY_COLLECTION_NAME = "Currency";
    private FirebaseAuth _auth = FirebaseAuth.DefaultInstance;
    private FirebaseFirestore _db = FirebaseFirestore.DefaultInstance;
    public async UniTask Save(CurrencySaveData saveData)
    {
        try
        {
            string userId = _auth.CurrentUser.UserId;

            await _db.Collection(CURRENCY_COLLECTION_NAME).Document(userId).SetAsync(saveData).AsUniTask();
        }
        catch (Exception e)
        {
            Debug.LogWarning("Currency 저장 실패: " + e.Message);
        }
    }

    public async UniTask<CurrencySaveData> Load()
    {
        try
        {
            string userId = _auth.CurrentUser.UserId;
            DocumentSnapshot snapshot = await _db.Collection(CURRENCY_COLLECTION_NAME).Document(userId).GetSnapshotAsync().AsUniTask();

            if (!snapshot.Exists)
            {
                return CurrencySaveData.Default;
            }

            return snapshot.ConvertTo<CurrencySaveData>() ?? CurrencySaveData.Default;
        }
        catch (Exception e)
        {
            Debug.LogError("Currency 로드 실패" + e.Message);
            return null;
        }
    }
}
#endif
