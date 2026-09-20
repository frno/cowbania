using System.Buffers.Binary;
using Cowbania.Host.Diagnostics;
using Microsoft.Xna.Framework.Graphics;

namespace Cowbania.Host.Presentation.Rendering;

internal sealed class TitleFilmPlayer : IDisposable
{
    private const uint Magic = 0x46565743; // CWVF

    private readonly GraphicsDevice graphicsDevice;
    private readonly FileStream stream;
    private readonly long[] frameOffsets;
    private readonly int[] frameLengths;
    private readonly int framesPerSecond;
    private Texture2D? currentFrame;
    private int currentFrameIndex = -1;
    private double elapsedSeconds;

    private TitleFilmPlayer(
        GraphicsDevice graphicsDevice,
        FileStream stream,
        int width,
        int height,
        int framesPerSecond,
        long[] frameOffsets,
        int[] frameLengths)
    {
        this.graphicsDevice = graphicsDevice;
        this.stream = stream;
        Width = width;
        Height = height;
        this.framesPerSecond = framesPerSecond;
        this.frameOffsets = frameOffsets;
        this.frameLengths = frameLengths;
    }

    public int Width { get; }
    public int Height { get; }
    public int FrameCount => frameOffsets.Length;
    public double DurationSeconds => (double)FrameCount / framesPerSecond;
    public Texture2D? CurrentFrame => currentFrame;

    public static TitleFilmPlayer? TryLoad(GraphicsDevice graphicsDevice, string path)
    {
        if (!File.Exists(path))
        {
            RuntimeLog.Warn($"title film missing path=\"{path}\"; falling back to black background");
            return null;
        }
        var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            Span<byte> header = stackalloc byte[20];
            stream.ReadExactly(header);
            if (BinaryPrimitives.ReadUInt32LittleEndian(header) != Magic)
                throw new InvalidDataException("Title film has an invalid CWVF header.");
            var width = BinaryPrimitives.ReadInt32LittleEndian(header[4..]);
            var height = BinaryPrimitives.ReadInt32LittleEndian(header[8..]);
            var fps = BinaryPrimitives.ReadInt32LittleEndian(header[12..]);
            var count = BinaryPrimitives.ReadInt32LittleEndian(header[16..]);
            if (width <= 0 || height <= 0 || fps <= 0 || count <= 0)
                throw new InvalidDataException("Title film metadata must be positive.");

            var offsets = new long[count];
            var lengths = new int[count];
            Span<byte> lengthBytes = stackalloc byte[4];
            for (var index = 0; index < count; index++)
            {
                stream.ReadExactly(lengthBytes);
                lengths[index] = BinaryPrimitives.ReadInt32LittleEndian(lengthBytes);
                if (lengths[index] <= 0 || stream.Position + lengths[index] > stream.Length)
                    throw new InvalidDataException($"Title film frame {index} has an invalid length.");
                offsets[index] = stream.Position;
                stream.Position += lengths[index];
            }

            var player = new TitleFilmPlayer(graphicsDevice, stream, width, height, fps, offsets, lengths);
            player.DecodeFrame(0);
            RuntimeLog.Info($"title film loaded frames={count} fps={fps} durationSeconds={player.DurationSeconds:F1}");
            return player;
        }
        catch
        {
            stream.Dispose();
            RuntimeLog.Warn($"title film invalid path=\"{path}\"; falling back to black background");
            return null;
        }
    }

    public void Update(float elapsed)
    {
        elapsedSeconds = (elapsedSeconds + Math.Max(0, elapsed)) % DurationSeconds;
        DecodeFrame((int)(elapsedSeconds * framesPerSecond) % FrameCount);
    }

    private void DecodeFrame(int index)
    {
        if (index == currentFrameIndex)
            return;
        stream.Position = frameOffsets[index];
        var bytes = new byte[frameLengths[index]];
        stream.ReadExactly(bytes);
        using var frame = new MemoryStream(bytes, writable: false);
        var next = Texture2D.FromStream(graphicsDevice, frame);
        currentFrame?.Dispose();
        currentFrame = next;
        currentFrameIndex = index;
    }

    public void Dispose()
    {
        currentFrame?.Dispose();
        stream.Dispose();
    }
}