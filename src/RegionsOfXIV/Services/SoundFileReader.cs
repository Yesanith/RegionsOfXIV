using System;
using System.IO;
using NAudio;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace RegionsOfXIV.Services;

// Turning a file on disk into samples in the one format the device runs at, and saying plainly
// what went wrong when it cannot.
//
// Split out of FileSoundPlayer for the same reason FontLimits was split out of FontService: none
// of this depends on a device existing. It is all static, it is where the awkward format cases
// live, and the tests reach ToPcm directly to check real files without opening any audio hardware
// at all. What is left in the player is the device, the lifetime and the one-at-a-time rule.
internal static class SoundFileReader
{
    // What the device runs at, and therefore what everything here converts to. 44.1 kHz stereo is
    // what nearly every file already is, so the resampler usually has nothing to do.
    public const int SampleRate = 44100;

    public const int Channels = 2;

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

    // An exception carrying a message already written for the settings window.
    //
    // It exists because the message cannot be recovered from the exception type afterwards. NAudio
    // raises MmException when the conversion stream finds no codec, when the MP3 decoder is
    // missing, and when the output device will not open, and those three want different things
    // said about them. The throw site is the only place that knows which one it is.
    internal sealed class SoundFileFault(string message, Exception? inner = null)
        : Exception(message, inner);

    // What the sample converters are handed, and everything that has to be closed with it.
    internal readonly record struct Decoded(IWaveProvider Provider, IDisposable[] Owned);

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
    public static WaveStream Open(string path)
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

    // Into the mixer's format, then trimmed, then scaled. Volume last so the cap cannot be
    // applied to something already quietened and read as the file being short.
    public static ISampleProvider Convert(ISampleProvider sample, float volume)
    {
        sample = sample.WaveFormat.Channels switch
        {
            1 => new MonoToStereoSampleProvider(sample),
            Channels => sample,

            // Surround files exist and there is no sensible fold-down to write here. Refused with
            // a message rather than played as whichever two channels happened to come first.
            _ => throw new SoundFileFault(Loc.Get(
                "sound.problem.channels",
                "That file has more than two channels, which cannot be played here. A mono or "
                + "stereo file will work.")),
        };

        if (sample.WaveFormat.SampleRate != SampleRate)
            sample = new WdlResamplingSampleProvider(sample, SampleRate);

        return new VolumeSampleProvider(new OffsetSampleProvider(sample) { Take = MaxLength })
        {
            Volume = volume,
        };
    }

    // NAudio's own ToString stops at the outer tag, which is exactly what was misleading here, so
    // an extensible file gets its subformat spelled out beside it.
    public static string Describe(WaveFormat? format)
    {
        if (format == null)
            return "format unknown, the file never opened";

        var text = $"{format.Encoding}, {format.BitsPerSample} bit, {format.SampleRate} Hz, "
                   + $"{format.Channels} ch";

        return Unwrap(format) is { } standard
            ? $"{text}, extensible over {standard.Encoding} {standard.BitsPerSample} bit"
            : text;
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
}
