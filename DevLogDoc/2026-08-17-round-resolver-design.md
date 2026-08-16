# 2026-08-17 — `RoundResult`/`RoundResolver` 설계 결정

[`Deck`/`Hand`/`DeckAndHand` 구현](2026-08-17-type-narrowed-win-loss-judgment.md) 다음 단계로 `RoundResult`와 `RoundResolver`를 만들면서 나온 설계 질문 두 개와, 구현 중 실제로 걸려 넘어진 지점 하나를 정리한 기록.

## `WinLoss`가 `WinLossResult?`인 이유 — `Pass` 값을 안 만든 이유

"그냥 Pass 상태 하나 만들면 되는 거 아니냐"는 질문이 나왔음. `WinLossResult`에 4번째 값(`Pass`/`NoContest`)을 추가하면 `RoundResult.WinLoss`를 non-nullable로 만들 수 있긴 함. 근데 이러면 안 되는 이유:

`WinLossResult`는 `WinLossRules.Judge(NormalCard, NormalCard)`가 돌려주는 타입임. `Judge`는 [타입으로 좁혀서](2026-08-17-type-narrowed-win-loss-judgment.md) 진짜 일반카드 두 장만 받게 만들어뒀고, 그 목적 자체가 "조커 같은 게 여기 들어올 일이 없다"를 보장하는 것이었음. `WinLossResult`에 `Pass`를 넣으면 **`Judge`가 절대 만들어낼 수 없는 값이 자기 반환 타입에 끼게 됨** — 좁혀놨던 걸 다시 넓히는 셈.

`RoundResult.WinLoss`가 표현해야 하는 건 "이 라운드가 애초에 `Judge`까지 갔는지"인데, 이건 `WinLossResult`가 알 개념이 아니라 그 바깥(`RoundResult`)이 알 개념임. 그래서 `null` = "판정 자체가 없었다"(에러/누락이 아니라 도메인 사실)로 표현하고, `WinLossResult` 자체는 순수하게 유지함.

## `RoundResolver`가 `DeckAndHand`를 직접 건드리는 이유

`GameLogic/CLAUDE.md`엔 원래 "`RoundResolver` — 결과만 계산해서 반환, 자기 입력은 안 건드림(No mutation of its inputs)"이라고 적혀있었음. 근데 이 문구는 `DeckAndHand`가 생기기 전에 쓴 거라, "입력"이 사실 `CardName` 두 개(값 타입이라 애초에 못 건드림)만 가리키던 것이었음.

실제로 짤 때 갈림길이 있었음:

- **안 A (채택)**: `Resolve(player1Card, player2Card, player1: DeckAndHand, player2: DeckAndHand)`가 판정+적용+드로우를 한 번에 다 하고 `RoundResult`를 반환
- **안 B**: `Resolve(player1Card, player2Card)`가 순수하게 "운명/승패"만 계산한 작은 결과 객체를 반환하고, `MatchSession`이 그걸 받아서 `DeckAndHand`에 실제로 적용

안 B가 더 "순수 함수"에 가깝고 `[Theory]` 테스트가 `DeckAndHand` 없이 `CardName` 두 개만으로 돌아갈 수 있다는 장점이 있음. 그럼에도 안 A로 간 이유: 실제로 판정과 적용이 항상 붙어 다니는데(계산만 하고 적용 안 하는 경우가 없음) 안 B는 그 둘을 분리하기 위한 중간 타입(`RoundVerdict` 같은)을 하나 더 만들어야 함 — 지금 그 분리로 얻는 이득이 없어서 스킵함. 테스트도 실제로는 `DeckAndHand` 생성이 2줄(`new DeckAndHand(new Deck(...), new Hand(...))`)이라 부담이 크지 않았음.

**재검토 조건**: `MatchSession`을 짤 때 "판정 계산"과 "실제 적용" 사이에 뭔가 끼워 넣어야 하는 상황이 생기면(예: 적용 전에 양쪽에 확인을 받아야 한다든가) 그때 안 B로 갈라야 함.

## 실제로 걸려 넘어진 것: 적용 순서가 결과에 영향을 줌

`Resolve` 내부에서 **"운명 적용(덱바닥복귀/소멸)"이 "드로우"보다 먼저 일어남**:

```csharp
ApplyFate(player1, player1Card, player1Fate);   // 먼저
player1.Draw();                                  // 그다음
```

테스트를 짜다가 이 순서를 거꾸로 가정해서 하나 깨졌음 — 덱이 `[Paper]`인 상태에서 `Rock`을 냈을 때, "Rock을 먼저 덱바닥에 넣고 그다음 드로우"이므로 덱은 `[Paper, Rock]`이 됐다가 드로우로 `Paper`가 빠져서 최종적으로 `[Rock]`만 남음. 반대로 짰으면 `Rock`을 뽑아올 수도 있었음.

이 순서가 결과에 실제로 영향을 주는 이유는 덱 크기가 작을 때(테스트처럼 1장) 자기가 방금 낸 카드를 자기가 바로 또 뽑는 경우가 생길 수 있어서임. DESIGN.md엔 이 순서를 명시한 문장이 없어서 지금은 "복귀 먼저, 드로우 나중"으로 임의로 정함 — 게임 규칙상 큰 의미 차이는 없어 보이지만(카드 20장 덱에서 순간적으로 자기 카드를 다시 뽑을 확률 자체가 낮고, 뽑아도 그냥 다시 손에 들어오는 것뿐), 확정된 기획 결정은 아니라는 점은 남겨둠.

## 스코프: 우선순위 큐는 아직 없음

DESIGN.md의 조커>리셋>기타특수>나머지 우선순위는 **특수카드가 실제로 있을 때만** 의미가 생김. 지금 `RoundResolver`는 일반/더미/조커만 다루는데, 이 범위에선 "조커가 있으면 무조건 양쪽 다 소멸(승패 없음), 없으면 각자 원래 운명"으로 전부 정리됨 — 별도 우선순위 큐 없이 `if` 하나로 충분함.

**이게 깨지는 시점**: 실제 특수카드(`ICardEffect`)가 들어오는 순간. 그때는 "조커 > 리셋 > 기타특수" 순서로 실행 큐를 정렬하는 로직이 진짜로 필요해짐 — 지금 짠 `RoundResolver`는 그 전까지만 유효한 축소판이라는 걸 명시해둠.
