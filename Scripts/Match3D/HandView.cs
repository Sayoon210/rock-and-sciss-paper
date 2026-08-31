using System;
using System.Collections.Generic;
using Godot;
using RockAndScissPaper.Autoload;
using RockAndScissPaper.Cards;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Match3D;

/// <summary>My hand as real cards standing on CardRest, replacing the DebugHandPreview mockup
/// that stood in the same spots. Reads GameState.View.MyHand and nothing else (Scripts/
/// CLAUDE.md), rebuilding on MyHandChanged with a slot-stability diff — a card name still in
/// the hand keeps its existing node instead of the whole row being torn down.
///
/// Also the owner of the submit gesture's consequences: a card grabbed and released past the
/// arm threshold (CardView's gesture) is removed from the row, flown facedown onto MyCardSlot,
/// and sent to GameState.RequestCardPlay, while the camera is sent home. The submission is
/// OPTIMISTIC — the host never confirms an accepted play, only rejects a bad one
/// (RequestRejected), so the card leaves the hand immediately and comes back if a rejection
/// arrives. View.MyHand itself still contains the played card until the round resolves, which
/// is why rebuilds are driven by MyHandChanged rather than by re-reading the view here.
///
/// Launched standalone (no match — the scene run on its own for visual work), the hand falls
/// back to a random deal of the real mulligan size so the grab-and-submit flow stays testable
/// single-instance; submitting then only logs, and the fallback is replaced wholesale the
/// moment a real MyHandChanged first fires.</summary>
public partial class HandView : Node3D
{
	private const string CARD_VIEW_SCENE_PATH = "res://Scenes/Match3D/CardView.tscn";
	private const string HEAD_CAMERA_PATH = "../../../MySeat/Camera3D";
	private const string MY_CARD_SLOT_PATH = "../../../Table/MyCardSlot";

	// The exact spots the debug mockup hand stood on, kept verbatim — the layout was already
	// tuned against the rest and the hand view camera. Local to this node, whose own transform
	// in MatchWorld.tscn carries the tilt matching the rest's card-holding face.
    private static readonly Vector3 HAND_CENTER = new Vector3(0.0008f, 0.156f, 0.362658692f);
    private const float CARD_SPACING = 0.14f;
    private const float CARD_SCALE = 2f;

    private const float SUBMIT_FLIGHT_SECONDS = 0.3f;
    private const float SUBMIT_FLIGHT_ARC_METERS = 0.08f;
    private const float RELAYOUT_SECONDS = 0.15f;

	/// <summary>The submitted card's flight just ended on MyCardSlot — MatchWorldView shows
	/// its facedown slab at this exact moment, so the swap between the flying hand card and
	/// the table slab is invisible.</summary>
	public event Action? MyCardLanded;

	/// <summary>The host rejected my pending submission — the hand is being restored here;
	/// whoever showed the table slab should take it back too.</summary>
	public event Action? MySubmissionRejected;

	private HeadFollowCamera _headCamera = null!;
	private Node3D _myCardSlot = null!;
	private readonly List<CardView> _handCards = new List<CardView>();
	private CardView? _flightCard;
	private bool _isFallbackHand;
	private bool _isSubmissionPending;

	public override void _Ready()
	{
		_headCamera = GetNode<HeadFollowCamera>(HEAD_CAMERA_PATH);
		_myCardSlot = GetNode<Node3D>(MY_CARD_SLOT_PATH);

		GameState.Instance!.MyHandChanged += OnMyHandChanged;
		GameState.Instance.RoundRevealed += OnRoundRevealed;
		GameState.Instance.RequestRejected += OnRequestRejected;

		if (GameState.Instance.View.MyHand.Count > 0)
		{
			RebuildFromCards(GameState.Instance.View.MyHand);
		}
		else
		{
			SpawnFallbackHand();
		}
	}

