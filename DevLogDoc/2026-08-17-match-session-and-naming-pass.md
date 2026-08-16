# 2026-08-17 — `MatchSession` 완성, 그리고 이름 점검 한 바퀴

[`RoundResult`/`RoundResolver` 설계](2026-08-17-round-resolver-design.md) 이후, 특수카드 효과 3종을 붙이고 `MatchSession`까지 만들면서 나온 결정들. **`GameLogic`이 (변화/예지/교체 3종 빼고) 다 찼고, 다음은 Godot 레이어**라는 지점까지의 기록.

## `RoundResolver`에 특수카드 복구 — 순서가 핵심이었음

`ICardEffect` 3종(리셋/보충/드로우)이 생기면서, 모든 특수카드를 거부하던 임시 분기가 **적극적으로 틀린 코드**가 됐음(합법적인 보충 플레이를 거부함). `CardName → ICardEffect` 테이블로 교체.

이때 순서 문제가 드러남. `Resolve` 안의 실행 순서는:

```
1. 운명/승패 계산
2. 낸 카드를 손패에서 제거 (ApplyFate)
3. 특수 효과 실행
4. 양쪽 드로우
```

**2가 3보다 먼저인 게 결정적**임. 리셋 효과는 "지금 내 손패 전부"를 덱에 넣는데, 방금 낸 리셋 카드가 아직 손패에 있으면 **리셋 카드가 소멸 대신 덱으로 들어감** — DESIGN.md의 "특수카드는 사용 후 무조건 소멸"을 위반. 순서를 못박고 테스트로 고정함.

우선순위는 DESIGN.md 그대로: 조커 있으면 양쪽 소멸 + 효과 실행 자체를 안 함(그래서 조커가 미구현 특수카드를 막아도 예외가 안 남) → 없으면 리셋 먼저(양쪽 다 리셋이면 2번, P1 먼저) → 그 외 특수, P1 먼저.

## 예외가 상태를 반쯤 망가뜨리던 문제

코드 점검 중 발견. `ApplyFate`가 이미 양쪽 손패를 건드린 뒤에 `RunEffect`가 미구현 특수카드로 `NotImplementedException`을 던져서, `DeckAndHand`가 **라운드가 반만 적용된 상태**로 남았음. 테스트만 호출할 땐 무해했지만 `MatchSession`이 곧 호출할 예정이었음.

수정: 미구현 특수카드 검사를 **아무것도 변경하기 전으로** 옮김. 예외 후 양쪽 `DeckAndHand`가 그대로인지 확인하는 테스트 추가.

**아직 남은 같은 종류의 문제**: `MatchSession.SubmitCard`는 제출을 저장한 *다음* `Resolve`를 부르므로, 미구현 특수카드가 나오면 제출 슬롯이 채워진 채 예외가 나서 **그 세션이 영구 정지**함(이후 모든 제출이 "이미 제출함"으로 거절). 3종이 구현되면 자연히 사라질 임시 발판이라 지금은 방어하지 않기로 함.

## `RoundResult`가 "뽑은 카드 1장"으론 부족했음

원래 `Player1Drew`/`Player2Drew`(각 1장)를 담았는데, 이걸로 `GameState`가 "너한테만 보내는 손패 정보"를 만들 계획이었음. 근데 실제로 확인해보니:

| 낸 카드 | 실제 손패 변화 | `Drew` 필드가 말하는 것 |
|---|---|---|
| 일반/더미 | -1 내고 +1 뽑음 | 정확 |
| 드로우 | -1, **+2(효과)**, +1 = 순증 2장 | 3장 중 1장만 |
| 리셋 | **양쪽 손패 전체 교체** | 새 손패 중 1장만 |

11종 중 8종에만 맞고 나머지는 조용히 틀린 필드라, 없느니만 못했음. `Player1Hand`/`Player2Hand`(라운드 후 손패 전체) + 덱 카운트로 교체.

**"결과 객체가 무거워지지 않나"**에 대한 판단: 6장짜리 리스트 2개 크기는 정확성 앞에서 무의미함. `Hand.Cards`가 계속 변하는 리스트의 라이브 뷰라서 **생성자에서 복사**하는 것도 같이 넣음(테스트로 고정).

## `MatchSession` 설계

```csharp
var session = new MatchSession(player1Deck, player2Deck, rng, log);
RoundResult? result = session.SubmitCard(Side.Player1, card);  // null이면 상대 대기 중
session.Winner;  // 5승 채우면 Side, 아니면 null
```

