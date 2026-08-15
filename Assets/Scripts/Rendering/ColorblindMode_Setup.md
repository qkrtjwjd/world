# 색맹 모드 — 유니티 에디터 배선 가이드

설정 메뉴의 **접근성 탭 → 색맹 모드**는 코드가 완결돼 있으나, 실제로 화면 색이 바뀌려면
아래 URP 배선을 **에디터에서 1회 수동**으로 해야 한다. (기존 `RealityGradeRenderFeature`와 동일한 방식)

> 배선 전에도 색맹 모드 UI/설정 저장은 동작하며, 다른 설정에는 아무 영향이 없다.
> `ColorblindRenderFeature.Instance == null`이면 코드가 조용히 무시하기 때문.

---

## 준비물 (이미 코드로 생성됨)
- 셰이더: `Assets/Shaders/ColorblindCorrect.shader`  (`Custom/ColorblindCorrect`)
- 렌더러 피처: `Assets/Scripts/Rendering/ColorblindRenderFeature.cs`
- 렌더 패스: `Assets/Scripts/Rendering/ColorblindRenderPass.cs`
- 브리지: `PostProcessingController`가 설정값을 피처에 전달 (별도 작업 불필요)

---

## 1단계 — 머티리얼 생성
1. Project 창에서 `Assets/Shaders/ColorblindCorrect.shader` 선택.
2. 우클릭 → **Create ▸ Material** (또는 `Assets/Shaders/` 폴더에서 새 머티리얼 생성).
3. 이름을 **`ColorblindCorrect`** 로 지정 → 최종 경로 `Assets/Shaders/ColorblindCorrect.mat`.
4. 머티리얼의 Shader 드롭다운이 **`Custom/ColorblindCorrect`** 인지 확인
   (셰이더에서 우클릭→Create Material로 만들면 자동 지정됨).
   - 참고 대조: 기존 `Assets/Shaders/RealityColorGrade.mat` 과 동일한 구조.

## 2단계 — Renderer에 피처 추가
1. **활성 URP Renderer** 열기: `Assets/New Universal Render Pipeline Asset 1_Renderer.asset`
   (여기에 이미 `RealityGradeRenderFeature`, `GlitchRenderFeature`가 등록돼 있다 — 같은 곳에 추가).
   - 헷갈리면 Project Settings ▸ Graphics(또는 Quality)에서 현재 사용 중인 URP Asset →
     그 Asset의 Renderer 슬롯이 위 파일을 가리키는지 확인.
2. Inspector 하단 **Add Renderer Feature** ▸ **Colorblind Render Feature** 선택.
3. 추가된 피처의 **Correct Material** 슬롯에 1단계의 `ColorblindCorrect` 머티리얼을 드래그해 연결.

## 3단계 — 확인
1. Play 진입 → `ESC`(또는 타이틀의 설정 버튼) → 설정 패널 → **♿ 접근성 탭**.
2. **색맹 모드**에서 `없음 / 적록1형 / 적록2형 / 청황` 을 전환.
3. 월드(게임) 화면의 색이 daltonization 보정으로 바뀌면 성공.
   - UI(설정 패널 자체)는 Screen Space - Overlay Canvas라 보정에서 자동 제외됨 → 정상.
   - 씬을 옮겨도 유지되는지 확인(설정은 전역 싱글턴 + PlayerPrefs).

---

## 동작 원리 (참고)
- `SettingsManager.SetColorblindMode(int)` → `OnColorblindModeChanged` 이벤트 발행.
- `PostProcessingController`가 이 이벤트를 구독 + `Start()`에서 초기값을 전달 →
  `ColorblindRenderFeature.Instance.SetMode(mode)` → 셰이더 `_Mode` 갱신.
- 모드 값: `0=없음(패스 미삽입, 오버헤드 0)`, `1=적록1형(Protanopia)`, `2=적록2형(Deuteranopia)`, `3=청황(Tritanopia)`.
- 효과는 `AfterRenderingPostProcessing` 타이밍, Game 카메라의 Base 카메라에만 적용.

## 트러블슈팅
- **색이 안 바뀜**: (a) Correct Material 미연결, (b) 다른(비활성) Renderer에 추가, (c) 머티리얼 셰이더가 `Custom/ColorblindCorrect`가 아님 — 3가지를 우선 점검.
- **전체가 이상하게 물듦**: 정상. daltonization은 구분 안 되는 색 성분을 다른 채널로 재분배하므로 비색맹 사용자에겐 부자연스러워 보일 수 있다.
