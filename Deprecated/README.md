# Deprecated

게임에서 떼어낸 코드를 지우지 않고 모아두는 곳. **어느 프로젝트에도 컴파일되지 않고, Godot도 읽지 않는다.**

- `RockAndScissPaper.csproj`가 `Deprecated/**/*.cs`를 컴파일 대상에서 제외한다. Godot SDK는
  프로젝트 루트에서 `**/*.cs`를 긁어가므로, 이 한 줄이 없으면 그냥 다시 빌드에 들어온다.
- `.gdignore` 파일이 있어서 Godot 에디터는 이 폴더 전체를 스캔하지 않는다 — `.tres`도 임포트되지 않는다.

원래 있던 자리를 알 수 있게 폴더 구조를 그대로 옮겨 적었다. 되살릴 때는 파일을 같은 경로로
되돌리고, 아래 "떼어낼 때 같이 지운 것"을 되짚으면 된다.

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
