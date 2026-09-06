
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

    public async UniTask<SaveLoadResult<CurrencySaveData>> Load()
    {
        try
        {
            string userId = _auth.CurrentUser.UserId;
            DocumentSnapshot snapshot = await _db.Collection(CURRENCY_COLLECTION_NAME).Document(userId).GetSnapshotAsync().AsUniTask();

            if (!snapshot.Exists)
            {
                return SaveLoadResult<CurrencySaveData>.NotFound();
            }

            int schemaVersion = SaveSchema.LegacyVersion;
            if (snapshot.TryGetValue<long>(
                    nameof(ISaveData.SchemaVersion),
                    out long storedSchemaVersion))
            {
                schemaVersion = (int)storedSchemaVersion;
            }

            // 상위 버전은 현재 앱이 해석할 수 없다. 그대로 로드하면 다음 저장이
            // 최신 데이터를 낮은 버전으로 덮어쓴다.
            if (schemaVersion > SaveSchema.CurrencyCurrentVersion)
            {
                return SaveLoadResult<CurrencySaveData>.Failed(
                    ESaveLoadFailure.UnsupportedVersion,
                    UnsupportedSaveVersionException.BuildMessage(
                        "Currency",
                        schemaVersion,
                        SaveSchema.CurrencyCurrentVersion));
            }

            CurrencySaveData data = snapshot.ConvertTo<CurrencySaveData>();
            if (data == null)
            {
                return SaveLoadResult<CurrencySaveData>.Failed(
                    ESaveLoadFailure.Unreadable,
                    "Currency 문서를 변환하지 못했습니다.");
            }

            return SaveLoadResult<CurrencySaveData>.Loaded(data);
        }
        catch (Exception e)
        {
            // 문서가 없는 것과 읽지 못한 것은 다르다. 실패를 기본값으로 바꾸지 않는다.
            Debug.LogError("Currency 로드 실패" + e.Message);
            return SaveLoadResult<CurrencySaveData>.Failed(
                ESaveLoadFailure.Unreachable,
                e.Message);
        }
    }
}
