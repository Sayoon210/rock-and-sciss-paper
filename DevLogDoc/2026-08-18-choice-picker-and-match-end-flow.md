# 2026-08-18 — 교체/변화 픽커, 그리고 매치 화면의 남은 구멍들

[2026-08-18-first-real-screens.md](2026-08-18-first-real-screens.md)에서 만든 `MatchScreen`은
카드를 낼 수는 있었지만 교체/변화를 낸 뒤 실제로 고르는 화면이 없었다 — 그 자리는 라벨 하나뿐이었다.
이 문서는 그 픽커를 실제로 만든 기록과, 그 다음에 이어서 처리한 매치 화면의 나머지 구멍들.

## 픽커 — 손패를 딱딱 클릭해서 고른다

사용자가 처음부터 원한 방식: 교체를 냈으면 덱에 넣을 카드를 **손패에서 직접 클릭**해서 토글.
따로 뜨는 다이얼로그가 아니다.

`HandView`에 `HandSelectionMode` 열거형을 추가했다 — `Play` / `SelectMultipleForSwap` /
`SelectOneForTransform`. 클릭 한 번의 의미가 이 모드에 따라 완전히 달라진다:

- `Play`: 그대로 `RequestCardPlay`
- `SelectMultipleForSwap`: 클릭할 때마다 토글, 몇 장이든 선택 가능
- `SelectOneForTransform`: 단일 선택, 다른 카드를 클릭하면 갈아탐

선택 여부는 `CardName`이 아니라 **노드 참조**로 추적한다. 손패에 같은 카드가 두 장 있을 때
하나만 선택했다는 걸 구분하려면 이게 필수 — `CardName`으로 추적했으면 한쪽을 선택한 게
양쪽 다 선택된 것처럼 보였을 것이다.

`CardView`에는 `SelectionOverlay`(금색 반투명 패널, 최상단 자식)와 `SetSelected(bool)`을 추가했다.
카드 노드 자신은 여전히 "선택됐다"가 무엇을 뜻하는지 모른다 — 그냥 시각적으로 켜고 끌 뿐.

### 모드 설정자는 반드시 멱등이어야 한다

`RefreshPromptStrip()`은 선택이 바뀔 때마다 `SelectionChanged` 시그널을 통해 다시 불린다.
이때 `SetSelectionModeForSwap()`을 매번 새로 호출하는데, 만약 이게 이미 같은 모드일 때도
선택을 초기화해버리면 — 카드 한 장 클릭할 때마다 방금 고른 게 날아간다. 그래서 각
`SetSelectionMode*` 메서드는 "이미 이 모드면 아무것도 안 함"을 제일 먼저 확인한다.

## `MatchScreen.tscn`의 `PromptStrip` 확장

기존엔 라벨 하나였던 자리에 `ConfirmButton`(교체용 "확인")과 `TargetPaletteRow`(변화의
"무엇으로" 팔레트)를 추가했다. 변화는 대상 카드가 손패에 없으므로 손패 클릭만으로는 끝나지
않는다 — 손패에서 바꿀 카드를 고른 다음, 팔레트에서 무엇으로 바꿀지 한 번 더 고른다.

팔레트는 `CardDatabase.Instance.LoadedCardNames`에서 **필터 없이** 전부 만든다. 조커나 리셋을
변화 대상으로 골라도 그대로 전송되고 호스트가 거부한다 — `MatchDebugUI`가 이미 쓰던 방침
("규칙 판단은 호스트가, 화면은 보내기만")을 그대로 따른 것.

## 하다가 찾은 버그 — 호스트 자신의 픽커가 헌 손패를 보여줌

`GameState.PromptOneChooser`의 호스트 자기 자신 분기는 `View.CardIMustChooseFor`만 세팅하고
`View.MyHand`는 갱신하지 않았다. 클라이언트는 `ChoiceRequiredRpc`가 손패를 새로 실어다 주니
문제가 없었는데, 호스트는 그 경로를 안 탄다.

픽커가 라벨뿐이던 동안은 안 드러났다 — 이제 픽커가 실제로 `View.MyHand`를 그려서 클릭
가능한 카드로 보여주므로, 방금 낸 교체/변화 카드가(그리고 리셋이 막 갈아치운 손패가) 그대로
화면에 남아 클릭 가능한 상태로 보이는 게 눈에 띄었다. `ChoiceRequiredRpc`가 클라이언트에게
하는 것과 똑같이 `View.MyHand`를 다시 채우고 `MyHandChanged`를 쏘도록 고쳤다.

## 매치가 끝나면 갇히는 문제

승패가 갈리면 `MatchEnded` 시그널은 나가는데 화면엔 "매치 승리"/"매치 패배" 텍스트만 뜨고
그걸로 끝이었다. 재대결도, 타이틀로 돌아가는 길도 없었다. 상대가 나가도 마찬가지 — 알림
텍스트 하나 뜨고 그 화면에 갇힌다.