- **`SubmitCard`가 `RoundResult?` 반환** — 동시 제출이라 "지금 판정됐나"를 호출자가 알아야 함. 별도 상태 조회 메서드 대신 반환값으로 표현.
- **덱 구성은 `MatchSession`이 안 함** — 조립된 덱을 받음. `CardDatabase`가 Godot 쪽이라, 여기서 "특수 6종" 같은 걸 정하면 CLAUDE.md의 덱빌딩 확장성 규칙 위반.
- **불법 제출은 예외** (매치 종료 / 같은 쪽 2번 / 손패에 없는 카드). `GameState`가 클라 요청 중계 시 catch해서 버려야 함.

## 이름 점검 한 바퀴

`MatchSession.ResolveRound`가 `RoundResolver` 클래스와 겹쳐 보인다는 지적에서 시작해서, 한 바퀴 돌았음.

| 전 | 후 | 문제였던 점 |
|---|---|---|
| `_player1Submission` | `_player1SubmittedCard` | "제출"이라는 행위만 말하고 담긴 게 `CardName`이란 걸 안 알려줌 |
| `SubmissionOf` | `SubmittedCardOf` | 위와 동일 |
| `HasSubmitted` | `HasSubmittedCard` | 위와 동일 |
| `ZoneOf` | `DeckAndHandOf` | **`PlayerZone` 이름 정할 때 버린 "Zone"이 private 헬퍼로 살아남아 있었음** |
| `ResolveRound` | `AdvanceToNextRound` | 아래 참고 |

**`RoundResolver`는 그대로 뒀음.** 제안된 `RoundResultResolver`는 오히려 틀린 이름 — 그 클래스는 `RoundResult`를 *해결하는* 게 아니라 *만들어내는* 것이라서. 겹침의 원인은 두 타입이 아니라, **일을 안 하면서 이름만 빌려간 메서드** 쪽이었음.

`ResolveRound`가 실제로 한 일: 해결은 `RoundResolver`에 위임한 한 줄뿐이고, 본체는 점수 기록 + 제출 슬롯 비우기 + 라운드 번호 증가였음. `RoundResolver.Resolve` 호출을 `SubmitCard`로 끌어올려서 호출부가 "해결하고, 기록한다"로 읽히게 만들고, 남은 뒷정리에 `AdvanceToNextRound`를 붙임. 다만 이 이름은 **점수 기록을 감추므로** doc 주석에 명시함.

## 디버그 로거: 왜 `Console.WriteLine`이 아닌가

`MatchSession`에 콘솔 출력을 넣을지 논의. 확인해보니 두 가지 문제:

1. **`GameLogic/CLAUDE.md`의 "Results, not side effects" 위반** — 이 프로젝트는 결과를 반환할 뿐 스스로 출력하지 않는다는 규칙이 있음. 세션이 혼자 출력하면 `GameLogic` 유일의 자체 부수효과가 됨.
2. **실제로 잘 보이지도 않음** — 실험해보니 `dotnet test`의 기본 출력엔 `Console.WriteLine`이 안 나옴. `--logger "console;verbosity=detailed"`를 붙여야 나옴.

그래서 **주입식 sink**(`Action<string>?`, 기본 null)로 감. 이득:
- `GameLogic`은 `GD.Print`를 못 부르는데, Godot 쪽에서 `new MatchSession(..., GD.Print)`로 꽂을 수 있음 — 사실상 유일한 경로
- 테스트는 `ITestOutputHelper.WriteLine`을 넘기거나, **리스트에 모아서 데이터로 검증** 가능(실제로 새 테스트가 이 방식)
- 기존 83개 테스트는 기본값 null이라 조용함

이건 "나중에 필요할까 봐 만드는 추상화"가 아님 — 지금 필요하고, 필드 1개 + null 체크가 전부임.

## `Scripts/CLAUDE.md` 신설

로거 사용법을 어디 적을지 논의하다, **`Scripts/` 최상위에 CLAUDE.md가 아예 없다**는 걸 발견. `Scripts/Autoload/`엔 있었지만 Godot 레이어 전반 규칙은 커밋 메시지와 devlog에 흩어져 있었음. 지금까지 정해진 것들을 모아서 신설:

- 입력은 노드 로컬, 중앙 `InputManager` 없음, 노드는 판단하지 않음
- **호스트의 UI도 `GameState.View`만 읽어야 함** (호스트는 네트워크가 안 막아주므로 규율만이 유일한 방어선)
- `CardName` → `CardDatabase` → `CardData` 해석은 표시 시점에
- 손패 순서는 `GameLogic`에서 무의미하고, 화면 슬롯 안정성은 이 레이어 책임
- 로거 sink에 `GD.Print` 꽂는 법
