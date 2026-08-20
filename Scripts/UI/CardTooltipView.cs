using Godot;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.UI;

/// <summary>The panel that appears when the cursor rests on a card: its name, what kind of
/// card it is, and its rules text.
///
/// One of these is built, shown and freed for every hover — that is Godot's contract for
/// <c>Control._MakeCustomTooltip</c>, which is what puts this on screen. Everything about
/// when it appears, where it sits and when it goes away belongs to the engine; this scene is
/// only its contents. That is the whole reason the tooltip is done this way rather than as a
/// panel some screen shows and hides itself — the hover delay, the follow-the-cursor
/// placement and the clamping to the window edge all come for free.
///
/// It is told what to show rather than looking anything up. CardView has already resolved the
/// CardData and the 카드 종류 colour to draw the card itself, so passing them along keeps the
/// colour table in exactly one place.</summary>
public partial class CardTooltipView : PanelContainer
{
    private string _cardName = string.Empty;
    private string _description = string.Empty;
    private CardType _cardType;
    private Color _typeColor;

    /// <summary>Fill in what to show. Called on a freshly instantiated scene, before it is in
    /// the tree, so it only records — _Ready puts the values on the nodes.</summary>
    public void Fill(string cardName, string description, CardType cardType, Color typeColor)
    {
        _cardName = cardName;
        _description = description;
        _cardType = cardType;
        _typeColor = typeColor;
    }

    public override void _Ready()
    {
        // Both styleboxes come from the scene, so they are one resource shared by every
        // instance — tinting in place would repaint any other tooltip alive at the same time.
        // Same copy-on-use as CardView's border.
        var panelStyle = (StyleBoxFlat)GetThemeStylebox("panel").Duplicate();
        panelStyle.BorderColor = _typeColor;
        AddThemeStyleboxOverride("panel", panelStyle);

        var badge = GetNode<PanelContainer>("Margin/Rows/Header/TypeBadge");
        var badgeStyle = (StyleBoxFlat)badge.GetThemeStylebox("panel").Duplicate();
        badgeStyle.BgColor = _typeColor;
        badge.AddThemeStyleboxOverride("panel", badgeStyle);

        GetNode<Label>("Margin/Rows/Header/NameLabel").Text = _cardName;
        GetNode<Label>("Margin/Rows/Header/TypeBadge/TypeLabel").Text = TypeNameOf(_cardType);
        GetNode<Label>("Margin/Rows/DescriptionLabel").Text = _description;
        GetNode<ColorRect>("Margin/Rows/Divider").Color = _typeColor with { A = 0.45f };
    }

    /// <summary>DESIGN.md's 카드 분류 names. They live here rather than in a .tres because they
    /// name a CardType, and CardType is a grouping the rules make up — no single card owns
    /// one, so there is no .tres for it to sit in.</summary>
    private static string TypeNameOf(CardType cardType)
    {
        switch (cardType)
        {
            case CardType.Normal:
                return "Normal Card";

            case CardType.Blank:
                return "Blank Card";

            case CardType.Joker:
                return "Joker Card";

            case CardType.Ability:
                return "Ability Card";

            default:
                return string.Empty;
        }
    }
}
