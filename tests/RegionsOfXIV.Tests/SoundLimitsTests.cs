using System.IO;
using RegionsOfXIV.Services;

namespace RegionsOfXIV.Tests;

// Everything that can be told about a sound file from its path, without decoding it.
//
// These write real files, because every answer here comes from the disk and a fake filesystem
// would be testing the fake. They are small and in the temp folder, and each one cleans up after
// itself.
//
// In the Localization collection because the messages come from Loc.
[Collection("Localization")]
public class SoundLimitsTests : IDisposable
{
    private readonly string folder =
        Path.Combine(Path.GetTempPath(), "RegionsOfXIV.SoundLimits." + Guid.NewGuid().ToString("N"));

    public SoundLimitsTests() => Directory.CreateDirectory(this.folder);

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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NothingChosenSaysSo(string? path)
    {
        Assert.Contains("chosen", SoundLimits.CustomSoundProblem(path));
    }

    [Theory]
    [InlineData(".wav")]
    [InlineData(".WAV")]
    [InlineData(".mp3")]
    [InlineData(".Mp3")]
    public void BothFormatsAreAcceptedInAnyCasing(string extension)
    {
        Assert.Null(SoundLimits.CustomSoundProblem(Write("chime" + extension, 64)));
    }

    // Rejected on the extension rather than by trying to decode it, so someone who points this at
    // an .ogg or a .flac is told what will work instead of hearing nothing.
    [Theory]
    [InlineData(".ogg")]
    [InlineData(".flac")]
    [InlineData(".txt")]
    [InlineData("")]
    public void OtherFormatsAreRefusedByName(string extension)
    {
        Assert.Contains(".wav", SoundLimits.CustomSoundProblem(Write("chime" + extension, 64)));
    }

    [Fact]
    public void AMissingFileSaysSo()
    {
        var path = Path.Combine(this.folder, "gone.wav");

        Assert.Contains("no file", SoundLimits.CustomSoundProblem(path));
    }

    // A directory answers Exists with false on FileInfo, so without its own check this would be
    // reported as a missing file, which sends someone looking for something they can see is there.
    [Fact]
    public void AFolderIsNamedAsAFolder()
    {
        var path = Path.Combine(this.folder, "sounds.wav");
        Directory.CreateDirectory(path);

        Assert.Contains("folder", SoundLimits.CustomSoundProblem(path));
    }

    [Fact]
    public void AnEmptyFileSaysSo()
    {
        Assert.Contains("empty", SoundLimits.CustomSoundProblem(Write("silence.wav", 0)));
    }

    [Fact]
    public void SomethingFarTooLargeIsLeftAlone()
    {
        Assert.Contains("larger", SoundLimits.CustomSoundProblem(
            Write("film.wav", (32L * 1024 * 1024) + 1)));
    }

    // Contents are never looked at here. A file that is not really a WAV gets as far as the
    // decoder, which is the only thing that can tell, and reports from there.
    [Fact]
    public void ContentsAreNotInspected()
    {
        var path = Path.Combine(this.folder, "notreally.wav");
        File.WriteAllText(path, "this is not a wave file");

        Assert.Null(SoundLimits.CustomSoundProblem(path));
    }

    private string Write(string name, long bytes)
    {
        var path = Path.Combine(this.folder, name);

        using var file = File.Create(path);
        file.SetLength(bytes);

        return path;
    }
}
