using System;
using NAudio.Wave;

public abstract class MusicSource : IDisposable
{
    public abstract float CurrentTime { get; set; }
    public abstract float TotalTime { get; }
    public Action onBufferChange;

    public abstract IWaveProvider Init();
    public abstract void Dispose();
}