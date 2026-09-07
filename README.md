# RockAndScissPaper 3D

**가위바위보를 카드 게임으로 재해석한 3D 1:1 멀티플레이어 카드 게임.**

테이블을 사이에 두고 마주 앉아 카드를 낸다. 이긴 쪽은 진 쪽을 실제로 때리고, **무엇으로
이겼는지에 따라 연출과 피해량이 갈린다** — 바위는 얼굴을 치고(2 피해), 가위는 상대의 손을
찌르고(1 피해), 보는 책상을 내려친다(1 피해).

| | |
|---|---|
| **ROLE** | 기획 및 개발 (1인) |
| **DURATION** | 2026.08 ~ 진행 중 |
| **ENGINE** | Godot 4.7 (Mono) / C# .NET 8 |
| **TEST** | xUnit — 테스트 146개, Godot 없이 0.1초 |
| **시연 영상** | https://youtu.be/wyBZq2DbPMs |
| **프로젝트 페이지** | https://poolc.org/project/518 |

---

## 이 저장소에서 무엇을 보면 되는가

읽는 순서를 기준으로 정리했다. 각 항목은 아래 해당 절로 연결된다.

| | 무엇을 | 핵심 코드 |
|---|---|---|
| **1** | [규칙과 엔진을 빌드로 분리했다](#1-규칙과-엔진의-분리--문서가-아니라-빌드가-지킨다) | [`GameLogic/`](GameLogic/) · [`RockAndScissPaper.csproj`](RockAndScissPaper.csproj) |
| **2** | [규칙을 테스트로 검증한다](#2-테스트--에디터를-켜지-않고-규칙을-검증한다) | [`Tests/`](Tests/) |
| **3** | [호스트만 판정하고, 클라이언트를 신뢰하지 않는다](#3-멀티플레이--호스트-권위와-불신) | [`GameState.cs`](Scripts/Autoload/GameState.cs) |
| **4** | [표현 계층은 판정하지 않고 결과만 받는다](#4-표현-계층--판정하지-않고-그리기만-한다) | [`MatchWorldView.cs`](Scripts/Match3D/MatchWorldView.cs) |
| **5** | [네트워크 수신값을 보간해 3D 움직임을 부드럽게 했다](#5-3d-움직임-최적화--전송을-늘리지-않고-해결) | [`RemoteHeadLook.cs`](Scripts/Match3D/RemoteHeadLook.cs) |
| **6** | [2D를 3D로 갈아엎으며 초기 설계를 증명했다](#6-2d--3d-전환--초기-설계가-실제로-값을-한-지점) | [`DevLogDoc/`](DevLogDoc/) |

---

## 1. 규칙과 엔진의 분리 — 문서가 아니라 빌드가 지킨다

```
RockAndScissPaper.csproj   Godot.NET.Sdk        씬 · 노드 · 입력 · RPC · UI
        │
        └── references ──▶ GameLogic/           Microsoft.NET.Sdk   순수 규칙, 참조 없음
                                ▲
Tests/  xUnit ──────────────────┘               GameLogic만 참조
```

`GameLogic`은 **아무것도 참조하지 않는다.** 그래서 그 안에서 `using Godot;`을 쓰면 규칙 위반
경고가 아니라 **컴파일 에러(`CS0246`)로 빌드가 실패한다.**

이렇게 만든 이유는 AI 협업 때문이다. 구현을 맡기면 가장 먼저 무너지는 것이 계층 경계인데,
마크다운 문서에 "여기엔 엔진 코드를 넣지 마시오"라고 적어두는 것만으로는 강제력이 없다.
경계를 빌드 시스템에 넣으면 지켜지지 않는 것 자체가 불가능해진다.

**볼 코드**

| | |
|---|---|
| 순수 규칙의 진입점 | [`GameLogic/MatchSession.cs`](GameLogic/MatchSession.cs) — 매치 하나의 전체 상태 머신. `Node`도 `Resource`도 없다 |
| 판정 로직 | [`GameLogic/WinLossRules.cs`](GameLogic/WinLossRules.cs) — 상성과 [기호별 피해량 상수](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/GameLogic/WinLossRules.cs#L24-L26) |
| 경계를 강제하는 설정 | [`GameLogic/RockAndScissPaper.GameLogic.csproj`](GameLogic/RockAndScissPaper.GameLogic.csproj) — `Microsoft.NET.Sdk`, 참조 0개 |
| 엔진 쪽 규칙 문서 | [`GameLogic/CLAUDE.md`](GameLogic/CLAUDE.md) — 이 경계가 무엇을 금지하고 왜 그런지 |

`MatchSession`은 라운드를 **상태 머신**으로 관리한다. [`ERoundPhase`](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/GameLogic/MatchSession.cs#L11-L17)를
별도 필드로 두지 않고 "지금 판정 대기 중인 라운드 객체가 있는가"에서 파생시켜, 상태값과 실제
상태가 어긋날 여지를 없앴다. 정산 도중 예외가 나도 매치가 영구히 멈추지 않도록
[`finally`로 라운드를 반드시 정리한다](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/GameLogic/MatchSession.cs#L297-L327).

## 2. 테스트 — 에디터를 켜지 않고 규칙을 검증한다

```bash
dotnet test
```

```
통과!  - 실패: 0, 통과: 146, 건너뜀: 0, 전체: 146, 기간: 100 ms
```

Godot을 띄우지 않고, 인스턴스 두 개를 켜서 클릭하지 않고, **0.1초 만에** 규칙 전체를 검증한다.
`GameLogic`이 Godot에 의존하지 않기 때문에 가능한 구조다.

**무엇을 검증하는가**

| | |
|---|---|
| 가위바위보 판정과 피해량 | [`WinLossRulesTests.cs`](Tests/WinLossRulesTests.cs) — `[Theory]` + `[InlineData]`로 9개 조합 전수 검사. 파일 전체가 24줄 |
| 상태 머신 전이 규칙 | [`MatchSessionTests.cs`](Tests/MatchSessionTests.cs) — 잘못된 시점의 제출/선택은 예외, 매치 종료 후 제출 불가, 거부된 입력은 상태를 건드리지 않음 |
| 결정론 | 같은 시드면 같은 초기 손패, 선택이 P1/P2 어느 순서로 도착해도 같은 결과 — 네트워크 도착 순서가 판정을 바꾸면 안 되기 때문 |
| 회귀 방지 | [`SwapEffectTests.cs`](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/Tests/Effects/SwapEffectTests.cs#L90-L105) — 과거에 매치를 멈추게 했던 버그가 왜 그랬는지, 무엇이 다시 나타나면 그 버그가 돌아온 것인지를 테스트에 주석으로 박아둠 |

테스트 이름은 식별자가 아니라 **문장**으로 짓는다 — `SubmitCard_throws_once_the_match_is_over`,
`A_choice_made_against_the_offered_hand_survives_the_opponents_Reset`. 콘솔에 뜨는 실패
테스트의 이름만 읽어도 무엇이 깨졌는지 알 수 있고, AI 에이전트에게 실패 로그를 그대로 넘겨도
맥락 설명이 따로 필요 없다.

## 3. 멀티플레이 — 호스트 권위와 불신

`ENetMultiplayerPeer` + Godot 하이레벨 멀티플레이어 API. **규칙은 호스트에서만 실행된다.**

**하나의 진입점.** 카드를 내는 동작은 호스트든 클라이언트든 같은 함수를 부른다 —
[`GameState.RequestCardPlay`](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/Scripts/Autoload/GameState.cs#L217-L231).
호스트는 로컬에서 처리하고 클라이언트는 `RpcId`로 요청만 보내는데, **그 분기는 이 함수 안쪽에서만
일어난다.** 카드를 그리는 쪽은 자신이 호스트인지 클라이언트인지 알 필요가 없다.

**클라이언트를 신뢰하지 않는다.** 클라이언트가 보내는 "이 카드를 냈다"는 클라이언트가 만든
데이터이므로 전제하지 않고 재검증한다 —
[`HandleSubmission`](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/Scripts/Autoload/GameState.cs#L523-L562).
검증은 별도의 방어 코드가 아니라 **판정 로직 자체가 예외를 던지는 방식**이다. 손패에 없는 카드,
이미 제출한 뒤의 재제출 같은 시도는 `MatchSession`이 거부하고, 호스트는 그 예외를 잡아
요청을 조용히 기각한다. 그래서 잘못된 입력이 서버를 중단시키거나 상태를 어중간하게 망가뜨리지
않고, 다음 시도를 그대로 받을 수 있는 상태가 유지된다.

**정보 은닉.** 공개 정보(승패, 체력, 손패 **장수**)는 `Rpc`로 전체에 브로드캐스트하고, 손패
**내용** 같은 비공개 정보는 `RpcId`로 그 주인에게만 보낸다 —
[`BroadcastRoundResult`](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/Scripts/Autoload/GameState.cs#L782-L865).
호스트 프로세스는 양쪽의 진짜 손패를 메모리에 들고 있으므로 네트워크가 막아주지 않는다. 그래서
**호스트의 화면조차 내부 세션을 직접 읽지 않고** 클라이언트와 같은 읽기 모델(`GameState.View`)을
경유하도록 규칙으로 정했다 — [`Scripts/CLAUDE.md`](Scripts/CLAUDE.md).

## 4. 표현 계층 — 판정하지 않고 그리기만 한다

호스트와 클라이언트는 결과를 **서로 다른 경로로** 받는다. 호스트는 자기가 계산한 결과를 그대로
쓰고, 클라이언트는 [`RoundResolvedRpc`](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/Scripts/Autoload/GameState.cs#L437-L485)로
받는다. 그런데 양쪽 다 마지막에 **같은 시그널**(`RoundResolved`)을 자기 프로세스 안에서 발행하고,
화면은 그 시그널만 구독한다 —
[`MatchWorldView`의 구독부](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/Scripts/Match3D/MatchWorldView.cs#L211-L214).

그 결과 **표현 계층에는 호스트/클라이언트 분기가 하나도 없다.** `MatchWorldView`는 판정 로직을
한 줄도 갖지 않고, 이미 결정된 승패를 읽어 어떤 애니메이션을 재생할지만 고른다.

## 5. 3D 움직임 최적화 — 전송을 늘리지 않고 해결

마우스를 따라 움직이는 캐릭터 머리의 본 회전을 **매 프레임(60Hz) 전송하는 것은 낭비**라고 판단해
15Hz로 제한해 보냈다. 그런데 받는 쪽이 도착한 값을 그 프레임에 그대로 적용하다 보니, 값이 오는
순간에만 움직이고 그 사이는 멈춰 있다가 다음 값에서 한 번에 튀었다.

508프레임을 실측한 결과 **실제로 움직인 프레임은 8.3%뿐**이었고, 최대 회전 폭이 평균의 **12.1배**였다.

수신값을 즉시 반영하는 대신 **목표값으로 설정하고 매 프레임 지수적으로 접근**하도록 고쳤다 —
[`RemoteHeadLook.cs`](https://github.com/Sayoon210/rock-and-sciss-paper/blob/000d7a3ca0db8c713a8bed13e1fff7d23e5ef4c2/Scripts/Match3D/RemoteHeadLook.cs#L96-L106).

```csharp
float weight = 1f - Mathf.Exp(-LOOK_SMOOTHING_PER_SECOND * (float)delta);
_shownLocalDelta = _shownLocalDelta.Slerp(_targetLocalDelta, weight).Normalized();
```

세 가지가 의도적이다. `rate * dt`가 아니라 `1 - e^(-rate·dt)`라서 **프레임레이트가 달라도 같은
곡선**이고 긴 프레임에서 오버슈트하지 않는다. `Lerp`가 아니라 `Slerp`라서 쿼터니언이 **일정한
각속도**로 돈다. 매 프레임 정규화해서 반복 보간의 부동소수 오차가 스케일로 새지 않는다.

**결과: 100%의 프레임에서 회전이 갱신되었고 최대 회전 폭은 평균의 2.2배로 감소.** 전송 빈도는
그대로 두었으므로 대역폭을 추가로 쓰지 않았다.

## 6. 2D → 3D 전환 — 초기 설계가 실제로 값을 한 지점

| 날짜 | |
|---|---|
| **08-16** | 로직 경계를 긋고 빌드로 강제하기로 결정. 이유는 **테스트를 빠르게 돌리려는 것**이었고 3D 계획은 없었다 |
| **08-22** | 2D → 3D 전환 검토. 견적 산출 — `Scripts/UI`의 **3,776줄만** 재작성하고 나머지 55%는 유지 |
| **08-28** | 실행. 견적대로 45%만 들어냈고, **`GameLogic`과 `Tests`는 0줄 재작성** |

전체 8,435줄 중 다시 쓴 3,776줄은 **이 게임에서 어렵지 않았던 45%**였다. 실제로 어려웠던 세
가지(라운드 판정 우선순위, 호스트 권위, 히든 정보)는 전부 화면과 무관한 곳에 있었고, 2D에 강하게
묶여 있던 것들(드래그 좌표, 손패 배치, 툴팁)은 하나같이 게임 규칙을 몰랐다. 경계가 이 두 축을
갈라놓았기 때문에 3D 전환이 "어려운 코드를 손대는 일"이 아니라 "쉬운 코드를 갈아끼우는 일"이 됐다.

전환이 쉬웠던 직접적인 이유는, **새로 만든 3D 표현 계층이 기존 2D UI가 구독하던 것과 같은
시그널을 그대로 구독**했기 때문이다. 판정 쪽 코드는 자신을 구독하는 것이 부채꼴로 펼쳐진 2D
손패인지 좌석에 앉은 3D 캐릭터인지 알 필요도, 알 방법도 없었다.

> 자세한 과정: [**2D에서 3D로 — 전환 회고**](DevLogDoc/2026-09-06-2d-to-3d-retrospective.md) ·
> [견적을 낸 날](DevLogDoc/2026-08-22-boundary-pays-off-in-3d.md) ·
> [실행한 날](DevLogDoc/2026-08-28-presentation-layer-swap.md)

---

## 코드 맵

| 폴더 | 무엇이 | 비고 |
|---|---|---|
| [`GameLogic/`](GameLogic/) | 규칙 — 판정 · 덱 · 손패 · 세션 | Godot 참조 0. `using Godot;`이 컴파일 실패 |
| [`GameLogic/Effects/`](GameLogic/Effects/) | 능력카드 4종 + `ICardEffect` | 현재 덱에서 빠져 있으나 코드와 테스트는 유지 |
| [`Tests/`](Tests/) | xUnit 테스트 146개 | `GameLogic`만 참조 |
| [`Scripts/Autoload/`](Scripts/Autoload/) | 전역 서비스 — `GameState` · `NetworkManager` · `CardDatabase` 등 | 세션을 **소유하고 중계만** 함 |
| [`Scripts/Match3D/`](Scripts/Match3D/) | 3D 매치 화면 전부 | 판정하지 않음 |
| [`Scripts/Cards/`](Scripts/Cards/) | `CardData`(Resource) · [`DeckAssembler`](Scripts/Cards/DeckAssembler.cs) | 덱 구성은 여기 한 곳 |
| [`Scripts/Network/`](Scripts/Network/) | `CardChoiceCodec` — 선택 ↔ RPC 정수 배열 | |
| [`Deprecated/`](Deprecated/) | 2D 표현 계층 | **컴파일 글롭 밖.** 지운 게 아니라 옮긴 것 |

## 실행

Godot 4.7 (**.NET / Mono 빌드**)과 .NET 8 SDK가 필요하다.

```bash
dotnet build
```

```bash
dotnet test
```

에디터에서는 `project.godot`을 Godot 4.7 Mono로 연다. 멀티플레이는 인스턴스 두 개를 띄워
한쪽에서 호스트, 다른 쪽에서 접속한다.

## 지금 되는 것 / 안 되는 것

**된다** — 접속, 3D 매치 진행, 카드 제출과 공개, 승패 판정, 체력 감소, 승리 연출 3종(바위/가위/보),
마우스 시선 추적과 그 동기화, 라운드 타임아웃, 타이틀 화면 3D 배경.

**아직 안 된다** — 가위 승리의 부가효과(다음 턴 아이템 봉쇄)와 보 승리의 부가효과(상대 패 공개)는
기획에만 있고 구현되지 않았다. **아이템 시스템 자체가 없기 때문이다.** 능력카드 4종은 코드와
테스트가 살아 있지만 현재 덱에서는 빠져 있다 — 덱은 가위/바위/보 3장씩이다.

## 문서

이 저장소는 **결정의 이유를 코드 바깥에 기록해 두는 것**을 규칙으로 삼는다. 커밋 메시지에는
무엇을 바꿨는지와 함께 **왜 그렇게 했는지**를 반드시 남긴다.

| | |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | 지금 무엇이 있고 어떻게 이어져 있나 (사실 기록) |
| [DESIGN.md](DESIGN.md) | 게임 규칙과 콘텐츠 (기획서) |
| [IDEAS.md](IDEAS.md) | 아직 정하지 않은 확장 |
| [DevLogDoc/](DevLogDoc/) | **결정과 그 이유, 날짜순.** 무엇을 틀렸고 어떻게 알아냈는지까지 |
| [CLAUDE.md](CLAUDE.md) | 코드베이스 규칙. AI 협업 시 지시문으로도 쓰인다 |
| [ATTRIBUTIONS.md](ATTRIBUTIONS.md) | 서드파티 에셋 출처와 라이선스 |

`CLAUDE.md`(변하지 않는 규칙)와 `ARCHITECTURE.md`(오늘의 상태)를 분리한 이유가 있다. 전자는 매
작업마다 **지시문으로 읽히기 때문에**, 거기 낡은 문장이 있으면 방치되는 게 아니라 그대로 실행된다.
실제로 존재하지 않는 클래스에 입력을 넣으라는 지시가 남아 있던 적이 있다.

## 라이선스

코드 라이선스는 아직 정하지 않았다. 서드파티 에셋은 각자의 라이선스를 따르며 출처와 조건은
[ATTRIBUTIONS.md](ATTRIBUTIONS.md)에 있다 — **일부 에셋은 재배포가 제한**되므로 저장소를 그대로
재배포하기 전에 확인이 필요하다.
