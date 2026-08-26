namespace RockAndScissPaper.Autoload;

/// <summary>Every sound the game can ask for. The member name is also the file name —
/// AudioManager loads Assets/Audio/{member}.wav — so renaming a member renames a file. The
/// source file's own name is not kept; ATTRIBUTIONS.md records where each one came from.
///
/// The values are written out rather than left implicit: the moment an ESoundName is stored in
/// a .tres (see IDEAS.md's staged plan) it is serialized as its integer, and inserting a
/// member in the middle would shift every later one and silently repoint saved data.</summary>
public enum ESoundName
{
    CardFlip = 1,
    RoundWon = 2,
    RoundLost = 3,
    Joker = 4,
}
