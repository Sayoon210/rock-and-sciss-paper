# Godot RPC / 멀티플레이어 — 2026-08-17 정리

`GameState`/`NetworkManager`를 만들면서 공부한 RPC 개념. 개념 → 왜 그런지 → 예시 순.

---

## 1. RPC란 무엇인가

RPC = Remote Procedure Call, "원격 함수 호출".

평소 함수를 부르면 그 함수는 **내 프로세스 안에서** 실행된다. 멀티플레이어 게임에서 호스트와 클라이언트는 서로 다른 컴퓨터(또는 다른 프로세스)에서 각자 게임을 돌리고 있어서, 클라이언트가 호스트 쪽 객체의 메서드를 직접 부를 방법이 원래 없다 — 완전히 분리된 두 프로그램이니까.

RPC는 이걸 **함수 호출처럼 쓸 수 있게** 해주는 메커니즘이다.

```
클라이언트가 함수를 "호출"한다
   → 실제로는 "이 메서드를 이 인자로 실행해줘"라는 메시지가 네트워크로 전송된다
   → 호스트가 메시지를 받아서, 호스트 프로세스 안에서 같은 이름의 메서드를 실행한다
```

전화로 비유하면: 상대방 리모컨 버튼을 직접 못 누르니까 전화로 "3번 버튼 눌러줘"라고 말하는 것과 같다. 부르는 쪽은 "호출"이지만, 실행은 상대방 쪽에서 일어난다.

---

## 2. `[Rpc(...)]` — 원격 호출 가능 표시

```csharp
[Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
private void SubmitCardRpc(int card, int cardToTransform, int transformInto, int[] cardsToReturn)
{
    ...
}
```

메서드 앞에 `[Rpc(...)]`가 붙으면 "네트워크 너머에서 이 이름으로 부르는 요청이 오면 실행해달라"는 등록이 된다. 몸통은 평소 메서드랑 똑같이 생겼다 — 어노테이션 하나가 "원격 호출 가능" 표시를 붙여주는 것뿐.

**중요**: 이 메서드는 호스트/클라이언트 양쪽 실행 파일 모두에 존재해야 한다. `GameState.cs` 하나가 호스트용/클라이언트용으로 따로 나뉘어 있는 게 아니라, 같은 코드가 양쪽에 다 포함돼 있고 "누가 호출했고 대상이 누구냐"에 따라 어느 쪽에서 실제로 몸통이 도는지가 갈린다.

---

## 3. `RpcId` vs `Rpc` — 한 명한테 vs 전체한테

```csharp
// 특정 한 사람한테만
RpcId(1, MethodName.SubmitCardRpc, encoded.Card, encoded.CardToTransform, encoded.TransformInto, encoded.CardsToReturn);

// 연결된 모두한테
Rpc(MethodName.RoundResolvedRpc, ...);
```

| | 대상 | 이 프로젝트에서 쓰는 곳 |
|---|---|---|
| `RpcId(peerId, 메서드, 인자...)` | peer id가 `peerId`인 **한 명만** | 카드 제출(클라 → 호스트), 개인 결과 전송(호스트 → 그 사람만) |
| `Rpc(메서드, 인자...)` | 연결된 **모두** | 라운드 공개 결과 브로드캐스트 |

`RpcId(1, ...)`은 peer id 1 — 항상 호스트를 가리킨다 (아래 6번 참고).

---

## 4. 실제 흐름 — 카드 한 장 제출부터 결과까지

```
[클라이언트]                                    [호스트]
GameState.Instance.RequestCardPlay(play)
  └─ RpcId(1, SubmitCardRpc, ...)
        │
        │──── 네트워크 패킷 전송 ────▶
        │
                                        SubmitCardRpc(card, ...) 자동 실행
                                          └─ MatchSession.SubmitCard(...) 로 검증/처리
                                          └─ 결과 나오면 Rpc(RoundResolvedRpc, ...) 전체 발송
        ◀──── 네트워크 패킷 전송 ────
        │
RoundResolvedRpc(...) 자동 실행
  └─ View 갱신, 시그널(RoundResolved) 발생
```

클라이언트가 "호출"한 함수(`SubmitCardRpc`)와 호스트가 "호출"한 함수(`RoundResolvedRpc`)는 서로 다른 프로세스에서 각자 실행된다. `GameState.RequestCardPlay`는 이 왕복의 시작점이자 **유일한 진입점**이다 — 호출하는 쪽(카드 클릭 코드)은 자기가 호스트인지 클라이언트인지 신경 쓸 필요가 없다:

```csharp
public void RequestCardPlay(CardPlay play)
{
    if (Multiplayer.IsServer())
    {
        HandleSubmission(_mySide, play, null);   // 호스트는 네트워크 없이 로컬에서 바로 처리
    }
    else
    {
        RpcId(1, MethodName.SubmitCardRpc, ...);  // 클라이언트는 호스트한테 RPC
    }
}
```

---

## 5. `RpcMode` — 누가 이 메서드를 부를 수 있나

