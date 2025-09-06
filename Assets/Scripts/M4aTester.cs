using System;
using System.Diagnostics;
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

    private Player player;
    private bool isBufferChangeGot;

    private async void Start()
    {
        player = new();
        player.onBufferChange += () =>
        {
            if (isBufferChangeGot) return;
            isBufferChangeGot = true;

            UnityMainThreadDispatcher.TryEnqueue(() =>
            {
                if (player.Source is LocalMusicSource local)
                {
                    bufferShaderController.Refresh(local.waveBuffer.file);
                }

                isBufferChangeGot = false;
            });
        };

        await Task.Run(() =>
        {
            // MP3EFormatter.MP3_to_MP3E("C:/Hated Love Dance.mp3", "C:/Hated Love Dance.mp3e");

            // byte[] mp3eBytes = File.ReadAllBytes("C:/Melody Chase.mp3e");
            // byte[] mp3eBytes = File.ReadAllBytes("C:/Hated Love Dance.mp3e");

            LocalMusicSource localSource = new("C:/Melody Chase.mp3e");
            player.ChangeSource(localSource);

            UnityMainThreadDispatcher.TryEnqueue(() =>
            {
                slider.maxValue = player.TotalTime;
                totalTimeText.text = TimeSpan.FromSeconds(player.TotalTime).ToPrettyTime();
                currentTimeText.text = TimeSpan.Zero.ToPrettyTime();
            });
        });

        slider.onValueChanged.AddListener(OnSliderChange);
    }

    private void Update()
    {
        if (player == null) return;

        playIcon.gameObject.SetActive(player.PlaybackState != PlaybackState.Playing);
        pauseIcon.gameObject.SetActive(player.PlaybackState == PlaybackState.Playing);

        float currentTime = player.CurrentTime;
        slider.SetValueWithoutNotify(currentTime);
        currentTimeText.text = TimeSpan.FromSeconds(currentTime).ToPrettyTime();

        if (Input.GetKeyDown(KeyCode.K))
        {
            Task.Run(() =>
            {
                LocalMusicSource localSource = new("C:/Hated Love Dance.mp3e");
                player.ChangeSource(localSource);

                UnityMainThreadDispatcher.TryEnqueue(() =>
                {
                    slider.maxValue = player.TotalTime;
                    totalTimeText.text = TimeSpan.FromSeconds(player.TotalTime).ToPrettyTime();
                    currentTimeText.text = TimeSpan.Zero.ToPrettyTime();
                });
            });
        }
    }

    public void OnSliderChange(float value)
    {
        player.CurrentTime = value;
    }

    public void OnPlayClick()
    {
        if (player.PlaybackState == PlaybackState.Playing)
        {
            player.Pause();
        }
        else
        {
            player.Play();
        }
    }

    private void OnDestroy()
    {
        player?.Dispose();
    }
}