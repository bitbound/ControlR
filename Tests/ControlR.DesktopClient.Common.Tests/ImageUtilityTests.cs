using System.Diagnostics;
using System.Drawing;
using ControlR.DesktopClient.Common.Services;
using SkiaSharp;

namespace ControlR.DesktopClient.Common.Tests;

public class ImageUtilityTests(ITestOutputHelper testOutput)
{
    private readonly ImageUtility _imageUtility = new();
    private readonly ITestOutputHelper _testOutput = testOutput;

    [Fact]
    public void ClampToGridSections_AreaAtGridBoundary_ClampsCorrectly()
    {
        var bitmapSize = new Size(1920, 1080);
        var sectionWidth = 1920 / 4;
        var sectionHeight = 1080 / 2;
        var changedAreas = new SKRect[] { new(0, 0, sectionWidth, sectionHeight) };

        var result = _imageUtility.ClampToGridSections(bitmapSize, changedAreas, gridColumns: 4, gridRows: 2);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        var area = result.Value[0];
        Assert.Equal(0, area.Left);
        Assert.Equal(0, area.Top);
        Assert.Equal(sectionWidth, area.Right);
        Assert.Equal(sectionHeight, area.Bottom);
    }

    [Fact]
    public void ClampToGridSections_AreaSpanningMultipleSections_ReturnsMultipleClampedAreas()
    {
        var bitmapSize = new Size(1920, 1080);
        var sectionWidth = 1920 / 4;
        var sectionHeight = 1080 / 2;
        var changedAreas = new SKRect[] { new(sectionWidth - 50, sectionHeight - 50, sectionWidth + 50, sectionHeight + 50) };

        var result = _imageUtility.ClampToGridSections(bitmapSize, changedAreas, gridColumns: 4, gridRows: 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value.Length);
    }

    [Fact]
    public void ClampToGridSections_EmptyAreaInArray_IgnoresEmptyArea()
    {
        var bitmapSize = new Size(1920, 1080);
        var changedAreas = new SKRect[] { SKRect.Empty, new(100, 100, 200, 200) };

        var result = _imageUtility.ClampToGridSections(bitmapSize, changedAreas);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
    }

