using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using Concentus.Oggfile;
using Concentus.Structs;
using NAudio.Utils;
using NAudio.Wave;
using PimDeWitte.UnityMainThreadDispatcher;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

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
            byte[] mp3Bytes = File.ReadAllBytes("C:/Melody Chase.mp3");
            byte[] rencoded = Reencode(mp3Bytes);
            File.WriteAllBytes("C:/Melody Chase.mp3e", rencoded);

            Debug.Log("Done");

            byte[] mp3eBytes = File.ReadAllBytes("C:/Melody Chase.mp3e");
            PlayMP3E(mp3eBytes);
        });
    }

    private void Update()
    {
        if (waveBuffer == null) return;
        // slider.SetValueWithoutNotify((float) (waveBuffer.BufferDuration - waveBuffer.BufferedDuration).TotalSeconds);
        // Debug.Log(slider.value);

        // if (Input.GetKeyDown(KeyCode.K))
        // {
        //     waveBuffer.
        // }

        if (task != null && taskStartTime.Elapsed.TotalSeconds > 5)
        {
            task.Dispose();
            Debug.LogError("Task timeout");
            task = null;
        }
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


        // List<byte> rawmp3 = new(mp3Bytes.Length);
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
            }

            MP3ESegmentScheme scheme = new()
            {
                sizeInBytes = (ushort)frame.RawData.Length,
                deltaTimeInMilliseconds = (ushort)(frame.SampleCount * 1000 / (float)(file.header.sampleRate))
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

public class MP3EFileHeader
{
    public int sampleRate;
    public int totalSampleCount;
    public byte channelMode;
    public int frameLength;
    public int bitRate;

    public int segmentCount;

    public byte[] ToBytes()
    {
        return BinarySerializer.Serialize(this);
    }

    public static MP3EFileHeader FromStream(Stream stream)
    {
        return BinarySerializer.Deserialize<MP3EFileHeader>(stream);
    }
}

public class MP3ESegmentScheme
{
    public ushort sizeInBytes;
    public ushort deltaTimeInMilliseconds;
}
public class MP3ESegment
{
    public byte[] mp3Frame;

    [NonSerialized]
    public byte[] pcm;

    public byte[] ToBytes()
    {
        return BinarySerializer.Serialize(this);
    }

    public static MP3ESegment FromStream(Stream stream)
    {
        return BinarySerializer.Deserialize<MP3ESegment>(stream);
    }
}

public class MP3EFile
{
    public MP3EFileHeader header;
    public MP3ESegmentScheme[] scheme;
    public MP3ESegment[] segments;

    public static MP3EFile InitFromStream(Stream stream)
    {
        // MP3EFile file = new()
        // {
        //     header = MP3EFileHeader.FromStream(stream),
        //     scheme = BinarySerializer.Deserialize<MP3ESegmentScheme[]>(stream),
        //     segments = BinarySerializer.Deserialize<MP3ESegment[]>(stream),
        // };
        // return file;

        return BinarySerializer.Deserialize<MP3EFile>(stream);
    }
}


public class MP3EWaveProvider : IWaveProvider
{
    private CircularBuffer circularBuffer;
    private readonly WaveFormat waveFormat;

    private AcmMp3FrameDecompressor decompressor;

    public WaveFormat WaveFormat => OutputWaveFormat;
    public WaveFormat OutputWaveFormat { get; }

    private int position;
    private MP3EFile file;

    private byte[] samplesBuffer = new byte[16384];

    public MP3EWaveProvider(MP3EFile file)
    {
        this.file = file;

        waveFormat = new Mp3WaveFormat(file.header.sampleRate, file.header.channelMode, file.header.frameLength, file.header.bitRate);
        decompressor = new AcmMp3FrameDecompressor(waveFormat);

        OutputWaveFormat = decompressor.OutputFormat;


        for (int i = 0; i < file.segments.Length; i++)
        {
            MP3ESegment segment = file.segments[i];
            Mp3Frame frame = Mp3Frame.LoadFromStream(new MemoryStream(segment.mp3Frame));
            int bytesDecompressed = decompressor.DecompressFrame(frame, samplesBuffer, 0);

            segment.pcm = new byte[bytesDecompressed];
            Buffer.BlockCopy(samplesBuffer, 0, segment.pcm, 0, bytesDecompressed);
        }


        circularBuffer = new(OutputWaveFormat.AverageBytesPerSecond * 4 * 60);
        int bufferedMilliseconds = 0;
        int toBufferMilliseconds = 4 * 60 * 1000;
        for (int i = 0; i < file.segments.Length; i++)
        {
            MP3ESegmentScheme scheme = file.scheme[i];
            MP3ESegment segment = file.segments[i];

            if (bufferedMilliseconds + scheme.deltaTimeInMilliseconds > toBufferMilliseconds)
            {
                break;
            }

            bufferedMilliseconds += scheme.deltaTimeInMilliseconds;
            circularBuffer.Write(segment.pcm, 0, segment.pcm.Length);
        }

        Debug.Log("Buffered: " + TimeSpan.FromMilliseconds(bufferedMilliseconds) + " ms");
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        return circularBuffer.Read(buffer, offset, count);
    }
}