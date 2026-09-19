using System.Buffers.Binary;
using Microsoft.Xna.Framework.Audio;

namespace Cowbania.Host.Audio;

internal readonly record struct ManagedPcmWav(byte[] PcmData, int SampleRate, AudioChannels Channels)
{
    private const uint Riff = 0x46464952;
    private const uint Wave = 0x45564157;
    private const uint Format = 0x20746D66;
    private const uint Data = 0x61746164;

    public static ManagedPcmWav Read(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12 ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes) != Riff ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]) != Wave)
            throw new InvalidDataException("The audio file is not a RIFF/WAVE file.");

        var riffSize = BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..]);
        if (riffSize > bytes.Length - 8)
            throw new InvalidDataException("The RIFF container length exceeds the file length.");

        ushort? channelCount = null;
        int? sampleRate = null;
        ushort? blockAlign = null;
        byte[]? pcmData = null;
        var offset = 12;
        while (offset <= bytes.Length - 8)
        {
            var chunkId = BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
            var chunkLength = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(offset + 4)..]);
            var chunkStart = offset + 8;
            if (chunkLength > bytes.Length - chunkStart)
                throw new InvalidDataException("A WAV chunk length exceeds the file length.");
            var chunk = bytes.Slice(chunkStart, checked((int)chunkLength));
            if (chunkId == Format)
            {
                if (chunk.Length < 16)
                    throw new InvalidDataException("The WAV format chunk is incomplete.");
                var encoding = BinaryPrimitives.ReadUInt16LittleEndian(chunk);
                var parsedChannels = BinaryPrimitives.ReadUInt16LittleEndian(chunk[2..]);
                var rate = BinaryPrimitives.ReadUInt32LittleEndian(chunk[4..]);
                var alignment = BinaryPrimitives.ReadUInt16LittleEndian(chunk[12..]);
                var bitsPerSample = BinaryPrimitives.ReadUInt16LittleEndian(chunk[14..]);
                if (encoding != 1 || bitsPerSample != 16)
                    throw new InvalidDataException("Only uncompressed 16-bit PCM WAV audio is supported.");
                if (parsedChannels is not (1 or 2))
                    throw new InvalidDataException("Only mono or stereo WAV audio is supported.");
                if (rate is 0 or > int.MaxValue)
                    throw new InvalidDataException("The WAV sample rate is invalid.");
                if (alignment != parsedChannels * 2)
                    throw new InvalidDataException("The WAV block alignment is invalid.");
                channelCount = parsedChannels;
                sampleRate = (int)rate;
                blockAlign = alignment;
            }
            else if (chunkId == Data)
                pcmData = chunk.ToArray();

            var nextOffset = (long)chunkStart + chunkLength + (chunkLength & 1);
            if (nextOffset > bytes.Length)
                throw new InvalidDataException("The padded WAV chunk length exceeds the file length.");
            offset = (int)nextOffset;
        }

        if (channelCount is null || sampleRate is null || blockAlign is null)
            throw new InvalidDataException("The WAV format chunk is missing.");
        if (pcmData is null || pcmData.Length == 0)
            throw new InvalidDataException("The WAV data chunk is missing or empty.");
        if (pcmData.Length % blockAlign.Value != 0)
            throw new InvalidDataException("The WAV sample data is not block aligned.");
        return new ManagedPcmWav(
            pcmData,
            sampleRate.Value,
            channelCount == 1 ? AudioChannels.Mono : AudioChannels.Stereo);
    }
}
