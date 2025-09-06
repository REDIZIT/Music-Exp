using System.Collections.Generic;
using System.IO;
using NAudio.Wave;
using UnityEngine;

public static class MP3EFormatter
{
    public static void MP3_to_MP3E(string MP3FilePath, string outputMP3EFilePath)
    {
        byte[] mp3Bytes = File.ReadAllBytes(MP3FilePath);
        byte[] rencoded = Reencode(mp3Bytes);
        File.WriteAllBytes(outputMP3EFilePath, rencoded);

        Debug.Log("Done");
    }

    private static byte[] Reencode(byte[] mp3Bytes)
    {
        using MemoryStream stream = new(mp3Bytes);
        Mp3Frame frame = Mp3Frame.LoadFromStream(stream);

        // Debug.Log("frame: " + frame.SampleCount);
        // Debug.Log("SampleRate: " + frame.SampleRate);

        MP3EFile file = new();
        file.header = new()
        {
            sampleRate = frame.SampleRate,
            channelMode = (byte)(frame.ChannelMode == ChannelMode.Mono ? 1 : 2),
            frameLength = frame.FrameLength,
            bitRate = frame.BitRate
        };

        var waveFormat = new Mp3WaveFormat(frame.SampleRate, frame.ChannelMode == ChannelMode.Mono ? 1 : 2, frame.FrameLength, frame.BitRate);
        AcmMp3FrameDecompressor decompressor = new AcmMp3FrameDecompressor(waveFormat);

        stream.Position = 0;


        List<MP3ESegmentScheme> schemes = new();
        List<MP3ESegment> segments = new();

        byte[] samplesBuffer = new byte[16384];
        while (true)
        {
            long a = stream.Position;
            frame = Mp3Frame.LoadFromStream(stream);
            int read = (int) (stream.Position - a);


            if (frame == null)
            {
                // Debug.Log("End with read = " + read);
                break;
            }

            int bytesDecompressed = decompressor.DecompressFrame(frame, samplesBuffer, 0);

            if (bytesDecompressed > 0)
            {
                file.header.totalSampleCount += frame.SampleCount;

                MP3ESegmentScheme scheme = new()
                {
                    sizeInBytes = (ushort)frame.RawData.Length,
                    deltaTimeInMilliseconds = (ushort)(frame.SampleCount * 1000 / (float)(file.header.sampleRate)),
                    pcmSizeInBytes = (ushort)bytesDecompressed,
                };

                byte[] mp3FrameBytes = new byte[read];
                stream.Position = a;
                stream.Read(mp3FrameBytes, 0, read);
                // Debug.Log("mp3FrameBytes: " + mp3FrameBytes.Length + ", deltaTime " + scheme.deltaTimeInMilliseconds + " ms");

                MP3ESegment segment = new()
                {
                    mp3Frame = mp3FrameBytes
                };

                schemes.Add(scheme);
                segments.Add(segment);
            }
        }

        file.scheme = schemes.ToArray();
        file.segments = segments.ToArray();

        file.header.segmentCount = file.segments.Length;

        Debug.Log("Segments count: " + file.segments.Length);

        return BinarySerializer.Serialize(file);
    }
}