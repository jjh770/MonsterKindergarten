
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

    public async UniTask Save(SlimeStatusSaveData saveData)
    {
        try
        {
            string userId = _auth.CurrentUser.UserId;
            await _db.Collection(COLLECTION_NAME).Document(userId).SetAsync(saveData).AsUniTask();
        }
        catch (Exception e)
        {
            Debug.LogError("SlimeStatus 저장 실패: " + e.Message);
        }
    }

    public async UniTask<SaveLoadResult<SlimeStatusSaveData>> Load()
    {
        try
        {
            string userId = _auth.CurrentUser.UserId;
            DocumentSnapshot snapshot = await _db.Collection(COLLECTION_NAME).Document(userId).GetSnapshotAsync().AsUniTask();

            if (!snapshot.Exists)
            {
                return SaveLoadResult<SlimeStatusSaveData>.NotFound();
            }

            int schemaVersion = SaveSchema.LegacyVersion;
            if (snapshot.TryGetValue<long>(
                    nameof(ISaveData.SchemaVersion),
                    out long storedSchemaVersion))
            {
                schemaVersion = (int)storedSchemaVersion;
            }

            if (schemaVersion > SaveSchema.SlimeCurrentVersion)
            {
                return SaveLoadResult<SlimeStatusSaveData>.Failed(
                    ESaveLoadFailure.UnsupportedVersion,
                    UnsupportedSaveVersionException.BuildMessage(
                        "SlimeStatus",
                        schemaVersion,
                        SaveSchema.SlimeCurrentVersion));
            }

            if (schemaVersion < SaveSchema.SlimeInstanceVersion)
            {
                LegacySlimeStatusSaveData legacyData =
                    snapshot.ConvertTo<LegacySlimeStatusSaveData>();
                return SaveLoadResult<SlimeStatusSaveData>.Loaded(
                    SlimeStatusSaveMigration.Upgrade(legacyData));
            }

            SlimeStatusSaveData data = snapshot.ConvertTo<SlimeStatusSaveData>();
            if (data == null)
            {
                return SaveLoadResult<SlimeStatusSaveData>.Failed(
                    ESaveLoadFailure.Unreadable,
                    "SlimeStatus 문서를 변환하지 못했습니다.");
            }

            if (schemaVersion < SaveSchema.SlimeCurrentVersion)
            {
                data = SlimeStatusSaveMigration.UpgradeInstanceData(data);
            }

            data.ActiveSlimes ??= new System.Collections.Generic.List<SlimeInstanceSaveData>();
            data.NormalCollectionRegistered =
                SlimeStatusSaveData.NormalizeNormalCollection(
                    data.NormalCollectionRegistered);
            SlimeStatusSaveData.NormalizeCollectionStats(data);
            return SaveLoadResult<SlimeStatusSaveData>.Loaded(data);
        }
        catch (Exception e)
        {
            // 문서가 없는 것과 읽지 못한 것은 다르다. 실패를 기본값으로 바꾸지 않는다.
            Debug.LogError("SlimeStatus 로드 실패: " + e.Message);
            return SaveLoadResult<SlimeStatusSaveData>.Failed(
                ESaveLoadFailure.Unreachable,
                e.Message);
        }
    }
}
