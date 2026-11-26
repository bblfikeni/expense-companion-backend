using Companion.Extraction.Ocr;

namespace Companion.Api.Tests;

public sealed class TesseractOcrEngineTests
{
    [Fact]
    public void DefaultsToTesseractLanguageCombinationSyntax()
    {
        Assert.Equal("eng+afr", new TesseractOcrEngine().Languages);
    }

    [Fact]
    public void KeepsConfiguredTessdataDirectoryInsteadOfItsParent()
    {
        var path = Path.Combine(Path.GetTempPath(), "companion", "tessdata");

        Assert.Equal(Path.GetFullPath(path), TesseractOcrEngine.ResolveDataPath(path));
    }

    [Fact]
    public void PageAndWordConfidenceUseTheirDifferentScales()
    {
        Assert.Equal(0.95, TesseractOcrEngine.NormalizePageConfidence(0.95f), 2);
        Assert.Equal(0.95, TesseractOcrEngine.NormalizeWordConfidence(95f), 2);
    }
}