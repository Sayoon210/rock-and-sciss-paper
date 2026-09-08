using Godot;
using RockAndScissPaper.Autoload;

namespace RockAndScissPaper.UI;

/// <summary>The first screen, and now the only menu screen: the game's name over a list of
/// entries, and three panels that take turns in the one slot under it — the main list, the
/// connection list (ConnectionScreenUI), and settings.
///
/// 대전 used to change the scene to ConnectionScreen.tscn and 설정 used to raise a panel over
/// the menu, so the same three things were reached three different ways. They are one gesture
/// now: swap what is in the slot. The title and the 3D table behind it never move, which is
/// what makes the swap read as one screen rather than a scene change.
///
/// Nothing here touches match state — it picks what to show, quits, or hands off to
/// ScreenRouter for the two places that really are other scenes (the match, the debug
/// harness).</summary>
public partial class TitleScreenUI : Control
{
    private const string MAIN_MENU_PATH = "Layout/MainMenu";
    private const string CONNECTION_PATH = "Layout/Connection";
    private const string SETTINGS_PATH = "Layout/Settings";

    private const string MATCH_BUTTON_PATH = "Layout/MainMenu/MatchRow/MatchButton";
    private const string SETTINGS_BUTTON_PATH = "Layout/MainMenu/SettingsRow/SettingsButton";
    private const string DEBUG_HARNESS_BUTTON_PATH = "Layout/MainMenu/DebugHarnessRow/DebugHarnessButton";
    private const string QUIT_BUTTON_PATH = "Layout/MainMenu/QuitRow/QuitButton";

    private const string SOUND_EFFECT_VOLUME_SLIDER_PATH = "Layout/Settings/SoundEffectVolumeSlider";
    private const string MUSIC_VOLUME_SLIDER_PATH = "Layout/Settings/MusicVolumeSlider";
    private const string SETTINGS_BACK_BUTTON_PATH = "Layout/Settings/BackRow/BackButton";

    private Control _mainMenu = null!;
    private ConnectionScreenUI _connection = null!;
    private Control _settings = null!;
    private HSlider _soundEffectVolumeSlider = null!;
    private HSlider _musicVolumeSlider = null!;

    public override void _Ready()
    {
        _mainMenu = GetNode<Control>(MAIN_MENU_PATH);
        _connection = GetNode<ConnectionScreenUI>(CONNECTION_PATH);
        _settings = GetNode<Control>(SETTINGS_PATH);
        _soundEffectVolumeSlider = GetNode<HSlider>(SOUND_EFFECT_VOLUME_SLIDER_PATH);
        _musicVolumeSlider = GetNode<HSlider>(MUSIC_VOLUME_SLIDER_PATH);

        GetNode<Button>(MATCH_BUTTON_PATH).Pressed += OnMatchPressed;
        GetNode<Button>(SETTINGS_BUTTON_PATH).Pressed += OnSettingsPressed;
        GetNode<Button>(DEBUG_HARNESS_BUTTON_PATH).Pressed += OnDebugHarnessPressed;
        GetNode<Button>(QUIT_BUTTON_PATH).Pressed += OnQuitPressed;
        GetNode<Button>(SETTINGS_BACK_BUTTON_PATH).Pressed += ShowMainMenu;
        _soundEffectVolumeSlider.ValueChanged += OnSoundEffectVolumeChanged;
        _musicVolumeSlider.ValueChanged += OnMusicVolumeChanged;

        // A plain C# event on a node in this same scene, so both are freed together and there
        // is nothing to unhook in _ExitTree — unlike the Autoload signals ConnectionScreenUI
        // itself subscribes to (Scripts/Autoload/CLAUDE.md).
        _connection.Closed += ShowMainMenu;

        // Every button on this screen, including the connection panel's and the settings
        // panel's — the whole menu is one node tree, so it is one call.
        ButtonClickSound.HookUp(this);

        ShowMainMenu();

        // The menu music would be started from here rather than from AudioManager itself: the
        // Autoload is a service and does not know which screen is up. Left silent for now while
        // the game's sound is worked out — the track that is in the project was picked against
        // the old 2D look and does not belong to the one being built. AudioManager still owns
        // the Music bus, the player and the volume, so this is one line away from coming back.
        //
        // AudioManager.Instance!.PlayMainMenuMusic();
    }

    private void OnMatchPressed()
    {
        ShowOnly(_connection);
        _connection.Open();
    }

    /// <summary>Read from AudioManager rather than a value this screen remembers itself —
    /// AudioServer's bus volume is already the one place that number lives (Scripts/Autoload/
    /// AudioManager.cs), and a slider that opened with a value of its own could disagree with
    /// it the moment anything else ever changes a bus's volume.</summary>
    private void OnSettingsPressed()
    {
        _soundEffectVolumeSlider.SetValueNoSignal(AudioManager.Instance!.GetSoundEffectVolume());
        _musicVolumeSlider.SetValueNoSignal(AudioManager.Instance!.GetMusicVolume());
        ShowOnly(_settings);
    }

    private void OnDebugHarnessPressed()
    {
        ScreenRouter.GoToDebugHarness(this);
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }

    private void ShowMainMenu()
    {
        ShowOnly(_mainMenu);
    }

    private void ShowOnly(Control panel)
    {
        _mainMenu.Visible = panel == _mainMenu;
        _connection.Visible = panel == _connection;
        _settings.Visible = panel == _settings;
    }

    private void OnSoundEffectVolumeChanged(double value)
    {
        AudioManager.Instance!.SetSoundEffectVolume((float)value);
    }

    private void OnMusicVolumeChanged(double value)
    {
        AudioManager.Instance!.SetMusicVolume((float)value);
    }
}
