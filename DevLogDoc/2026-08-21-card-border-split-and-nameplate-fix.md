# 2026-08-21 — 카드 종류 테두리 분리, 이름표 정렬, 그리고 사라진 locale/fallback

이번엔 굵직한 설계 결정은 없고, 화면 다듬기 두 개와 에디터가 흘린 설정 하나를 잡은 기록.

## 일반카드/공백카드는 각진 검은 테두리로

`CardView`의 종류 테두리(`TypeBorder`)는 `_borderStyle` 하나(인스턴스마다 복제한
`StyleBoxFlat`)를 공유해서, 색은 `TypeColorOf(cardType)`이, 모서리 반경은 씬에 박힌 값
10이 전부 담당했다 — 카드 종류에 상관없이 항상 둥글고 항상 종류색이었다.

일반카드와 공백카드만 검은 사각 테두리로 바꿔달라는 요청이라, 색과 반경을 한 자리에서
같이 분기했다:

```csharp
bool squareBlackBorder = cardType == CardType.Normal || cardType == CardType.Blank;
_borderStyle.BorderColor = squareBlackBorder ? Colors.Black : typeColor;
_borderStyle.SetCornerRadiusAll(squareBlackBorder ? 0 : 10);
```

조커/능력카드는 그대로 둥근 종류색 테두리. 배경 채우기(`_placeholderFill`)와 툴팁 배지는
계속 `TypeColorOf`를 그대로 쓰므로 안 건드렸다 — 이번 요청은 카드 자체의 테두리 얘기였지
종류색 자체를 없애는 얘기가 아니었다.

## 이름 라벨이 이름표랑 안 맞았던 이유

`NameLabel`은 씬에서 `offset_bottom = -14`, 아래 정렬로 박혀 있었다. 가위/바위/보는 이미
`CardArt`가 있는데, 그 아트(`CardSprite.png`) 자체에 카드 하단에 두루마리 모양 이름표가
그려져 있다는 걸 스프라이트를 잘라 확대해보고서야 알았다.

`ArtView`는 `STRETCH_KEEP_ASPECT_COVERED`라 90×129 원본이 200×280 칸을 거의 꽉 채우게
2.22배로 스케일된다. 그 배율로 원본의 이름표 영역(대략 y 97~117 / 129)을 옮기면 화면
기준 y 212~257 — 라벨이 있던 y 266 근처보다 한참 위다. 그래서 이름 텍스트가 이름표를
벗어나 카드 맨 아래 여백에 떠 있었다.

`offset_bottom`을 `-30`으로 올려서 이름표 안에 들어오게 맞췄다. 라벨은 카드 전체에
걸린 하나의 노드라 아직 아트가 없는 카드(공백/조커/능력카드 5종)도 같이 올라갔는데,
이건 오히려 의도에 맞다 — 나중에 그 카드들도 같은 틀의 아트를 받을 걸 가정하고 라벨
위치를 아트 쪽에 맞춰둔 셈이니까.

## 사라진 locale/fallback

Debug → Customize Run Instances로 로컬 실행 인자(`--resolution ...`)를 넣은 뒤
`project.godot`를 보니 `[internationalization]`의 `locale/fallback="en"`이 통째로
없어져 있었다. 그 줄을 건드릴 이유가 있는 조작이 아니었는데도 에디터가 프로젝트 설정을
다시 쓰는 과정에서 같이 날아간 것 — 에디터의 project.godot 재저장이 무손실이 아니라는
뜻이라, 앞으로 에디터에서 프로젝트 설정을 만졌으면 diff를 한 번 보는 게 안전하다.

지원 안 하는 로케일이 영어로 안 떨어지고 심볼 그대로(`TITLE_PLAY` 등) 뜨는 걸 막는
설정이라 [Scripts/CLAUDE.md](../Scripts/CLAUDE.md)의 로컬라이제이션 규칙이 실제로
지켜지려면 있어야 하는 줄이다. 다시 넣었다.

## 검증한 것

- `dotnet build` 경고 0 / 오류 0
- `TitleScreenUI`에 임시 프로브를 넣어 바위(일반)/공백/조커/리셋(능력) 네 장을
  1600×1000 창에서 실제로 그려 스크린샷으로 확인 — 일반/공백만 각진 검은 테두리,
  나머지는 둥근 종류색 테두리. 바위 카드는 "바위" 글자가 이름표 두루마리 안에 들어감.
  프로브 코드는 커밋 전에 되돌렸다.
