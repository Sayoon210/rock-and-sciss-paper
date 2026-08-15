# 2026-08-16 — 프로젝트 구조와 빌드 경계

[로직 경계 결정](2026-08-16-logic-boundary-decision.md)에서 "경계를 긋고 테스트 프레임워크를 도입한다"까지 정한 뒤, 그 경계를 **실제로 어떻게 강제할 것인가**를 정한 기록.

## 테스트 프레임워크: xUnit 선택

- 후보: xUnit / NUnit / MSTest. 셋 다 기능은 대동소이.
- **xUnit 채택. Why**: 새 .NET 프로젝트의 사실상 기본값이라 자료가 가장 많고, `[SetUp]`/`[TearDown]` 같은 프레임워크 전용 속성 없이 생성자와 `IDisposable`을 그대로 씀 — 배울 게 적음.
- 이 프로젝트 규모에선 셋 중 뭘 골라도 실질 차이 없음. 고민할 지점이 아니라고 판단하고 빠르게 확정.
- `[Theory]` + `[InlineData]`가 이 게임에 특히 잘 맞음 — 상성 9조합, 조커 × 6종 특수, 리셋 충돌 같은 매트릭스를 함수 하나로 커버 가능.
- **부수 이득**: 경계를 그은 덕분에 Godot 전용 테스트 도구(GdUnit4 등) 없이 평범한 .NET 표준 도구를 씀.

## 경계를 어떻게 강제할 것인가

두 가지 방식이 있었음:

**A. 게임 프로젝트에 테스트 프로젝트만 추가**
- 간단하지만 테스트 프로젝트가 Godot 프로젝트를 참조하므로 GodotSharp가 딸려옴.
- 경계가 **코드 규율로만** 지켜짐 — 실수로 `Resource`를 써도 빌드는 통과함.

**B. 순수 로직을 별도 클래스 라이브러리로 분리 (채택)**
- 프로젝트 3개: `Core`(순수 로직) / Godot 게임 프로젝트 / `Tests`
- 참조 방향이 단방향: `Tests → Core ← Godot 프로젝트 → GodotSharp`
- `Core`는 **아무것도 참조하지 않음.** Godot 프로젝트의 존재조차 모름.
- **Why**: Core 안에서 `using Godot;`이나 `CardData`를 쓰면 컴파일러가 이름을 해석할 수 없어서 `error CS0246`으로 **빌드가 실패함.** 규율이 아니라 컴파일러가 경계를 강제함.
- 대가: 프로젝트가 3개로 늘어남. 지금 파일이 거의 없는 시점이라 감당 가능하다고 판단.

## 타입 배치가 자연히 갈림

참조가 단방향이라 아래 배치가 강제됨:

| Core (Godot 모름) | Godot 프로젝트 (Core 참조) |
|---|---|
| `CardKind` (enum) | `CardData : Resource` (`CardKind` 필드를 가짐) |
| `MatchSession` | `CardDatabase` |
| `RoundResolver` | `GameState` / `NetworkManager` |
| `RoundResult` | UI |
| `ICardEffect` 구현들 | |

- `CardData`가 `CardKind`를 가지는 건 가능 (Godot 프로젝트가 Core를 참조하므로).
- `MatchSession`이 `CardData`를 가지는 건 **불가능** — 이게 원했던 결과.

## 알게 된 것: .csproj

- Godot C#은 표준 .NET 방식을 그대로 노출함. 첫 C# 스크립트를 만들거나 빌드하는 순간 `.csproj`(프로젝트 설정)와 `.sln`(프로젝트 묶음 목록)이 자동 생성됨.
- GDScript만 쓰면 생성되지 않고, Unity는 자동 생성 후 숨겨서 관리하기 때문에 안 보임 — Unity의 `.asmdef`(Assembly Definition)로 어셈블리를 나누는 게 이것의 Unity판.
- **경계가 선언되는 물리적 위치가 이 파일임.** Godot 쪽 `.csproj`에 `<ProjectReference>`(Core 참조)와 `<Compile Remove="Core/**" />`(Core 폴더는 내가 컴파일하지 않음)를 적고, Core 쪽은 `Microsoft.NET.Sdk`(순수 .NET)를 쓰며 참조를 비워둠. 이 몇 줄이 "빌드 실패로 경계를 강제한다"의 실체.

## 남은 결정 / 작업

- **폴더 구조 미확정** — 두 안 중 선택 필요:
  - B-1: 리포 루트에 `Core/` 추가하고 Godot `.csproj`에서 제외. 지금 구조에서 `Scripts/Game/`만 옮기면 됨.
  - B-2: Godot 프로젝트를 하위 폴더로 내리고(`game/`) `core/`, `tests/`를 형제로. 구조는 깔끔하나 `project.godot` 포함 전부 이사.
- 현재 `.csproj`가 아직 없음 (C# 파일이 하나도 없어서 미생성). 세팅 1단계가 이것.
- `Scripts/Game/`은 현재 Godot 프로젝트 폴더 안이라 그대로 두면 Godot `.csproj`가 자동으로 긁어감 — 반드시 이동 필요.
- 구조 확정 후 [CLAUDE.md](../CLAUDE.md)에 "새 순수 로직은 어디에 두는가" 규칙 추가 예정.
