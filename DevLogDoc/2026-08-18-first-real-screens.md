# 2026-08-18 — 첫 실제 화면: CardView, HandView, 화면 3개

같은 날 [선택 시점 변경](2026-08-18-choose-after-reveal.md)에 이어지는 작업. 로직/네트워킹 레이어가 두 인스턴스로 검증된 뒤, 처음으로 `MatchDebugUI`가 아닌 실제 화면을 만들었다.

## 왜 지금

`MatchDebugUI`는 버릴 물건으로 처음부터 정해뒀다. 그 아래 레이어(라운드 해결, 선택 시점, RPC)가 오늘 다 검증됐으니 UI를 시작할 차례였고, UI 설계 문서(B)가 이미 레이아웃과 씬 분리 근거를 정해둔 상태였다.

## 만든 것

```
Scenes/Match/CardView.tscn        Scripts/UI/CardView.cs      (149줄)
Scenes/Match/HandView.tscn        Scripts/UI/HandView.cs      (107줄)
Scenes/Screens/TitleScreen.tscn        TitleScreenUI.cs        (37줄)
Scenes/Screens/ConnectionScreen.tscn   ConnectionScreenUI.cs   (139줄)
Scenes/Screens/MatchScreen.tscn        MatchScreenUI.cs        (303줄)
```

`project.godot`의 `run/main_scene`이 `MatchDebugUI.tscn` → `TitleScreen.tscn`으로 바뀌었다. `MatchDebugUI`는 지우지 않고 타이틀 화면에 "디버그 하네스" 버튼을 새로 달아 계속 접근 가능하게 남겨뒀다 — 교체/변화 선택 흐름을 실제로 낼 수 있는 유일한 수단이라 아직 버릴 수 없다.

`GameLogic/`, `Tests/`, `Scripts/Autoload/`는 전혀 건드리지 않았다. `project.godot`만 `run/main_scene`과 창 크기(1152×648 → 1600×1000) 두 줄이 바뀌었다 — 카드가 200×280 고정이라 상대 패/필드/내 패 세 줄이 940px를 필요로 해서, 기본 해상도로는 아래쪽 손패가 화면 밖으로 밀려났다.

### CardView — 카드 하나가 모든 카드를 그린다

루트 `CLAUDE.md`가 카드 변형별 서브클래스 트리를 금지하고 있어서, 씬 하나 스크립트 하나가 10종 카드 전부를 그린다. 자식 노드 5개가 전부 같은 200×280 사각형에 겹쳐 쌓인다: 색 채우기(카드 종류별) → 아트(`CardArt`가 `null`이면 숨김) → 뒷면 → 종류 테두리 → 이름 라벨(아트 위에 겹침).

공개 표면은 딱 셋 — 앞면으로 보여주기, 뒷면으로 보여주기, 인자 없는 `Clicked` 시그널. `View`를 안 읽고, 어떤 오토로드 시그널도 안 듣고, 클릭이 뭘 뜻하는지 스스로 안 정한다. 클릭 처리 주체(`HandView`)가 노드를 바인딩한다: `cardView.Clicked += () => OnCardClicked(cardView);`.

**설계 문서(B)가 세워둔 아트 대비 규칙 넷을 그대로 지켰다**: 종횡비 고정(플레이스홀더든 아트든 동일), 이름 라벨이 처음부터 아트 위에 겹침(옆이 아니라), 종류 테두리가 아트 모드에서도 살아남음(색 채우기에만 실려 있지 않음), `CardArt`는 영구히 nullable.

### 내가 지시하면서 실수한 것 — 히든 정보가 샐 뻔했다

"카드 종류 테두리는 항상 보이게"라고 지시했는데, 이게 뒷면 위에 그려지는 노드라서 그대로 구현하면 **상대 손패의 모든 카드 종류가 화면에 그대로 공개**될 뻔했다. 실행한 에이전트가 이걸 잡아서 뒷면일 땐 회색 중립색으로, 앞면일 때만 종류색으로 칠하도록 고쳤다:

```csharp
// The border node stays visible, but neutral. Tinting it by 카드 종류 here would
// publish exactly the information the back exists to hide.
_borderStyle.BorderColor = new Color(0.45f, 0.47f, 0.55f);
```

지시를 코드로 옮기기 전에 한 번 더 검증이 필요했던 지점 — 확정하고 커밋하기 전에 직접 코드를 읽고 확인했다.

### MatchScreenUI — 시그널을 듣는 곳은 한 군데뿐

