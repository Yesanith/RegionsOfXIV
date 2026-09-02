using System;
using System.IO;
using System.Threading.Tasks;
using NAudio;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace RegionsOfXIV.Services;

// Playing a sound file of the player's own, through NAudio, in a way that behaves like the rest
// of the game's audio even though it goes nowhere near the game's mixer.
//
// The device is opened once and kept. Opening a WaveOut device takes long enough to be felt, and
// a notification is exactly the moment there is no time to spare, so the cost is paid on the first
// sound rather than on every one. It is opened lazily all the same: somebody who never turns this
// on never holds an audio device at all.
//
// Everything is mixed into one fixed format for the same reason. A WaveOutEvent can only be
// initialised once, so the device cannot be re-pointed at each file's own sample rate; instead
// each file is converted to the mixer's format on the way in.
internal sealed class FileSoundPlayer : IDisposable
{
    // Lower than NAudio's 300 ms default, because the sound is announcing something that is on
    // screen already and a third of a second behind it reads as a fault rather than as latency.
    private const int DesiredLatencyMs = 150;

    private readonly IGameAudio audio;
    private readonly object gate = new();

    private WaveOutEvent? device;
    private MixingSampleProvider? mixer;

    // The reader and, when the file needed converting, the conversion stream over it. Outermost
    // first, because a conversion stream may or may not close the reader underneath it depending
    // on the NAudio version, and closing both in that order is correct either way.
    private IDisposable[]? holding;

    private bool playing;
    private bool disposed;

    // A failure and the file it belongs to, held as one reference so the two cannot be read apart.
    //
    // The path is half the value. Without it a decode failure outlives the file that caused it:
    // choosing a working file after a broken one left the broken one's message sitting in red
    // under a path that was perfectly fine, until something happened to play successfully.
    private sealed record Fault(string Path, string Message);

    // Written on the decode thread, read on the draw thread. A reference assignment is atomic and
    // a settings window one frame behind the truth is not worth a lock.
    private volatile Fault? fault;

    private string? lastLogged;

    public FileSoundPlayer(IGameAudio audio) => this.audio = audio;

    // What the Sound tab shows under the file row, for the file it is currently showing. Null when
    // the last attempt at that file was fine, when there has not been one, or when the last
    // failure was some other file's.
    public string? ProblemFor(string path) =>
        this.fault is { } current && current.Path == path ? current.Message : null;

    // Called on the framework thread, which is where the game's settings can be read. Only the
    // decode goes elsewhere: opening and parsing a file is slow enough to be seen as a stutter if
    // it happened here.
    public bool Play(string path)
    {
        if (GameMixerRules.Decide(this.audio).Volume is not { } volume)
            return false;

        if (SoundLimits.CustomSoundProblem(path) is { } fault)
        {
            Fail(path, fault, null, null);
            return false;
        }

        lock (this.gate)
        {
            // Dropped, not queued, matching the interval rule upstream. A second sound arriving
            // over the first is the thing being avoided, and one that waited its turn would arrive
            // detached from whatever caused it.
            if (this.disposed || this.playing)
                return false;

            this.playing = true;
        }

        _ = Task.Run(() => Decode(path, volume));

        return true;
    }

    private void Decode(string path, float volume)
    {
        WaveStream? reader = null;
        IDisposable[] owned = [];

        try
        {
            reader = SoundFileReader.Open(path);

            var decoded = SoundFileReader.ToPcm(reader);
            owned = decoded.Owned;

            var chain = SoundFileReader.Convert(decoded.Provider.ToSampleProvider(), volume);

            lock (this.gate)
            {
                if (this.disposed)
                {
                    Close(owned);
                    this.playing = false;
                    return;
                }

                this.holding = owned;
            }

            Start(chain);

            this.fault = null;
            this.lastLogged = null;
        }
        catch (Exception ex)
        {
            Close(owned.Length > 0 ? owned : reader is null ? [] : [reader]);

            lock (this.gate)
            {
                this.holding = null;
                this.playing = false;
            }

            // The format goes into the log on every failure, whatever the cause. An encoding this
            // could not read once reported itself as "damaged", which is a sentence that sends
            // somebody to look at the wrong thing; the one line below says what was actually
            // there, and it is the line that would have settled it first time.
            Fail(path, DescribeFailure(ex), ex, reader?.WaveFormat);
        }
    }

    // Adding to the mixer happens outside the lock deliberately. The mixer takes a lock of its own
    // while reading, and raises MixerInputEnded from inside it, so holding this one across the add
    // would be two locks taken in opposite orders on two threads.
    private void Start(ISampleProvider chain)
    {
        MixingSampleProvider target;

        lock (this.gate)
        {
            target = this.mixer ??= OpenDevice();
        }

        target.AddMixerInput(chain);
    }

