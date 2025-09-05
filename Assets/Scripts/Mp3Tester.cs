using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NAudio.FileFormats.Mp3;
using NAudio.MediaFoundation;
using UnityEngine;
using NAudio.Wave;
using UnityEngine.UI;

public class NAudioTests : MonoBehaviour
{
    [SerializeField] private Slider slider;


    public void OnSliderChange(float value)
    {

    }



    // private WaveOut waveOut;
    // private BufferedWaveProvider bufferedWaveProvider;
    // private IMp3FrameDecompressor decompressor;
    //
    // void Start()
    // {
    //     // 1. Создаем плеер. Он пока ничего не знает о формате аудио.
    //     waveOut = new WaveOut();
    //
    //     // 2. Запускаем корутину, которая будет делать всё остальное:
    //     //    - Читать файл
    //     //    - Определять формат
    //     //    - Инициализировать буфер и декомпрессор
    //     //    - Декодировать и проигрывать
    //     // StartCoroutine(SimulateDownloadAndDecode());
    //     Task.Run(SimulateDownloadAndDecode);
    // }
    //
    // private void SimulateDownloadAndDecode()
    // {
    //     string filePath = "C:/Melody Chase.mp3";
    //     if (!File.Exists(filePath))
    //     {
    //         Debug.LogError("Audio file not found!");
    //         return;
    //     }
    //
    //
    //     using MemoryStream stream = new MemoryStream(File.ReadAllBytes(filePath));
    //         // Эти значения можно вынести в настройки класса
    //     // Общая длительность буфера в секундах
    //     const int totalBufferSeconds = 20;
    //     // Начинаем ждать, когда буфер заполнится до этого уровня (в секундах)
    //     const int highWatermarkSeconds = 15;
    //     bool IsEndOfStream = false;
    //
    //     while (true)
    //     {
    //         // --- НОВАЯ ЛОГИКА ОЖИДАНИЯ ---
    //         if (bufferedWaveProvider != null &&
    //             bufferedWaveProvider.BufferedDuration.TotalSeconds > highWatermarkSeconds)
    //         {
    //             //Console.WriteLine($"Буфер почти полон ({_bufferedWaveProvider.BufferedDuration.TotalSeconds:F2}с), ждем...");
    //             Thread.Sleep(100); // Подождать 100 мс и проверить снова
    //             continue; // Возвращаемся к началу цикла while для повторной проверки
    //         }
    //         // --- КОНЕЦ НОВОЙ ЛОГИКИ ---
    //
    //         try
    //         {
    //             Mp3Frame frame = Mp3Frame.LoadFromStream(stream);
    //
    //             if (frame == null)
    //             {
    //                 if (IsEndOfStream)
    //                 {
    //                     break;
    //                 }
    //                 Thread.Sleep(100);
    //                 continue;
    //             }
    //
    //             if (decompressor == null)
    //             {
    //                 var waveFormat = new Mp3WaveFormat(frame.SampleRate, frame.ChannelMode == ChannelMode.Mono ? 1 : 2, frame.FrameLength, frame.BitRate);
    //                 decompressor = new AcmMp3FrameDecompressor(waveFormat);
    //
    //                 // Используем константу для настройки размера буфера
    //                 bufferedWaveProvider = new BufferedWaveProvider(decompressor.OutputFormat)
    //                 {
    //                     BufferDuration = TimeSpan.FromSeconds(totalBufferSeconds),
    //                     // Важное свойство! По умолчанию false.
    //                     // Если true, то при переполнении старые данные будут отброшены, а не вызовут исключение.
    //                     // Это может привести к "щелчкам" или пропускам, но предотвратит падение.
    //                     // Лучше использовать логику ожидания выше, чем включать это.
    //                     // DiscardOnBufferOverflow = true
    //                 };
    //
    //                 waveOut = new();
    //                 waveOut.Init(bufferedWaveProvider);
    //                 waveOut.Play();
    //             }
    //
    //             byte[] buffer = new byte[16384];
    //             int bytesDecompressed = decompressor.DecompressFrame(frame, buffer, 0);
    //
    //             if (bytesDecompressed > 0 && bufferedWaveProvider != null)
    //             {
    //                 bufferedWaveProvider.AddSamples(buffer, 0, bytesDecompressed);
    //             }
    //         }
    //         catch (EndOfStreamException)
    //         {
    //             if (IsEndOfStream)
    //             {
    //                 break;
    //             }
    //             Thread.Sleep(100);
    //         }
    //         catch (Exception ex)
    //         {
    //             Debug.Log($"Ошибка в потоке декодирования: {ex.Message}");
    //             break;
    //         }
    //     }
    //
    //     Debug.Log("Декодирование завершено!");
    // }
    //
    // private void DecodeAndPlayFrame(Mp3Frame frame)
    // {
    //     // Буфер для PCM-данных. Размер должен быть достаточным.
    //     byte[] pcmBuffer = new byte[16384];
    //     int decodedBytes = decompressor.DecompressFrame(frame, pcmBuffer, 0);
    //     if (decodedBytes > 0)
    //     {
    //         // Добавляем декодированные сэмплы в наш буфер
    //         bufferedWaveProvider.AddSamples(pcmBuffer, 0, decodedBytes);
    //     }
    // }
    //
    // void OnDestroy()
    // {
    //     if (waveOut != null)
    //     {
    //         waveOut.Dispose();
    //     }
    //     if (decompressor != null)
    //     {
    //         decompressor.Dispose();
    //     }
    // }
    //
    // void OnGUI()
    // {
    //     if (waveOut != null)
    //     {
    //         if (GUI.Button(new Rect(10, 10, 100, 30), waveOut.PlaybackState == PlaybackState.Playing ? "Pause" : "Play"))
    //         {
    //             if (waveOut.PlaybackState == PlaybackState.Playing) waveOut.Pause();
    //             else waveOut.Play();
    //         }
    //     }
    // }
}