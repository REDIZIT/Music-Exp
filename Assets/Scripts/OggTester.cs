using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Concentus;
using Concentus.Oggfile;
using Concentus.Structs;
using NAudio.Wave;
using UnityEngine;

public class BlockingStream : Stream
{
    private byte[] fileBytes;


    public BlockingStream(string filepath)
    {
        fileBytes = File.ReadAllBytes(filepath);
    }

    public override void Flush()
    {
        throw new NotImplementedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        long available = fileBytes.LongLength - Position;
        long read = Math.Min(count, available);
        Array.Copy(fileBytes, Position, buffer, offset, read);
        return (int)read;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotImplementedException();
    }

    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotImplementedException();
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => fileBytes.LongLength;
    public override long Position { get; set; }
}

public class OggTester : MonoBehaviour
{
    private WaveOut waveOut;
    private BufferedWaveProvider waveBuffer;

    private void Start()
    {
        Task.Run(() =>
        {
            try
            {
                string filepath = "C:/Melody Chase.ogg";

                // using FileStream stream = new FileStream(filepath, FileMode.Open);

                // using MemoryStream stream = new MemoryStream(File.ReadAllBytes(filepath)[..100_000]);
                using Stream stream = new BlockingStream(filepath);
                Debug.Log("stream");

                using (var vorbis = new NVorbis.VorbisReader(stream))
                {
                    Debug.Log("vorbis");

                    // get the channels & sample rate
                    var channels = vorbis.Channels;
                    var sampleRate = vorbis.SampleRate;

                    // OPTIONALLY: get a TimeSpan indicating the total length of the Vorbis stream
                    var totalTime = vorbis.TotalTime;

                    Debug.Log("Total time: " + totalTime);

                    // create a buffer for reading samples
                    var readBuffer = new float[channels * sampleRate / 5];	// 200ms

                    // get the initial position (obviously the start)
                    var position = TimeSpan.Zero;


                    waveBuffer = new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels));
                    waveBuffer.BufferDuration = TimeSpan.FromSeconds(4 * 60);
                    byte[] pcmByteBuffer = new byte[readBuffer.Length * 4];

                    waveOut = new();
                    waveOut.Init(waveBuffer);
                    waveOut.Volume = 0.2f;
                    waveOut.Play();

                    // go grab samples
                    int cnt;
                    while ((cnt = vorbis.ReadSamples(readBuffer, 0, readBuffer.Length)) > 0)
                    {
                        // do stuff with the buffer
                        // samples are interleaved (chan0, chan1, chan0, chan1, etc.)
                        // sample value range is -0.99999994f to 0.99999994f unless vorbis.ClipSamples == false

                        cnt *= sizeof(float);
                        Buffer.BlockCopy(readBuffer, 0, pcmByteBuffer, 0, cnt);


                        waveBuffer.AddSamples(pcmByteBuffer, 0, pcmByteBuffer.Length);


                        // OPTIONALLY: get the position we just read through to...
                        position = vorbis.TimePosition;
                    }

                    Debug.Log(position);
                }
            }
            catch (Exception err)
            {
                Debug.LogException(err);
                Dispose();
            }
        });
    }

    private void OnDestroy()
    {
        Dispose();
    }

    private void Dispose()
    {
        waveOut?.Dispose();
    }
}