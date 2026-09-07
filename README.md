<div align="center">

# RockAndScissPaper 3D

**가위바위보를 카드 게임으로 재해석한 3D 1:1 멀티플레이어 카드 게임**

![Godot](https://img.shields.io/badge/Godot-4.7%20Mono-478CBF?style=flat-square&logo=godotengine&logoColor=white)
![C#](https://img.shields.io/badge/C%23-.NET%208-512BD4?style=flat-square&logo=dotnet&logoColor=white)
![Tests](https://img.shields.io/badge/xUnit-146%20passed-3FB950?style=flat-square)
![Multiplayer](https://img.shields.io/badge/multiplayer-1:1%20host--authoritative-6E5494?style=flat-square)
![Status](https://img.shields.io/badge/status-in%20development-F0883E?style=flat-square)

[**시연 영상**](https://youtu.be/wyBZq2DbPMs) · [**프로젝트 페이지**](https://poolc.org/project/518) · [**개발 로그**](DevLogDoc/) · [**아키텍처 문서**](ARCHITECTURE.md)

<a href="https://youtu.be/wyBZq2DbPMs">
  <img src="https://img.youtube.com/vi/wyBZq2DbPMs/hqdefault.jpg" width="480" alt="게임 시연 영상 (클릭 시 YouTube로 이동)">
</a>

</div>

---

테이블을 사이에 두고 마주 앉아 카드를 냅니다. 이긴 쪽은 진 쪽을 실제로 때리며, **무엇으로
이겼는지에 따라 연출과 피해량이 달라집니다** — 바위는 얼굴을 가격하고(2 피해), 가위는 상대의
손을 찌르며(1 피해), 보는 책상을 내려칩니다(1 피해).

| | |
|---|---|
| **역할** | 기획 및 개발 (1인 개인 프로젝트) |
| **기간** | 2026.08 ~ 진행 중 |
| **엔진** | Godot 4.7 (Mono) |
| **언어 / 런타임** | C# · .NET 8 |
| **테스트** | xUnit — 146개, Godot 없이 0.1초 |
| **네트워크** | ENetMultiplayerPeer · Godot 하이레벨 멀티플레이어 API |

## 이 저장소에서 무엇을 보면 되는가

기술적으로 봐주셨으면 하는 지점을 여섯 가지로 정리했습니다. 각 항목은 해당 절과 핵심 코드로
연결됩니다.

| | 내용 | 핵심 코드 |
|:---:|---|---|
| **1** | [규칙과 엔진을 빌드 수준에서 분리했습니다](#1-규칙과-엔진의-분리) | [`GameLogic/`](GameLogic/) |
| **2** | [규칙을 에디터 없이 테스트로 검증합니다](#2-테스트-구조) | [`Tests/`](Tests/) |
| **3** | [호스트만 판정하며, 클라이언트를 신뢰하지 않습니다](#3-호스트-권위-멀티플레이) | [`GameState.cs`](Scripts/Autoload/GameState.cs) |
| **4** | [표현 계층은 판정하지 않고 결과만 받습니다](#4-표현-계층의-역할-분리) | [`MatchWorldView.cs`](Scripts/Match3D/MatchWorldView.cs) |
| **5** | [수신값을 보간해 3D 움직임을 부드럽게 했습니다](#5-3d-움직임-네트워크-최적화) | [`RemoteHeadLook.cs`](Scripts/Match3D/RemoteHeadLook.cs) |
| **6** | [2D를 3D로 교체하며 초기 설계를 증명했습니다](#6-2d에서-3d로의-전환) | [`DevLogDoc/`](DevLogDoc/) |

---

## 1. 규칙과 엔진의 분리

```
RockAndScissPaper.csproj   Godot.NET.Sdk        씬 · 노드 · 입력 · RPC · UI
        │
        └── references ──▶ GameLogic/           Microsoft.NET.Sdk   순수 규칙, 참조 없음
                                ▲
Tests/  xUnit ──────────────────┘               GameLogic만 참조
```

`GameLogic`은 아무것도 참조하지 않습니다. 따라서 이 프로젝트 안에서 `using Godot;`을 작성하면
규칙 위반 경고가 아니라 **컴파일 에러(`CS0246`)가 발생하여 빌드 자체가 실패합니다.**

이렇게 설계한 이유는 AI 협업 환경 때문입니다. 구현을 맡겼을 때 가장 먼저 무너지는 것이 계층
경계인데, 마크다운 문서에 "여기에는 엔진 코드를 넣지 말 것"이라고 적어두는 방식은 강제력이
없습니다. 경계를 빌드 시스템에 넣으면 지켜지지 않는 상태 자체가 성립하지 않습니다.

**주요 코드**

| 파일 | 내용 |
|---|---|
| [`GameLogic/MatchSession.cs`](GameLogic/MatchSession.cs) | 매치 하나의 전체 상태 머신. `Node`도 `Resource`도 사용하지 않습니다 |
| [`GameLogic/WinLossRules.cs`](GameLogic/WinLossRules.cs) | 상성 판정과 [기호별 피해량 상수](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/GameLogic/WinLossRules.cs#L24-L26) |
| [`RockAndScissPaper.GameLogic.csproj`](GameLogic/RockAndScissPaper.GameLogic.csproj) | `Microsoft.NET.Sdk`, 참조 0개 — 경계를 강제하는 설정 |
| [`GameLogic/CLAUDE.md`](GameLogic/CLAUDE.md) | 이 경계가 무엇을 금지하며 왜 그런지에 대한 규칙 문서 |

라운드는 **상태 머신**으로 관리됩니다. [`ERoundPhase`](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/GameLogic/MatchSession.cs#L11-L17)를
별도 필드로 두지 않고 "현재 판정 대기 중인 라운드 객체가 존재하는가"에서 파생시켜, 상태값과 실제
상태가 어긋날 여지를 제거했습니다. 또한 정산 도중 예외가 발생하더라도 매치가 영구히 멈추지
않도록 [`finally` 블록에서 라운드를 반드시 정리합니다](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/GameLogic/MatchSession.cs#L297-L327).

## 2. 테스트 구조

```bash
dotnet test
```

```
통과!  - 실패: 0, 통과: 146, 건너뜀: 0, 전체: 146, 기간: 100 ms
```

Godot을 실행하지 않고, 인스턴스 두 개를 띄워 클릭하지 않고, **0.1초 만에** 규칙 전체를
검증합니다. `GameLogic`이 엔진에 의존하지 않기 때문에 가능한 구조입니다.

**검증 항목**

| 항목 | 위치 |
|---|---|
| 가위바위보 판정과 기호별 피해량 | [`WinLossRulesTests.cs`](Tests/WinLossRulesTests.cs) — `[Theory]` + `[InlineData]`로 9개 조합 전수 검사. 파일 전체가 24줄입니다 |
| 상태 머신 전이 규칙 | [`MatchSessionTests.cs`](Tests/MatchSessionTests.cs) — 잘못된 시점의 제출·선택은 예외 처리, 매치 종료 후 제출 차단, 거부된 입력이 상태를 변경하지 않음 |
| 결정론 | 동일 시드에서 동일한 초기 손패가 나오는지, 선택이 어느 순서로 도착해도 결과가 같은지 — 네트워크 도착 순서가 판정을 바꾸면 안 되기 때문입니다 |
| 회귀 방지 | [`SwapEffectTests.cs`](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/Tests/Effects/SwapEffectTests.cs#L90-L105) — 과거 매치를 멈추게 했던 버그의 원인과, 무엇이 다시 나타나면 그 버그가 재발한 것인지를 주석으로 명시했습니다 |

테스트 이름은 식별자가 아니라 **문장 형태**로 작성했습니다. `SubmitCard_throws_once_the_match_is_over`,
`A_choice_made_against_the_offered_hand_survives_the_opponents_Reset` 같은 형태입니다. 콘솔에
출력되는 실패 테스트의 이름만 읽어도 무엇이 깨졌는지 파악할 수 있으며, AI 에이전트에게 실패
로그를 그대로 전달해도 별도의 맥락 설명이 필요하지 않습니다.

## 3. 호스트 권위 멀티플레이

게임 규칙에 해당하는 것은 전부 호스트에 두었습니다. 승패 판정은 호스트 한 곳에서만 일어나고
클라이언트는 결과를 받아서 그리기만 합니다.

**하나의 진입점.** 카드를 내는 동작은 호스트든 클라이언트든 동일한 함수를 호출합니다 —
[`GameState.RequestCardPlay`](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/Scripts/Autoload/GameState.cs#L217-L231).
호스트는 로컬에서 처리하고 클라이언트는 `RpcId`로 요청을 전달하는데, **그 분기는 이 함수
내부에서만 일어납니다.** 화면을 그리는 쪽은 자신이 호스트인지 클라이언트인지 알 필요가 없습니다.

**클라이언트를 신뢰하지 않습니다.** 클라이언트가 보내는 "이 카드를 냈다"는 클라이언트가 생성한
데이터이므로 정상이라고 전제하지 않고 재검증합니다 —
[`HandleSubmission`](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/Scripts/Autoload/GameState.cs#L523-L562).
검증은 별도의 방어 코드가 아니라 **판정 로직 자체가 예외를 던지는 방식**으로 구현했습니다.
손패에 없는 카드를 내거나 이미 제출을 마친 상태에서 다시 제출하는 등 규칙에 어긋나는 시도는
`MatchSession`이 거부하고, 호스트는 그 예외를 잡아 요청을 기각합니다. 그 결과 잘못된 입력이
서버를 중단시키거나 게임 상태를 어중간하게 망가뜨리지 않으며, 다음 시도를 그대로 받을 수 있는
상태가 유지됩니다.

**정보 은닉.** 공개 정보(승패·체력·손패 **장수**)는 `Rpc`로 전체에 브로드캐스트하고, 손패
**내용**과 같은 비공개 정보는 `RpcId`로 해당 플레이어에게만 전송합니다 —
[`BroadcastRoundResult`](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/Scripts/Autoload/GameState.cs#L782-L865).
호스트 프로세스는 양쪽의 실제 손패를 메모리에 보유하므로 네트워크가 이를 막아주지 않습니다.
따라서 **호스트의 화면조차 내부 세션을 직접 읽지 않고** 클라이언트와 동일한 읽기 모델
(`GameState.View`)을 경유하도록 규칙으로 정했습니다 — [`Scripts/CLAUDE.md`](Scripts/CLAUDE.md).

## 4. 표현 계층의 역할 분리

호스트와 클라이언트는 결과를 **서로 다른 경로로** 수신합니다. 호스트는 자신이 계산한 결과를
그대로 사용하고, 클라이언트는 [`RoundResolvedRpc`](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/Scripts/Autoload/GameState.cs#L437-L485)로
전달받습니다. 그러나 양쪽 모두 마지막에 **동일한 시그널**(`RoundResolved`)을 자신의 프로세스
안에서 발행하며, 화면은 그 시그널만 구독합니다 —
[`MatchWorldView`의 구독부](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/Scripts/Match3D/MatchWorldView.cs#L211-L214).

그 결과 **표현 계층에는 호스트/클라이언트 분기가 하나도 존재하지 않습니다.** `MatchWorldView`는
판정 로직을 한 줄도 갖지 않으며, 이미 결정된 승패를 읽어 어떤 애니메이션을 재생할지만
선택합니다.

## 5. 3D 움직임 네트워크 최적화

마우스를 따라 움직이는 캐릭터 머리의 본 회전값을 **매 프레임(60Hz) 전송하는 것은
비효율적이라고 판단**하여 15Hz로 제한해 전송했습니다. 그런데 수신 측이 도착한 값을 해당
프레임에 즉시 적용하다 보니, 값이 도착하는 순간에만 움직이고 그 사이에는 멈춰 있다가 다음
값에서 한 번에 튀는 문제가 발생했습니다.

508프레임을 실측한 결과 **실제로 회전이 갱신된 프레임은 8.3%에 불과**했고, 최대 회전 폭이
평균의 **12.1배**로 나타났습니다.

수신값을 즉시 반영하는 대신 **목표값으로 설정하고 매 프레임 지수적으로 접근**하도록
수정했습니다 — [`RemoteHeadLook.cs`](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/Scripts/Match3D/RemoteHeadLook.cs#L96-L106).

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

## 6. 2D에서 3D로의 전환

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
시그널을 그대로 구독**했기 때문입니다. 판정 쪽 코드는 자신을 구독하는 대상이 부채꼴로 펼쳐진 2D
손패인지 좌석에 앉은 3D 캐릭터인지 알 필요도, 알 방법도 없었습니다.

> 상세 내용: [**2D에서 3D로 — 전환 회고**](DevLogDoc/2026-09-06-2d-to-3d-retrospective.md) ·
> [견적을 산출한 날](DevLogDoc/2026-08-22-boundary-pays-off-in-3d.md) ·
> [실행한 날](DevLogDoc/2026-08-28-presentation-layer-swap.md)

---

## 코드 맵

| 폴더 | 내용 | 비고 |
|---|---|---|
| [`GameLogic/`](GameLogic/) | 규칙 — 판정 · 덱 · 손패 · 세션 | Godot 참조 0개. `using Godot;`이 컴파일 실패 |
| [`GameLogic/Effects/`](GameLogic/Effects/) | 능력카드 4종과 `ICardEffect` | 현재 덱에서 제외되었으나 코드와 테스트는 유지 |
| [`Tests/`](Tests/) | xUnit 테스트 146개 | `GameLogic`만 참조 |
| [`Scripts/Autoload/`](Scripts/Autoload/) | 전역 서비스 — `GameState` · `NetworkManager` · `CardDatabase` 등 | 세션을 **소유하고 중계만** 함 |
| [`Scripts/Match3D/`](Scripts/Match3D/) | 3D 매치 화면 전체 | 판정하지 않음 |
| [`Scripts/Cards/`](Scripts/Cards/) | `CardData`(Resource) · [`DeckAssembler`](Scripts/Cards/DeckAssembler.cs) | 덱 구성을 결정하는 유일한 지점 |
| [`Scripts/Network/`](Scripts/Network/) | `CardChoiceCodec` — 선택 ↔ RPC 정수 배열 변환 | |
| [`Deprecated/`](Deprecated/) | 2D 표현 계층 | **컴파일 글롭 제외.** 삭제가 아니라 이동 |

## 빌드 및 실행

Godot 4.7 (**.NET / Mono 빌드**)과 .NET 8 SDK가 필요합니다.

```bash
# 전체 빌드 (Godot 프로젝트 · GameLogic · Tests)
dotnet build
```

```bash
# 규칙 검증 (Godot 실행 불필요)
dotnet test
```

에디터에서는 `project.godot`을 Godot 4.7 Mono로 엽니다. 멀티플레이는 인스턴스 두 개를 실행하여
한쪽에서 호스트, 다른 쪽에서 접속하는 방식으로 확인합니다.

## 구현 현황

**구현 완료** — 접속, 3D 매치 진행, 카드 제출과 공개, 승패 판정, 체력 감소, 승리 연출 3종
(바위·가위·보), 마우스 시선 추적과 그 동기화, 라운드 타임아웃, 타이틀 화면 3D 배경.

**미구현** — 가위 승리의 부가효과(다음 턴 아이템 봉쇄)와 보 승리의 부가효과(상대 손패 공개)는
기획에만 존재하며 구현되지 않았습니다. **아이템 시스템 자체가 아직 없기 때문입니다.** 능력카드
4종은 코드와 테스트가 유지되고 있으나 현재 덱에서는 제외되어 있으며, 덱은 가위·바위·보 3장씩으로
구성됩니다.

## 문서

이 저장소는 **결정의 이유를 코드 바깥에 기록해 두는 것**을 규칙으로 삼고 있습니다. 커밋
메시지에도 무엇을 바꿨는지와 함께 **왜 그렇게 했는지**를 반드시 남깁니다.

| 문서 | 내용 |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | 현재 무엇이 있고 어떻게 연결되어 있는지 (사실 기록) |
| [DESIGN.md](DESIGN.md) | 게임 규칙과 콘텐츠 (기획서) |
| [IDEAS.md](IDEAS.md) | 아직 확정하지 않은 확장 방향 |
| [DevLogDoc/](DevLogDoc/) | **결정과 그 이유를 날짜순으로 기록.** 무엇을 틀렸고 어떻게 알아냈는지까지 포함 |
| [CLAUDE.md](CLAUDE.md) | 코드베이스 규칙. AI 협업 시 지시문으로도 사용됩니다 |
| [ATTRIBUTIONS.md](ATTRIBUTIONS.md) | 서드파티 에셋의 출처와 라이선스 |

`CLAUDE.md`(변하지 않는 규칙)와 `ARCHITECTURE.md`(현재 상태)를 분리한 데에는 이유가 있습니다.
전자는 매 작업마다 **지시문으로 읽히기 때문에**, 낡은 문장이 남아 있으면 방치되는 것이 아니라
그대로 실행됩니다. 실제로 존재하지 않는 클래스에 입력을 연결하라는 지시가 남아 있던 적이
있었습니다.

## 라이선스

코드 라이선스는 아직 확정하지 않았습니다. 서드파티 에셋은 각자의 라이선스를 따르며 출처와
조건은 [ATTRIBUTIONS.md](ATTRIBUTIONS.md)에 정리되어 있습니다. **일부 에셋은 재배포가
제한**되므로 저장소를 그대로 재배포하기 전에 확인이 필요합니다.

<div align="center">

**김사윤** · [GitHub](https://github.com/Sayoon210) · sayoon210@naver.com

</div>
