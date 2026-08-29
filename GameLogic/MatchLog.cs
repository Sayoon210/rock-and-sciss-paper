using System;
using System.Collections.Generic;

namespace RockAndScissPaper.GameLogic;

/// <summary>How one logged round came out, from the log owner's own side.</summary>
public enum EMatchLogOutcome
{
    MyWin,
    OpponentWin,
    Draw,

    /// <summary>Neither side won and it was not a draw either — a round containing a blank,
    /// a 조커 or an ability card produces no verdict at all. Not currently reachable with
    /// the deck DeckAssembler builds, but the distinction is real in the rules and collapsing
    /// it into Draw would log those rounds as something they are not.</summary>
    NoContest,
}

/// <summary>One finished round as a record. Health is what each side had AFTER the round;
/// the damage figures are what that round took off, which is why this is built through
/// MatchLog.RecordRound rather than constructed directly — the damage is derived there from
/// the health the previous entry left behind.</summary>
public sealed class MatchLogEntry
{
    public int RoundNumber { get; }
    public ECardName? MyCard { get; }
    public ECardName? OpponentCard { get; }
    public EMatchLogOutcome Outcome { get; }
    public int MyDamageTaken { get; }
    public int OpponentDamageTaken { get; }
    public int MyHealthAfter { get; }
    public int OpponentHealthAfter { get; }

    public MatchLogEntry(
        int roundNumber,
        ECardName? myCard,
        ECardName? opponentCard,
        EMatchLogOutcome outcome,
        int myDamageTaken,
        int opponentDamageTaken,
        int myHealthAfter,
        int opponentHealthAfter)
    {
        RoundNumber = roundNumber;
        MyCard = myCard;
        OpponentCard = opponentCard;
        Outcome = outcome;
        MyDamageTaken = myDamageTaken;
        OpponentDamageTaken = opponentDamageTaken;
        MyHealthAfter = myHealthAfter;
        OpponentHealthAfter = opponentHealthAfter;
    }
}

/// <summary>A running record of one match — what each side played, what it cost them, and
/// who took the match in the end.
///
/// Shaped "me/opponent" rather than Player1/Player2, the same way MatchView is and for the
/// same reason: it is read by one screen, which only ever cares about its own side. That
/// also makes it fillable identically on the host and on a client, both of which see the
/// public part of every round. It records; it decides nothing.
///
/// It lives in GameLogic despite being a viewer's record rather than a rule, because it has
/// no Godot types and Tests/ references this project alone — the damage arithmetic below is
/// exactly the kind of thing worth having covered by a test rather than by playing a match.
///
/// Damage is derived, not passed in. Only a round's outcome moves health, so the drop from
/// the previous entry's health IS that round's damage; taking it as a parameter would mean
/// two sources for one number, which could then disagree.
///
/// No item column yet. DESIGN.md's item system does not exist in code — there is no type to
/// record — so recording it is deferred rather than stubbed. It becomes another field here
/// and another argument to RecordRound when there is something real to put in it.</summary>
public sealed class MatchLog
{
    private readonly List<MatchLogEntry> _entries = new List<MatchLogEntry>();
    private int _myLastHealth;
    private int _opponentLastHealth;

    /// <summary>Both sides' health before any round — MatchSession.STARTING_HEALTH unless a
    /// test wants otherwise. The first entry's damage is measured against this.</summary>
    public MatchLog(int startingHealth = MatchSession.STARTING_HEALTH)
    {
        _myLastHealth = startingHealth;
        _opponentLastHealth = startingHealth;
    }

    public IReadOnlyList<MatchLogEntry> Entries
    {
        get { return _entries; }
    }

    /// <summary>Who took the match, or null while it is still running.</summary>
    public bool? DidIWin { get; private set; }

    /// <summary>Appends one resolved round. The cards are nullable because a round can be
    /// logged before either card is known to this screen — a defensive shape rather than an
    /// expected one, since the reveal always precedes the resolution that calls this.</summary>
    public void RecordRound(
        int roundNumber,
        ECardName? myCard,
        ECardName? opponentCard,
        EMatchLogOutcome outcome,
        int myHealthAfter,
        int opponentHealthAfter)
    {
        // Clamped at zero: health only ever falls, but a caller passing a stale or
        // out-of-order reading should show as no damage rather than as healing.
        int myDamageTaken = Math.Max(0, _myLastHealth - myHealthAfter);
        int opponentDamageTaken = Math.Max(0, _opponentLastHealth - opponentHealthAfter);

        _entries.Add(new MatchLogEntry(
            roundNumber,
            myCard,
            opponentCard,
            outcome,
            myDamageTaken,
            opponentDamageTaken,
            myHealthAfter,
            opponentHealthAfter));

        _myLastHealth = myHealthAfter;
        _opponentLastHealth = opponentHealthAfter;
    }

    public void RecordMatchEnd(bool didIWin)
    {
        DidIWin = didIWin;
    }

    /// <summary>Clears the record for a rematch. GameState reuses one log across matches the
    /// same way it reuses one MatchView, so the reset has to be explicit — leftover rounds
    /// from a finished match silently appearing in the next one is the failure mode this
    /// exists to prevent (Scripts/Autoload/CLAUDE.md's "Lifecycle & reset").</summary>
    public void Reset(int startingHealth = MatchSession.STARTING_HEALTH)
    {
        _entries.Clear();
        DidIWin = null;
        _myLastHealth = startingHealth;
        _opponentLastHealth = startingHealth;
    }
}