    [Fact]
    public void ClampToGridSections_EmptyAreas_ReturnsEmptyArray()
    {
        var bitmapSize = new Size(1920, 1080);
        var changedAreas = Array.Empty<SKRect>();

        var result = _imageUtility.ClampToGridSections(bitmapSize, changedAreas);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public void ClampToGridSections_HighFrequency_MeasuresPerformance()
    {
        var bitmapSize = new Size(1920, 1080);
        var sectionWidth = 1920 / 4;
        var sectionHeight = 1080 / 2;
        
        var changedAreas = new SKRect[]
        {
            new(100, 100, 300, 300),
            new(sectionWidth - 50, sectionHeight - 50, sectionWidth + 50, sectionHeight + 50),
            new(sectionWidth * 2, sectionHeight, sectionWidth * 3 - 100, sectionHeight + 200),
            new(1500, 800, 1800, 1000)
        };

        const int iterations = 10000;
        var sw = Stopwatch.StartNew();

        for (var i = 0; i < iterations; i++)
        {
            _ = _imageUtility.ClampToGridSections(bitmapSize, changedAreas, gridColumns: 4, gridRows: 2);
        }

        sw.Stop();
        var totalMs = sw.Elapsed.TotalMilliseconds;
        var avgMicroseconds = (totalMs * 1000) / iterations;
        var opsPerSecond = iterations / totalMs * 1000;

        _testOutput.WriteLine($"ClampToGridSections Performance:");
        _testOutput.WriteLine($"  Iterations: {iterations}");
        _testOutput.WriteLine($"  Total time: {totalMs:F2} ms");
        _testOutput.WriteLine($"  Average: {avgMicroseconds:F3} µs/op");
        _testOutput.WriteLine($"  Throughput: {opsPerSecond:F0} ops/sec");
    }

    [Fact]
    public void ClampToGridSections_InvalidBitmapSize_ReturnsFailure()
    {
        var bitmapSize = new Size(0, 1080);
        var changedAreas = new SKRect[] { new(100, 100, 200, 200) };

        var result = _imageUtility.ClampToGridSections(bitmapSize, changedAreas);

        Assert.False(result.IsSuccess);
        Assert.Contains("Bitmap dimensions are smaller than the grid size", result.Reason);
    }

    [Fact]
    public void ClampToGridSections_LargeBitmap_MeasuresPerformance()
    {
        var bitmapSize = new Size(3840, 2160);
        
        var changedAreas = new SKRect[]
        {
            new(100, 100, 500, 500),
            new(1000, 1500, 2000, 2000),
            new(3000, 100, 3500, 800),
            new(500, 1800, 1500, 2100)
        };

        const int iterations = 5000;
        var sw = Stopwatch.StartNew();

        for (var i = 0; i < iterations; i++)
        {
            _ = _imageUtility.ClampToGridSections(bitmapSize, changedAreas, gridColumns: 4, gridRows: 2);
        }

        sw.Stop();
        var totalMs = sw.Elapsed.TotalMilliseconds;
        var avgMicroseconds = (totalMs * 1000) / iterations;
        var opsPerSecond = iterations / totalMs * 1000;

        _testOutput.WriteLine($"ClampToGridSections Large Bitmap Performance (3840x2160):");
        _testOutput.WriteLine($"  Iterations: {iterations}");
        _testOutput.WriteLine($"  Total time: {totalMs:F2} ms");
        _testOutput.WriteLine($"  Average: {avgMicroseconds:F3} µs/op");
        _testOutput.WriteLine($"  Throughput: {opsPerSecond:F0} ops/sec");

        Assert.True(avgMicroseconds < 200, $"Expected less than 200 µs/op, got {avgMicroseconds:F3} µs/op");
    }

    [Fact]
    public void ClampToGridSections_ManyAreas_MeasuresPerformance()
    {
        var bitmapSize = new Size(1920, 1080);
        
        var random = new Random(42);
        var changedAreas = new SKRect[100];
        for (var i = 0; i < changedAreas.Length; i++)
        {
            var x = random.Next(0, 1800);
            var y = random.Next(0, 1000);
            var w = random.Next(50, 200);
            var h = random.Next(50, 200);
            changedAreas[i] = new SKRect(x, y, x + w, y + h);
        }

        const int iterations = 1000;
        var sw = Stopwatch.StartNew();

        for (var i = 0; i < iterations; i++)
        {
            _ = _imageUtility.ClampToGridSections(bitmapSize, changedAreas, gridColumns: 4, gridRows: 2);
        }

        sw.Stop();
        var totalMs = sw.Elapsed.TotalMilliseconds;
        var avgMicroseconds = (totalMs * 1000) / iterations;
        var opsPerSecond = iterations / totalMs * 1000;

        _testOutput.WriteLine($"ClampToGridSections Many Areas Performance (100 areas):");
        _testOutput.WriteLine($"  Iterations: {iterations}");
        _testOutput.WriteLine($"  Total time: {totalMs:F2} ms");
        _testOutput.WriteLine($"  Average: {avgMicroseconds:F3} µs/op");
        _testOutput.WriteLine($"  Throughput: {opsPerSecond:F0} ops/sec");

        Assert.True(avgMicroseconds < 500, $"Expected less than 500 µs/op, got {avgMicroseconds:F3} µs/op");
    }

    [Fact]
    public void ClampToGridSections_MultipleChangedAreas_ReturnsExpectedClampedAreas()
    {
        var bitmapSize = new Size(1920, 1080);
        var sectionWidth = 1920 / 4;
        var sectionHeight = 1080 / 2;

        // First changed area spans two sections in the top row.
        var area1 = new SKRect(100, 100, 600, 300);

        // Second changed area spans the last column and crosses the row boundary.
        var area2 = new SKRect(1800, 500, 2000, 1100);

        var changedAreas = new[] { area1, area2 };

        var result = _imageUtility.ClampToGridSections(bitmapSize, changedAreas, gridColumns: 4, gridRows: 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value.Length);

        // area1 should produce two clamped rectangles in the top row.
        Assert.Equal(new SKRect(100, 100, sectionWidth, 300), result.Value[0]);
        Assert.Equal(new SKRect(sectionWidth, 100, 600, 300), result.Value[1]);

        // area2 should produce one clamped rectangle in the top-right section and one in the bottom-right section.
        Assert.Equal(new SKRect(1800, 500, 1920, sectionHeight), result.Value[2]);
        Assert.Equal(new SKRect(1800, sectionHeight, 1920, 1080), result.Value[3]);
    }

    [Fact]
    public void ClampToGridSections_MultipleComplexChangedAreas_ReturnsExpectedClampedAreas()
    {
        var bitmapSize = new Size(1920, 1080);
        var sectionWidth = 1920 / 4;
        var sectionHeight = 1080 / 2;

        // One area spans multiple columns and rows.
        var spanningArea = new SKRect(400, 100, 900, 600);

        // One area extends outside the bitmap bounds (should clamp to edges).
        var outOfBoundsArea = new SKRect(-50, -50, 2000, 1200);

        var changedAreas = new[] { spanningArea, outOfBoundsArea };

        var result = _imageUtility.ClampToGridSections(bitmapSize, changedAreas, gridColumns: 4, gridRows: 2);

        Assert.True(result.IsSuccess);

        // spanningArea should produce 4 areas (2 columns x 2 rows)
        var expectedSpanning = new[]
        {
            new SKRect(400, 100, sectionWidth, 540),
            new SKRect(sectionWidth, 100, 900, 540),
            new SKRect(400, 540, sectionWidth, 600),
            new SKRect(sectionWidth, 540, 900, 600)
        };

        // outOfBoundsArea should produce 8 areas (full grid) because it covers entire bitmap.
        var expectedFullGrid = new List<SKRect>();
        for (var row = 0; row < 2; row++)
        {
            for (var col = 0; col < 4; col++)
            {
                var left = col * sectionWidth;
                var top = row * sectionHeight;
                var right = col == 3 ? 1920 : (col + 1) * sectionWidth;
                var bottom = row == 1 ? 1080 : (row + 1) * sectionHeight;
                expectedFullGrid.Add(new SKRect(left, top, right, bottom));
            }
        }

        Assert.Equal(expectedSpanning.Length + expectedFullGrid.Count, result.Value.Length);

        foreach (var expected in expectedSpanning.Concat(expectedFullGrid))
        {
            Assert.Contains(expected, result.Value);
        }
    }

    [Fact]
    public void ClampToGridSections_SingleAreaSpanningOneSection_ReturnsClampedArea()
    {
        var bitmapSize = new Size(1920, 1080);
        var changedAreas = new SKRect[] { new(100, 100, 200, 200) };

        var result = _imageUtility.ClampToGridSections(bitmapSize, changedAreas, gridColumns: 4, gridRows: 2);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        var area = result.Value[0];
        Assert.Equal(100, area.Left);
        Assert.Equal(100, area.Top);
        Assert.Equal(200, area.Right);
        Assert.Equal(200, area.Bottom);
    }
}
