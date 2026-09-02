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
    // What the device runs at. 44.1 kHz stereo is what nearly every file already is, so the
    // resampler usually has nothing to do.
    private const int MixerSampleRate = 44100;

    private const int MixerChannels = 2;

    // Long enough for anything that is announcing an arrival, short enough that a file chosen by
    // mistake is over before it becomes a problem. Somebody will point this at a five minute
    // track, and five minutes of music over a notification that left the screen in seven seconds
    // is worse than no sound at all.
    //
    // The alternative was ending the sound with the notification, which was rejected on two
    // counts: it would put the overlay's timings into a class that has no business knowing them,
    // and a notification's life is configurable, so the cap would move when somebody adjusted a
    // fade. This number depends on nothing.
    //
    // A long file is truncated rather than refused. Hearing the first five seconds says plainly
    // what happened; silence and a message would leave someone wondering whether the file was
    // even read.
    private static readonly TimeSpan MaxLength = TimeSpan.FromSeconds(5);

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

    // Written on the decode thread, read on the draw thread. A reference assignment is atomic and
    // a settings window one frame behind the truth is not worth a lock.
    private volatile string? problem;

    private string? lastLogged;

    public FileSoundPlayer(IGameAudio audio) => this.audio = audio;

    // What the Sound tab shows under the file row. Null when the last attempt was fine, or
    // when there has not been one.
    public string? Problem => this.problem;

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
            reader = Open(path);

            var decoded = ToPcm(reader);
            owned = decoded.Owned;

            var chain = Convert(decoded.Provider.ToSampleProvider(), volume);

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

            this.problem = null;
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

    // Mp3FileReaderBase with the ACM decoder rather than Mp3FileReader, which does not exist in
    // this pair of packages: NAudio 2.3 puts the reader in Core, where it cannot know about any
    // decoder, and each decoder in the package that owns it. Composing the two by hand is what
    // Core's constructor is shaped for, and it is the reason WinMM is referenced at all beyond the
    // output device.
    //
    // The decoder is built by ACM, so it can fail for want of a codec in the same way the WAV
    // conversion below can. Caught here rather than left to the catch-all, which would otherwise
    // report a missing MP3 decoder as a missing output device: both arrive as MmException and the
    // type alone cannot tell them apart.
    private static WaveStream Open(string path)
    {
        if (!Path.GetExtension(path).Equals(".mp3", StringComparison.OrdinalIgnoreCase))
            return new WaveFileReader(path);

        try
        {
            return new Mp3FileReaderBase(path, format => new AcmMp3FrameDecompressor(format));
        }
        catch (MmException ex)
        {
            throw new SoundFileFault(
                Loc.Get(
                    "sound.problem.mp3codec",
                    "Windows has no MP3 decoder available, so this file cannot be played. A .wav "
                    + "file will still work."),
                ex);
        }
    }

    // What the sample converters are handed, and everything that has to be closed with it.
    //
    // Internal, with ToPcm below it, so the format handling can be tested against real files
    // without a device: the bug this exists to prevent was a valid WAV being refused, and that is
    // decided here rather than anywhere reachable through Play.
    internal readonly record struct Decoded(IWaveProvider Provider, IDisposable[] Owned);

    // Getting a reader to a form the sample converters accept, which is not the same question as
    // whether the file is playable elsewhere.
    //
    // The case that sent this wrong: WAVE_FORMAT_EXTENSIBLE is a container tag rather than an
    // encoding. The samples it holds are named by a subformat GUID and are very often ordinary
    // PCM, but the converters look only at the outer tag, so a perfectly normal 24 bit stereo WAV
    // was refused with "Unsupported source encoding" while every other program on the machine
    // played it. Re-reporting the standard format the extension describes is the whole fix, and it
    // is free: not a byte of the audio changes.
    //
    // Only then, and only for a file that really is in another encoding, is ACM asked to convert.
    // A plain PCM file takes neither path and is handed on exactly as it was.
    internal static Decoded ToPcm(WaveStream reader)
    {
        var format = reader.WaveFormat;

        if (Unwrap(format) is { } standard)
        {
            if (ReadableAsSamples(standard))
                return new Decoded(new Retagged(reader, standard), [reader]);

            // Not readable, but still the honest description of what is in the file, so the
            // conversion and any message about it are about the real encoding rather than about
            // the word "Extensible".
            format = standard;
        }

        if (ReadableAsSamples(format))
            return new Decoded(reader, [reader]);

        try
        {
            var converted = WaveFormatConversionStream.CreatePcmStream(reader);

            return new Decoded(converted, [converted, reader]);
        }
        catch (Exception ex) when (ex is MmException or ArgumentException or InvalidOperationException
                                       or NotSupportedException)
        {
            throw new SoundFileFault(
                Loc.Format(
                    "sound.problem.encoding",
                    "This file is a WAV in the {0} encoding, and Windows has no converter for it. "
                    + "Saving it again as ordinary PCM, or as an MP3, will work.",
                    format.Encoding.ToString()),
                ex);
        }
    }

    // The plain format an extensible header is describing, or null if it is not describing one this
    // can read directly.
    //
    // Read out of the raw extension bytes rather than through WaveFormatExtensible, which looks
    // like the type for this and is not: WaveFileReader returns a WaveFormatExtraData for any fmt
    // chunk with trailing bytes, and NAudio only ever builds a WaveFormatExtensible when something
    // asks it to construct one. A pattern match on that type therefore never fires for a file read
    // from disk, which is the whole population this cares about.
    //
    // The extension is the 22 bytes cbSize counts: two of valid bits, four of channel mask, then a
    // sixteen byte subformat GUID whose first two bytes are the format tag the file really holds.
    // Everything else is copied across unchanged, because the extension describes the same samples
    // and only names them differently.
    private static WaveFormat? Unwrap(WaveFormat format)
    {
        if (format.Encoding != WaveFormatEncoding.Extensible)
            return null;

        if (format is not WaveFormatExtraData extra || extra.ExtraData is not { Length: >= 8 } bytes)
            return null;

        var subFormat = (WaveFormatEncoding)BitConverter.ToUInt16(bytes, 6);

        if (subFormat is not (WaveFormatEncoding.Pcm or WaveFormatEncoding.IeeeFloat))
            return null;

        return WaveFormat.CreateCustomFormat(
            subFormat,
            format.SampleRate,
            format.Channels,
            format.AverageBytesPerSecond,
            format.BlockAlign,
            format.BitsPerSample);
    }

    // What SampleProviderConverters actually accepts, restated so an unreadable format is found
    // before it throws rather than after. NAudio raises a bare ArgumentException reading
    // "Unsupported source encoding" for anything outside this, which names neither the file nor
    // the encoding and is the reason this is asked in advance.
    private static bool ReadableAsSamples(WaveFormat format) => format.Encoding switch
    {
        WaveFormatEncoding.Pcm => format.BitsPerSample is 8 or 16 or 24 or 32,
        WaveFormatEncoding.IeeeFloat => format.BitsPerSample is 32 or 64,
        _ => false,
    };

    // Hands a reader's bytes on under a different WaveFormat. Used only where the two formats
    // describe the same samples, which is the whole of what WAVE_FORMAT_EXTENSIBLE adds over the
    // format it wraps: a channel mask, a valid-bits count and a GUID naming the encoding.
    private sealed class Retagged(IWaveProvider source, WaveFormat format) : IWaveProvider
    {
        public WaveFormat WaveFormat => format;

        public int Read(byte[] buffer, int offset, int count) => source.Read(buffer, offset, count);
    }

    // An exception carrying a message already written for the settings window.
    //
    // It exists because the message cannot be recovered from the exception type afterwards. NAudio
    // raises MmException when the conversion stream finds no codec, when the MP3 decoder is
    // missing, and when the output device will not open, and those three want different things
    // said about them. The throw site is the only place that knows which one it is.
    private sealed class SoundFileFault(string message, Exception? inner = null)
        : Exception(message, inner);

    // Into the mixer's format, then trimmed, then scaled. Volume last so the cap cannot be
    // applied to something already quietened and read as the file being short.
    private static ISampleProvider Convert(ISampleProvider sample, float volume)
    {
        sample = sample.WaveFormat.Channels switch
        {
            1 => new MonoToStereoSampleProvider(sample),
            MixerChannels => sample,

            // Surround files exist and there is no sensible fold-down to write here. Refused with
            // a message rather than played as whichever two channels happened to come first.
            _ => throw new SoundFileFault(Loc.Get(
                "sound.problem.channels",
                "That file has more than two channels, which cannot be played here. A mono or "
                + "stereo file will work.")),
        };

        if (sample.WaveFormat.SampleRate != MixerSampleRate)
            sample = new WdlResamplingSampleProvider(sample, MixerSampleRate);

        return new VolumeSampleProvider(new OffsetSampleProvider(sample) { Take = MaxLength })
        {
            Volume = volume,
        };
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
            WaveFormat.CreateIeeeFloatWaveFormat(MixerSampleRate, MixerChannels))
        {
            // Without this the mixer reports zero samples whenever nothing is playing, which a
            // WaveOutEvent reads as the end of the stream and stops on. It has to keep handing
            // back silence for the device to stay open between sounds.
            ReadFully = true,
        };

        mixed.MixerInputEnded += OnInputEnded;

        this.device = new WaveOutEvent { DesiredLatency = DesiredLatencyMs };
        this.device.Init(mixed);
        this.device.Play();

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
        SoundFileFault fault => fault.Message,

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
        this.problem = message;

        // Once per distinct message. A notification that keeps arriving with the same broken file
        // would otherwise write the same line into the log every few seconds.
        if (this.lastLogged == message)
            return;

        this.lastLogged = message;

        var detail = $"Sound file {path} [{Describe(format)}]: {message}";

        if (ex == null)
            Log.Warning(detail);
        else
            Log.Warning(ex, detail);
    }

    // NAudio's own ToString stops at the outer tag, which is exactly what was misleading here, so
    // an extensible file gets its subformat spelled out beside it.
    private static string Describe(WaveFormat? format)
    {
        if (format == null)
            return "format unknown, the file never opened";

        var text = $"{format.Encoding}, {format.BitsPerSample} bit, {format.SampleRate} Hz, "
                   + $"{format.Channels} ch";

        return Unwrap(format) is { } standard
            ? $"{text}, extensible over {standard.Encoding} {standard.BitsPerSample} bit"
            : text;
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