`MatchEndOverlay`를 `MatchScreen.tscn` 루트의 마지막 자식(=최상단)으로 추가했다 —
결과 라벨, 재대결 버튼, 재대결 대기 라벨, 타이틀로 버튼.

**재대결은 호스트만 누른다.** `ConnectionScreenUI`의 "매치 시작" 버튼이 이미 같은 비대칭을
쓰고 있어서 — 클라이언트는 호스트가 눌러주길 기다리는 쪽. 클라이언트 화면엔 재대결 버튼
대신 "호스트가 재대결을 시작하길 기다리는 중..." 텍스트만 보인다. 재대결 버튼을 누르면
그냥 `GameState.HostStartsMatch()`를 다시 부른다 — 이 메서드는 이미 내부에서 `ResetMatch()`를
먼저 하니 별도의 리셋 코드가 필요 없었다.

`OpponentLeft`는 재대결 자체가 의미가 없다(재연결은 범위 밖). 재대결 버튼은 숨기고
타이틀로 버튼만 남긴다.

타이틀로 돌아가려면 연결 자체를 끊어야 하는데 그런 메서드가 없었다 — `NetworkManager`에
`Disconnect()`를 추가했다 (`MultiplayerPeer.Close()` 후 null). 호출 순서는
`Disconnect()` → `GameState.ResetConnection()` → 씬 전환.

## 상대가 뭘 했는지 화면에 안 보임

`RoundResult`에 이미 `Player1SwappedCardCount`/`Player1TransformApplied` 같은 필드가 있고
`GameState.View`에도 다 들어와 있었는데, `MatchDebugUI`만 읽고 있었고 실제 화면
(`MatchScreenUI`)은 하나도 안 읽고 있었다. 즉 상대가 교체를 몇 장 했는지, 변화를 썼는지
화면에 텍스트로도 안 나오는 상태.

`Field`의 각 카드 아래에 `MyActionLabel`/`OpponentActionLabel`을 붙이고, `RefreshField()`에서
`DescribeAction(swappedCount, transformApplied)`로 "교체 N장" / "변화 적용" 텍스트를 채운다.
카드 정체성은 여전히 안 실린다 — 세는 값과 플래그뿐이라는 원래 설계(`MatchView`의 주석)를
그대로 따른다. 리빌 시점엔 0/false로 리셋되므로, 라운드가 실제로 해결되기 전까지는 빈
텍스트로 남는다.

애니메이션(카드가 덱으로 들어가고 다시 뽑히는 움직임 등)은 이 텍스트 표시가 먼저 있어야
값을 하는 작업이라 아직 안 건드렸다 — 사용자가 직접 우선순위 밖으로 미뤘다.

## 한글 폰트 — 프로젝트 전역 테마 추가

`project.godot`에 폰트/테마 설정이 전혀 없었다. Godot 4 기본 테마의 한글 글리프 커버리지는
화면으로 직접 봐야 확인되는 것이라(Godot MCP엔 스크린샷 도구가 없다) 눈으로 볼 수 없었지만,
방치하면 라벨 전체가 두부로 보일 수 있는 종류의 문제라 선제적으로 고쳤다.

윈도우에 이미 설치돼 있는 맑은 고딕(`C:\Windows\Fonts\malgun.ttf`)을
`Assets/Fonts/MalgunGothic.ttf`로 복사하고, `Assets/Fonts/DefaultTheme.tres`(전역 기본 폰트만
지정한 `Theme` 리소스)를 만들어 `project.godot`의 `gui/theme/custom`으로 등록했다.

새로 추가한 리소스는 Godot 에디터를 실제로 한 번 거쳐야 `.import`가 생긴다 — 처음 실행에서
`No loader found for resource` 에러가 났고, 헤드리스 임포트(`godot --headless --import`)를
한 번 돌린 뒤에는 `MatchScreen`/`TitleScreen` 둘 다 에러 없이 로드됐다.

⚠ **맑은 고딕은 마이크로소프트 라이선스 폰트다.** 지금은 개발 중 확인 용도로 로컬에 이미
있는 파일을 그대로 복사해 썼지만, 배포를 고려하는 시점엔 OFL 라이선스의 Noto Sans KR 같은
자유 배포 가능한 폰트로 바꿔야 한다. 파일 크기도 13MB로 가볍지 않다.

## 검증한 것 / 안 한 것

- `dotnet build`: 경고 0, 오류 0
- `dotnet test`: 122/122
- `MatchScreen.tscn`, `TitleScreen.tscn`: 헤드리스 임포트 후 Godot MCP로 로드, 에러 없음
- **두 인스턴스로 실제 플레이는 아직 안 함** — 교체/변화 픽커도, 매치 종료/재대결 흐름도,
  이번에 추가한 한글 폰트가 화면에 실제로 어떻게 보이는지도 전부 사람이 직접 봐야 하는 것들
