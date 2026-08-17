# 2026-08-17 — 멀티플레이어 오토로드 레이어 구축

`GameLogic`이 다 끝난 상태에서 Godot 쪽 절반을 실제로 짠 기록. [2026-08-17-multiplayer-round-flow-design.md](2026-08-17-multiplayer-round-flow-design.md)가 설계안이었다면 이 문서는 그걸 구현하면서 확정한 결정들.

두 갈래(카드 표시 데이터 / 멀티플레이어)를 병렬로 진행했고, 멀티플레이어 쪽은 코드보다 설계 판단이 많아서 설계를 먼저 뽑고 확정한 뒤 구현했다.

## 이전 설계 문서의 미해결 2개는 이미 풀려 있었음

설계 문서가 남겨둔 미해결 항목 두 개가 그 사이 `MatchSession`/`CardPlay` 작업에서 해결됐음을 확인:

- **미해결 1 (제출 대기 상태를 어디 두나)** → `MatchSession._player1SubmittedCard` / `_player2SubmittedCard` nullable 필드 두 개. 설계 문서가 유력하다고 본 안 그대로.
- **미해결 2 (`RequestCardPlay(CardName)`이 특수카드 3종엔 부족)** → `CardPlay`가 "낸 카드 + 그걸 내기 위해 필요했던 선택"을 함께 실어 해결. 셋 중 예지는 아예 삭제됐고(제출 시점에 선택이 끝나야 한다는 DESIGN.md 보장과 충돌), 남은 변화/교체는 `CardPlay.Transforming`/`Swapping`으로 커버됨.

## 확정한 결정 3개

설계 단계에서 "사람이 정해야 한다"고 남긴 것들:

### 호스트 = 항상 Player1 (동전 던지기 안 함)

`RoundResolver`는 동순위 특수카드가 충돌하면 항상 Player 1부터 처리한다(DESIGN.md). 즉 Player1에게 작지만 **고정된 타이브레이크 우위**가 있고, 호스트를 항상 Player1로 두면 그 우위가 매 판 호스트에게 간다.

랜덤 배정도 비용은 없었다 — 어차피 `MatchStartedRpc`가 side를 실어 보내야 하니 추가 왕복이 필요 없음. 그럼에도 고정을 택한 이유는 구현이 단순하고, 지금은 공정성보다 "돌아가는 걸 먼저 본다"가 우선이라서. 나중에 뒤집고 싶으면 `MatchStartedRpc` 한 곳만 고치면 됨.

### 매치 시작은 호스트 트리거

접속하자마자 자동 시작이 아니라 `GameState.HostStartsMatch()`를 호스트가 명시적으로 부르는 방식. 자동 시작이 더 간단하지만, "클라 접속 완료"와 "매치 시작" 사이에 아무 틈이 없어진다. 덱빌딩이 들어오는 순간 그 틈이 반드시 필요해지므로(클라가 자기 덱을 호스트에 보내야 함) 지금 틈을 남겨두는 쪽을 택함.

### 재접속 미지원 — 의도적 제외

ENet은 재접속마다 새 peer id를 배정하므로 `_sideByPeerId`가 "돌아온 그 사람"을 알아볼 방법이 없다. 알아보게 하려면 애플리케이션 레벨의 영속 플레이어 토큰이 필요한데 그건 배선 문제가 아니라 별도 기능. 게다가 `MatchSession`은 호스트 프로세스 메모리에만 살아서 복구할 상태 자체가 프로세스 수명을 못 넘김. **v1에서는 어느 쪽이 끊기든 매치 종료.**

## 설계 검토 중 발견한 갭: `CardDatabase`가 로스터를 못 알려줬음

`DeckAssembler`가 특수카드를 덱에 넣으려면 "지금 존재하는 특수카드가 뭐뭐냐"를 물어봐야 하는데, `CardDatabase`의 공개 API가 `GetCardData(CardName)` 하나뿐이라 **"이 카드가 뭐냐"는 답해도 "어떤 카드들이 있냐"는 답할 수 없었다.** 그대로 두면 `DeckAssembler`가 5종을 직접 나열해야 하고, 그건 root CLAUDE.md가 명시적으로 금지한 하드코딩.

