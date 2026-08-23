# 사운드 출처

`Assets/Audio/` 에 들어간 외부 사운드의 출처와 라이선스 기록.

**파일을 커밋할 때 같이 채운다.** 나중에 몰아서 하면 어느 파일이 어디서 왔는지 이미 모른다 —
받아온 직후가 아니면 복원할 수 없는 정보다. 직접 만들거나 녹음한 소리도 "자작"으로 한 줄
남긴다. 비워두면 출처를 못 찾은 건지 자작인지 구분이 안 된다.

## 목록

| 파일 | `ESoundName` | 제작자 | 출처 | 라이선스 | 수정 |
|---|---|---|---|---|---|
| `RoundWon.wav` | `RoundWon` | rhodesmas | ["Level Up 01"](https://freesound.org/s/320655/) | CC BY 4.0 | 파일명만 변경 |
| `RoundLost.wav` | `RoundLost` | AceOfSpadesProduc100 | ["8-bit "failure" sound"](https://freesound.org/s/333785/) | CC BY 4.0 | 파일명만 변경 |
| `Joker.wav` | `Joker` | bulbastre | ["Evil laughter joker"](https://freesound.org/s/103987/) | CC BY 4.0 | 파일명만 변경 |

`RoundWon.wav`는 한때 같은 소리의 [mp3 재업로드본(337049)](https://freesound.org/s/337049/)을
쓰다가 rhodesmas의 원본으로 교체한 것이다. 재업로더가 아니라 **원작자를 표기하는 게 맞고**,
원본은 CC BY 4.0이라 재업로드본(3.0)과 버전도 다르다.

- **파일** — `Assets/Audio/` 기준 경로. 확장자까지.
- **`ESoundName`** — [Scripts/Autoload/ESoundName.cs](Scripts/Autoload/ESoundName.cs)의 대응 멤버.
  BGM처럼 enum에 없는 것은 `-`.
- **출처** — 받은 페이지 URL. 사이트 이름만 적으면 나중에 그 파일을 다시 못 찾는다.
- **라이선스** — `CC0`, `CC BY 4.0`, `CC BY-SA 4.0`, 상용 라이선스명, `자작` 등.
- **수정** — 자른 것도, 볼륨만 맞춘 것도 수정이다. 아래 참고.

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

빌드에 실어야 하는 문장을 원문 그대로 모아두는 자리. 둘 다 CC BY라 **표기가 의무다.**

```
"Level Up 01" by rhodesmas -- https://freesound.org/s/320655/
-- License: Attribution 4.0

8-bit "failure" sound by AceOfSpadesProduc100 -- https://freesound.org/s/333785/
-- License: Attribution 4.0

Evil laughter joker.wav by bulbastre -- https://freesound.org/s/103987/
-- License: Attribution 4.0
```

> 아직 게임 안에 크레딧을 보여줄 화면이 없다. 표기 의무가 있는 소리를 처음 넣는 시점에
> 이 문구가 갈 곳(타이틀의 크레딧 화면 등)도 같이 정해야 한다. 파일에만 적어두고 빌드에
> 안 실으면 표기를 안 한 것과 같다.

## 사운드가 아닌 것

이 문서는 사운드만 다룬다. 다만 `Assets/Fonts/MalgunGothic.ttf`도 외부 파일이고 재배포
조건이 사운드보다 까다로울 수 있으니, 배포를 준비할 때 같이 확인할 것.
