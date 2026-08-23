using System;
using System.Collections.Generic;
using Godot;

namespace RockAndScissPaper.Autoload;

/// <summary>Plays a sound when asked to. Owns the buses, the loaded streams and the mixing;
/// it does not own <i>when</i> anything is heard.
///
/// That split is deliberate. The obvious design — this Autoload subscribing to GameState's
/// signals and playing sounds off them — cannot express the reveal sequence: the win/loss
/// beat is the rendezvous of two independently timed events (the opponent's card finishing
/// its turn, and the round resolving), and which of them lands second changes with the kind
/// of round. Knowing that here would mean keeping a second copy of MatchScreenUI's
/// _opponentCardFlipPending / _roundAlreadyResolved interlock, and that interlock has already
/// produced one bug (DevLogDoc/2026-08-21-round-reveal-choreography.md). So whoever already
/// holds the timing calls Play instead, and this class stays a service. See IDEAS.md §5.</summary>
public partial class AudioManager : Node
{
    private const string SOUND_DIRECTORY_PATH = "res://Assets/Audio/";
    private const string SOUND_FILE_EXTENSION = ".wav";
    private const string SOUND_EFFECT_BUS_NAME = "SFX";

    /// <summary>Music is named by path rather than through ESoundName. That enum exists so a
    /// sound can be asked for by name and found on disk by the same name, which needs every
    /// entry to share one extension — music is .ogg (streamed and compressed, since a looping
    /// track as .wav is tens of megabytes) while the effects are .wav.</summary>
    private const string MAIN_MENU_MUSIC_PATH = "res://Assets/Audio/MainMenuBGM.ogg";
    private const string MUSIC_BUS_NAME = "Music";

    /// <summary>How many sounds may overlap. Both cards vanishing at once is normal here
    /// (a 조커 always does it), so overlap is a supported case, not an edge one.</summary>
    private const int MAX_SIMULTANEOUS_SOUNDS = 16;

    public static AudioManager? Instance { get; private set; }

    private readonly Dictionary<ESoundName, AudioStream> _streamsByName = new Dictionary<ESoundName, AudioStream>();

    private AudioStreamPlayer _soundEffectPlayer = null!;
    private AudioStreamPlaybackPolyphonic _soundEffectPlayback = null!;

    // Its own player on its own bus, so music and effects can be mixed against each other
    // later without one being a special case of the other.
    private AudioStreamPlayer _musicPlayer = null!;

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        LoadSounds();
        CreateSoundEffectPlayer();
        CreateMusicPlayer();
    }

    /// <summary>Starts one sound. Overlapping calls mix rather than cut each other off.
    /// A sound with no file loaded is silently skipped — the missing files are reported once
    /// at startup instead, so a sound cued every frame cannot flood the log.</summary>
    public void Play(ESoundName soundName)
    {
        if (!_streamsByName.TryGetValue(soundName, out AudioStream? stream))
        {
            return;
        }

        _soundEffectPlayback.PlayStream(stream);
    }

    /// <summary>Starts the title music, and does nothing if it is already the thing playing.
    /// Coming back to the title from a finished match runs TitleScreenUI._Ready a second time,
    /// and starting the track over from the top there would be heard as the music stuttering
    /// rather than as a new screen.</summary>
    public void PlayMainMenuMusic()
    {
        if (_musicPlayer.Playing)
        {
            return;
        }

        if (!ResourceLoader.Exists(MAIN_MENU_MUSIC_PATH))
        {
            GD.Print($"AudioManager: no music file at {MAIN_MENU_MUSIC_PATH}.");
            return;
        }

        AudioStream stream = GD.Load<AudioStream>(MAIN_MENU_MUSIC_PATH);

        // Set here rather than left to the .import file. Looping is what makes this menu music
        // instead of a one-shot, and an import setting is invisible from the code that depends
        // on it — a track that quietly stops partway through the title screen would send
        // someone looking through this class for a bug that is not in it.
        if (stream is AudioStreamOggVorbis oggStream)
        {
            oggStream.Loop = true;
        }

        _musicPlayer.Stream = stream;
        _musicPlayer.Play();
    }

    /// <summary>Stops whatever music is playing. The title track is menu music, so the match
    /// screen silences it on the way in.</summary>
    public void StopMusic()
    {
        _musicPlayer.Stop();
    }

    /// <summary>Walks the enum and loads the file named after each member, rather than
    /// scanning the directory the way CardDatabase does. A .wav is an imported asset, and an
    /// exported build does not list those under their source names — driving this from the
    /// enum also means a file whose name does not match any member is reported as missing
    /// instead of quietly loading into nothing.</summary>
    private void LoadSounds()
    {
        List<string> missingSoundNames = new List<string>();

        foreach (ESoundName soundName in Enum.GetValues<ESoundName>())
        {
            string resourcePath = SOUND_DIRECTORY_PATH + soundName + SOUND_FILE_EXTENSION;
            if (!ResourceLoader.Exists(resourcePath))
            {
                missingSoundNames.Add(soundName.ToString());
                continue;
            }

            _streamsByName[soundName] = GD.Load<AudioStream>(resourcePath);
        }

        if (missingSoundNames.Count > 0)
        {
            GD.Print($"AudioManager: no file under {SOUND_DIRECTORY_PATH} for {string.Join(", ", missingSoundNames)}.");
        }
    }

    private void CreateSoundEffectPlayer()
    {
        AudioStreamPolyphonic polyphonicStream = new AudioStreamPolyphonic();
        polyphonicStream.Polyphony = MAX_SIMULTANEOUS_SOUNDS;

        _soundEffectPlayer = new AudioStreamPlayer();
        _soundEffectPlayer.Stream = polyphonicStream;
        _soundEffectPlayer.Bus = SOUND_EFFECT_BUS_NAME;
        AddChild(_soundEffectPlayer);

        // A polyphonic stream is an empty mixer until something is pushed into it, and its
        // playback object only exists while the player is running — so this Play() starts
        // silence that stays open for the session, not a sound.
        _soundEffectPlayer.Play();
        _soundEffectPlayback = (AudioStreamPlaybackPolyphonic)_soundEffectPlayer.GetStreamPlayback();
    }

    // No stream and nothing playing until something asks for music. Unlike the effects player
    // there is no mixer to open here — one track plays at a time, so the stream is swapped in
    // when it is wanted.
    private void CreateMusicPlayer()
    {
        _musicPlayer = new AudioStreamPlayer();
        _musicPlayer.Bus = MUSIC_BUS_NAME;
        AddChild(_musicPlayer);
    }
}