	/// <summary>A freed node still connected to a session-lifetime Autoload signal is a
	/// crash waiting for the next emit (Scripts/Autoload/CLAUDE.md).</summary>
	public override void _ExitTree()
	{
		if (GameState.Instance != null)
		{
			GameState.Instance.MyHandChanged -= OnMyHandChanged;
			GameState.Instance.RoundRevealed -= OnRoundRevealed;
			GameState.Instance.RequestRejected -= OnRequestRejected;
		}
	}

	private void OnMyHandChanged()
	{
		// A real hand always wins over the fallback, and it arrives already post-play — the
		// session removed the played card before any hand refresh is sent — so whatever
		// submission was pending is settled by the time this data exists.
		_isFallbackHand = false;
		_isSubmissionPending = false;
		FreeFlightCard();
		RebuildFromCards(GameState.Instance!.View.MyHand);
	}

	private void OnRoundRevealed()
	{
		if (_isSubmissionPending)
		{
			// Mine was accepted — a rejection would have arrived instead of a reveal.
			_isSubmissionPending = false;
			return;
		}

		// I never submitted: the submit timer had the host play a card for me, and the reveal
		// says which (View.MyCard) — it has to leave the displayed hand here too, since no
		// MyHandChanged fires until the round resolves.
		if (GameState.Instance!.View.MyCard is not ECardName autoPlayedCard)
		{
			return;
		}

		for (int i = 0; i < _handCards.Count; i++)
		{
			if (_handCards[i].ShownCard == autoPlayedCard)
			{
				_handCards[i].QueueFree();
				_handCards.RemoveAt(i);
				LayoutHand(null);
				return;
			}
		}
	}

	private void OnRequestRejected(string reason)
	{
		if (!_isSubmissionPending)
		{
			return;
		}

		_isSubmissionPending = false;
		FreeFlightCard();
		MySubmissionRejected?.Invoke();
		RebuildFromCards(GameState.Instance!.View.MyHand);
	}

	/// <summary>No match is running — deal a mulligan the way a real one is dealt: shuffle the
	/// deck DeckAssembler would have built and take the top HAND_SIZE off it.
	///
	/// Dealt from the deck rather than from CardDatabase.LoadedCardNames, which is what this
	/// used to do. The database holds every card that has ever been designed, so the fallback
	/// was handing out 조커, 공백 and ability cards — none of which a real match can deal, since
	/// the deck is normal cards only. Judging the grab and submit feel against cards that cannot
	/// occur is worse than not testing it, and a duplicate is part of what has to feel right
	/// (three copies of each card means a repeated hand is normal, not a bug).</summary>
	private void SpawnFallbackHand()
	{
		_isFallbackHand = true;

		List<ECardName> deck = DeckAssembler.BuildDeck();
		Random random = new Random();
		for (int i = deck.Count - 1; i > 0; i--)
		{
			int swapWith = random.Next(i + 1);
			(deck[i], deck[swapWith]) = (deck[swapWith], deck[i]);
		}

		RebuildFromCards(deck.GetRange(0, Math.Min(MatchSession.HAND_SIZE, deck.Count)));
	}

	/// <summary>Slot-stability diff (Scripts/CLAUDE.md): a card name still present keeps its
	/// existing node instead of the whole row being torn down, so the card the player was
	/// looking at stays the same object; only genuinely gone cards free and genuinely new
	/// ones spawn. Kept nodes glide to their new slots; new ones appear in place.</summary>
	private void RebuildFromCards(IReadOnlyList<ECardName> hand)
	{
		List<CardView> unmatched = new List<CardView>(_handCards);
		List<CardView> nextRow = new List<CardView>();
		HashSet<CardView> spawnedNow = new HashSet<CardView>();

		foreach (ECardName cardName in hand)
		{
			CardView? kept = null;
			foreach (CardView candidate in unmatched)
			{
				if (candidate.ShownCard == cardName)
				{
					kept = candidate;
					break;
				}
			}

			if (kept != null)
			{
				unmatched.Remove(kept);
			}
			else
			{
				kept = SpawnCard(cardName);
				spawnedNow.Add(kept);
			}

			nextRow.Add(kept);
		}

		foreach (CardView leftover in unmatched)
		{
			leftover.QueueFree();
		}

		_handCards.Clear();
		_handCards.AddRange(nextRow);
		LayoutHand(spawnedNow);
	}

