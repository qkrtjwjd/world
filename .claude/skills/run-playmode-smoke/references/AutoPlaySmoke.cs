using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 임시 스모크 테스트 진입점 (커맨드라인 -executeMethod AutoPlaySmoke.Run 전용).
/// 지정 씬을 열고 플레이 모드로 8초 구동한 뒤 스크린샷과 에러 로그를 남기고 에디터를 종료합니다.
/// Assets\Editor\ 에 복사해 사용하고, 검증 후 반드시 삭제할 것 (.meta 포함).
/// </summary>
public static class AutoPlaySmoke
{
    const string FlagKey = "AutoPlaySmoke.Active";
    const string OutKey  = "AutoPlaySmoke.OutDir";

    const float CaptureAt = 8f;
    const float ExitAt    = 10f;
    const float HardLimit = 120f;

    static double _start;
    static bool   _captured;
    static readonly List<string> _errors = new List<string>();

    public static void Run()
    {
        string scene  = GetArg("-smokeScene") ?? "Assets/Scenes/Home.unity";
        string outDir = GetArg("-smokeOut") ?? Path.Combine(Path.GetTempPath(), "unity_smoke");
        Directory.CreateDirectory(outDir);

        SessionState.SetBool(FlagKey, true);
        SessionState.SetString(OutKey, outDir);

        EditorSceneManager.OpenScene(scene);

        // 도메인 리로드가 꺼진 프로젝트에서도 동작하도록 진입 전에 직접 훅
        Attach();
        EditorApplication.EnterPlaymode();
    }

    // 도메인 리로드가 켜진 프로젝트: 플레이 모드 진입 시 재컴파일 후 여기로 재훅
    [InitializeOnLoadMethod]
    static void HookAfterReload()
    {
        if (!SessionState.GetBool(FlagKey, false)) return;
        if (!EditorApplication.isPlayingOrWillChangePlaymode) return;
        Attach();
    }

    static void Attach()
    {
        _start = EditorApplication.timeSinceStartup;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            _errors.Add($"[{type}] {condition}\n{stackTrace}");
    }

    static void Tick()
    {
        double elapsed = EditorApplication.timeSinceStartup - _start;
        string outDir  = SessionState.GetString(OutKey, Path.GetTempPath());

        if (!EditorApplication.isPlaying)
        {
            if (elapsed > HardLimit) Finish(outDir, 2, "플레이 모드 진입 실패 (타임아웃)");
            return;
        }

        if (!_captured && elapsed >= CaptureAt)
        {
            _captured = true;
            ScreenCapture.CaptureScreenshot(Path.Combine(outDir, "smoke_gameview.png"));
            CaptureCamera(Path.Combine(outDir, "smoke_camera.png"));
        }

        if (elapsed >= ExitAt || elapsed > HardLimit)
            Finish(outDir, _errors.Count == 0 ? 0 : 1, $"에러/예외 {_errors.Count}건");
    }

    static void Finish(string outDir, int code, string summary)
    {
        var lines = new List<string> { $"result: {summary}" };
        lines.AddRange(_errors);
        File.WriteAllLines(Path.Combine(outDir, "smoke_log.txt"), lines);
        EditorApplication.Exit(code);
    }

    /// <summary>Screen Space Overlay UI 는 안 잡히지만 Game View 부재 시의 폴백.</summary>
    static void CaptureCamera(string path)
    {
        var cam = Camera.main;
        if (cam == null) cam = Object.FindAnyObjectByType<Camera>();
        if (cam == null) return;

        var rt   = new RenderTexture(1280, 720, 24);
        var prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prev;

        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        RenderTexture.active = null;

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
    }

    static string GetArg(string name)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }
}
