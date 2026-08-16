# 2026-08-17 — 카드는 값(value)이지 객체가 아니다

[`Deck`/`Hand`/`DeckAndHand` 구현](2026-08-17-type-narrowed-win-loss-judgment.md) 직후 나온 질문들 — "왜 `CardName`이지 `Card`가 아니냐", "`Hand`가 `List<CardName>`인 게 이상하지 않냐", "변화 카드로 손패를 바꾸면 화면이 어떻게 되냐" — 를 정리한 기록. 결론부터 말하면 **셋 다 지금 구조가 맞고, 세 질문이 사실 하나로 이어져 있음**을 확인한 대화.

## 왜 `CardName`이지 `Card`가 아니냐

Godot 쪽에 이미 "카드"를 가리키는 이름이 둘(예정 포함) 있음:
- `CardData : Resource` — 아트/이름/설명, `.tres`
- `CardController : Node2D` — 화면의 카드 노드, 클릭/드래그 입력 담당

여기에 `GameLogic`까지 `Card`를 쓰면 프로젝트 전체에서 "카드"가 세 곳에서 각자 다른 걸 가리키게 됨 — `Core`를 `GameLogic`으로 바꾼 이유(`Scripts/Cards/`와 헷갈림)와 같은 문제. `CardName`은 "이건 정체성 값일 뿐, 실체가 아니다"를 이름에서부터 못박아둠.

## 왜 `Hand`가 `List<CardName>`인가 (객체 리스트가 아니라)

DESIGN.md 어디에도 카드 한 장 한 장을 구분할 이유가 없음 — Rock 카드 3장은 완전히 상호교환 가능하고, 인스턴스별 상태(개별 강화, 레벨 등)도 없음. 그래서:

- `Hand`가 실제 `Card` 객체 리스트였다면 `Remove(특정 객체)`에서 "어떤 Rock을 지울지" 구분해야 하는데, 게임 규칙상 그 구분엔 의미가 없음
- `Remove(CardName.Rock)`은 "Rock 아무거나 하나 지워"라는 뜻이고, 이게 게임이 실제로 원하는 동작과 정확히 일치함

즉 카드가 **값(value)이지 개체(entity)가 아니라서** 이렇게 간 것. 인스턴스 상태가 필요한 설계였다면 오히려 `List<CardName>`이 틀린 선택이었을 것.

## 전체 구조도

경계를 넘어가는 건 `CardName` 하나뿐 — 나머지는 경계 이쪽 아니면 저쪽에만 존재:

```
GameLogic (순수 로직, Godot 모름)         │  Scripts/ (Godot, 화면·입력)
──────────────────────────────────────────┼─────────────────────────────────
CardName (enum)                            │  CardData : Resource
  카드 정체성 11종                          │    카드 1장의 표시용 데이터
                                            │    .tres 파일 1개 = CardName 1개
CardType (enum, CardName에서 파생)          │
  Normal/Dummy/Joker/Special                │  CardDatabase (Autoload)
  RoundResolver가 이걸로 분기 처리           │    CardName → CardData 매핑 테이블
                                            │
Deck : 내부에 List<CardName>                │  CardController : Node2D
Hand : 내부에 List<CardName>                │    화면의 카드 한 장. 표시할 땐
DeckAndHand (Draw/ReturnToDeckBottom/Vanish)│    CardDatabase로 CardData 조회해 그림
```

`CardData`와 `CardController`는 `GameLogic`에 발도 못 들임(컴파일이 막음).

## 함정: `Hand`는 호스트에만 진짜로 존재함

`Hand`(및 `Deck`, `MatchSession` 전체)는 `GameState._session` 안에서만 실존함 — 그런데 `_session`은 호스트에서만 실제 객체이고 클라이언트에선 항상 `null` ([DevLogDoc/2026-08-16-core-boundary-clarification.md](2026-08-16-core-boundary-clarification.md) 참고). 클라이언트의 `CardController`들은 `GameLogic.Hand` 인스턴스를 참조하는 게 절대 아니고, 호스트가 타겟 RPC로 "네 손패는 지금 이거야"라고 보내준 `CardName` 리스트 복사본(`GameState.View`)만 그림.

## 변화(Transform) 카드로 확인한 결과

패에 있는 일반/더미 카드 1장을 원하는 카드로 바꾸는 효과. `Hand`가 이미 가진 연산만으로 충분함, 새 메서드 불필요:

```csharp
hand.Remove(CardName.Rock);
hand.Add(CardName.Paper);
```

**리스트 순서는 유지 안 됨** — `Remove`는 있던 자리를 지우고 `Add`는 맨 끝에 붙임. 문제가 안 되는 이유: DESIGN.md에 "손패 몇 번째 칸"이란 개념 자체가 없음, 즉 손패 순서는 규칙상 의미 없는 정보.

**근데 화면에서 "그 자리가 바뀐 것처럼" 보이게 하는 건 별개로 가능함.** `CardController` 노드는 Godot 씬 트리 안에서 진짜 정체성과 위치를 갖는 실제 객체라서, 플레이어가 어떤 슬롯을 클릭했는지는 클라이언트가 이미 알고 있음(자기 자신의 행동이니까). 그 정보를 이용해서 리스트 전체를 다시 그리는 대신 "그 슬롯"만 새 카드로 갱신하면 됨:

| 상황 | 화면 갱신 방식 |
|---|---|
| 변화(플레이어가 슬롯을 직접 골랐음) | 그 슬롯 노드만 새 카드로 교체 |
| 드로우/소멸(손패 수 자체가 바뀜) | 노드 하나 추가/제거 |
| 리셋(전체가 새로 섞임) | 손패 전체 재배치 |

**핵심:** `GameLogic`의 값 기반 `Hand`가 정체성을 안 갖는 것과, 화면의 카드 슬롯이 정체성을 갖는 것은 서로 다른 레이어의 결정이라 충돌하지 않음. `GameLogic`은 "리스트가 이렇게 바뀌었다"만 알려주고, 그걸 전체 재배치로 반영할지 특정 슬롯 갱신으로 반영할지는 순전히 Godot 쪽(클라이언트) 판단임 — `GameLogic`이 위치 정보를 제공할 필요도, 제공해서도 안 됨.