	private CardView SpawnCard(ECardName cardName)
	{
		CardView card = GD.Load<PackedScene>(CARD_VIEW_SCENE_PATH).Instantiate<CardView>();
		AddChild(card);
		card.ShowFaceUp(cardName);
		card.EnableGrab(CanGrabNow, OnSubmitGesture);
		return card;
	}

	/// <summary>Centers the row on HAND_CENTER whatever the count. Cards spawned in this very
	/// pass (spawnedNow) are placed instantly — there is nowhere sensible for them to glide
	/// from; everything else glides. A card currently held by the pointer is skipped outright,
	/// so a layout move never fights the grab for it.</summary>
	private void LayoutHand(IReadOnlySet<CardView>? spawnedNow)
	{
		float startX = HAND_CENTER.X - (_handCards.Count - 1) * CARD_SPACING / 2f;
		for (int i = 0; i < _handCards.Count; i++)
		{
			CardView card = _handCards[i];
			if (card.IsGrabbed)
			{
				continue;
			}

			Transform3D localSlot = new Transform3D(
				Basis.Identity.Scaled(Vector3.One * CARD_SCALE),
				new Vector3(startX + i * CARD_SPACING, HAND_CENTER.Y, HAND_CENTER.Z));

			if (spawnedNow != null && spawnedNow.Contains(card))
			{
				card.Transform = localSlot;
			}
			else
			{
				card.CancelPoseAnimation();
				card.BeginPoseAnimation(GlobalTransform * localSlot, RELAYOUT_SECONDS, 0f, null);
			}
		}
	}

	private bool CanGrabNow()
	{
		if (!_headCamera.IsHandViewHeld)
		{
			return false;
		}

		if (_isSubmissionPending || _flightCard != null)
		{
			return false;
		}

		if (_isFallbackHand)
		{
			return true;
		}

		return GameState.Instance != null && GameState.Instance.View.SubmissionPhaseActive;
	}

	private void OnSubmitGesture(CardView card)
	{
		if (card.ShownCard is not ECardName playedCard)
		{
			return;
		}

		_handCards.Remove(card);
		_flightCard = card;
		_isSubmissionPending = true;

		// Camera home first — the round flow has the view travelling back WHILE the card
		// lands, not after.
		_headCamera.ReturnToHeadView();

		if (_isFallbackHand)
		{
			GD.Print($"HandView: fallback submit {playedCard} (no match running — visuals only).");
		}
		else
		{
			GameState.Instance!.RequestCardPlay(playedCard);
		}

		// Scaled, not just rotated: a played card sits on the table bigger than it stood in the
		// hand (MatchWorldView.SUBMITTED_CARD_SCALE), and InterpolateWith carries scale as well
		// as pose — so the card grows over the flight instead of popping at the far end, where
		// the slab that replaces it is already that size.
		Transform3D facedownOnSlot = _myCardSlot.GlobalTransform
			* new Transform3D(
				Basis.FromEuler(MatchWorldView.MY_CARD_FACEDOWN_ROTATION)
					.Scaled(Vector3.One * MatchWorldView.SUBMITTED_CARD_SCALE),
				Vector3.Zero);
		card.BeginPoseAnimation(
			facedownOnSlot, SUBMIT_FLIGHT_SECONDS, SUBMIT_FLIGHT_ARC_METERS, OnFlightLanded);

		LayoutHand(null);
	}

	private void OnFlightLanded()
	{
		FreeFlightCard();

		if (_isFallbackHand)
		{
			// No reveal will ever clear this without a match — clear it here so the flow can
			// be exercised repeatedly single-instance.
			_isSubmissionPending = false;
		}

		MyCardLanded?.Invoke();
	}

	private void FreeFlightCard()
	{
		if (_flightCard != null)
		{
			_flightCard.QueueFree();
			_flightCard = null;
		}
	}
}
