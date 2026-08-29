using System.Text;
using Godot;
using RockAndScissPaper.Autoload;
using RockAndScissPaper.Cards;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Match3D;

/// <summary>Renders GameState.Log — every round's cards, verdict, damage and resulting health,
/// then who took the match. Toggled with Tab, hidden to start, because it is a thing to check
/// rather than a thing to watch.
///
/// Rebuilt whole on each round rather than appended to. The log is short (a match is a
/// handful of rounds), and rebuilding means the panel cannot drift out of step with the record
/// it is showing — there is exactly one path from log to text.
///
/// Card names come through CardDatabase, so the panel prints 바위 rather than Rock: ECardName
/// is identity, and the readable name lives in Data/Cards/*.tres like every other card string
/// (Scripts/CLAUDE.md's "no sentences in source" — every line here is composed from symbols).</summary>
public partial class MatchLogPanel : Control
{
	private const string TITLE_LABEL_PATH = "Panel/Layout/TitleLabel";
	private const string ENTRIES_LABEL_PATH = "Panel/Layout/Scroll/EntriesLabel";

	private Label _titleLabel = null!;
	private Label _entriesLabel = null!;

	public override void _Ready()
	{
		_titleLabel = GetNode<Label>(TITLE_LABEL_PATH);
		_entriesLabel = GetNode<Label>(ENTRIES_LABEL_PATH);
		_titleLabel.Text = Tr("MATCH_LOG_TITLE");

		Visible = false;

		GameState.Instance!.MatchStarted += Refresh;
		GameState.Instance.RoundResolved += Refresh;

		Refresh();
	}

	/// <summary>A freed node still connected to a session-lifetime Autoload signal is a
	/// crash waiting for the next emit (Scripts/Autoload/CLAUDE.md).</summary>
	public override void _ExitTree()
	{
		if (GameState.Instance != null)
		{
			GameState.Instance.MatchStarted -= Refresh;
			GameState.Instance.RoundResolved -= Refresh;
		}
	}

	/// <summary>Tab toggles. _UnhandledInput rather than _Input so a focused Control still
	/// gets first refusal, matching how HeadFollowCamera reads Escape and Space.</summary>
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Tab)
		{
			Visible = !Visible;
			GetViewport().SetInputAsHandled();
		}
	}

	private void Refresh()
	{
		MatchLog log = GameState.Instance!.Log;

		if (log.Entries.Count == 0)
		{
			_entriesLabel.Text = Tr("MATCH_LOG_EMPTY");
			return;
		}

		StringBuilder text = new StringBuilder();
		foreach (MatchLogEntry entry in log.Entries)
		{
			text.AppendLine(string.Format(
				Tr("MATCH_LOG_ROUND_LINE"),
				entry.RoundNumber,
				DescribeCard(entry.MyCard),
				DescribeCard(entry.OpponentCard),
				DescribeOutcome(entry)));
			text.AppendLine(string.Format(
				Tr("MATCH_LOG_HEALTH_LINE"), entry.MyHealthAfter, entry.OpponentHealthAfter));
		}

		if (log.DidIWin.HasValue)
		{
			text.AppendLine();
			text.AppendLine(log.DidIWin.Value ? Tr("MATCH_LOG_MATCH_WON") : Tr("MATCH_LOG_MATCH_LOST"));
		}

		_entriesLabel.Text = text.ToString();
	}

	/// <summary>TranslationServer.Translate rather than Tr — Tr is an instance method and
	/// this is static, which is exactly the case Scripts/CLAUDE.md points at it for.</summary>
	private static string DescribeOutcome(MatchLogEntry entry)
	{
		switch (entry.Outcome)
		{
			case EMatchLogOutcome.MyWin:
				return string.Format(
					TranslationServer.Translate("MATCH_LOG_MY_WIN"), entry.OpponentDamageTaken);

			case EMatchLogOutcome.OpponentWin:
				return string.Format(
					TranslationServer.Translate("MATCH_LOG_OPPONENT_WIN"), entry.MyDamageTaken);

			case EMatchLogOutcome.Draw:
				return TranslationServer.Translate("MATCH_LOG_DRAW");

			default:
				return TranslationServer.Translate("MATCH_LOG_NO_CONTEST");
		}
	}

	/// <summary>"?" for a card this screen was never told — a round logged before its reveal.
	/// Not a symbol, because it is punctuation standing in for a missing name rather than a
	/// sentence anyone reads.</summary>
	private static string DescribeCard(ECardName? cardName)
	{
		if (cardName == null)
		{
			return "?";
		}

		CardData? cardData = CardDatabase.Instance?.GetCardData(cardName.Value);
		if (cardData == null)
		{
			return cardName.Value.ToString();
		}

		return TranslationServer.Translate(cardData.DisplayName);
	}
}
