
using Cysharp.Threading.Tasks;
using Firebase.Auth;
using Firebase.Firestore;
using System;
using UnityEngine;

public class FirebaseUpgradeRepository : IUpgradeRepository
{
    private string UPGRADE_COLLECTION_NAME = "Upgrade";
    private FirebaseAuth _auth = FirebaseAuth.DefaultInstance;
    private FirebaseFirestore _db = FirebaseFirestore.DefaultInstance;

    public async UniTask Save(UpgradeSaveData saveData)
    {
        try
        {
            string userId = _auth.CurrentUser.UserId;
            await _db.Collection(UPGRADE_COLLECTION_NAME).Document(userId).SetAsync(saveData).AsUniTask();
        }
        catch (Exception e)
        {
            Debug.LogError("Upgrade 저장 실패: " + e.Message);
        }
    }

    public async UniTask<SaveLoadResult<UpgradeSaveData>> Load()
    {
        try
        {
            string userId = _auth.CurrentUser.UserId;
            DocumentSnapshot snapshot = await _db.Collection(UPGRADE_COLLECTION_NAME).Document(userId).GetSnapshotAsync().AsUniTask();

            if (!snapshot.Exists)
            {
                return SaveLoadResult<UpgradeSaveData>.NotFound();
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
            if (schemaVersion > SaveSchema.UpgradeCurrentVersion)
            {
                return SaveLoadResult<UpgradeSaveData>.Failed(
                    ESaveLoadFailure.UnsupportedVersion,
                    UnsupportedSaveVersionException.BuildMessage(
                        "Upgrade",
                        schemaVersion,
                        SaveSchema.UpgradeCurrentVersion));
            }

            UpgradeSaveData data = snapshot.ConvertTo<UpgradeSaveData>();
            if (data == null)
            {
                return SaveLoadResult<UpgradeSaveData>.Failed(
                    ESaveLoadFailure.Unreadable,
                    "Upgrade 문서를 변환하지 못했습니다.");
            }

            return SaveLoadResult<UpgradeSaveData>.Loaded(data);
        }
        catch (Exception e)
        {
            // 문서가 없는 것과 읽지 못한 것은 다르다. 실패를 기본값으로 바꾸지 않는다.
            Debug.LogError("Upgrade 로드 실패: " + e.Message);
            return SaveLoadResult<UpgradeSaveData>.Failed(
                ESaveLoadFailure.Unreachable,
                e.Message);
        }
    }
}