```csharp
[Rpc(MultiplayerApi.RpcMode.AnyPeer, ...)]     // 아무나 호출 가능
private void SubmitCardRpc(...) { ... }

[Rpc(MultiplayerApi.RpcMode.Authority, ...)]   // 권한자(호스트)만 호출 가능
private void RoundResolvedRpc(...) { ... }
```

| 모드 | 의미 | 왜 이걸 쓰나 |
|---|---|---|
| `AnyPeer` | 아무 peer나 이 메서드를 원격 호출할 수 있음 | 클라이언트가 호스트한테 "카드 낼래요" 요청을 보내야 하니까 |
| `Authority` | 그 노드의 권한자(이 프로젝트에선 항상 peer id 1, 즉 호스트)만 호출 가능 | 클라이언트가 다른 클라이언트한테 가짜 라운드 결과를 위조해서 보내는 걸 **엔진 차원에서** 막아줌 — 코드로 막는 게 아니라 애초에 호출 자체가 거부됨 |

`Authority` 모드는 "호스트만 진실을 말할 수 있다"는 이 게임의 원칙(host-authoritative)을 네트워크 계층에서 강제하는 장치다.

---

## 6. peer id 1 = 호스트, 그리고 `Multiplayer.IsServer()`

ENet 규칙:
- `CreateServer()`로 시작한 쪽은 자기 고유 id가 항상 **1**로 고정된다
- `CreateClient()`로 접속한 쪽은 서버가 접속 시점에 1이 아닌 다른 id를 배정해준다

```csharp
public void StartHost()
{
    ENetMultiplayerPeer peer = new ENetMultiplayerPeer();
    peer.CreateServer(PORT, MAX_CLIENTS);
    Multiplayer.MultiplayerPeer = peer;
}
```

`Multiplayer.IsServer()`는 "내 로컬 peer id가 1인가?"를 확인하는 것뿐이다. 이 프로젝트에서 호스트/클라이언트를 가르는 판단이 전부 이 한 가지 사실(id가 1이냐 아니냐)에서 나온다 — `RpcMode.Authority`가 "peer 1만 호출 가능"인 것도 같은 규칙을 쓰는 것.

**주의**: `_sideByPeerId` 같은 필드는 타입으로 "호스트 전용"이 강제되는 게 아니라, `if (!Multiplayer.IsServer()) { return; }` 같은 **런타임 체크**로만 지켜진다. 이건 컴파일러가 잡아주는 게 아니라서, 쓰기 지점을 코드 리뷰로 직접 확인해야 하는 종류의 보장이다. (반대로 `_session`은 `MatchSession?` 타입 자체가 nullable이라 컴파일러가 잘못된 접근을 걸러준다 — 같은 "호스트 전용"이라도 보장 강도가 다르다.)

---

## 7. `TransferMode` — 패킷을 얼마나 확실하게 보낼지

```csharp
TransferMode = MultiplayerPeer.TransferModeEnum.Reliable
```

- `Reliable` — 유실되면 안 되고 순서도 보장돼야 하는 메시지 (카드 제출, 라운드 결과처럼 하나라도 놓치면 게임이 깨지는 것)
- `Unreliable` — 좀 놓쳐도 괜찮은 실시간 데이터 (예: 초당 여러 번 갱신되는 캐릭터 위치. 다음 갱신이 금방 또 오니까 하나 놓쳐도 티가 안 남)

이 프로젝트는 RPC가 전부 `Reliable`이다 — 놓쳐도 되는 데이터가 없는 턴제 게임이라서.

---

## 8. RPC 메서드가 못 받는 타입 — 인코딩이 필요한 이유

Godot의 RPC 직렬화는 int, string, bool, 배열 같은 원시 타입만 이해한다. `CardPlay` 같은 이 프로젝트의 커스텀 클래스는 그대로 못 실어 보낸다. 그래서 `CardPlayCodec`이 존재한다:

```csharp
public static EncodedCardPlay Encode(CardPlay play)   // CardPlay → int들
public static CardPlay Decode(int card, ...)            // int들 → CardPlay
```

`CardName` enum도 마찬가지로 `(int)`로 캐스팅해서 보내고, 받는 쪽에서 `(CardName)`으로 다시 캐스팅한다. `-1` 같은 값은 "이 선택은 안 했음"을 나타내는 sentinel로 쓴다 (`NO_CARD`, `NO_WIN_LOSS_SENTINEL` 등).

---

## 한 줄 요약

- RPC는 "호출은 여기서, 실행은 저기서" 일어나게 해주는 메커니즘이다
- `[Rpc(...)]`가 붙은 메서드는 호스트/클라이언트 코드 양쪽에 똑같이 존재해야 한다
- `RpcId(대상, ...)`은 한 명한테, `Rpc(...)`는 전체한테
- `RpcMode.Authority`는 "호스트만 부를 수 있다"를 엔진이 강제해준다 — 코드로 막는 게 아니라 애초에 호출이 거부됨
- peer id 1 = 항상 호스트, `Multiplayer.IsServer()`는 그걸 확인하는 것뿐
- 커스텀 클래스는 RPC로 못 보낸다 — int/string/배열로 인코딩해서 보내고 받는 쪽에서 다시 복원해야 한다
