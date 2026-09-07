<div align="center">

# RockAndScissPaper 3D

**가위바위보를 카드 게임으로 재해석한 3D 1:1 멀티플레이어 카드 게임**

![Godot](https://img.shields.io/badge/Godot-4.7%20Mono-478CBF?style=flat-square&logo=godotengine&logoColor=white)
![C#](https://img.shields.io/badge/C%23-.NET%208-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![Tests](https://img.shields.io/badge/xUnit-75%20passed-3FB950?style=flat-square)
![Status](https://img.shields.io/badge/status-in%20development-F0883E?style=flat-square)

[시연 영상](https://youtu.be/wyBZq2DbPMs) · [프로젝트 페이지](https://poolc.org/project/518) · [아키텍처](ARCHITECTURE.md) · [개발 로그](DevLogDoc/)

<a href="https://youtu.be/wyBZq2DbPMs">
  <img src="https://img.youtube.com/vi/wyBZq2DbPMs/hqdefault.jpg" width="520" alt="게임 시연 영상 (클릭 시 YouTube로 이동)">
</a>

</div>

---

테이블을 사이에 두고 마주 앉아 카드를 냅니다. 이긴 쪽은 진 쪽을 실제로 때리며, **무엇으로
이겼는지에 따라 연출과 피해량이 달라집니다.** 바위는 얼굴을 가격하고(2 피해), 가위는 상대의
손을 찌르며(1 피해), 보는 책상을 내려칩니다(1 피해).

## 한눈에 보기

| | |
|---|---|
| **역할** | 기획 및 개발 (1인 개인 프로젝트) |
| **기간** | 2026.08 ~ 진행 중 |
| **엔진** | Godot 4.7 Mono · C# · .NET 8 |
| **테스트** | xUnit 75개 — Godot 없이 0.1초 |
| **네트워크** | ENetMultiplayerPeer · 호스트 권위 1:1 |

## 아키텍처

```
RockAndScissPaper.csproj   Godot.NET.Sdk        씬 · 노드 · 입력 · RPC · UI
        │
        └── references ──▶ GameLogic/           Microsoft.NET.Sdk   순수 규칙, 참조 없음
                                ▲
Tests/  xUnit ──────────────────┘               GameLogic만 참조
```

`GameLogic`은 아무것도 참조하지 않습니다. 따라서 이 프로젝트 안에서 `using Godot;`을 작성하면
규칙 위반 경고가 아니라 **컴파일 에러(`CS0246`)가 발생하여 빌드 자체가 실패합니다.** 경계를
문서가 아니라 빌드 시스템이 지키게 한 것이 이 프로젝트의 가장 중요한 설계 결정입니다.

## 주요 구현

### 1. 규칙과 엔진의 분리

게임 규칙은 엔진을 전혀 알지 못하는 순수 .NET 라이브러리에 있으며, 그 경계는 빌드가 강제합니다.

📂 [`GameLogic/MatchSession.cs`](GameLogic/MatchSession.cs) · [`WinLossRules.cs`](GameLogic/WinLossRules.cs) · [`GameLogic.csproj`](GameLogic/RockAndScissPaper.GameLogic.csproj)

<details>
<summary><b>구현 세부</b></summary>

<br>

이렇게 설계한 이유는 AI 협업 환경 때문입니다. 구현을 맡겼을 때 가장 먼저 무너지는 것이 계층
경계인데, 마크다운 문서에 "여기에는 엔진 코드를 넣지 말 것"이라고 적어두는 방식은 강제력이
없습니다. 경계를 빌드 시스템에 넣으면 지켜지지 않는 상태 자체가 성립하지 않습니다.

라운드는 **상태 머신**으로 관리됩니다.
[`ERoundPhase`](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/GameLogic/MatchSession.cs#L11-L17)를
별도 필드로 두지 않고 "현재 판정 대기 중인 라운드 객체가 존재하는가"에서 파생시켜, 상태값과
실제 상태가 어긋날 여지를 제거했습니다.

정산 도중 예외가 발생하더라도 매치가 영구히 멈추지 않도록
[`finally` 블록에서 라운드를 반드시 정리합니다](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/GameLogic/MatchSession.cs#L297-L327).
이 처리가 없던 시절에는 정산 중 예외가 나면 양쪽 모두 "제출 완료" 상태로 굳어 매치가 죽었습니다.

</details>

### 2. 테스트 구조

Godot을 실행하지 않고, 인스턴스 두 개를 띄워 클릭하지 않고, **0.1초 만에** 현재 게임
메커니즘 전체를 검증합니다.

📂 [`Tests/WinLossRulesTests.cs`](Tests/WinLossRulesTests.cs) · [`MatchSessionTests.cs`](Tests/MatchSessionTests.cs)

<details>
<summary><b>구현 세부</b></summary>

<br>

```
통과!  - 실패: 0, 통과: 75, 건너뜀: 52, 전체: 127, 기간: 92 ms
```

건너뛴 52개는 **현재 덱에 없는 메커니즘**(공백카드·조커·능력카드 4종)을 검증하는 테스트입니다.
규칙 코드는 그대로 컴파일되고 있으며, 이 카드들이 아이템으로 복귀할 때 다시 실행됩니다 —
[`DormantMechanics.cs`](Tests/DormantMechanics.cs)의 상수 하나를 `null`로 바꾸면 전부
되살아납니다.

| 검증 항목 | 내용 |
|---|---|
| 판정과 피해량 | `[Theory]` + `[InlineData]`로 가위바위보 9개 조합 전수 검사. 파일 전체가 24줄입니다 |
| 상태 머신 전이 | 잘못된 시점의 제출·선택은 예외 처리, 매치 종료 후 제출 차단, 거부된 입력이 상태를 변경하지 않음 |
| 결정론 | 동일 시드에서 동일한 초기 손패가 나오는지, 선택이 어느 순서로 도착해도 결과가 같은지 — 네트워크 도착 순서가 판정을 바꾸면 안 되기 때문입니다 |
| 회귀 방지 | [`SwapEffectTests.cs`](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/Tests/Effects/SwapEffectTests.cs#L90-L105) — 과거 매치를 멈추게 했던 버그의 원인과, 무엇이 다시 나타나면 그 버그가 재발한 것인지를 주석으로 명시 |

테스트 이름은 식별자가 아니라 **문장 형태**로 작성했습니다.
`SubmitCard_throws_once_the_match_is_over` 같은 형태입니다. 콘솔에 출력되는 실패 테스트의
이름만 읽어도 무엇이 깨졌는지 파악할 수 있으며, AI 에이전트에게 실패 로그를 그대로 전달해도
별도의 맥락 설명이 필요하지 않습니다.

</details>

### 3. 호스트 권위 멀티플레이

승패 판정은 호스트 한 곳에서만 일어나며, **호스트는 클라이언트가 보낸 값을 신뢰하지 않고
재검증합니다.**

📂 [`GameState.RequestCardPlay`](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/Scripts/Autoload/GameState.cs#L217-L231) · [`HandleSubmission`](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/Scripts/Autoload/GameState.cs#L523-L562) · [`BroadcastRoundResult`](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/Scripts/Autoload/GameState.cs#L782-L865)

<details>
<summary><b>구현 세부</b></summary>

<br>

**하나의 진입점.** 카드를 내는 동작은 호스트든 클라이언트든 동일한 함수를 호출합니다. 호스트는
로컬에서 처리하고 클라이언트는 `RpcId`로 요청을 전달하는데, **그 분기는 이 함수 내부에서만
일어납니다.** 화면을 그리는 쪽은 자신이 호스트인지 클라이언트인지 알 필요가 없습니다.

**클라이언트를 신뢰하지 않습니다.** 클라이언트가 보내는 "이 카드를 냈다"는 클라이언트가 생성한
데이터이므로 정상이라고 전제하지 않고 재검증합니다. 검증은 별도의 방어 코드가 아니라 **판정
로직 자체가 예외를 던지는 방식**으로 구현했습니다. 손패에 없는 카드를 내거나 이미 제출을 마친
상태에서 다시 제출하는 등 규칙에 어긋나는 시도는 `MatchSession`이 거부하고, 호스트는 그 예외를
잡아 요청을 기각합니다. 그 결과 잘못된 입력이 서버를 중단시키거나 게임 상태를 어중간하게
망가뜨리지 않으며, 다음 시도를 그대로 받을 수 있는 상태가 유지됩니다.

**정보 은닉.** 공개 정보(승패·체력·손패 **장수**)는 `Rpc`로 전체에 브로드캐스트하고, 손패
**내용**과 같은 비공개 정보는 `RpcId`로 해당 플레이어에게만 전송합니다. 호스트 프로세스는
양쪽의 실제 손패를 메모리에 보유하므로 네트워크가 이를 막아주지 않습니다. 따라서 **호스트의
화면조차 내부 세션을 직접 읽지 않고** 클라이언트와 동일한 읽기 모델(`GameState.View`)을
경유하도록 규칙으로 정했습니다.

</details>

### 4. 표현 계층의 역할 분리

표현 계층에는 **호스트/클라이언트 분기가 하나도 존재하지 않습니다.** 판정 로직도 한 줄
없습니다.

📂 [`MatchWorldView` 시그널 구독부](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/Scripts/Match3D/MatchWorldView.cs#L211-L214) · [`RoundResolvedRpc`](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/Scripts/Autoload/GameState.cs#L437-L485)

<details>
<summary><b>구현 세부</b></summary>

<br>

호스트와 클라이언트는 결과를 **서로 다른 경로로** 수신합니다. 호스트는 자신이 계산한 결과를
그대로 사용하고, 클라이언트는 `RoundResolvedRpc`로 전달받습니다. 그러나 양쪽 모두 마지막에
**동일한 시그널**(`RoundResolved`)을 자신의 프로세스 안에서 발행하며, 화면은 그 시그널만
구독합니다.

```
카드 클릭 → RequestCardPlay → [IsServer?]
                                 ├─ 호스트   : MatchSession 판정 → 결과 브로드캐스트
                                 └─ 클라이언트 : RpcId로 요청 → 결과 수신
                                          ↓
                              RoundResolved 시그널 (양쪽 동일)
                                          ↓
                                MatchWorldView.OnRoundResolved
```

`MatchWorldView`는 이미 결정된 승패를 읽어 어떤 애니메이션을 재생할지만 선택합니다.

</details>

### 5. 3D 움직임 네트워크 최적화

머리 회전값 전송을 15Hz로 제한한 뒤 발생한 끊김 현상을, **전송량을 늘리지 않고** 수신 측
보간만으로 해결했습니다.

📂 [`RemoteHeadLook.cs`](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/Scripts/Match3D/RemoteHeadLook.cs#L96-L106)

<details>
<summary><b>구현 세부</b></summary>

<br>

마우스를 따라 움직이는 캐릭터 머리의 본 회전값을 매 프레임(60Hz) 전송하는 것은 비효율적이라고
판단하여 15Hz로 제한해 전송했습니다. 그런데 수신 측이 도착한 값을 해당 프레임에 즉시 적용하다
보니, 값이 도착하는 순간에만 움직이고 그 사이에는 멈춰 있다가 다음 값에서 한 번에 튀는 문제가
발생했습니다.

508프레임을 실측한 결과 **실제로 회전이 갱신된 프레임은 8.3%에 불과**했고, 최대 회전 폭이
평균의 **12.1배**로 나타났습니다.

수신값을 즉시 반영하는 대신 목표값으로 설정하고 매 프레임 지수적으로 접근하도록 수정했습니다.

```csharp
float weight = 1f - Mathf.Exp(-LOOK_SMOOTHING_PER_SECOND * (float)delta);
_shownLocalDelta = _shownLocalDelta.Slerp(_targetLocalDelta, weight).Normalized();
```

세 가지가 의도적인 선택입니다. `rate * dt`가 아니라 `1 - e^(-rate·dt)`를 사용하여
**프레임레이트가 달라도 동일한 곡선**을 그리며 긴 프레임에서 오버슈트하지 않습니다. `Lerp`가
아니라 `Slerp`를 사용하여 쿼터니언이 **일정한 각속도**로 회전합니다. 매 프레임 정규화하여 반복
보간에서 누적되는 부동소수점 오차가 스케일로 새어 나가지 않도록 했습니다.

**결과적으로 100%의 프레임에서 회전이 갱신되었고, 최대 회전 폭은 평균의 2.2배로 감소했습니다.**
전송 빈도는 변경하지 않았으므로 대역폭을 추가로 사용하지 않았습니다.

</details>

### 6. 2D에서 3D로의 전환

표현 계층 전체를 교체하면서 **`GameLogic`과 `Tests`는 0줄 수정**했습니다. 초기 설계가 실제로
값을 한 지점입니다.

📂 [전환 회고](DevLogDoc/2026-09-06-2d-to-3d-retrospective.md) · [견적을 산출한 날](DevLogDoc/2026-08-22-boundary-pays-off-in-3d.md) · [실행한 날](DevLogDoc/2026-08-28-presentation-layer-swap.md)

<details>
<summary><b>구현 세부</b></summary>

<br>

| 날짜 | 내용 |
|:---:|---|
| **08-16** | 로직 경계를 긋고 빌드로 강제하기로 결정. 이유는 **테스트를 빠르게 돌리기 위함**이었고 3D 계획은 없었습니다 |
| **08-22** | 2D → 3D 전환 검토 및 견적 산출 — `Scripts/UI`의 **3,776줄만** 재작성하고 나머지 55%는 유지 |
| **08-28** | 실제 실행. 견적대로 45%만 교체했으며, **`GameLogic`과 `Tests`는 0줄 재작성** |

전체 8,435줄 중 다시 작성한 3,776줄은 **이 게임에서 어렵지 않았던 45%**였습니다. 실제로
어려웠던 세 가지(라운드 판정 우선순위, 호스트 권위, 히든 정보)는 전부 화면과 무관한 곳에
있었고, 2D에 강하게 결합되어 있던 것들(드래그 좌표, 손패 배치, 툴팁)은 하나같이 게임 규칙을
알지 못했습니다. 경계가 이 두 축을 분리해 놓았기 때문에, 3D 전환이 "어려운 코드를 손대는 일"이
아니라 "쉬운 코드를 교체하는 일"이 되었습니다.

전환이 수월했던 직접적인 이유는 **새로 만든 3D 표현 계층이 기존 2D UI가 구독하던 것과 동일한
시그널을 그대로 구독**했기 때문입니다. 판정 쪽 코드는 자신을 구독하는 대상이 부채꼴로 펼쳐진
2D 손패인지 좌석에 앉은 3D 캐릭터인지 알 필요도, 알 방법도 없었습니다.

</details>

## 프로젝트 구조

| 폴더 | 내용 | 비고 |
|---|---|---|
| [`GameLogic/`](GameLogic/) | 규칙 — 판정 · 덱 · 손패 · 세션 | Godot 참조 0개 |
| [`GameLogic/Effects/`](GameLogic/Effects/) | 능력카드 4종과 `ICardEffect` | 현재 덱에서 제외, 코드와 테스트는 유지 |
| [`Tests/`](Tests/) | xUnit 테스트 — 현재 메커니즘 75개, 휴면 52개 | `GameLogic`만 참조 |
| [`Scripts/Autoload/`](Scripts/Autoload/) | `GameState` · `NetworkManager` · `CardDatabase` 등 | 세션을 소유하고 중계만 함 |
| [`Scripts/Match3D/`](Scripts/Match3D/) | 3D 매치 화면 전체 | 판정하지 않음 |
| [`Scripts/Cards/`](Scripts/Cards/) | `CardData`(Resource) · [`DeckAssembler`](Scripts/Cards/DeckAssembler.cs) | 덱 구성을 결정하는 유일한 지점 |
| [`Scripts/Network/`](Scripts/Network/) | `CardChoiceCodec` — 선택 ↔ RPC 정수 배열 | |
| [`Deprecated/`](Deprecated/) | 2D 표현 계층 | 컴파일 대상 제외. 삭제가 아니라 이동 |

## 빌드 및 실행

Godot 4.7 (**.NET / Mono 빌드**)과 .NET 8 SDK가 필요합니다.

```bash
dotnet build   # Godot 프로젝트 · GameLogic · Tests 전체 빌드
dotnet test    # 규칙 검증 (Godot 실행 불필요)
```

에디터에서는 `project.godot`을 Godot 4.7 Mono로 엽니다. 멀티플레이는 인스턴스 두 개를 실행하여
한쪽에서 호스트, 다른 쪽에서 접속하는 방식으로 확인합니다.

## 구현 현황

**구현 완료** — 접속, 3D 매치 진행, 카드 제출과 공개, 승패 판정, 체력 감소, 승리 연출 3종
(바위·가위·보), 마우스 시선 추적과 그 동기화, 라운드 타임아웃, 타이틀 화면 3D 배경.

**미구현** — 가위 승리의 부가효과(다음 턴 아이템 봉쇄)와 보 승리의 부가효과(상대 손패 공개)는
기획에만 존재합니다. 아이템 시스템 자체가 아직 없기 때문입니다. 능력카드 4종은 코드와 테스트가
유지되고 있으나 현재 덱에서는 제외되어 있으며, 덱은 가위·바위·보 3장씩으로 구성됩니다.

## 문서

이 저장소는 **결정의 이유를 코드 바깥에 기록해 두는 것**을 규칙으로 삼고 있습니다. 커밋
메시지에도 무엇을 바꿨는지와 함께 **왜 그렇게 했는지**를 반드시 남깁니다.

| 문서 | 내용 |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | 현재 무엇이 있고 어떻게 연결되어 있는지 |
| [DESIGN.md](DESIGN.md) | 게임 규칙과 콘텐츠 |
| [IDEAS.md](IDEAS.md) | 아직 확정하지 않은 확장 방향 |
| [DevLogDoc/](DevLogDoc/) | 결정과 그 이유를 날짜순으로 기록. 무엇을 틀렸고 어떻게 알아냈는지까지 |
| [CLAUDE.md](CLAUDE.md) | 코드베이스 규칙. AI 협업 시 지시문으로도 사용 |
| [ATTRIBUTIONS.md](ATTRIBUTIONS.md) | 서드파티 에셋의 출처와 라이선스 |

## 라이선스

코드 라이선스는 아직 확정하지 않았습니다. 서드파티 에셋은 각자의 라이선스를 따르며, 출처와
조건은 [ATTRIBUTIONS.md](ATTRIBUTIONS.md)에 정리되어 있습니다. **일부 에셋은 재배포가
제한**되므로 저장소를 그대로 재배포하기 전에 확인이 필요합니다.

<div align="center">
<br>

**김사윤** · [GitHub](https://github.com/Sayoon210) · sayoon210@naver.com

</div>
