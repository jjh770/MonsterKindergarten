using System.Collections.Generic;

// 소유자 기반 정지 요청 집합. 요청이 하나라도 있으면 정지.
// 우선순위가 없어 단순 집합으로 충분하다(Clicker의 모드 스택과 대비).
// 여러 소유자(튜토리얼, 엔딩 등)가 겹쳐 요청해도, 마지막 소유자가 해제해야 재개된다.
public sealed class OwnerPauseSet
{
    private static readonly IEqualityComparer<object> ReferenceComparer =
        new ReferenceEqualityComparer();

    private readonly HashSet<object> _owners = new HashSet<object>(ReferenceComparer);

    public bool IsPaused => _owners.Count > 0;

    // null owner는 무시한다. 같은 owner의 중복 요청은 idempotent다.
    public void Push(object owner)
    {
        if (owner == null) return;
        _owners.Add(owner);
    }

    // 없는 owner 해제는 무시한다. 중복 해제도 무해하다.
    public void Release(object owner)
    {
        if (owner == null) return;
        _owners.Remove(owner);
    }

#if UNITY_EDITOR
    public int Count => _owners.Count;
#endif

    // owner는 참조 동일성으로 구분한다. Clicker가 ReferenceEquals로 owner를
    // 구분하는 것과 의미를 맞춘다.
    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public new bool Equals(object x, object y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) =>
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
