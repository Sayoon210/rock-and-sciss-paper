using System.Collections.Generic;
using Godot;
using RockAndScissPaper.Cards;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Autoload;

// 코드로 .tres 파일을 Dictionary로 로딩시키는코드 (싱글톤)

/// <summary>Loads every CardData .tres under Data/Cards/ and indexes it by CardName.
/// Does not define card stats itself — that text lives only in the .tres files. Scans the
/// directory rather than listing filenames, since the ability-card roster may grow later
/// and this must not need editing when it does.</summary>
public partial class CardDatabase : Node
{
    private const string CARD_DIRECTORY_PATH = "res://Data/Cards/";

    public static CardDatabase? Instance { get; private set; }

    private readonly Dictionary<CardName, CardData> _cardsByName = new Dictionary<CardName, CardData>();

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        LoadCards();
    }

    public CardData? GetCardData(CardName cardName)
    {
        if (_cardsByName.TryGetValue(cardName, out CardData? cardData))
        {
            return cardData;
        }

        GD.PrintErr($"CardDatabase: no CardData loaded for {cardName}.");
        return null;
    }

    /// <summary>Every card name a .tres was loaded for. Deck-assembly code reads the
    /// ability-card roster from here rather than listing card names itself, so the roster
    /// can grow without a code change — see CLAUDE.md's "don't hardcode the ability-card
    /// roster" rule.</summary>
    public IReadOnlyCollection<CardName> LoadedCardNames
    {
        get
        {
            return _cardsByName.Keys;
        }
    }

    private void LoadCards()
    {
        string[] fileNames = DirAccess.GetFilesAt(CARD_DIRECTORY_PATH);
        foreach (string fileName in fileNames)
        {
            if (!fileName.EndsWith(".tres"))
            {
                continue;
            }

            string resourcePath = CARD_DIRECTORY_PATH + fileName;
            CardData cardData = GD.Load<CardData>(resourcePath);
            if (cardData != null)
            {
                _cardsByName[cardData.CardName] = cardData;
            }
        }
    }
}
