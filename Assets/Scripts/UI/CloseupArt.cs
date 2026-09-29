using UnityEngine.UI;

/// <summary>
/// 전체화면 클로즈업 Image 를 띄워도 되는지 판정한다 — <b>그림이 들어 있을 때만</b>.
///
/// 2026-09-27: 컷씬의 클로즈업 Image 들은 슬롯은 배선됐지만 스프라이트가 비어 있고 색이 불투명한 흰색이다
/// (수동작업 3 「남은 것은 스프라이트뿐」). 컷씬들이 Image 가 null 인지만 보고 켜서, 그림이 오기 전까지
/// 해당 장면마다 화면 전체가 흰 사각형으로 덮였다(S#04B 각설탕 · 도자기 손, S#04F 각설탕 · 잠금장치,
/// S#08 서랍, S#10 코트 주머니, S#11 라디오 다이얼).
/// 흰 화면 자체가 의도인 효과(문틈 화이트아웃 · 마시멜로 흐림)는 이 판정을 쓰지 않는다.
/// </summary>
public static class CloseupArt
{
    public static bool Has(Image image) => image != null && image.sprite != null;
}
