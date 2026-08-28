# Deprecated

게임에서 떼어낸 코드를 지우지 않고 모아두는 곳. **어느 프로젝트에도 컴파일되지 않고, Godot도 읽지 않는다.**

- `RockAndScissPaper.csproj`가 `Deprecated/**/*.cs`를 컴파일 대상에서 제외한다. Godot SDK는
  프로젝트 루트에서 `**/*.cs`를 긁어가므로, 이 한 줄이 없으면 그냥 다시 빌드에 들어온다.
- `.gdignore` 파일이 있어서 Godot 에디터는 이 폴더 전체를 스캔하지 않는다 — `.tres`도 임포트되지 않는다.

원래 있던 자리를 알 수 있게 폴더 구조를 그대로 옮겨 적었다. 되살릴 때는 파일을 같은 경로로
되돌리고, 아래 "떼어낼 때 같이 지운 것"을 되짚으면 된다.

## 2D 대전 화면 전체 — 2026-08-28 제거 (`rebuild-3d`)

3D로 갈아엎으면서 대전 화면의 **표현 계층 전부**를 들어냈다. 규칙(`GameLogic`), 네트워크,
`GameState`/`MatchView`는 **하나도 안 건드렸다** — 신호를 받아 그리는 쪽만 통째로 바뀐 것이라
여기 있는 파일은 전부 "그리는 코드"다.

대체물은 [Scenes/Screens/MatchWorld.tscn](../Scenes/Screens/MatchWorld.tscn) +
[Scripts/Match3D/MatchWorldView.cs](../Scripts/Match3D/MatchWorldView.cs).

| 파일 | 원래 위치 |
| --- | --- |
| `Scripts/UI/MatchScreenUI.cs` | `Scripts/UI/` |
| `Scripts/UI/HandView.cs`, `DeckView.cs` | `Scripts/UI/` |
| `Scripts/UI/CardView.cs`, `CardDropZone.cs`, `CardTooltipView.cs` | `Scripts/UI/` |
| `Scripts/UI/CardFlipEffect.cs`, `CardVanishEffect.cs`, `CardOutcomeEffect.cs`, `CardSlideInEffect.cs`, `JokerDevourEffect.cs` | `Scripts/UI/` |
| `Scenes/Screens/MatchScreen.tscn` | `Scenes/Screens/` |
| `Scenes/Match/` 8종 전부 | `Scenes/Match/` |
| `Shaders/CardDissolve.gdshader`, `CardShatter.gdshader` | `Shaders/` |
| `Assets/Materials/CardDissolve.tres`, `CardShatter.tres` | `Assets/Materials/` |

떼어낼 때 같이 바꾼 것 — 되살리려면 도로 돌려야 하는 것들:

- `ConnectionScreenUI.MATCH_SCENE_PATH` — `MatchScreen.tscn` → `MatchWorld.tscn`
- 매치 진입 시 메뉴 BGM 정지(`AudioManager.StopMusic()`)를 `MatchScreenUI._Ready`에서
  `MatchWorldView._Ready`로 옮겼다
- `Shaders/`와 `Assets/Materials/` 폴더는 안이 비어서 지웠다 — 3D용 셰이더를 처음 쓸 때 다시 만들 것

**주의 — 셰이더 두 개는 `shader_type canvas_item`이다.** 3D에서 그대로 못 쓴다.
[ASSETS-3D.md](../ASSETS-3D.md)에 적어둔 대로 `spatial`로 다시 써야 하고, 그때 이 파일들은
참고 자료지 되살릴 대상이 아니다.

**남겨둔 것 — `MatchDebugUI`.** 2D `Control`이지만 `CardView`/`HandView`에 의존하지 않고
`GameState`만 부른다. 3D 화면이 아직 패도 카드도 안 그리는 동안 **경기를 굴려볼 수 있는
유일한 수단**이라 게임에 남겼다.

## 보충 (Refill) — 2026-08-20 제거

덱에 더미카드 2장을 추가로 넣고 셔플하던 특수카드. 규칙에서 빠졌다.

| 파일 | 원래 위치 |
| --- | --- |
| `GameLogic/Effects/RefillEffect.cs` | `GameLogic/Effects/` |
| `Tests/Effects/RefillEffectTests.cs` | `Tests/Effects/` |
| `Data/Cards/Refill.tres` | `Data/Cards/` |

떼어낼 때 같이 지운 것 — 되살리려면 도로 넣어야 하는 것들:

- `CardName.Refill` (`GameLogic/CardName.cs`) — enum 멤버와 `GetCardType`의 `case`
- `RoundResolver`의 효과 표 한 줄
- `Tests/CardTypeTests.cs` / `Tests/RoundResolverTests.cs` / `Tests/Effects/ResetEffectTests.cs`가
  보충을 표본 특수카드로 쓰던 자리 — 드로우로 갈아끼웠다
- `DESIGN.md`의 특수카드 5종 목록

**주의 — 어휘가 바뀌었다.** 이 폴더의 코드는 `CardName.Dummy` / `CardType.Special`을 쓰는데,
게임에서는 각각 `CardName.Blank` / `CardType.Ability`로 이름이 바뀌었다(더미카드 → 공백카드,
특수카드 → 능력카드). 되살릴 때 같이 고쳐야 한다.

**주의 — `Refill.tres`의 `CardName = 8`은 이제 틀린 값이다.** 그 숫자는 `CardName` enum의 정수값이고,
보충이 빠지면서 뒤 항목이 하나씩 당겨졌다(드로우가 9에서 8이 되었다). 되살릴 때는 두 파일의
숫자를 enum 순서에 맞춰 다시 매기는 것부터 해야 한다.
