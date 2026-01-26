using UnityEngine;

[CreateAssetMenu(fileName = "MonsterLevelData", menuName = "Data/MonsterLevelData")]
public class MonsterLevelData : ScriptableObject
{
    [System.Serializable]
    public class LevelInfo
    {
        public int Level;
        public int Point;
        public float AutoClickInterval;
    }

    [SerializeField] private LevelInfo[] _levelInfos;

    public int GetPoint(int level)
    {
        if (_levelInfos == null || _levelInfos.Length == 0)
            return 1;

        // 레벨에 해당하는 데이터 찾기
        foreach (var info in _levelInfos)
        {
            if (info.Level == level)
                return info.Point;
        }

        // 못 찾으면 마지막 레벨 데이터 반환
        return _levelInfos[_levelInfos.Length - 1].Point;
    }

    public float GetAutoClickInterval(int level)
    {
        if (_levelInfos == null || _levelInfos.Length == 0)
            return 0f;

        foreach (var info in _levelInfos)
        {
            if (info.Level == level)
                return info.AutoClickInterval;
        }

        return _levelInfos[_levelInfos.Length - 1].AutoClickInterval;
    }
}
