using System.IO;
using NAudio.Wave;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.Tests;

// Getting a WAV into a form the sample converters accept.
//
// The case this exists for: a file downloaded from a sound library was refused with NAudio's
// "Unsupported source encoding" while playing in every other program on the machine. It was
// ordinary 24 bit stereo PCM wrapped in WAVE_FORMAT_EXTENSIBLE, which is a container tag rather
// than an encoding, and the converters read only the outer tag. Nothing about it was damaged.
//
// The files are written here by hand rather than checked in, because the whole question is what
// bytes are in the header and a fixture would hide that.
//
// In the Localization collection because a refusal comes back with a message from Loc.
[Collection("Localization")]
public class SoundFormatTests : IDisposable
{
    private const int Pcm = 0x0001;
    private const int Extensible = 0xFFFE;

    private readonly string folder =
        Path.Combine(Path.GetTempPath(), "RegionsOfXIV.SoundFormat." + Guid.NewGuid().ToString("N"));

    public SoundFormatTests() => Directory.CreateDirectory(this.folder);

    public void Dispose()
    {
        try
        {
            Directory.Delete(this.folder, true);
        }
        catch (IOException)
        {
            // A leftover temp folder is not worth failing a test run over.
        }
    }

    // The exact shape of the file that failed: tag 0xFFFE, 24 bit, 44100 Hz, stereo, subformat
    // PCM. If this ever throws again, the extensible unwrapping has been lost.
    [Fact]
    public void AnExtensiblePcmFileIsReadable()
    {
        using var reader = new WaveFileReader(WriteWav(Extensible, 24, 44100, 2));

        var decoded = SoundFileReader.ToPcm(reader);

        Assert.NotNull(decoded.Provider.ToSampleProvider());
    }

    // Retagged rather than converted, so the audio is passed through untouched. Anything else
    // would mean ACM had been asked to do a conversion that changes nothing.
    [Fact]
    public void AnExtensiblePcmFileIsRetaggedRatherThanConverted()
    {
        using var reader = new WaveFileReader(WriteWav(Extensible, 24, 44100, 2));

        var decoded = SoundFileReader.ToPcm(reader);

        Assert.Equal(WaveFormatEncoding.Pcm, decoded.Provider.WaveFormat.Encoding);
        Assert.Equal(24, decoded.Provider.WaveFormat.BitsPerSample);
        Assert.Equal(reader, Assert.Single(decoded.Owned));
    }

    // A plain file takes neither path. The reader itself is handed on, which is what it did before
    // any of this existed.
    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    public void PlainPcmIsHandedOnUnchanged(int bits)
    {
        using var reader = new WaveFileReader(WriteWav(Pcm, bits, 44100, 2));

        var decoded = SoundFileReader.ToPcm(reader);

        Assert.Same(reader, decoded.Provider);
        Assert.Equal(reader, Assert.Single(decoded.Owned));
    }

    [Fact]
    public void AMonoFileIsHandedOnUnchanged()
    {
        using var reader = new WaveFileReader(WriteWav(Pcm, 16, 22050, 1));

        Assert.Same(reader, SoundFileReader.ToPcm(reader).Provider);
    }

    // Writes a RIFF/WAVE container with the given format chunk and a short run of silence.
    //
    // Built here rather than through NAudio's own writer because NAudio will not write a 0xFFFE
    // header for a format it considers plain, and that header is the entire subject.
    private string WriteWav(int tag, int bits, int rate, int channels)
    {
        var path = Path.Combine(this.folder, $"{tag:x4}-{bits}-{rate}-{channels}.wav");

        var blockAlign = channels * (bits / 8);
        var samples = rate / 10;
        var data = new byte[samples * blockAlign];
        // cbSize plus the 22 bytes it counts: a valid-bits count, a channel mask and the subformat
        // GUID. The fmt chunk of an extensible file is 40 bytes rather than 16.
        var extension = tag == Extensible ? 24 : 0;

        using var file = File.Create(path);
        using var write = new BinaryWriter(file);

        write.Write("RIFF"u8);
        write.Write(4 + 8 + 16 + extension + 8 + data.Length);
        write.Write("WAVE"u8);

        write.Write("fmt "u8);
        write.Write(16 + extension);
        write.Write((short)tag);
        write.Write((short)channels);
        write.Write(rate);
        write.Write(rate * blockAlign);
        write.Write((short)blockAlign);
        write.Write((short)bits);

        if (tag == Extensible)
        {
            write.Write((short)22);
            write.Write((short)bits);
            write.Write(channels == 2 ? 3 : 4);

            // KSDATAFORMAT_SUBTYPE_PCM: the format tag in the first two bytes, then the fixed
            // 00000000-1000-8000-00AA00389B71 tail every WAVE subformat GUID shares.
            write.Write((short)Pcm);
            write.Write(new byte[]
            {
                0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00,
                0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71,
            });
        }

        write.Write("data"u8);
        write.Write(data.Length);
        write.Write(data);

        return path;
    }
}
