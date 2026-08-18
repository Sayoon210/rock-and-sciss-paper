using System.Collections.Generic;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Autoload;

/// <summary>Which side of the last resolved round I came out on, from my own perspective
/// rather than Player1/Player2 — View is "me/opponent" shaped throughout, so the UI should
/// never have to compare a Side against GameState's own bookkeeping to know if it won.</summary>
public enum RoundOutcome
{
    MyWin,
    OpponentWin,
    Draw,
}

/// <summary>Who won the match, from my own perspective. Same reasoning as RoundOutcome.</summary>
public enum MatchOutcome
{
    IWon,
    OpponentWon,
}

/// <summary>GameState's read model for the current match. Shaped "me/opponent" rather than
/// "Player1/Player2" so the same property names and the same UI code work unmodified on
/// both host and client — see Scripts/CLAUDE.md's "Reading match state". GameState fills
/// this two different ways (host: in-process from MatchSession, client: from RPC payloads)
/// but the shape here is identical either way.</summary>
public sealed class MatchView
{
    public IReadOnlyList<CardName> MyHand { get; set; } = new List<CardName>();
    public int OpponentHandCount { get; set; }

    public int MyDeckCount { get; set; }
    public int OpponentDeckCount { get; set; }

    // This round's revealed cards and fates. Null until the first round resolves.
    public CardName? MyCard { get; set; }
    public CardName? OpponentCard { get; set; }
    public CardFate? MyCardFate { get; set; }
    public CardFate? OpponentCardFate { get; set; }

    public RoundOutcome? LastRoundOutcome { get; set; }

    // The card I still have to choose for this round (교체 or 변화), or null when I owe no
    // choice. Set from the targeted prompt, so it is never populated on the wrong screen.
    public CardName? CardIMustChooseFor { get; set; }

    /// <summary>Whether the opponent is still picking. Public information — which card they
    /// played is already revealed, and the rules say plainly which cards need a choice.</summary>
    public bool OpponentIsChoosing { get; set; }

    // What each side's choice did, for animating it. Counts and flags only: 교체 and 변화
    // both leave hand size unchanged, so neither is derivable from the counts above, and
    // neither of these says which cards were involved.
    public int MySwappedCardCount { get; set; }
    public int OpponentSwappedCardCount { get; set; }
    public bool MyTransformApplied { get; set; }
    public bool OpponentTransformApplied { get; set; }

    public int MyScore { get; set; }
    public int OpponentScore { get; set; }
    public int RoundNumber { get; set; }

    public MatchOutcome? MatchResult { get; set; }
}
