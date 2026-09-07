using System.Buffers.Binary;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Dororong.App.Controls;

namespace Dororong.App.Tests.Controls;

public sealed class HeadTiltComparisonTests
{
    [Fact]
    public void Same_pose_comparison_changes_only_sampling_and_is_identical_at_zero_angle() => CheekProductTests.Sta(() =>
    {
        var destination = Environment.GetEnvironmentVariable("DORORONG_TILT_PROOF");
        if (destination is not null) { Assert.False(Directory.Exists(destination)); Directory.CreateDirectory(destination); }
        var records = new List<object>(); var paths = new List<string>();
        foreach (var mirror in new[] { false, true })
        {
            var (presenter, pet, direct) = HeadTiltSamplingTests.Setup(mirror);
            for (var frame = 0; frame <= 60; frame++)
            {
                // A controlled angular sweep, not a claim about live mouse timing.
                var requested = frame is 0 or 30 or 60 ? 0 : 18 * Math.Sin(frame * Math.PI / 30);
                presenter.Render(pet, direct with { HeadSwingDegrees = requested }, TimeSpan.Zero);
                CheekProductTests.Layout(presenter);
                var image = (Image)presenter.FindName("DororongImage");
                var source = image.Source;
                var sourceHash = Hash((BitmapSource)source);
                var transform = image.TransformToAncestor(presenter);
                var pin = transform.Transform(new Point(48.5, 12.25));
                var mode = RenderOptions.GetBitmapScalingMode(image);
                var angle = ((RotateTransform)presenter.FindName("BodyRotateTransform")).Angle;
                var applied = HeadTiltSamplingTests.Snapshot(presenter);
                RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.NearestNeighbor);
                var previous = HeadTiltSamplingTests.Snapshot(presenter);
                RenderOptions.SetBitmapScalingMode(image, mode);
                Assert.Same(source, image.Source);
                Assert.Equal(sourceHash, Hash((BitmapSource)image.Source));
                Assert.Equal(pin, image.TransformToAncestor(presenter).Transform(new Point(48.5, 12.25)));
                var oldPixels = Pixels(previous); var newPixels = Pixels(applied);
                var changed = Enumerable.Range(0, 144 * 144).Count(i => !oldPixels.AsSpan(i * 4, 4).SequenceEqual(newPixels.AsSpan(i * 4, 4)));
                if (angle == 0) Assert.Equal(0, changed); else Assert.True(changed > 0);
                records.Add(new { mirror, frame, requestedAngle = requested, actualAngle = angle, mode = mode.ToString(), sourceSha256 = sourceHash, changedPixels = changed, sameSourceAndTransform = true });
                if (destination is null) continue;
                var pair = Pair(previous, applied, 1);
                var path = Path.Combine(destination, $"pair-{(mirror ? "left" : "right")}-{frame:D2}.png");
                Save(path, pair);
                if (!mirror) paths.Add(path);
                if (frame == 8)
                {
                    Save(Path.Combine(destination, $"old-{(mirror ? "left" : "right")}-native.png"), previous);
                    Save(Path.Combine(destination, $"new-{(mirror ? "left" : "right")}-native.png"), applied);
                    Save(Path.Combine(destination, $"comparison-{(mirror ? "left" : "right")}-4x.png"), Pair(previous, applied, 4));
                }
            }
        }
        if (destination is not null)
        {
            File.WriteAllText(Path.Combine(destination, "comparison-facts.json"), JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true }));
            WriteApng(Path.Combine(destination, "old-left-new-right-motion.png"), paths);
        }
    });

    private static BitmapSource Pair(BitmapSource previous, BitmapSource applied, int zoom)
    {
        // Compose at physical144px first. Enlargement is nearest only for review.
        var visual = new DrawingVisual(); RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.NearestNeighbor);
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(245, 245, 245)), null, new Rect(0, 0, 288 * zoom, 144 * zoom));
            drawing.DrawImage(previous, new Rect(0, 0, 144 * zoom, 144 * zoom));
            drawing.DrawImage(applied, new Rect(144 * zoom, 0, 144 * zoom, 144 * zoom));
        }
        var bitmap = new RenderTargetBitmap(288 * zoom, 144 * zoom, 96, 96, PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); return bitmap;
    }
    private static byte[] Pixels(BitmapSource source)
    {
        var stride = source.PixelWidth * 4; var pixels = new byte[stride * source.PixelHeight]; source.CopyPixels(pixels, stride, 0); return pixels;
    }
    private static string Hash(BitmapSource source) => Convert.ToHexString(SHA256.HashData(Pixels(source)));
    private static void Save(string path, BitmapSource source)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source)); using var output = File.Create(path); encoder.Save(output);
    }

    // Lossless full-frame APNG packaging of already-rendered PNGs. No re-rasterizing.
    private static void WriteApng(string path, List<string> frames)
    {
        using var output = File.Create(path); output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }); uint sequence = 0;
        for (var n = 0; n < frames.Count; n++)
        {
            var bytes = File.ReadAllBytes(frames[n]); var chunks = new List<(string Name, byte[] Data)>();
            for (var at = 8; at < bytes.Length;)
            {
                var size = (int)BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(at, 4));
                chunks.Add((Encoding.ASCII.GetString(bytes, at + 4, 4), bytes.AsSpan(at + 8, size).ToArray())); at += size + 12;
            }
            var header = chunks.Single(c => c.Name == "IHDR").Data;
            Assert.Equal(8, header[8]); Assert.Equal(6, header[9]);
            if (n == 0) { Chunk(output, "IHDR", header); var actl = new byte[8]; U32(actl, 0, (uint)frames.Count); Chunk(output, "acTL", actl); }
            var control = new byte[26]; U32(control, 0, sequence++); Array.Copy(header, 0, control, 4, 8);
            BinaryPrimitives.WriteUInt16BigEndian(control.AsSpan(20, 2), (ushort)(n == frames.Count - 1 ? 320 : 32));
            BinaryPrimitives.WriteUInt16BigEndian(control.AsSpan(22, 2), 1000); Chunk(output, "fcTL", control);
            foreach (var chunk in chunks.Where(c => c.Name == "IDAT"))
                if (n == 0) Chunk(output, "IDAT", chunk.Data);
                else { var data = new byte[chunk.Data.Length + 4]; U32(data, 0, sequence++); Array.Copy(chunk.Data, 0, data, 4, chunk.Data.Length); Chunk(output, "fdAT", data); }
        }
        Chunk(output, "IEND", []);
    }
    private static void U32(byte[] data, int offset, uint value) => BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(offset, 4), value);
    private static void Chunk(Stream stream, string type, byte[] data)
    {
        var name = Encoding.ASCII.GetBytes(type); var size = new byte[4]; U32(size, 0, (uint)data.Length); stream.Write(size); stream.Write(name); stream.Write(data);
        uint crc = 0xffffffff; foreach (var b in name.Concat(data)) { crc ^= b; for (var j = 0; j < 8; j++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xedb88320 : crc >> 1; }
        U32(size, 0, crc ^ 0xffffffff); stream.Write(size);
    }
}
