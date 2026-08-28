using Godot;
using RockAndScissPaper.Autoload;

namespace RockAndScissPaper.UI;

/// <summary>The first screen. Nothing here touches match state — it only picks the next
/// scene, or opens the settings overlay in place.</summary>
public partial class TitleScreenUI : Control
{
    private const string CONNECTION_SCENE_PATH = "res://Scenes/Screens/ConnectionScreen.tscn";

    // The 교체/변화 choice phase has not been verified across two instances yet, and the
    // debug harness is still the only thing that can drive it. It stopped being the project's
    // main scene when this screen took over, so it needs a door.
    private const string DEBUG_HARNESS_SCENE_PATH = "res://Scenes/MatchDebugUI.tscn";

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

        // Started from here rather than from AudioManager itself: the Autoload is a service
        // and does not know which screen is up. It keeps playing through the connection
        // screen, since that is still the menu — MatchWorldView is what stops it.
        AudioManager.Instance!.PlayMainMenuMusic();
    }

    private void OnMatchPressed()
    {
        GetTree().ChangeSceneToFile(CONNECTION_SCENE_PATH);
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }

    private void OnDebugHarnessPressed()
    {
        GetTree().ChangeSceneToFile(DEBUG_HARNESS_SCENE_PATH);
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
