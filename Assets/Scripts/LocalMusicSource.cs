using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using Debug = UnityEngine.Debug;

public class LocalMusicSource : MusicSource
{
    private string mp3eFilePath;
    private byte[] fileBytes;

    public MP3EWaveProvider waveBuffer;

    private Task task;
    private Thread thread;

    public override float CurrentTime
    {
        get
        {
            if (waveBuffer == null) return 0;
            return waveBuffer.Position / (float) waveBuffer.OutputWaveFormat.AverageBytesPerSecond;
        }
        set
        {
            if (waveBuffer == null) throw new("Wave buffer is not inited yet");
            waveBuffer.Position = (int) (value * waveBuffer.OutputWaveFormat.AverageBytesPerSecond);
        }
    }

    public override float TotalTime => waveBuffer.file.header.TotalSeconds;

    public LocalMusicSource(string mp3eFilePath)
    {
        this.mp3eFilePath = mp3eFilePath;
    }

    public override IWaveProvider Init()
    {
        fileBytes = File.ReadAllBytes(mp3eFilePath);
        using MemoryStream stream = new(fileBytes);

        Stopwatch w = Stopwatch.StartNew();
        MP3EFile file = MP3EFile.InitFromStream(stream);
        Debug.Log($"File inited in {w.ElapsedMilliseconds}ms");

        waveBuffer = new(file);

        // task = Task.Run(FeedBuffer);
        thread = new(FeedBuffer);
        thread.Start();

        return waveBuffer;
    }

    public override void Dispose()
    {
        task?.Dispose();
        thread?.Abort();
    }

    private void FeedBuffer()
    {
        bool isLoadCompleted = false;
        while (isLoadCompleted == false)
        {
            isLoadCompleted = true;
            for (int i = 0; i < waveBuffer.file.segments.Length; i++)
            {
                int index = (waveBuffer.currentSegmentIndex + i) % waveBuffer.file.segments.Length;

                MP3ESegmentScheme scheme = waveBuffer.file.scheme[index];
                if (scheme.isLoaded) continue;

                isLoadCompleted = false;

                // Debug.Log(index);
                waveBuffer.DecompressSegment(index);
                // Thread.Sleep(10);

                onBufferChange?.Invoke();

                break;
            }
        }

        Debug.Log("PCM load completd");
    }
}