using UnityEngine;

/// <summary>
/// 싱글턴 중복 인스턴스를 안전하게 지운다.
///
/// <para>⚠ <b>가드에서 <c>Destroy(gameObject)</c> 를 그냥 부르면 안 된다.</b>
/// 한 GameObject 에 매니저를 여러 개 얹은 경우 — 이 프로젝트의 <c>GameManager</c> 프리팹은
/// 스크립트 <b>20개</b>가 한 GO 에 실려 있다 — 늦게 깨어난 쪽이 그 GO 를 <b>통째로</b> 파괴한다.
/// 어느 쪽이 먼저 깨는지는 정해져 있지 않아 증상이 들쭉날쭉하고, <b>에러가 0건이라 콘솔로도
/// 안 잡힌다.</b> 증상은 "아이템창이 안 열린다" 처럼 엉뚱한 곳에서 나타난다.</para>
///
/// <para>실제로 두 번 당했다 — 2026-08-18 에 적 프리팹 5종이 이 함정으로 <b>모든 턴제 전투를
/// 스폰 즉시 승리</b>로 끝내고 있었고, <c>Shelter.unity</c> 에서는 <c>InventoryManager</c> 가
/// 두 프리팹으로 겹쳐 있었다(2026-08-27 해소).</para>
/// </summary>
public static class SingletonGuard
{
    /// <summary>
    /// 중복된 컴포넌트를 지운다.
    /// 같은 GameObject 에 <b>다른 MonoBehaviour 가 있으면 컴포넌트만</b> 지우고,
    /// 자기 혼자면 빈 껍데기가 남지 않게 GameObject 째로 지운다.
    /// </summary>
    public static void DestroyDuplicate(Component duplicate)
    {
        if (duplicate == null) return;

        // 판정 기준을 전체 Component 가 아니라 MonoBehaviour 로 잡는다.
        // 전용 GO 라도 AudioSource·Collider 같은 것이 함께 붙어 있는 경우가 흔한데,
        // 그것들까지 세면 껍데기 GO 가 계속 남는다.
        int others = 0;
        foreach (var mb in duplicate.GetComponents<MonoBehaviour>())
            if (mb != null && !ReferenceEquals(mb, duplicate)) others++;

        if (others == 0) Object.Destroy(duplicate.gameObject);
        else             Object.Destroy(duplicate);
    }
}
