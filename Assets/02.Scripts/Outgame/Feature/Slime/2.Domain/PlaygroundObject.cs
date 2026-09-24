using System;

// 장식장 놀이터에 놓는 오브젝트. 저장 데이터에는 정수로 남으므로 기존 값은
// 바꾸지 않고 새 종류는 뒤에 추가한다.
public enum EPlaygroundObjectType
{
    Bumper = 0,
    Cannon = 1,

    Count,
}

// 방에 놓인 오브젝트 하나. 위치는 방 안의 좌표다.
//
// 슬라임 위치는 저장하지 않고 복원할 때 흩뿌리지만(기획서 §7.2) 이쪽은 다르다.
// 플레이어가 직접 놓은 것이라 매번 자리가 바뀌면 꾸민 의미가 없다.
[Serializable]
public readonly struct PlacedPlaygroundObject
{
    public EPlaygroundObjectType Type { get; }
    public float X { get; }
    public float Y { get; }

    public PlacedPlaygroundObject(EPlaygroundObjectType type, float x, float y)
    {
        Type = type;
        X = x;
        Y = y;
    }
}

public static class PlaygroundRules
{
    // 종류마다 방에 둘 수 있는 개수. 방 안쪽이 5.6 x 8.4라 더 늘리면 슬라임이
    // 다닐 틈이 없어진다. 밸런스 값이므로 저장에서 넘치면 막지 않고 잘라낸다.
    public const int MaxPerType = 2;

    // 놓을 수 있는 범위. 벽 안쪽(x ±2.8, y ±4.2)에서 오브젝트 반지름만큼 물러난 값이다.
    // 벽에 붙여 놓으면 슬라임이 지나갈 틈이 사라진다.
    public const float AreaMinX = -2.2f;
    public const float AreaMaxX = 2.2f;
    public const float AreaMinY = -3.6f;
    public const float AreaMaxY = 3.6f;

    // 위 범위가 벽에서 물러난 거리. 가장 큰 오브젝트인 대포의 그림 반지름이
    // 0.600이라, 중심을 여기까지만 놓아야 그림이 벽을 넘지 않는다.
    public const float WallInset = 0.6f;

    // 서로 이만큼은 떨어뜨린다. 겹쳐 놓으면 어느 쪽이 슬라임을 잡았는지 알 수 없다.
    public const float MinimumSpacing = 1.1f;

    // 벽은 씬에 있고 이 규칙은 도메인에 있다. 세이브를 불러올 때 쓰는 값이라
    // 씬에서 읽어 오면 세이브의 유효성이 씬 상태를 따라 흔들린다. 그래서 값을
    // 가져오는 대신 둘이 어긋났는지 확인할 방법만 내어 준다.
    public static bool MatchesWalls(
        float wallMinX,
        float wallMinY,
        float wallMaxX,
        float wallMaxY)
    {
        const float Tolerance = 0.01f;
        return IsNear(wallMinX + WallInset, AreaMinX, Tolerance) &&
               IsNear(wallMaxX - WallInset, AreaMaxX, Tolerance) &&
               IsNear(wallMinY + WallInset, AreaMinY, Tolerance) &&
               IsNear(wallMaxY - WallInset, AreaMaxY, Tolerance);
    }

    private static bool IsNear(float a, float b, float tolerance)
    {
        float difference = a - b;
        return difference < 0f ? -difference <= tolerance : difference <= tolerance;
    }

    public static bool IsValid(EPlaygroundObjectType type)
    {
        return type >= EPlaygroundObjectType.Bumper &&
               type < EPlaygroundObjectType.Count;
    }

    public static float ClampX(float x)
    {
        return x < AreaMinX ? AreaMinX : (x > AreaMaxX ? AreaMaxX : x);
    }

    public static float ClampY(float y)
    {
        return y < AreaMinY ? AreaMinY : (y > AreaMaxY ? AreaMaxY : y);
    }
}
