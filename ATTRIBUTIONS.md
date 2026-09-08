# 외부 에셋 출처

`Assets/` 에 들어간 외부 에셋의 출처와 라이선스 기록. 사운드로 시작했지만 사운드 전용
문서는 아니다 — 종류별로 아래에서 나뉜다.

**파일을 커밋할 때 같이 채운다.** 나중에 몰아서 하면 어느 파일이 어디서 왔는지 이미 모른다 —
받아온 직후가 아니면 복원할 수 없는 정보다. 직접 만들거나 녹음한 소리도 "자작"으로 한 줄
남긴다. 비워두면 출처를 못 찾은 건지 자작인지 구분이 안 된다.

## 사운드

`Assets/Audio/`.

### 목록

| 파일 | `ESoundName` | 제작자 | 출처 | 라이선스 | 수정 |
|---|---|---|---|---|---|
| `RoundWon.wav` | `RoundWon` | rhodesmas | ["Level Up 01"](https://freesound.org/s/320655/) | CC BY 4.0 | 파일명만 변경 |
| `RoundLost.wav` | `RoundLost` | AceOfSpadesProduc100 | ["8-bit "failure" sound"](https://freesound.org/s/333785/) | CC BY 4.0 | 파일명만 변경 |
| `Joker.wav` | `Joker` | bulbastre | ["Evil laughter joker"](https://freesound.org/s/103987/) | CC BY 4.0 | 파일명만 변경 |
| `ButtonPressed.mp3` | `ButtonPressed` | soundshelfstudio (받은 파일명 기준) | **받은 페이지 URL이 기록되지 않음** — 아래 참고 | **미확인** | 파일명만 변경 |
| `MainMenuBGM.ogg` | `-` | Alexander Nakarada ft. Kevin MacLeod | ["Tavern Brawl"](https://creatorchords.com/music/tavern-brawl-ft-kevin-macleod/) (chosic.com 경유) | CC BY 4.0 | 파일명 변경, **ogg로 변환** |

`ButtonPressed.mp3`는 **받은 페이지가 기록되지 않았다.** 복원할 수 있는 단서는 원본 파일명
`soundshelfstudio-ui-click-retro-514601.mp3` 하나뿐이다 — 제작자 이름과 숫자 ID가 거기 들어
있으니 그것으로 다시 찾을 수는 있겠지만, **어느 사이트인지도 라이선스가 무엇인지도 확인되지
않았다.** 배포를 고려하는 시점에는 이 두 칸이 채워져 있어야 하고, 지금 채우는 편이 나중보다
쉽다. 표기 의무가 있는 라이선스로 밝혀지면 아래 "배포용 표기 문구"에도 한 줄이 는다.

`RoundWon.wav`는 한때 같은 소리의 [mp3 재업로드본(337049)](https://freesound.org/s/337049/)을
쓰다가 rhodesmas의 원본으로 교체한 것이다. 재업로더가 아니라 **원작자를 표기하는 게 맞고**,
원본은 CC BY 4.0이라 재업로드본(3.0)과 버전도 다르다.

- **파일** — `Assets/Audio/` 기준 경로. 확장자까지.
- **`ESoundName`** — [Scripts/Autoload/ESoundName.cs](Scripts/Autoload/ESoundName.cs)의 대응 멤버.
  BGM처럼 enum에 없는 것은 `-`.
- **출처** — 받은 페이지 URL. 사이트 이름만 적으면 나중에 그 파일을 다시 못 찾는다.
- **라이선스** — `CC0`, `CC BY 4.0`, `CC BY-SA 4.0`, 상용 라이선스명, `자작` 등.
- **수정** — 자른 것도, 볼륨만 맞춘 것도 수정이다. 아래 참고.

## UI / 이미지

| 파일 | 제작자 | 출처 | 라이선스 | 수정 |
|---|---|---|---|---|
| `Assets/kenney_ui-pack-pixel-adventure/` (전체) | Kenney | ["UI Pack: Pixel Adventure"](https://kenney.nl/assets/ui-pack-pixel-adventure) | CC0 1.0 | 없음 (원본 그대로 커밋) |
| `Assets/Cards/rock_art.png`, `paper_art.png`, `scissors_art.png` | AI 이미지 생성 | **사용한 서비스가 기록되지 않음** — 아래 참고 | 해당 서비스 약관에 따름 (미확인) | 세 장을 416 × 661로 통일. 가위만 426 × 682에서 리샘플, 나머지 둘은 받은 그대로 |

카드 아트 세 장은 AI로 생성한 것이다. **어느 서비스로 생성했는지가 기록되지 않았다** — 상업적
이용 가능 여부와 산출물의 권리 귀속이 서비스마다 다르므로, 배포를 고려하는 시점에는 이 칸이
채워져 있어야 한다. 지금 채우는 편이 나중보다 쉽다.

이 세 장 이전에는 `Assets/Cards/CardSprite.png` 한 장에 세 카드가 나란히 그려진 시트를
`AtlasTexture`로 잘라 썼다. 그 시트는 출처가 기록된 적이 없고, 지금은 쓰이지 않아
`Assets/_Source/`로 물러났다(저장소에는 포함되지 않는다 — `.gitignore` 참고).

**이 팩은 현재 어디에서도 쓰이지 않는다.** 한때 타이틀 화면 버튼 다섯 개가
`Tilesheets/Large tiles/Thick outline/tilemap_packed.png`의 브라운 타일 한 장
(`Rect2(32, 0, 32, 32)`)을 `StyleBoxTexture`로 9-slice 해서 썼는데, 메뉴가 상자 없는
좌측 정렬 텍스트 목록([Assets/Themes/TextMenu.tres](Assets/Themes/TextMenu.tres))으로
바뀌면서 그 버튼들이 사라졌다. 팩은 지우지 않고 그대로 뒀다 — CC0라 저장소에 두는 데
아무 제약이 없고, UI 요소가 다시 필요해질 때 받아올 곳을 다시 찾지 않아도 된다.
표기 의무도 없다(License.txt에도 "not a requirement"라고 명시).

## 3D / 캐릭터

| 파일 | 제작자 | 출처 | 라이선스 | 수정 |
|---|---|---|---|---|
| `Assets/Models/MainCharacter.glb` (+ 딸린 `MainCharacter_Ch28_1001_*.png`) | Adobe (Mixamo) | [Mixamo](https://www.mixamo.com/) 캐릭터 `Ch28` | Mixamo 라이선스 — CC0 아님. **재배포 금지, 게임에 포함하는 것은 무료·무제한** | 앉은 자세로 리깅 조정, 애니메이션 자작 3종 추가, `.glb`로 재익스포트 |
| `Assets/Models/cc0_scissors.glb` (+ 임포트가 추출한 `cc0_scissors_{0,1,2}.png`) | plaggy | [plaggy.net](https://plaggy.net/) — Fab/Sketchfab 경유로 받은 `.fbx`를 `.glb`로 변환한 것 (파일 안에 `fab-model-conversion` 표시가 남아 있다) | CC0 1.0 Universal — 퍼블릭 도메인, **표기 의무 없음** | 없음 (받은 그대로). 텍스처 3장은 Godot 임포트가 `.glb`에서 꺼내 놓은 것 |
| `Assets/Models/ybot_main.glb` | Adobe (Mixamo) | [Mixamo](https://www.mixamo.com/) 캐릭터 `Y Bot` | Mixamo 라이선스 — 위 `Ch28`과 동일 조건 | 본 접두사를 `mixamorig10:`으로 일괄 변경, 아마추어 오브젝트를 `Armature`로 개명, `.glb`로 재익스포트. **현재 씬에서 쓰이지 않는다** |
| `Assets/Textures/dark_wood_2k/` (전체) | Poly Haven | ["Dark Wood"](https://polyhaven.com/a/dark_wood) | CC0 1.0 — **표기 의무 없음** | **`dark_wood_diff_2k.png`를 무채색으로 변환** (아래 참고). 나머지 맵은 원본 그대로 |

**재배포 금지가 무슨 뜻이냐면** — 완성된 게임에 넣어 파는 것은 허용되지만, 이 파일 자체를
에셋팩처럼 따로 배포하는 것은 안 된다. 소스 저장소가 공개로 바뀌면 이 `.glb`가 사실상
재배포에 해당할 수 있으니 그때 다시 확인할 것.

**탁상 텍스처를 무채색으로 구운 이유** — 당시 화면 전체에
[MonochromeExceptRed.gdshader](Shaders/MonochromeExceptRed.gdshader)가 걸려 빨강만 남기고
채도를 걷어냈는데, 다크우드는 색상환에서 빨강 바로 옆(주황)이라 그 필터를 통과해 혼자
갈색으로 남았다. 임계값을 조여서 막으려 하면 어두운 피까지 같이 걸린다. 탁상의 갈색은
정보가 아니므로 알베도를 아예 무채색으로 구워두는 편이 나았다 — 셰이더는 피에만 쓰고,
정적 에셋의 색은 처음부터 없앤다.

**그 셰이더는 이제 매치 화면에 없다** (타이틀 화면에는 남아 있다). 구운 텍스처는 되돌리지
않았으므로 매치 화면의 탁상은 필터와 무관하게 회색으로 남는다 — 나무 색을 되살리고 싶으면
위 URL에서 CC0 원본을 다시 받아 `dark_wood_diff_2k.png`를 갈아 끼우면 되고, 그때 이 칸의
"수정" 내용도 같이 지워야 한다.

애니메이션(`Anim_Punch_Baked`, `Anim_StabScissor_Baked`, `Anim_NoNoNoFinger`,
`Anim_Paper_Flip_Baked`)은 Mixamo 프리셋이 아니라 **Blender에서 직접 만든 자작**이다.
이들이 담긴 `Assets/Models/main_amature_nomesh.glb`와 `Assets/Models/Paper_animation_only.glb`는
메쉬 없이 뼈대와 애니메이션만 들고 있지만, 그 뼈대 자체는 위 `Ch28`의 것이므로 **같은
Mixamo 라이선스가 걸린다** — 애니메이션이 자작이라고 해서 재배포 금지가 풀리지는 않는다.
작업 파일은 `Assets/_Source/`에 있고
`.gitignore`로 저장소에서 빠져 있다 — 그 폴더의 `.gdignore`는 Godot도 그 안을 임포트하지
않게 하려고 둔 것이다.

## 폰트

| 파일 | 제작자 | 출처 | 라이선스 | 수정 |
|---|---|---|---|---|
| `Assets/Fonts/drybrush/` (전체) | 1001fonts 배포 | ["Drybrush"](https://www.1001fonts.com/drybrush-font.html#license) | **퍼블릭 도메인 / CC0 1.0** — 상업 이용·수정·재배포 전부 허용, **표기 의무 없음** | 없음 (받은 그대로 커밋) |
| `Assets/Fonts/MalgunGothic.ttf` | Microsoft (추정) | **기록되지 않음** — 아래 참고 | **미확인** | 없음 |

Drybrush가 게임 전체의 기본 폰트다 —
[Assets/Fonts/DefaultTheme.tres](Assets/Fonts/DefaultTheme.tres)의 `default_font`이고,
`project.godot`의 `gui/theme/custom`이 그 테마를 가리키므로 따로 지정하지 않은 모든
`Control`이 이걸 쓴다. 받은 패키지에 `.otf`와 `.ttf`가 같이 들어 있고 **쓰이는 것은
`.ttf`뿐이다**; `.otf`는 같은 서체의 다른 포맷이라 지우지 않고 뒀다. 동봉된
`License.txt`(CC0 전문)와 `1001fonts-drybrush-eula.txt`도 원본 그대로 함께 커밋했다.

**맑은고딕은 지우지 않고 대체 폰트(fallback)로 남겼다.** Drybrush에는 한글 글리프가 없어서,
그냥 갈아치웠으면 `strings.csv`의 `ko` 열이 영영 못 그려지는 열이 된다 — 언어 옵션을 나중에
붙일 수 있게 두기로 한 결정([Scripts/CLAUDE.md](Scripts/CLAUDE.md))이 그때 무효가 된다.
`FontVariation.fallbacks`에 한 줄 얹는 것으로 영어는 Drybrush가, Drybrush에 없는 글자는
맑은고딕이 그린다.

**그래서 맑은고딕의 출처·라이선스는 여전히 확인해야 한다.** 파일명으로 보아 Windows 동봉
폰트로 보이는데, 그렇다면 **재배포 조건이 위 항목들보다 까다로울 가능성이 높다** — 게임에
폰트 파일을 실어 배포하는 것이 허용되는지가 쟁점이다. 지금은 화면에 거의 안 나오는(영어
고정) 대체 폰트일 뿐이라 급하지 않지만, 배포를 준비할 때는 확인하거나 한글용 오픈 폰트로
교체해야 한다.

## 라이선스별로 실제로 해야 하는 것

**CC BY 계열**은 크레딧 표기가 **의무**다. 표기를 빼면 라이선스 위반이라 그냥 무단 사용이 된다.
아래 "배포용 표기 문구"에 원문 그대로 쓸 문장을 만들어 둔다.

**CC BY는 수정 사실도 밝히도록 요구한다.** 그런데 게임에 넣는 소리는 거의 항상 수정된다 —
길이 자르기, 볼륨 정규화, 피치 조정([IDEAS.md](IDEAS.md) §5의 피치 랜덤화 포함). "수정" 칸을
`잘라냄 / 볼륨 조정` 처럼 구체적으로 적어두고, 표기 문구에도 반영한다.

**CC0 / 퍼블릭 도메인**은 표기 의무가 없다. 그래도 목록에는 남긴다 — 의무가 없다는 사실 자체가
기록되어 있어야 나중에 다시 확인하지 않는다.

**상용 에셋 팩**은 대개 재배포를 금지한다. 소스 저장소가 공개라면 파일 자체를 커밋해도 되는지
라이선스를 먼저 읽을 것.

## 배포용 표기 문구

빌드에 실어야 하는 문장을 원문 그대로 모아두는 자리. 전부 CC BY라 **표기가 의무다.**
(UI 팩은 CC0라 여기 없다 — 위 참고.)

```
"Level Up 01" by rhodesmas -- https://freesound.org/s/320655/
-- License: Attribution 4.0

8-bit "failure" sound by AceOfSpadesProduc100 -- https://freesound.org/s/333785/
-- License: Attribution 4.0

Evil laughter joker.wav by bulbastre -- https://freesound.org/s/103987/
-- License: Attribution 4.0

Tavern Brawl by Alexander Nakarada ft. Kevin MacLeod | https://creatorchords.com
Music promoted by https://www.chosic.com/free-music/all/
Creative Commons CC BY 4.0
https://creativecommons.org/licenses/by/4.0/
```

> 아직 게임 안에 크레딧을 보여줄 화면이 없다. 표기 의무가 있는 소리를 처음 넣는 시점에
> 이 문구가 갈 곳(타이틀의 크레딧 화면 등)도 같이 정해야 한다. 파일에만 적어두고 빌드에
> 안 실으면 표기를 안 한 것과 같다.

## 아직 정리 안 된 것

두 가지가 남아 있고, 둘 다 **출처가 기록되지 않아** 배포 전에 확인이 필요하다.

- `Assets/Fonts/MalgunGothic.ttf` — 위 "폰트" 참고.
- `Assets/Audio/ButtonPressed.mp3` — 위 "사운드" 참고.
- `Assets/Cards/*_art.png` — 위 "UI / 이미지" 참고 (어느 AI 서비스로 생성했는지 미기록).
