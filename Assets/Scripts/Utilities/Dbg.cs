using System.Diagnostics;

/// <summary>
/// 조건부 컴파일 디버그 래퍼.
/// UNITY_EDITOR 또는 DEVELOPMENT_BUILD 심볼이 없는 빌드에서는
/// Dbg.Log() 호출 자체가 컴파일 단계에서 제거됩니다(문자열 보간 비용 포함).
/// LogWarning / LogError는 항상 실행됩니다.
/// </summary>
public static class Dbg
{
    [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
    public static void Log(object message) => UnityEngine.Debug.Log(message);

    public static void LogWarning(object message) => UnityEngine.Debug.LogWarning(message);
    public static void LogError(object message)   => UnityEngine.Debug.LogError(message);
}
