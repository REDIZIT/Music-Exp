using System;
using System.Diagnostics;
using NAudio.Wave;
using Debug = UnityEngine.Debug;

public class Player : IDisposable
{
    public Action onBufferChange;

    private WaveOut waveOut;
    private MusicSource source;

    public PlaybackState PlaybackState => waveOut.PlaybackState;
    public MusicSource Source => source;

    public float CurrentTime
    {
        get => source.CurrentTime;
        set => source.CurrentTime = value;
    }

    public float TotalTime => source.TotalTime;

    public Player()
    {

    }

    public void Pause()
    {
        waveOut.Pause();
    }

    public void Play()
    {
        waveOut.Play();
    }

    public void ChangeSource(MusicSource source)
    {
        if (this.source != null)
        {
            this.source.onBufferChange -= this.onBufferChange;
            this.source.Dispose();
            this.source = null;
        }

        if (waveOut != null)
        {
            waveOut?.Dispose();
        }

        this.source = source;

        IWaveProvider provider = source.Init();
        Stopwatch w = Stopwatch.StartNew();
        waveOut = new();
        waveOut.Volume = 0.2f;
        waveOut.Init(provider);
        Debug.Log($"Wave init in {w.ElapsedMilliseconds}ms");

        source.onBufferChange += this.onBufferChange;

        waveOut.Play();

        GC.Collect();
    }

    public void Dispose()
    {
        waveOut?.Dispose();
        this.source?.Dispose();
    }
}