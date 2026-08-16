# 2026-08-17 — 멀티플레이어 라운드 흐름 설계

`MatchSession`/`RoundResolver`/`GameState.View`를 실제로 짜기 전에, "카드 한 장을 냈을 때 판정을 거쳐 다음 라운드로 넘어가기까지 전체 경로가 어떻게 되는가"를 먼저 그려본 기록. **아직 코드는 없음** — 이 문서는 다음에 구현할 때의 설계안이고, 그 과정에서 미해결로 남은 지점 두 개를 발견함.

## 입력은 InputManager로 중앙화하지 않기로 함

Godot이 이미 `Input` 싱글톤 + `InputMap`으로 로우 입력→액션 변환과 리바인딩을 제공하고, 노드 트리의 입력 라우팅(`Area2D.input_event`, `_GuiInput`)이 "이 클릭이 어느 카드 위였나"를 히트테스트로 풀어줌. 직접 만든 InputManager는 이걸 다시 구현하는 꼴이라 이 프로젝트엔 불필요 — CLAUDE.md의 "나중에 필요할까 봐 만드는 추상화 금지"에 해당.

대신 입력이 "모이는 지점"은 입력 레이어가 아니라 **의도(intent) 레이어**에 이미 있음: `CardController`는 판단 없이 클릭만 감지하고, 실제 판단은 전부 `GameState.RequestCardPlay` 한 곳으로 모임.

## 한 라운드의 흐름

```
CardController (클릭 감지, 판단 없음)
   │  GameState.Instance.RequestCardPlay(CardName.Rock)
   ▼
GameState.RequestCardPlay(CardName card)
   ├─ 클라이언트면: RpcId(1, "SubmitCard", (int)card)
   └─ 호스트면: 같은 메서드를 로컬로 직접 호출
   ▼ (호스트 쪽 한 메서드로 합류)
GameState.SubmitCard(int fromPeerId, CardName card)   ← 호스트에서만 실행
   │  peerId → Player1/Player2 변환 (GameLogic/CLAUDE.md "Sides, not peers")
   ▼
_session.SubmitCard(Side player, CardName card)   ← 여기서 GameLogic 진입
   │  - 실제로 그 플레이어 Hand에 있는 카드인지 검증
   │  - 이번 라운드 제출 슬롯에 저장
   │  - 양쪽 다 도착하면 RoundResolver 호출 → 우선순위 정렬 후 처리
   │    (조커 > 리셋 > 기타 특수 > 일반) → 각자 DeckAndHand에 반영
   ▼
RoundResult 반환
   ├─ 공개 정보 (브로드캐스트, 양쪽 다 받음)
   │    양쪽이 낸 카드, 점수, 손패 "장수"만
   └─ 비공개 정보 (타겟 RPC, 그 사람한테만)
        이번에 뽑은 카드, (예지 사용 시) 덱탑 확인 결과
   ▼
양쪽 화면 갱신 → 5승 체크 → 다음 라운드
```

## 확인한 것: 아무도 스스로 판정하지 않음

클라이언트(호스트 자신의 UI 포함)는 전부 결과를 받아서 그리기만 함. 판정은 오직 호스트 프로세스 안 `RoundResolver` 한 곳. 이건 [Scripts/Autoload/CLAUDE.md](../Scripts/Autoload/CLAUDE.md)의 "모든 행동은 하나의 진입점을 거친다" 원칙을 실제 라운드에 대입해본 것.

## 확인한 것: 호스트는 서버가 아니다 (재확인)

이 오해는 [2026-08-16-core-boundary-clarification.md](2026-08-16-core-boundary-clarification.md)에서 이미 한 번 정리했는데, 이번에 "호스트는 클라+서버 역할을 동시에 하는 애 아니냐"는 질문으로 다시 나와서 재확인함:

- `GameLogic.dll`은 호스트/클라 양쪽에 동일하게 존재함 (같은 실행 파일에 링크됨)
- 클라가 못 부르는 이유는 코드가 없어서가 아니라 `GameState._session`이 클라에서 항상 `null`이라서 — 런타임 분기일 뿐, 배포되는 코드의 차이가 아님

## 새로 나온 것: 호스트도 자기 UI에서 `_session`을 직접 읽으면 안 됨

호스트는 네트워크로 격리돼 있지 않아서, 기술적으로는 자기 UI 코드를 `_session.Player2Zone.Hand`(상대의 진짜 손패)에 직접 연결할 수 있음 — 컴파일도 실행도 됨. **그런데 그러면 안 됨.** 그 순간 상대 손패가 호스트 화면에 그대로 노출되는 정보 유출 버그가 생김.

그래서 `GameState.View`는 "양쪽 다"(both sides) 쓰는 걸로 이미 문서화돼 있었음 — 호스트의 화면도 반드시 `View`만 읽어야 함. `View`는 `Player1`/`Player2` 기준이 아니라 **"나/상대" 기준**으로 구성:

```
View
├─ MyHand : List<CardName>       ← 항상 내 진짜 손패
├─ OpponentHandCount : int       ← 항상 상대 손패 개수만
├─ MyDeckCount / OpponentDeckCount
└─ 이번 라운드 공개된 카드들, 점수
```

호스트 쪽은 `_session`에서 읽어 `View`를 인프로세스로 직접 채우고, 클라 쪽은 브로드캐스트+타겟 RPC로 받은 걸로 `View`를 채움 — **과정은 다르지만 `View`의 모양과 그걸 읽는 UI 코드는 완전히 동일**. 이게 클라를 위한 게 아니라 호스트 자신의 정보 유출을 막는 역할도 겸한다는 걸 이번에 확인함.

## 미해결 1: "이번 라운드에 누가 이미 냈는지" 저장할 자리가 없음

동시 제출이라 호스트는 한쪽만 도착했을 때 판정을 미루고 기다려야 하는데, 이 "제출 대기" 상태를 `MatchSession`이 아직 어떻게 들고 있을지 정하지 않음. `Player1Submission`/`Player2Submission` 같은 nullable 필드 두 개로 시작하는 안이 유력 — `MatchSession`을 실제로 짤 때 결정.

## 미해결 2: `RequestCardPlay(CardName)` 시그니처가 특수카드 3종엔 부족함

변화 카드 예시로 확인됨: "이 카드 낼게" 외에 "손패의 어떤 카드를, 뭘로 바꿀지"까지 같이 보내야 함. 마찬가지로 예지(덱탑 3장 중 1장 선택)와 교체(몇 장을, 어떤 걸 버릴지)도 추가 파라미터가 필요함. 일반/더미/조커/보충은 `CardName` 하나로 충분하지만 나머지 3종은 아님 — `ICardEffect` 설계할 때 같이 풀어야 할 지점으로 남김.
