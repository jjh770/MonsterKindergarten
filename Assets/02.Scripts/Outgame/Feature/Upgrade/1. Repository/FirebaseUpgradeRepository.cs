
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

    // 쓰기 실패를 삼키지 않는다.
    //
    // 여기서 잡으면 HybridRepository가 저장이 실패한 사실을 알 수 없고, 로컬만 최신인
    // 채로 클라우드가 조용히 멈춘다. 그 사실은 재설치나 기기 변경 때에야 드러난다.
    //
    // 로드 경로와 같은 규율이다. 리포지토리는 무슨 일이 있었는지 충실히 알리고,
    // 무엇을 할지는 두 저장소를 함께 아는 위층이 정한다.
    public async UniTask Save(UpgradeSaveData saveData)
    {
        string userId = _auth.CurrentUser.UserId;
        await _db.Collection(UPGRADE_COLLECTION_NAME).Document(userId).SetAsync(saveData).AsUniTask();
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
