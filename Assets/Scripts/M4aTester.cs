using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NAudio.Utils;
using NAudio.Wave;
using PimDeWitte.UnityMainThreadDispatcher;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

public class M4aTester : MonoBehaviour
{
    [SerializeField] private Slider slider;

    private WaveOut waveOut;
    private MP3EWaveProvider waveBuffer;
    private AcmMp3FrameDecompressor decompressor;

    private Task task;
    private Stopwatch taskStartTime;

    private async void Start()
    {
        waveOut = new();

        taskStartTime = Stopwatch.StartNew();
        task = Task.Run(() =>
        {
            // byte[] mp3Bytes = File.ReadAllBytes("C:/Melody Chase.mp3");
            // byte[] rencoded = Reencode(mp3Bytes);
            // File.WriteAllBytes("C:/Melody Chase.mp3e", rencoded);
            //
            // Debug.Log("Done");

            byte[] mp3eBytes = File.ReadAllBytes("C:/Melody Chase.mp3e");
            PlayMP3E(mp3eBytes);
        });

        slider.onValueChanged.AddListener(OnSliderChange);
    }

    private void Update()
    {
        if (waveBuffer == null) return;

        // Debug.Log(waveBuffer.Position + ", " + TimeSpan.FromSeconds(waveBuffer.Position / (float)waveBuffer.OutputWaveFormat.AverageBytesPerSecond));
        slider.SetValueWithoutNotify(waveBuffer.Position / (float)waveBuffer.OutputWaveFormat.AverageBytesPerSecond);
        // Debug.Log(slider.value);

        // if (Input.GetKeyDown(KeyCode.K))
        // {
        //     waveBuffer.
        // }

        if (task != null && task.IsCompleted == false && taskStartTime.Elapsed.TotalSeconds > 5)
        {
            task.Dispose();
            Debug.LogError("Task timeout");
            task = null;
        }
    }

    public void OnSliderChange(float value)
    {
        waveBuffer.Position = (int) (value * waveBuffer.OutputWaveFormat.AverageBytesPerSecond);
    }

    private void PlayMP3E(byte[] mp3eBytes)
    {
        Task.Run(() =>
        {
            try
            {
                using MemoryStream stream = new(mp3eBytes);

                MP3EFile file = MP3EFile.InitFromStream(stream);
                waveBuffer = new(file);

                waveOut.Init(waveBuffer);
                waveOut.Volume = 0.2f;
                waveOut.Play();

                UnityMainThreadDispatcher.TryEnqueue(() =>
                {
                    slider.maxValue = waveBuffer.file.header.totalSampleCount / (float) waveBuffer.OutputWaveFormat.AverageBytesPerSecond;
                });
            }
            catch (Exception err)
            {
                Debug.LogException(err);
            }
        });
    }

    private byte[] Reencode(byte[] mp3Bytes)
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
        decompressor = new AcmMp3FrameDecompressor(waveFormat);

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

    private void OnDestroy()
    {
        waveOut?.Dispose();
        decompressor?.Dispose();
    }
}

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
    private int segmentIndex;

    public MP3EFile file;

    private byte[] samplesBuffer = new byte[16384];

    public MP3EWaveProvider(MP3EFile file)
    {
        this.file = file;

        waveFormat = new Mp3WaveFormat(file.header.sampleRate, file.header.channelMode, file.header.frameLength, file.header.bitRate);
        decompressor = new AcmMp3FrameDecompressor(waveFormat);

        OutputWaveFormat = decompressor.OutputFormat;

        int pcmTotalSizeInBytes = file.scheme.Sum(s => s.pcmSizeInBytes);
        file.pcm = new byte[pcmTotalSizeInBytes];

        Debug.Log("pcmTotalSizeInBytes: " + pcmTotalSizeInBytes);
        Debug.Log("segments: " + file.scheme.Length);

        int segmentPcmOffset = 0;
        for (int i = 0; i < file.segments.Length; i++)
        {
            MP3ESegment segment = file.segments[i];
            MP3ESegmentScheme scheme = file.scheme[i];

            Mp3Frame frame = Mp3Frame.LoadFromStream(new MemoryStream(segment.mp3Frame));
            int pcmBytesDecompressed = decompressor.DecompressFrame(frame, samplesBuffer, 0);

            Buffer.BlockCopy(samplesBuffer, 0, file.pcm, segmentPcmOffset, pcmBytesDecompressed);


            scheme.pcmAbsBegin = segmentPcmOffset;

            segmentPcmOffset += pcmBytesDecompressed;
        }
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        // Buffer.BlockCopy(file.pcm, position, buffer, offset, count);
        // position += count;
        // return count;

        int absReadEnd = position + count;

        (int firstSegmentIndex, int lastSegmentIndex) = GetRangeOfSegments(absReadEnd);

        // Debug.Log($"[{firstSegmentIndex}-{lastSegmentIndex}]");


        Buffer.BlockCopy(file.pcm, position, buffer, offset, count);

        position += count;
        segmentIndex = lastSegmentIndex;

        return count;
    }

    public void SetPosition(int position)
    {
        int rate = 10;
        this.position = (int)(position / rate) * rate; // align by magic 10 (if not aligned may create white noise after reposition)

        (int firstSegmentIndex, int lastSegmentIndex) = GetRangeOfSegments(this.position, forceFullCheck: true);
        Debug.Log(firstSegmentIndex + " - " + lastSegmentIndex);

        segmentIndex = firstSegmentIndex;
    }

    private (int firstSegmentIndex, int lastSegmentIndex) GetRangeOfSegments(int absReadEnd, bool forceFullCheck = false)
    {
        int firstSegmentIndex = -1;
        int lastSegmentIndex = -1;
        int startI = forceFullCheck ? 0 : segmentIndex;

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