---
name: run-playmode-smoke
description: 무채색 낙원 게임을 실제로 구동해 확인할 때 사용. Unity 에디터 플레이 모드에 자동 진입해 지정 씬을 N초 구동하고 스크린샷 + 에러/예외 로그를 수집한 뒤 자동 종료한다. "게임 실행해줘", "스모크 테스트", "실제로 도는지 확인", /run 요청에 발동. 컴파일만 검증할 때는 이 스킬의 '컴파일 전용 검증' 절 참고.
---

# Unity 플레이 모드 스모크 (무채색 낙원)

에디터 GUI를 커맨드라인으로 띄워 플레이 모드에 자동 진입시키고, 스크린샷과 에러 로그를 남긴 뒤 스스로 종료하는 절차. 전체 소요 약 1~3분.

## 전제 조건

- Unity **6000.4.0f1** 설치 경로: `C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe`
- **Unity 에디터가 이 프로젝트를 열고 있으면 안 됨** (프로젝트 락). 확인:
  ```powershell
  Test-Path "C:\Users\ThinkPlant\world\Temp\UnityLockfile"   # False 여야 함
  ```
- 에디터 창이 화면에 잠깐 나타남 (batchmode 아님 — 플레이 모드 렌더링에 GUI 필요). 사용자에게 미리 알릴 것.

## 절차

1. **스모크 스크립트 투입**: `references/AutoPlaySmoke.cs` 를 `Assets\Editor\AutoPlaySmoke.cs` 로 복사.
   - 동작: 지정 씬 열기 → 플레이 모드 진입 → 8초 시점에 Game View 스크린샷(`smoke_gameview.png`, UI 포함) + 카메라 캡처(`smoke_camera.png`, 폴백) → 10초 시점에 `smoke_log.txt` 기록 후 `EditorApplication.Exit`.
   - 종료 코드: 0 = 에러/예외 없음, 1 = Error/Exception/Assert 로그 있음, 2 = 플레이 모드 진입 실패(120초 타임아웃).

2. **실행** (PowerShell, 백그라운드 권장 — 수 분 소요):
   ```powershell
   $out = "<스크래치패드>\smoke"; New-Item -ItemType Directory -Force $out | Out-Null
   & "C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe" `
     -projectPath "C:\Users\ThinkPlant\world" `
     -executeMethod AutoPlaySmoke.Run `
     -smokeScene "Assets/Scenes/Home.unity" `
     -smokeOut $out -logFile "$out\editor.log" | Out-Null; $LASTEXITCODE
   ```
   - `-smokeScene` 로 대상 씬 선택. 빌드 씬 목록: TitleScene, Home, MapScene, DarkReality, Shelter, BadEndingScene, IntroScene, CreditsScene (`ProjectSettings/EditorBuildSettings.asset`).
   - 검증 대상 코드가 있는 씬을 고를 것 (예: 상호작용 오브젝트·전투는 Home/MapScene/DarkReality).

3. **결과 판독**:
   - 종료 코드 0 + `smoke_log.txt` 첫 줄 "에러/예외 0건" 확인 (PS 5.1 `Get-Content` 은 UTF-8 무BOM 한글이 깨져 보임 — `-Encoding UTF8` 옵션 사용).
   - `smoke_gameview.png` 를 Read 로 **직접 열어볼 것**. 빈 화면/단색 프레임이면 구동 실패로 간주.

4. **정리 (필수)**: 스크립트와 자동 생성된 .meta 삭제 — 작업 트리에 남기면 안 됨.
   ```powershell
   Remove-Item -Force "C:\Users\ThinkPlant\world\Assets\Editor\AutoPlaySmoke.cs", "C:\Users\ThinkPlant\world\Assets\Editor\AutoPlaySmoke.cs.meta"
   ```

## 한계

- 부팅 + 씬 초기화 + 8초 방치 검증까지만 커버. 키 입력·상호작용·세이브/로드 등 플레이 검증은 사용자가 에디터에서 직접 해야 함.
- 대사/연출 시퀀스가 자동 시작되는 씬은 8초 시점 화면이 시퀀스 중간일 수 있음 (정상).

## 컴파일 전용 검증 (씬 구동 불필요할 때)

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe" -batchmode -quit -nographics `
  -projectPath "C:\Users\ThinkPlant\world" -logFile "<스크래치패드>\unity_compile.log"
```
로그에서 `error CS` 0건 + `Exiting batchmode successfully` 확인. 참고: `dotnet build` 는 생성 csproj 가 낡아 불가.
