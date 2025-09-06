using System;
using System.IO;
using System.Linq;
using NAudio.Utils;
using NAudio.Wave;
using UnityEngine;

public class MP3EWaveProvider : IWaveProvider
{
    private CircularBuffer circularBuffer;
    private readonly WaveFormat waveFormat;

    private AcmMp3FrameDecompressor decompressor;

    public WaveFormat WaveFormat => OutputWaveFormat;
    public WaveFormat OutputWaveFormat { get; }

    public int Position
    {
        get => position;
        set => SetPosition(value);
    }

    private int position;
    public int currentSegmentIndex;

    public MP3EFile file;

    private byte[] samplesBuffer = new byte[16384];

    public MP3EWaveProvider(MP3EFile file)
    {
        this.file = file;

        waveFormat = new Mp3WaveFormat(file.header.sampleRate, file.header.channelMode, file.header.frameLength, file.header.bitRate);
        decompressor = new AcmMp3FrameDecompressor(waveFormat);

        OutputWaveFormat = decompressor.OutputFormat;
        Debug.Log(OutputWaveFormat + ": samplerate = " + OutputWaveFormat.SampleRate);

        int pcmTotalSizeInBytes = file.scheme.Sum(s => s.pcmSizeInBytes);
        file.pcm = new byte[pcmTotalSizeInBytes];

        Debug.Log("pcmTotalSizeInBytes: " + pcmTotalSizeInBytes);
        Debug.Log("segments: " + file.scheme.Length);

        int segmentPcmOffset = 0;
        for (int i = 0; i < file.segments.Length; i++)
        {
            MP3ESegmentScheme scheme = file.scheme[i];
            scheme.pcmAbsBegin = segmentPcmOffset;
            segmentPcmOffset += scheme.pcmSizeInBytes;
        }

        // for (int i = 0; i < file.segments.Length; i++)
        // {
        //     DecompressSegment(i);
        // }
    }

    public void DecompressSegment(int segmentIndex)
    {
        MP3ESegment segment = file.segments[segmentIndex];
        MP3ESegmentScheme scheme = file.scheme[segmentIndex];

        Mp3Frame frame = Mp3Frame.LoadFromStream(new MemoryStream(segment.mp3Frame));
        int pcmBytesDecompressed = decompressor.DecompressFrame(frame, samplesBuffer, 0);

        Buffer.BlockCopy(samplesBuffer, 0, file.pcm, scheme.pcmAbsBegin, pcmBytesDecompressed);

        scheme.isLoaded = true;
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        // Buffer.BlockCopy(file.pcm, position, buffer, offset, count);
        // position += count;
        // return count;

        int absReadEnd = position + count;

        (int firstSegmentIndex, int lastSegmentIndex) = GetRangeOfSegments(absReadEnd);

        // Debug.Log($"[{firstSegmentIndex}-{lastSegmentIndex}]");

        for (int i = firstSegmentIndex; i <= lastSegmentIndex; i++)
        {
            MP3ESegmentScheme scheme = file.scheme[i];
            if (scheme.isLoaded == false)
            {
                return 0; // Pause
            }
        }

        Buffer.BlockCopy(file.pcm, position, buffer, offset, count);

        position += count;
        currentSegmentIndex = lastSegmentIndex;

        return count;
    }

    public void SetPosition(int position)
    {
        int rate = 10;
        this.position = (int)(position / rate) * rate; // align by magic 10 (if not aligned may create white noise after reposition)

        (int firstSegmentIndex, int lastSegmentIndex) = GetRangeOfSegments(this.position, forceFullCheck: true);
        Debug.Log(firstSegmentIndex + " - " + lastSegmentIndex);

        currentSegmentIndex = firstSegmentIndex;
    }

    private (int firstSegmentIndex, int lastSegmentIndex) GetRangeOfSegments(int absReadEnd, bool forceFullCheck = false)
    {
        int firstSegmentIndex = -1;
        int lastSegmentIndex = -1;
        int startI = forceFullCheck ? 0 : currentSegmentIndex;

        for (int i = startI; i < file.segments.Length; i++)
        {
            MP3ESegmentScheme scheme = file.scheme[i];

            if (position > scheme.pcmAbsBegin) continue;

            if (firstSegmentIndex == -1)
            {
                firstSegmentIndex = i;
            }

            int delta = (scheme.pcmAbsBegin + scheme.pcmSizeInBytes) - absReadEnd;
            if (delta >= 0)
            {
                lastSegmentIndex = i;
                break;
            }
        }

        return (firstSegmentIndex, lastSegmentIndex);
    }
}