씬 안에서 오토로드 시그널을 구독하는 스크립트는 `MatchScreenUI` 하나뿐이다. `CardView`/`HandView`는 메서드 호출로만 갱신된다 — 화면과 매치 상태가 어긋났을 때 볼 곳이 한 군데로 고정된다.

모든 갱신 메서드가 `GameState.Instance!.View`를 매번 새로 읽는다. `MatchView` 필드를 캐싱하지 않는다 — `ResetMatch()`가 객체 자체를 새로 갈아끼우기 때문에, 캐싱했다면 재대전 첫 판부터 죽은 객체를 계속 그리게 됐을 것.

스코어 표시는 `MatchSession.WINS_NEEDED_FOR_MATCH`를 읽어 코드로 점 10개를 만든다 — 하드코딩한 10개 노드가 아니라서, 승수가 또 바뀌어도 화면은 안 건드려도 된다.

`RequestRejected`는 호스트의 원시 예외 텍스트를 그대로 싣고 있다("Hand does not hold enough copies of 교체 to swap." 같은). 그걸 그대로 보여주지 않고 화면엔 "낼 수 없는 카드입니다."만 띄우고, 원문은 `GD.Print`로 로그에만 남긴다.

### 내가 놓쳤던 시그널 하나 — `ChoiceRequired`

호스트 자신이 교체/변화를 냈을 때, `GameState.PromptOneChooser`는 `BroadcastReveal`이 이미 `RoundRevealed`를 쏜 **뒤에** `View.CardIMustChooseFor`를 채우고 `ChoiceRequired`만 쏜다(`MyHandChanged`는 안 쏨). `RoundRevealed`만 구독했다면 호스트 자신의 선택 프롬프트를 영원히 못 봤을 것 — 이건 오늘 선택 시점 변경 작업에서 내가 짠 코드의 결과라, 화면을 실제로 만들면서야 드러난 누락이었다.

## 확정 안 하고 넘긴 것 3개

1. **창 크기 1600×1000.** 카드 크기(200×280 고정)와 해상도 중 어느 쪽을 기준값으로 볼지는 아직 안 정했다. 지금은 해상도를 늘리는 쪽으로 임시 처리.
2. **한글 폰트 미확인.** Godot 4 기본 테마 폰트(OpenSans SemiBold)엔 한글이 없고 저장소엔 폰트 파일이 없다. `MatchDebugUI`가 이미 한글 카드 이름을 보여주고 있었으니 실제로 깨졌다면 진작 드러났을 텐데, 화면을 직접 띄워 확인하기 전엔 단정할 수 없어서 보류.
3. **`OpponentLeft` 처리 시 갇힘.** "상대가 나갔습니다." 메시지만 뜨고 타이틀로 돌아갈 버튼이 없다. 작업 범위를 좁게 잡은 결과 — 버튼 하나 추가하면 끝나는 일.

## 의도적으로 안 만든 것

교체/변화 선택 UI(하단에 빈 라벨만), 매치 기록 레일, 결과 화면, 재대전 버튼, 애니메이션. `MatchView`에 아무 필드도 추가하지 않았다. 전부 UI 설계 문서(B)가 "다음 단계"로 남겨둔 항목들이고, 지금 만들면 오늘 검증도 안 된 선택 흐름 위에 또 미검증 레이어를 쌓는 셈이라 미뤘다.

## 검증

- `dotnet build` — 경고 0, 오류 0
- `dotnet test` — 122/122 통과 (이 작업으로 늘거나 준 테스트 없음, `GameLogic`/`Tests` 무변경)
- Godot 실행 확인 — 타이틀/접속/매치 화면 각각 에러 없이 로드, `CardView.tscn`을 코드에서 인스턴스화하는 경로까지 포함해서 확인
- **두 인스턴스 실제 플레이는 아직.** 카드 클릭 → `RequestCardPlay` 경로, 공개/해결 시점의 화면 갱신이 실제 매치에서 검증되지 않았다

## 남은 것

- 두 인스턴스로: (1) 디버그 하네스로 선택 흐름(교체/변화/조커 차단/동시 선택/타임아웃) 먼저, (2) 새 화면으로 일반 카드 플레이 확인 — 이 순서가 나은 이유는 새 화면이 아직 선택을 못 내서 일반 라운드만 검증 가능하기 때문
- 한글 폰트 실제 확인, 필요하면 폰트 에셋 추가(다운로드라 승인 필요)
- `OpponentLeft` 탈출구
- 창 크기/카드 크기 중 기준값 확정
