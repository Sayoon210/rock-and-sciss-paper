using System.Collections.Generic;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Autoload;

/// <summary>Which side of the last resolved round I came out on, from my own perspective
/// rather than Player1/Player2 — View is "me/opponent" shaped throughout, so the UI should
/// never have to compare a ESide against GameState's own bookkeeping to know if it won.</summary>
public enum ERoundOutcome
{
    MyWin,
    OpponentWin,
    Draw,
}

/// <summary>Who won the match, from my own perspective. Same reasoning as ERoundOutcome.</summary>
public enum EMatchOutcome
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
    public IReadOnlyList<ECardName> MyHand { get; set; } = new List<ECardName>();
    public int OpponentHandCount { get; set; }

    public int MyDeckCount { get; set; }
    public int OpponentDeckCount { get; set; }

    // This round's revealed cards and fates. Null until the first round resolves.
    public ECardName? MyCard { get; set; }
    public ECardName? OpponentCard { get; set; }
    public ECardFate? MyCardFate { get; set; }
    public ECardFate? OpponentCardFate { get; set; }

    public ERoundOutcome? LastRoundOutcome { get; set; }

    /// <summary>Whether this round is still taking cards. Kept here rather than derived on
    /// screen from "no card revealed yet", because both sides need to agree on when the
    /// submission clock is running and both get told the same thing at the same points — the
    /// round opening, and the reveal that closes it.</summary>
    public bool SubmissionPhaseActive { get; set; }

    // The card I still have to choose for this round (교체 or 변화), or null when I owe no
    // choice. Set from the targeted prompt, so it is never populated on the wrong screen.
    public ECardName? CardIMustChooseFor { get; set; }

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

    /// <summary>Whether 리셋 replaced both 패 this round. Not "a 리셋 was played": a 조커 in
    /// the round blocks it and nothing changes. The screen animates off this rather than off
    /// the played cards, so it cannot get the rule wrong on its own.</summary>
    public bool ResetApplied { get; set; }

    public int MyScore { get; set; }
    public int OpponentScore { get; set; }
    public int RoundNumber { get; set; }

    public EMatchOutcome? MatchResult { get; set; }
}