→ `CardDatabase.LoadedCardNames` 추가. 내부 `Dictionary`의 키 집합을 그대로 노출하는 것뿐이라 비용은 없고, `DeckAssembler`는 이걸 `GetCardType() == Special`로 걸러 쓴다. 특수카드 풀이 늘어도 `.tres` 파일만 추가하면 되고 코드는 안 건드린다.

## 구현한 것

| 파일 | 역할 |
|---|---|
| `EventBus` | 오토로드 간 신호 중계. 상태 없음 |
| `NetworkManager` | `Multiplayer.MultiplayerPeer` 단독 소유. 호스트/조인, 연결 이벤트를 `EventBus`로 재발행 |
| `GameState` | 호스트의 권위 세션 + peer↔Side 매핑 + `View`. RPC 5개 |
| `MatchView` | 화면이 읽는 "나/상대" 읽기 모델 |
| `CardPlayCodec` | `CardPlay` ↔ RPC 원시값 변환 |
| `DeckAssembler` | 20장 덱 조립 |

### `EventBus`가 필요했던 이유

`NetworkManager`가 연결 이벤트를 `GameState`에 직접 알려주면 **등록 순서가 두 파일 사이의 숨은 의존성**이 된다. `project.godot`의 오토로드 순서를 누가 바꾸면 엉뚱한 곳에서 `NullReferenceException`이 터지고 원인은 안 보임. 시그널 중계를 거치면 발행자가 구독자의 존재 여부를 몰라도 되므로 순서 문제가 사라진다. Scripts/Autoload/CLAUDE.md의 "오토로드끼리 직접 참조 금지" 규칙이 노리는 게 이것.

### 히든 정보 분리가 지켜지는 지점

`RoundResolvedRpc`(전체 브로드캐스트)는 낸 카드/fate/승패/덱 **장수**/패 **장수**만 싣는다. 패 내용은 `PrivateHandRpc`와 `MatchStartedRpc` — **둘 다 `RpcId`로 그 사람 한 명만 타겟** — 로만 나간다. 호스트 자신의 패는 아예 네트워크를 안 타고 `_session`에서 `View`로 직접 복사한다.

`RoundResult.Player1Hand`/`Player2Hand`가 양쪽 진짜 패를 다 들고 있으므로, 이걸 브로드캐스트 RPC에 실수로 실으면 그 즉시 전체 유출이다. 그래서 브로드캐스트 페이로드는 `.Count`만 뽑아 쓰도록 되어 있음.

## 상수 표기: `SCREAMING_SNAKE_CASE`

프로퍼티/메서드와 시각적으로 안 구분된다는 판단으로 프로젝트 전체 상수를 `WINS_NEEDED_FOR_MATCH` 형태로 통일. C#/Godot 표준(PascalCase)에서 벗어나는 선택이지만, 호출부에서 "이건 상수다"가 즉시 보이는 실용적 이득을 택함.

## 아직 안 된 것

**씬과 UI가 하나도 없어서 두 인스턴스를 띄운 실제 검증을 못 했다.** root CLAUDE.md는 "RPC/권한 동작은 기억이 아니라 실행 중인 두 인스턴스로 테스트하라"고 못박고 있는데, 지금은 `dotnet build`가 통과했다는 것 = 컴파일된다는 것까지만 확인된 상태. `[Rpc]` 속성이 컴파일은 되면서 런타임에 틀릴 수 있는 종류(잘못된 `RpcMode`, 전송 못 하는 인자 타입)는 아직 안 걸러졌다.

다음 단계는 호스트/조인 버튼과 카드 버튼 몇 개만 있는 최소 확인용 씬 — 콘솔 로그로 왕복이 도는지 눈으로 보는 것.
