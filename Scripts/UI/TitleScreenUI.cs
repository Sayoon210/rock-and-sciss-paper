using Godot;
using RockAndScissPaper.Autoload;

namespace RockAndScissPaper.UI;

/// <summary>The first screen. Nothing here touches match state — it only picks the next
/// scene, or opens the settings overlay in place.</summary>
public partial class TitleScreenUI : Control
{
    private PanelContainer _settingsOverlay = null!;
    private HSlider _soundEffectVolumeSlider = null!;
    private HSlider _musicVolumeSlider = null!;

    public override void _Ready()
    {
        GetNode<Button>("CenterContainer/Buttons/MatchButton").Pressed += OnMatchPressed;
        GetNode<Button>("CenterContainer/Buttons/QuitButton").Pressed += OnQuitPressed;
        GetNode<Button>("CenterContainer/Buttons/SettingsButton").Pressed += OnSettingsPressed;
        GetNode<Button>("CenterContainer/Buttons/DebugHarnessButton").Pressed += OnDebugHarnessPressed;

        _settingsOverlay = GetNode<PanelContainer>("SettingsOverlay");
        _soundEffectVolumeSlider = GetNode<HSlider>("SettingsOverlay/Center/Box/SoundEffectVolumeSlider");
        _musicVolumeSlider = GetNode<HSlider>("SettingsOverlay/Center/Box/MusicVolumeSlider");
        _soundEffectVolumeSlider.ValueChanged += OnSoundEffectVolumeChanged;
        _musicVolumeSlider.ValueChanged += OnMusicVolumeChanged;
        GetNode<Button>("SettingsOverlay/Center/Box/CloseSettingsButton").Pressed += OnCloseSettingsPressed;

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
        ScreenRouter.GoToConnectionScreen(this);
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }

    private void OnDebugHarnessPressed()
    {
        ScreenRouter.GoToDebugHarness(this);
    }

    /// <summary>Read from AudioManager rather than a value this screen remembers itself —
    /// AudioServer's bus volume is already the one place that number lives (Scripts/Autoload/
    /// AudioManager.cs), and a slider that opened with a value of its own could disagree with
    /// it the moment anything else ever changes a bus's volume.</summary>
    private void OnSettingsPressed()
    {
        _soundEffectVolumeSlider.SetValueNoSignal(AudioManager.Instance!.GetSoundEffectVolume());
        _musicVolumeSlider.SetValueNoSignal(AudioManager.Instance!.GetMusicVolume());
        _settingsOverlay.Visible = true;
    }

    private void OnCloseSettingsPressed()
    {
        _settingsOverlay.Visible = false;
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
