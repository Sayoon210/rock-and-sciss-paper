# C# / Godot 프로젝트 기초 — 2026-08-16 정리

Godot C# 프로젝트를 세팅하면서 공부한 개념들. 개념 → 왜 그런지 → 예시 순.

---

## 1. `static` — 객체 없이 불리는 멤버

C# 클래스의 멤버는 두 종류다.

```csharp
public class NetworkManager
{
    public int PeerCount;                     // 인스턴스 멤버 — 객체마다 하나씩
    public static NetworkManager Instance;    // static 멤버 — 클래스에 통째로 하나
}
```

| | 어디에 붙나 | 어떻게 접근 |
|---|---|---|
| 인스턴스 멤버 | 객체 하나하나 | `someManager.PeerCount` |
| `static` 멤버 | **클래스 자체** | `NetworkManager.Instance` |

`static` 멤버는 객체가 없어도 클래스 이름만으로 접근된다. 프로그램 전체에 딱 하나뿐이고, 어디서 읽든 같은 저장 공간이다.

**"그냥 호출되는" 이유**: 컴파일러가 클래스 이름으로 바로 찾아가기 때문. 객체를 어디서 구해올지 알 필요가 없다.

---

## 2. Godot Autoload과 `Instance` 패턴

### Autoload이란

`project.godot`에 등록하면 **게임 시작 시 자동으로 생성되어 게임이 끝날 때까지 살아있는 노드**. 씬이 바뀌어도 안 죽는다. `/root/이름` 경로로 어디서든 접근 가능.

### 기본 접근법의 문제

```csharp
var net = GetNode<NetworkManager>("/root/NetworkManager");
net.StartHost();
```

- `GetNode`는 **`Node`의 메서드**다. 호출하는 쪽도 `Node`를 상속해야 쓸 수 있다. 순수 C# 클래스에선 못 쓴다.
- `"/root/NetworkManager"`는 컴파일러 입장에선 그냥 글자. `NetwrokManager`라고 오타 내도 **빌드는 통과하고 실행 중에 터진다.**

### static Instance 패턴

```csharp
public partial class NetworkManager : Node
{
    public static NetworkManager Instance { get; private set; }

    public override void _EnterTree() => Instance = this;
}
```

호출부가 이렇게 짧아지고, 오타 내면 **컴파일 에러**로 미리 잡힌다.

```csharp
NetworkManager.Instance.StartHost();
```

### 핵심: Autoload이라 자동으로 되는 게 아니다

`Instance`는 위 두 줄을 직접 써야 생긴다. Autoload이 해주는 건 따로 있다.

```
Autoload → "인스턴스 하나뿐 + 세션 내내 생존" 보장 (Godot이 해줌)
   → 그래서 static 필드에 담아둬도 안전
      → 결과적으로 호출부에서 GetNode 안 해도 됨
```

일반 노드에 이 패턴을 쓰면 위험하다. 같은 노드가 씬 두 곳에 있으면 나중 것이 앞의 것을 덮어쓰고, 씬 전환으로 해제되면 static 필드가 죽은 객체를 붙들고 있게 된다. **Autoload의 보장이 이 패턴을 안전하게 만드는 것.**

`_Ready()`가 아니라 `_EnterTree()`에 넣는 이유: `_EnterTree`가 먼저 실행돼서, 다른 노드의 `_Ready()`에서 이미 `Instance`를 쓸 수 있다.

---

## 3. Godot 타입은 왜 Godot 없이 못 도나

`Node`, `Resource` 같은 Godot의 C# 타입은 **C++ 네이티브 객체를 감싼 껍데기**다. `new`로 만들 때 Godot 네이티브 라이브러리를 호출한다.

→ **Godot 런타임이 없으면 객체 생성 자체가 안 된다.**

`Resource`는 씬 트리가 필요 없어서 가벼워 보이지만, 이 점에선 `Node`와 똑같다. "Godot 없이 콘솔에서 돌린다"는 목표에선 `Resource`도 쓸 수 없다.

**실질적 결과**: 순수 로직에서 카드를 다룰 때 `CardData`(Resource) 대신 `CardKind`(enum) 같은 값 타입을 쓰게 된다. 덱이 "카드 객체 리스트"가 아니라 "카드 식별자 리스트"가 되는 것.

---

## 4. `.csproj` — C# 프로젝트 설정 파일

.NET 빌드 시스템에게 "이 프로젝트를 어떻게 빌드해라"를 알려주는 XML 파일. 담기는 것: 컴파일할 파일, 참조할 라이브러리, .NET 버전, 출력물 종류.

요즘 형식(SDK-style)은 짧다.

```xml
<Project Sdk="Godot.NET.Sdk/4.7.0">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>
```

`Sdk=` 부분이 나머지를 다 처리한다 — 폴더 안 `.cs` 파일을 전부 자동으로 긁어오는 것도 포함.

### 왜 처음 보나

- **GDScript만 썼으면** — C#을 안 켜서 생성 자체가 안 됨
- **Unity를 썼으면** — Unity도 만들지만 숨겨서 자동 관리함. Unity에서 어셈블리를 나누는 `.asmdef`가 이것의 Unity판

Godot C#은 숨기지 않고 표준 .NET 방식을 그대로 노출한다. 첫 C# 스크립트를 만들거나 빌드하면 `.csproj`와 `.sln`(프로젝트 묶음 목록)이 프로젝트 루트에 생긴다.

