using Godot;
using RockAndScissPaper.Autoload;
using RockAndScissPaper.Cards;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.UI;

/// <summary>One card on screen, face up or face down. There is exactly one of these for
/// every card in the game — 일반카드, 더미, 조커 and 특수 all render through it, because a
/// subclass per card variant is the thing root CLAUDE.md rules out.
///
/// It renders and it reports a click. It does not read GameState.View, does not subscribe to
/// any Autoload signal, and does not decide what a click on it means — the owner binds the
/// node when it connects (cardView.Clicked += () =&gt; OnCardClicked(cardView);) and decides
/// there.
///
/// No card art exists yet: every CardData.CardArt is null, so every card currently draws as
/// the placeholder fill. Four things make the eventual art a drop-in rather than a relayout —
/// the 5:7 rect is fixed and identical in both modes, the name label sits *over* the art
/// instead of beside it, the 카드 종류 border is carried by its own node instead of by the
/// fill colour, and CardArt stays nullable so a half-illustrated deck still renders.</summary>
public partial class CardView : Control
{
    /// <summary>Argument-free on purpose: what this click means is the owner's business.</summary>
    [Signal] public delegate void ClickedEventHandler();

    private ColorRect _placeholderFill = null!;
    private TextureRect _artView = null!;
    private ColorRect _faceDownBack = null!;
    private Panel _typeBorder = null!;
    private Label _nameLabel = null!;
    private StyleBoxFlat _borderStyle = null!;

    /// <summary>The card this view is currently showing face up, or null while it is face
    /// down. Read-only: it lets an owner that bound the *node* recover the card without the
    /// card ever deciding anything about itself.</summary>
    public CardName? ShownCard { get; private set; }

    public override void _Ready()
    {
        _placeholderFill = GetNode<ColorRect>("PlaceholderFill");
        _artView = GetNode<TextureRect>("ArtView");
        _faceDownBack = GetNode<ColorRect>("FaceDownBack");
        _typeBorder = GetNode<Panel>("TypeBorder");
        _nameLabel = GetNode<Label>("NameLabel");

        // The scene's border stylebox is one resource shared by every instance of the scene,
        // so tinting it in place would repaint every card on screen. Each view takes a copy.
        _borderStyle = (StyleBoxFlat)_typeBorder.GetThemeStylebox("panel").Duplicate();
        _typeBorder.AddThemeStyleboxOverride("panel", _borderStyle);

        ShowFaceDown();
    }

    /// <summary>Show this card's face. Resolves the CardName through CardDatabase and falls
    /// back to the enum name when no .tres was loaded for it, so a missing resource shows a
    /// readable card instead of a blank one.</summary>
    public void ShowFaceUp(CardName card)
    {
        CardData? cardData = CardDatabase.Instance?.GetCardData(card);

        string displayName;
        string description;
        Texture2D? art;
        if (cardData == null)
        {
            displayName = card.ToString();
            description = string.Empty;
            art = null;
        }
        else
        {
            displayName = cardData.DisplayName;
            description = cardData.Description;
            art = cardData.CardArt;
        }

        Color typeColor = TypeColorOf(card.GetCardType());

        _placeholderFill.Color = typeColor.Darkened(0.6f);

        // Art occupies the same rect as the placeholder and simply covers it once a .tres
        // carries one. Nothing else about the card moves when that happens.
        _artView.Texture = art;
        _artView.Visible = art != null;

        _faceDownBack.Visible = false;

        _borderStyle.BorderColor = typeColor;

        _nameLabel.Text = displayName;
        _nameLabel.Visible = true;

        TooltipText = description;
        ShownCard = card;
    }

    /// <summary>Show the back. Used for the opponent's 패, where this side is only ever told
    /// a count.</summary>
    public void ShowFaceDown()
    {
        _faceDownBack.Visible = true;
        _artView.Visible = false;
        _nameLabel.Visible = false;

        // The border node stays visible, but neutral. Tinting it by 카드 종류 here would
        // publish exactly the information the back exists to hide.
        _borderStyle.BorderColor = new Color(0.45f, 0.47f, 0.55f);

        TooltipText = string.Empty;
        ShownCard = null;
    }

    /// <summary>Reports the click and judges nothing — whether this card may be played is
    /// the host's answer, not this node's (Scripts/CLAUDE.md).</summary>
    public override void _GuiInput(InputEvent inputEvent)
    {
        if (inputEvent is InputEventMouseButton mouseButton
            && mouseButton.ButtonIndex == MouseButton.Left
            && mouseButton.Pressed)
        {
            EmitSignalClicked();
            AcceptEvent();
        }
    }

    /// <summary>The one colour a 카드 종류 gets. The placeholder fill is a darkened version
    /// of it rather than a second colour, so the fill and the border can never disagree.</summary>
    private static Color TypeColorOf(CardType cardType)
    {
        switch (cardType)
        {
            case CardType.Normal:
                return new Color(0.36f, 0.62f, 0.92f);

            case CardType.Dummy:
                return new Color(0.58f, 0.60f, 0.64f);

            case CardType.Joker:
                return new Color(0.85f, 0.32f, 0.34f);

            case CardType.Special:
                return new Color(0.92f, 0.74f, 0.30f);

            default:
                return new Color(0.50f, 0.50f, 0.50f);
        }
    }
}
