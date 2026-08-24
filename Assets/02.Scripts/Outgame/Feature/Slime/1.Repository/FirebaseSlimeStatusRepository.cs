#if !UNITY_WEBGL || UNITY_EDITOR

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

    public async UniTask<SlimeStatusSaveData> Load()
    {
        try
        {
            string userId = _auth.CurrentUser.UserId;
            DocumentSnapshot snapshot = await _db.Collection(COLLECTION_NAME).Document(userId).GetSnapshotAsync().AsUniTask();

            if (!snapshot.Exists)
            {
                return SlimeStatusSaveData.Default;
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
                throw new UnsupportedSaveVersionException(
                    "SlimeStatus",
                    schemaVersion,
                    SaveSchema.SlimeCurrentVersion);
            }

            if (schemaVersion < SaveSchema.SlimeCurrentVersion)
            {
                LegacySlimeStatusSaveData legacyData =
                    snapshot.ConvertTo<LegacySlimeStatusSaveData>();
                return SlimeStatusSaveMigration.Upgrade(legacyData);
            }

            SlimeStatusSaveData data = snapshot.ConvertTo<SlimeStatusSaveData>();
            if (data != null)
            {
                data.ActiveSlimes ??= new System.Collections.Generic.List<SlimeInstanceSaveData>();
                return data;
            }
            return SlimeStatusSaveData.Default;
        }
        catch (UnsupportedSaveVersionException)
        {
            throw;
        }
        catch (Exception e)
        {
            Debug.LogError("SlimeStatus 로드 실패: " + e.Message);
            return SlimeStatusSaveData.Default;
        }
    }
}
#endif