---

## 5. 프로젝트 참조 — 컴파일러로 경계 만들기

.NET 프로젝트는 **자기가 참조한 것만 볼 수 있다.** 이 성질로 "이 코드는 저 라이브러리를 쓸 수 없다"를 강제할 수 있다.

```
  Tests ─────→ Core ←───── Godot 게임 프로젝트
                                   │
                                   └──→ GodotSharp
```

`Core`는 화살표를 받기만 하고 **아무것도 참조하지 않는다.** Godot 프로젝트의 존재조차 모른다.

그래서 Core 안에 이렇게 쓰면:

```csharp
using Godot;                      // ← 이 네임스페이스가 참조 목록에 없음
private List<CardData> _deck;     // ← 이 타입도 없음
```

```
error CS0246: The type or namespace name 'Godot' could not be found
```

**린터 경고가 아니라 빌드 실패다.** "쓰지 말자"는 약속이 아니라, 쓸 수 있는 경로 자체가 없다.

설정은 이렇게 생겼다.

```xml
<!-- Godot 프로젝트 쪽 -->
<ProjectReference Include="Core/MyGame.Core.csproj" />
<Compile Remove="Core/**/*.cs" />          <!-- Core 폴더는 내가 컴파일 안 함 -->
```

```xml
<!-- Core 쪽 -->
<Project Sdk="Microsoft.NET.Sdk">          <!-- Godot.NET.Sdk가 아닌 순수 .NET -->
  <!-- 참조 없음 -->
</Project>
```

---

## 6. 테스트 프레임워크와 xUnit

### 테스트 프레임워크란

**"내 코드가 기대한 답을 내는지 확인하는 함수들을 쓰고, 전부 자동으로 돌려주는 도구."** 세 가지를 해준다.

1. 테스트 함수를 표시하는 규약 (`[Fact]` 같은 속성)
2. 표시된 함수를 전부 찾아서 실행하는 러너
3. 결과 보고 — 통과/실패 개수, 실패 시 기대값과 실제값

없어도 `Main()`에서 함수 부르고 `Console.WriteLine` 찍으면 같은 일을 할 수 있다. 프레임워크는 **찾기·돌리기·보고하기**를 대신해주는 것. 테스트가 수십 개로 늘어나도 명령어 하나로 다 돌아가는 게 차이다.

### xUnit

이름이 두 의미로 쓰인다.
- 일반명사: 단위 테스트 프레임워크 계열 전체 (JUnit 등이 같은 계보)
- 고유명사: .NET용 라이브러리 **xUnit.net** ← .NET 맥락에선 보통 이쪽

**`[Fact]`** — 인자 없는 테스트 하나

```csharp
[Fact]
public void 조커끼리_만나면_둘_다_소멸()
{
    var r = RoundResolver.Resolve(CardKind.Joker, CardKind.Joker);
    Assert.Equal(CardFate.Vanished, r.P1Fate);
    Assert.Equal(CardFate.Vanished, r.P2Fate);
}
```

**`[Theory]` + `[InlineData]`** — 같은 테스트를 입력만 바꿔 여러 번. 조합 매트릭스에 강하다.

```csharp
[Theory]
[InlineData(CardKind.Rock,     CardKind.Scissors, Outcome.P1Win)]
[InlineData(CardKind.Scissors, CardKind.Paper,    Outcome.P1Win)]
[InlineData(CardKind.Rock,     CardKind.Rock,     Outcome.Draw)]
public void 상성_판정(CardKind p1, CardKind p2, Outcome expected)
{
    Assert.Equal(expected, RoundResolver.Resolve(p1, p2).Outcome);
}
```

함수 하나로 3개의 독립된 테스트가 돌아간다.

**`Assert.*`** — 검사 도구: `Equal`, `NotEqual`, `True`, `Null`, `Throws`(예외 발생 확인), `Contains` 등

**실행**: 터미널에서 `dotnet test`

```
Passed!  - Failed: 0, Passed: 2
```

실패하면 뭘 기대했고 뭐가 나왔는지 알려준다.

```
Failed 조커는_상대_리셋을_소멸시킨다
  Expected: Vanished
  Actual:   ReturnedToDeck
```

### 왜 xUnit인가 (NUnit / MSTest 대신)

- 새 .NET 프로젝트의 사실상 기본값 — 자료가 가장 많음
- `[SetUp]`/`[TearDown]` 같은 전용 속성 없이 생성자와 `IDisposable`을 그대로 씀 — 배울 게 적음
- `[Theory]`가 깔끔함

작은 프로젝트에선 셋 중 뭘 골라도 실질 차이는 거의 없다.

---

## 한 줄 요약

- `static`은 클래스에 붙는 멤버라 객체 없이 접근된다
- Autoload은 "하나뿐 + 안 죽음"을 보장하고, 그 보장이 static Instance 패턴을 안전하게 만든다
- Godot 타입은 네이티브 래퍼라 Godot 없이는 생성조차 안 된다
- `.csproj`는 빌드 설정 파일이고, **프로젝트 참조 방향이 곧 컴파일 경계**다
- 테스트 프레임워크는 검증을 "몇 분"에서 "0.1초"로 줄여준다
