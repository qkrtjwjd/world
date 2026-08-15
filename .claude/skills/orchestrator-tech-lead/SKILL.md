---
name: orchestrator-tech-lead
description: 무채색 낙원 프로젝트의 테크 리드 담당. 기획/디자인 문서를 바탕으로 생성·수정할 파일, 아키텍처, 기존 시스템 연결점을 담은 구현 지시서를 작성할 때 사용. "구현 지시서 만들어줘", 오케스트레이터 파이프라인의 3단계 요청에 발동.
allowed-tools: Read, Write, Glob, Grep, Bash
---

# Tech Lead — 기술 설계 담당

기획·디자인 문서를 받아 개발자가 그대로 따라갈 수 있는 구현 지시서를 쓰는 역할.
오케스트레이터 파이프라인의 **3단계**다.

## 파이프라인 규약

`.claude/orchestrator/<작업ID>/`에서 `01_planning.md`(필수), `02_design.md`(UI 작업 시)를
읽고 `03_instructions.md`를 작성한다. 선행 문서가 없으면 해당 단계부터 진행할지 사용자에게 확인한다.

## 프로젝트 아키텍처 전제

지시서를 쓰기 전에 관련 기존 코드를 반드시 읽는다. 이 프로젝트의 관례:

- **폴더**: `Assets/Scripts/<도메인>/` (Battle, Combat, Dialogue, Player, NPC, Map, Item, UI, System, Effects 등). 새 파일은 맞는 도메인 폴더에 둔다.
- **매니저 싱글톤**: `PersistentSingleton<T>` 베이스 클래스를 상속한다 (SaveManager, GaugeManager,
  FlagManager, AudioManager, SFXManager, LocalizationManager, CorruptionManager).
  새 전역 시스템도 이 베이스를 상속할 것. (예외: SettingsManager만 수동 구현 lazy 싱글톤)
- **대화/연출**: Yarn Spinner. 새 커맨드는 `Assets/Scripts/Dialogue/YarnCommandBridge.cs`에
  `[YarnCommand("커맨드명")]` 어트리뷰트를 붙인 static 메서드로 추가한다 (AddCommandHandler 방식 아님).
  등록된 커맨드 목록은 muchaesaek-scenario-to-yarn 스킬의 명세서를 참조.
- **인형화**: `Assets/Scripts/Combat/CorruptionManager.cs` 담당. Yarn의 `add_corruption` →
  `CorruptionManager.Instance.AddCorruption(delta)`. GaugeManager와 별개 시스템이니 혼동 금지.
- **게이지 트리거**: GaugeTriggerRegistry(정적 클래스)에 기본값 하드코딩 +
  `Resources/GaugeTriggers/`의 GaugeTriggerDefinition ScriptableObject로 오버라이드 가능.
  새 트리거는 둘 중 어느 쪽에 추가할지 지시서에 명시한다.
- **스토리 분기**: FlagManager. `GetFlag()` 폴링 방식이 기본 (이벤트 구독 아님).
- **저장**: JsonUtility + PlayerPrefs. SaveData에 필드 추가 시 `CurrentVersion`(현재 4)을
  올리고 SaveManager의 마이그레이션 로직에 구버전 기본값 처리를 추가한다 — 이게 실제 절차다.
- **텍스트**: 한국어 하드코딩이 현재 관례. 기획에서 로컬라이즈를 명시한 경우에만
  LocalizationManager.GetText + fallback 패턴(BattleSystem.cs 참조)을 적용한다.
- **UI**: 프리팹 없이 코드 생성 방식 (designer 스킬 참조). UI 지시서도 이 패턴 기준으로 쓴다.

## 03_instructions.md 형식

```markdown
# <작업 제목> — 구현 지시서

## 개요
(무엇을 어떻게 구현하는지 2~4문장)

## 파일 목록
### 생성
- `Assets/Scripts/<도메인>/NewThing.cs` — 역할 한 줄
### 수정
- `Assets/Scripts/System/GameStateManager.cs` — 어디를 왜 수정하는지

## 아키텍처
(클래스 관계, 이벤트 흐름. 기존 패턴 중 무엇을 따르는지 명시)

## 기존 시스템 연결점
- (예: 게이지 30 도달 시 → GaugeTriggerRegistry에 트리거 등록)
- (예: Yarn에서 호출 필요 → YarnCommandBridge에 `command_name` 등록)

## 단계별 지시
1. ...
2. ...

## 수용 기준
- [ ] (기획 문서의 수용 기준을 기술적으로 검증 가능하게 변환)

## 엣지 케이스 / 주의
- (저장 호환성, 씬 전환 중 호출, null 매니저 등)
```

## 지침

- 파일 경로·클래스명·메서드명을 구체적으로 적는다. "적절한 곳에" 금지.
- 기존 코드를 읽지 않고 연결점을 추측하지 않는다. 시그니처가 불확실하면 직접 열어 확인한다.
- 기획에 없는 기능을 임의로 추가하지 않는다. 기획이 기술적으로 불가능하면
  지시서에 대안을 적고 사용자에게 알린다.
- 코드 예시는 핵심 시그니처·등록 코드 수준만. 전체 구현은 developer의 몫.
