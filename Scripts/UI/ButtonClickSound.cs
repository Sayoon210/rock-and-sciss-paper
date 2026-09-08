using Godot;
using RockAndScissPaper.Autoload;

namespace RockAndScissPaper.UI;

/// <summary>Gives every Button under a node the same click sound.
///
/// One call per screen instead of a line per button, because the sound is not about any
/// particular button — it is what a press feels like on this game's menus, and there is no
/// button that should be silent. Wiring each one by hand would put ten identical lines in two
/// screens and would leave the eleventh button silent for no reason other than that someone
/// added it after the list was written.
///
/// It does not make AudioManager listen for presses. That Autoload's own doc comment is
/// explicit about staying a service that plays what it is asked to, and about not learning
/// when things happen — so this hooks Godot's own Pressed signal, on the caller's side of the
/// boundary, and the thing that knows a button was clicked is still the button.
///
/// A static helper rather than a node: it holds nothing and lives no longer than the call.</summary>
public static class ButtonClickSound
{
    /// <summary>Connects every Button in this subtree, the node itself included. Call it once
    /// the tree below is built — buttons added afterwards are not picked up, which is a real
    /// limit rather than an oversight: everything on these screens is authored in the .tscn.
    ///
    /// Hidden buttons are hooked too. Visibility is what the screens swap around (the title's
    /// three panels, the host-only 매치 시작 row), so waiting for one to be shown would mean
    /// hooking on show and unhooking on hide, for a sound that costs nothing while nobody can
    /// press it.</summary>
    public static void HookUp(Node root)
    {
        if (root is Button button)
        {
            button.Pressed += PlayClick;
        }

        foreach (Node child in root.GetChildren())
        {
            HookUp(child);
        }
    }

    private static void PlayClick()
    {
        AudioManager.Instance?.Play(ESoundName.ButtonPressed);
    }
}