    private MixingSampleProvider OpenDevice()
    {
        var mixed = new MixingSampleProvider(
            WaveFormat.CreateIeeeFloatWaveFormat(
                SoundFileReader.SampleRate, SoundFileReader.Channels))
        {
            // Without this the mixer reports zero samples whenever nothing is playing, which a
            // WaveOutEvent reads as the end of the stream and stops on. It has to keep handing
            // back silence for the device to stay open between sounds.
            ReadFully = true,
        };

        mixed.MixerInputEnded += OnInputEnded;

        // Built locally and stored only once it is playing. Init and Play both throw when Windows
        // will not give up a device, and a half-opened one left in the field would be leaked:
        // this.mixer is still null at that point, so the next notification opens another and
        // overwrites it, one device handle and one thread per attempt for as long as the failure
        // lasts.
        var opening = new WaveOutEvent { DesiredLatency = DesiredLatencyMs };

        try
        {
            opening.Init(mixed);
            opening.Play();
        }
        catch (Exception)
        {
            mixed.MixerInputEnded -= OnInputEnded;
            opening.Dispose();
            throw;
        }

        this.device = opening;

        return mixed;
    }

    // Raised on NAudio's own playback thread when the trimmed file runs out. This is where the
    // reader is closed: nothing else knows when the sound is over, and leaving it open would hold
    // the player's file until the next one replaced it.
    private void OnInputEnded(object? sender, SampleProviderEventArgs e)
    {
        IDisposable[]? finished;

        lock (this.gate)
        {
            finished = this.holding;
            this.holding = null;
            this.playing = false;
        }

        Close(finished);
    }

    // Every failure is closed the same way and none of them may throw a second time on the way
    // out: a reader that failed to open is quite likely to fail to close.
    private static void Close(IDisposable[]? owned)
    {
        if (owned == null)
            return;

        foreach (var item in owned)
        {
            try
            {
                item.Dispose();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Closing a sound file failed.");
            }
        }
    }

    // The exception says what NAudio could not do; this says what the player can do about it.
    //
    // Everything with a name of its own gets a message of its own. The catch-all used to swallow
    // "Unsupported source encoding" into "damaged, or a form the plugin cannot read", which is
    // both wrong and unactionable, so what is left in it now points at the log rather than
    // guessing at a cause.
    private static string DescribeFailure(Exception ex) => ex switch
    {
        // Already written for the window, at the point that knew what had happened.
        SoundFileReader.SoundFileFault fault => fault.Message,

        FileNotFoundException or DirectoryNotFoundException => Loc.Get(
            "sound.problem.missing", "There is no file at that path any more."),

        // Before IOException, which it derives from. A file that stops early is a different
        // problem from one another program is holding open.
        EndOfStreamException => Loc.Get(
            "sound.problem.truncated",
            "That file stops part way through and could not be read to the end."),

        UnauthorizedAccessException or IOException => Loc.Get(
            "sound.problem.locked",
            "That file could not be opened. Another program may have it open."),

        // WaveFileReader on something with no RIFF header, and Mp3FileReaderBase on something with
        // no MP3 frames in it. Named separately because the fix is to check the file rather than
        // to re-save it.
        FormatException or InvalidDataException => Loc.Get(
            "sound.problem.notaudio",
            "That file is not really a WAV or an MP3, whatever its name says."),

        // What SampleProviderConverters raises for an encoding or a bit depth it cannot read.
        // ReadableAsSamples should have caught both before they got here, so reaching this means
        // that list has fallen behind NAudio.
        ArgumentException or InvalidOperationException => Loc.Get(
            "sound.problem.samples",
            "That file is in a form the plugin cannot turn into sound. Saving it again as "
            + "ordinary 16 bit PCM will work."),

        // Only the output device by this point: the two other sources of MmException are caught
        // where they happen and arrive as a SoundFileFault instead.
        MmException => Loc.Get(
            "sound.problem.device",
            "Windows would not open an audio device, so nothing can be played."),

        _ => Loc.Get(
            "sound.problem.decode",
            "That file could not be read. The plugin's log names the format it saw, which is the "
            + "quickest way to see why."),
    };

    private void Fail(string path, string message, Exception? ex, WaveFormat? format)
    {
        this.fault = new Fault(path, message);

        // Once per distinct message. A notification that keeps arriving with the same broken file
        // would otherwise write the same line into the log every few seconds.
        if (this.lastLogged == message)
            return;

        this.lastLogged = message;

        var detail = $"Sound file {path} [{SoundFileReader.Describe(format)}]: {message}";

        if (ex == null)
            Log.Warning(detail);
        else
            Log.Warning(ex, detail);
    }

    // A WaveOutEvent is a device handle and an owned thread. Left undisposed across a hot reload
    // it leaks both, and the sound it was playing carries on with nothing left to stop it.
    public void Dispose()
    {
        WaveOutEvent? closing;
        IDisposable[]? open;
        MixingSampleProvider? mixed;

        lock (this.gate)
        {
            if (this.disposed)
                return;

            this.disposed = true;
            this.playing = false;

            closing = this.device;
            open = this.holding;
            mixed = this.mixer;

            this.device = null;
            this.holding = null;
            this.mixer = null;
        }

        // Unhooked before the device closes, so the last read cannot raise this into a handler
        // that would then take a lock nothing is going to release.
        if (mixed != null)
            mixed.MixerInputEnded -= OnInputEnded;

        try
        {
            closing?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Closing the audio device failed.");
        }

        Close(open);
    }
}
