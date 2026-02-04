using Cysharp.Threading.Tasks;
using Firebase.Auth;
using Firebase.Firestore;
using System;
using UnityEngine;

public class FirebaseCurrencyRepository : ICurrencyRepository
{
    private string CURRENCY_COLLECTION_NAME = "Currency";
    private FirebaseAuth _auth = FirebaseAuth.DefaultInstance;
    private FirebaseFirestore _db = FirebaseFirestore.DefaultInstance;
    public async UniTaskVoid Save(CurrencySaveData saveData)
    {
        try
        {
            string email = _auth.CurrentUser.Email;

            await _db.Collection(CURRENCY_COLLECTION_NAME).Document(email).SetAsync(saveData).AsUniTask();
        }
        catch (Exception e)
        {
            Debug.Log("저장 실패" + e.Message);
        }
    }

    public async UniTask<CurrencySaveData> Load()
    {
        try
        {
            string email = _auth.CurrentUser.Email;
            DocumentSnapshot snapshot = await _db.Collection(CURRENCY_COLLECTION_NAME).Document(email).GetSnapshotAsync().AsUniTask();

            CurrencySaveData data = snapshot.ConvertTo<CurrencySaveData>();

            if (data != null)
            {
                return data;
            }
            return CurrencySaveData.Default;
        }
        catch (Exception e)
        {
            Debug.LogError("Currency 로드 실패" + e.Message);
            return CurrencySaveData.Default;
        }
    }
}
