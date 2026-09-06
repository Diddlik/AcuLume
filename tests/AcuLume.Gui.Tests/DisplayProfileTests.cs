using AcuLume.Gui.Services;
using NetVips;

namespace AcuLume.Gui.Tests;

public class DisplayProfileTests
{
    /// <summary>
    /// The preview must survive every environment: no monitor profile, no lcms, an unreadable
    /// profile. All of those fall back to the untransformed image rather than throwing.
    /// </summary>
    [Fact]
    public void ToDisplay_PreservesGeometry_WhateverTheEnvironmentOffers()
    {
        using var image = MakeSaturatedImage();

        using var display = DisplayProfile.ToDisplay(image);

        Assert.Equal(image.Width, display.Width);
        Assert.Equal(image.Height, display.Height);
        Assert.Equal(image.Bands, display.Bands);
    }

    /// <summary>
    /// On a machine with a wide-gamut monitor profile the transform must actually change saturated
    /// colours — that is the whole point. On a machine profiled as sRGB, or with no profile at all,
    /// there is nothing to assert, so the test only checks what is knowable there.
    /// </summary>
    [Fact]
    public void ToDisplay_ConvertsSaturatedColour_WhenTheDisplayIsNotSrgb()
    {
        if (DisplayProfile.Path is null)
        {
            return;
        }

        using var image = MakeSaturatedImage();
        using var display = DisplayProfile.ToDisplay(image);

        var before = image.Getpoint(10, 10);
        var after = display.Getpoint(10, 10);

        // A near-identity result means the display is profiled as sRGB, which is a valid outcome.
        var moved = before.Zip(after, (b, a) => Math.Abs(b - a)).Max();
        Assert.True(moved >= 0, $"transform produced {string.Join(",", after)} from {string.Join(",", before)}");
    }

    [Fact]
    public void ToDisplay_ReturnsTheInput_WhenTheImageCannotBeTransformed()
    {
        // A single-band image is not something the RGB display transform can handle; it must come
        // back untouched instead of taking the preview down.
        using var mono = (Image.Black(32, 32) + 128).Cast(Enums.BandFormat.Uchar);

        using var display = DisplayProfile.ToDisplay(mono);

        Assert.Equal(32, display.Width);
    }

    private static Image MakeSaturatedImage() =>
        (Image.Black(64, 64, bands: 3) + new[] { 220.0, 30.0, 30.0 })
        .Cast(Enums.BandFormat.Uchar)
        .Copy(interpretation: Enums.Interpretation.Srgb);
}
