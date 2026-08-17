using Godot;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Cards;

/// <summary>Presentation data for one card identity — display name and description text.
/// GameLogic knows only CardName; this is what a CardName looks like on screen.
/// One .tres file per CardName, loaded and indexed by CardDatabase. Card art has no
/// assets yet, so CardArt is left unset until some are added.</summary>
public partial class CardData : Resource
{
    [Export]
    public CardName CardName { get; set; }

    [Export]
    public string DisplayName { get; set; } = string.Empty;

    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } = string.Empty;

    [Export]
    public Texture2D? CardArt { get; set; }
}
