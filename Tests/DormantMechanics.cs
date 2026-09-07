namespace RockAndScissPaper.Tests;

/// <summary>Why a test is not being run, for the rules that are still implemented but are
/// currently out of play.
///
/// DeckAssembler builds a deck of 가위/바위/보 and nothing else, so 공백카드, 조커 and the four
/// 능력카드 cannot be drawn — and with them go the round outcomes only they produce: a round with
/// no win or loss, a card that 소멸s instead of returning to the deck bottom, and the whole
/// AwaitingChoices phase, which only 교체 and 변화 ever open. The code for all of it still
/// compiles and is still correct; there is simply no way to reach it from a match right now.
///
/// The tests stay in the project rather than moving to Deprecated/, because these cards are
/// coming back as items (DESIGN.md) and the assertions are what will say whether the rules
/// survived the wait. Compiling them keeps them honest against API drift; running them would
/// only report on a mechanic the game does not currently have.
///
/// To run them all again, set REASON to null — xUnit treats a null Skip as "not skipped", so
/// one edit here re-enables every test that names it.</summary>
internal static class DormantMechanics
{
    public const string REASON =
        "현재 덱에 없는 메커니즘 (공백/조커/능력카드) — 아이템 시스템 도입 시 복귀 예정";
}
