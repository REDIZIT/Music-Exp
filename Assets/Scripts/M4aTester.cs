using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
using PimDeWitte.UnityMainThreadDispatcher;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

public class M4aTester : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private BufferShaderController bufferShaderController;
    [SerializeField] private Image playIcon, pauseIcon;
    [SerializeField] private TextMeshProUGUI currentTimeText, totalTimeText;

    private WaveOut waveOut;
    private MP3EWaveProvider waveBuffer;
    private AcmMp3FrameDecompressor decompressor;

    private Task task;
    private Stopwatch taskStartTime;

    private int aboba;

    private async void Start()
    {
        waveOut = new();

        taskStartTime = Stopwatch.StartNew();
        task = Task.Run(() =>
        {
            byte[] mp3eBytes = File.ReadAllBytes("C:/Melody Chase.mp3e");
            PlayMP3E(mp3eBytes);
        });

        slider.onValueChanged.AddListener(OnSliderChange);
    }

    private void Update()
    {
        if (waveBuffer == null) return;

        playIcon.gameObject.SetActive(waveOut.PlaybackState != PlaybackState.Playing);
        pauseIcon.gameObject.SetActive(waveOut.PlaybackState == PlaybackState.Playing);

        float currentTime = waveBuffer.Position / (float) waveBuffer.OutputWaveFormat.AverageBytesPerSecond;
        slider.SetValueWithoutNotify(currentTime);
        currentTimeText.text = TimeSpan.FromSeconds(currentTime).ToPrettyTime();

        if (Input.GetKeyDown(KeyCode.K))
        {
            for (int i = 0; i < 100; i++)
            {
                int j = aboba + i;
                if (j >= waveBuffer.file.segments.Length) break;
                waveBuffer.DecompressSegment(j);
            }
            aboba += 100;

            bufferShaderController.Refresh(waveBuffer.file);
        }

        if (Input.GetKeyDown(KeyCode.J))
        {
            for (int i = 0; i < 100; i++)
            {
                int j = aboba + i + 4000;
                if (j >= waveBuffer.file.segments.Length) break;
                waveBuffer.DecompressSegment(j);
            }
            aboba += 100;

            bufferShaderController.Refresh(waveBuffer.file);
        }

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

    public void OnPlayClick()
    {
        if (waveOut.PlaybackState == PlaybackState.Playing)
        {
            waveOut.Pause();
        }
        else
        {
            waveOut.Play();
        }
    }

    private void PlayMP3E(byte[] mp3eBytes)
    {
        Task.Run(() =>
        {
            try
            {
                using MemoryStream stream = new(mp3eBytes);

                Stopwatch w = Stopwatch.StartNew();
                MP3EFile file = MP3EFile.InitFromStream(stream);
                Debug.Log($"File inited in {w.ElapsedMilliseconds}ms");
                waveBuffer = new(file);

                waveOut.Init(waveBuffer);
                waveOut.Volume = 0.2f;
                waveOut.Play();

                UnityMainThreadDispatcher.TryEnqueue(() =>
                {
                    slider.maxValue = waveBuffer.file.header.TotalSeconds;
                    totalTimeText.text = file.header.Duration.ToPrettyTime();
                    currentTimeText.text = TimeSpan.Zero.ToPrettyTime();
                });
            }
            catch (Exception err)
            {
                Debug.LogException(err);
            }
        });
    }

    private void OnDestroy()
    {
        waveOut?.Dispose();
        decompressor?.Dispose();
    }
}