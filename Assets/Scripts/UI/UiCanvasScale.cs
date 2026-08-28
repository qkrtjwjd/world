using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 코드에서 만든 Screen Space - Overlay 캔버스의 스케일 기준을 도트 규격에 맞춥니다.
/// </summary>
/// <remarks>
/// <para><b>기준 해상도의 단일 출처다.</b> 640x360 을 여기 말고 다른 곳에 적지 않는다 (CLAUDE.md §11).</para>
///
/// <para>⚠ 이걸 안 쓰면 UI 가 게임과 따로 논다. <c>PixelPerfectCamera</c> 는 640x360 을 정수배로
/// 확대해 그리는데, 기본값 <c>ConstantPixelSize</c> 캔버스는 확대에 전혀 반응하지 않아
/// 1080p(3배)에서 UI 만 1배로 남는다. 기준을 1920x1080 으로 두는 것도 같은 이유로 어긋난다.</para>
///
/// <para>창이 640x360 의 정수배일 때(1280x720 · 1920x1080 …) 게임 확대율과 정확히 일치한다.
/// 정수배가 아닌 창에서는 게임은 내림한 정수배, UI 는 소수배라 서로 조금 어긋난다 —
/// 에디터 Game View 가 그런 경우다.</para>
/// </remarks>
public static class UiCanvasScale
{
    public const int RefWidth  = 640;
    public const int RefHeight = 360;

    /// <summary>이미 붙어 있는 스케일러를 규격에 맞춥니다.</summary>
    public static CanvasScaler Apply(CanvasScaler scaler)
    {
        if (scaler == null) return null;
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.Expand;
        return scaler;
    }

    /// <summary><c>CanvasScaler</c> 를 붙이고 규격에 맞춥니다.</summary>
    public static CanvasScaler Add(GameObject go) => Apply(go.AddComponent<CanvasScaler>());
}
