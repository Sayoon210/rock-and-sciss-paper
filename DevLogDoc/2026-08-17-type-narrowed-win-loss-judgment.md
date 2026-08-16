# 2026-08-17 — 승패 판정을 타입으로 제한한 결정

`CardName`/`CardType` enum을 넣은 직후, `WinLossRules.Judge`가 왜 `CardName`이 아니라 별도의 `NormalCard`를 받는지 짚고 그대로 유지하기로 한 기록. **의도적으로 중복을 감수한 설계**라서, 나중에 "이거 왜 enum이 두 개지" 하고 합치려 들기 전에 읽어야 할 문서.

## 결정

`CardName`(11종 전체)과 `NormalCard`(가위/바위/보 3종)를 **별도 enum으로 유지**하고, 둘 사이는 `CardName.ToNormalCard()`로만 건넌다.

```csharp
// GameLogic/CardName.cs — 카드의 정체성 11종
public enum CardName { Rock, Paper, Scissors, Dummy, Joker,
                       Reset, Swap, Transform, Refill, Foresight, Draw }

// GameLogic/WinLossRules.cs — 승패 판정에 들어갈 수 있는 3종
public enum NormalCard { Rock, Paper, Scissors }

public static WinLossResult Judge(NormalCard player1, NormalCard player2)
```

## 왜 이렇게 했나

핵심은 **"조커는 승패 판정에 들어갈 수 없다"는 규칙을 어디에 적어두느냐**임.

| | 런타임 방어 | 타입 제한 (채택) |
|---|---|---|
| 시그니처 | `Judge(CardName, CardName)` | `Judge(NormalCard, NormalCard)` |
| 규칙이 사는 곳 | 함수 본문의 `if (조커면) throw` | 함수 시그니처 그 자체 |
| `Judge(Joker, Dummy)` | **빌드 통과**, 런타임에 터짐 | **컴파일 에러** |
| 언제 발견되나 | 그 분기를 실제로 밟았을 때 | 짜는 즉시 |

라운드 판정 분기가 단순하지 않은 게 결정적이었음 — 조커 > 리셋 > 나머지 특수 > 일반, 무승부, 소멸 vs 덱 바닥. `RoundResolver`를 짜다 보면 어떤 분기에서 특수 카드가 판정 경로로 새는 실수가 충분히 나올 만한 구조고, **그게 플레이테스트 중 desync로 나타나면 원인 추적이 오래 걸림**. 타입이 갈라져 있으면 같은 실수가 빌드 에러로 잡힘.

이건 [GameLogic/CLAUDE.md](../GameLogic/CLAUDE.md)의 "빠른 검증" 원칙과 같은 방향임. 경계를 컴파일러에게 맡겨서 규율이 아니라 빌드가 지키게 하는 것.

## `ToNormalCard`의 예외는 방어 코드가 아님

```csharp
public static NormalCard ToNormalCard(this CardName name)
{
    switch (name)
    {
        case CardName.Rock:     return NormalCard.Rock;
        case CardName.Paper:    return NormalCard.Paper;
        case CardName.Scissors: return NormalCard.Scissors;
        default:
            throw new ArgumentException($"{name} is not a normal card.", nameof(name));
    }
}
```

여기 도달했다는 건 **`RoundResolver`가 조커/특수 분기를 놓쳤다는 뜻**임. 그러니 기본값을 반환하거나 조용히 넘기면 안 되고 반드시 터져야 함. `Tests/CardTypeTests.cs`가 이 동작을 고정해둠:

```csharp
Assert.Throws<ArgumentException>(() => CardName.Joker.ToNormalCard());
```

정상 호출 경로는 항상 3단계 — `IsNormal()` 확인 → `ToNormalCard()` 변환 → `Judge()` 판정.

## 감수한 대가

싸지 않은 선택이라 명시해둠:

- `Rock`/`Paper`/`Scissors`가 두 enum에 **중복 정의**됨.
- 그 때문에 `ToNormalCard`라는, enum 하나였으면 존재하지 않았을 변환 함수가 필요함.
- 호출부가 1단계(`Judge(a, b)`)에서 3단계로 늘어남.

enum 하나로 갔으면 코드가 더 짧음. 프로젝트 CLAUDE.md의 *"Adding abstractions/scaffolding 'in case it's needed later'"* 금지 항목에 걸릴 여지도 있음 — **판단이 갈리는 지점이라는 걸 인정하고 넘어감**. 여기서는 "나중에 필요할까 봐"가 아니라 "지금 짤 `RoundResolver`가 실제로 틀리기 쉬워서"가 근거라 통과시킴.

## 재검토 조건

다음 중 하나가 사실이 되면 이 결정을 다시 볼 것:

- `RoundResolver`를 다 짰는데 `ToNormalCard` 호출부가 한두 곳뿐이고, 타입 분리가 잡아준 실수가 실제로 없었다.
- 세 번째, 네 번째 좁힌 enum(`SpecialCard` 등)을 만들고 싶어진다 — 그 시점엔 패턴이 아니라 증식임.

되돌리려면 `RoundResolver` 작성 **전**이 싸다. 호출부가 늘어난 뒤엔 번거로워짐.
