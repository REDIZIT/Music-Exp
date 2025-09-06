using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class BufferShaderController : MonoBehaviour
{
    [SerializeField] private Image image;

    private Material material;

    private const int MAX_SEGMENTS_IN_SHADER = 128;

    private void Awake()
    {
        material = image.material;
    }

    public void Refresh(MP3EFile file)
    {
        float[] widths = new float[file.scheme.Length];
        bool[] states = new bool[file.scheme.Length];

        for (int i = 0; i < widths.Length; i++)
        {
            var scheme = file.scheme[i];
            widths[i] = scheme.deltaTimeInMilliseconds;
            states[i] = scheme.isLoaded;
        }

        Refresh(widths, states);
    }

    public void Refresh(float[] widths, bool[] states)
    {
        float totalWidth = widths.Sum();

        float[] segments = new float[MAX_SEGMENTS_IN_SHADER];

        segments[0] = widths[0] / totalWidth;

        bool prevState = states[0];
        int segCount = 1;

        for (int i = 1; i < widths.Length; i++)
        {
            float normalizedWidth = widths[i] / totalWidth;
            bool state = states[i];

            if (prevState == state)
            {
                segments[segCount - 1] += normalizedWidth;
            }
            else
            {
                prevState = state;
                segments[segCount] = normalizedWidth;
                segCount++;
            }
        }

        // Debug.Log(string.Join(", ", segments.Select(f => f.ToString("F2"))));
        // Debug.Log("Segments count: " + segCount);

        material.SetInt("_NumSegments", segCount);
        material.SetFloatArray("_Segments", segments);
        material.SetInt("_StartWithActive", states[0] ? 1 : 0);
    }
}