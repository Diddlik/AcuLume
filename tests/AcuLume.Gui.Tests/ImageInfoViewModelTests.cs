using AcuLume.Gui.ViewModels;

namespace AcuLume.Gui.Tests;

public class ImageInfoViewModelTests
{
    [Theory]
    [InlineData("FocalLength", "85/1", "85 mm")]
    [InlineData("FocalLength", "24", "24 mm")]
    [InlineData("FNumber", "22/10", "f/2.2")]
    [InlineData("ExposureTime", "1/125", "1/125 s")]
    [InlineData("ExposureTime", "2/1", "2 s")]
    [InlineData("DateTimeOriginal", "2023:07:09 09:09:45", "2023-07-09 09:09:45")]
    public void FormatExif_RendersRationalsAsPhotographersReadThem(string tag, string raw, string expected) =>
        Assert.Equal(expected, ImageInfoViewModel.FormatExif(tag, raw));

    [Theory]
    [InlineData("FocalLength", "unknown")]
    [InlineData("FNumber", "1/0")]
    [InlineData("Model", "Canon EOS R6")]
    public void FormatExif_PassesThroughWhatItCannotParse(string tag, string raw) =>
        Assert.Equal(raw, ImageInfoViewModel.FormatExif(tag, raw));
}
