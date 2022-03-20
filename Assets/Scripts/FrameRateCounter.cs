using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class FrameRateCounter : MonoBehaviour
{
    // Tunables
    [SerializeField] TMP_Text display = null;
    [SerializeField] [Range(0.1f, 2f)] float sampleDuration = 1f;
    [SerializeField] DisplayMode displayMode = DisplayMode.FPS;

    // State
    int frames = 0;
    float duration, bestDuration = float.MaxValue;
    float worstDuration = 0f;

    // Data Structures
    public enum DisplayMode
    {
        FPS,
        MS
    }

    private void Update()
    {
        float frameDuration = Time.unscaledDeltaTime;
        frames += 1;
        duration += frameDuration;

        if (frameDuration < bestDuration)
        {
            bestDuration = frameDuration;
        }
        if (frameDuration > worstDuration)
        {
            worstDuration = frameDuration;
        }

        if (duration >= sampleDuration)
        {
            switch (displayMode)
            {
                case DisplayMode.MS:
                    display.text = $"MS\n{1000f * (duration / frames): 0.0}\n{1000f * bestDuration: 0.0}\n{1000f * worstDuration: 0.0}";
                    break;
                case DisplayMode.FPS:
                default:
                    display.text = $"FPS\n{frames / duration: 0}\n{1f / bestDuration: 0}\n{1f / worstDuration: 0}";
                    break;
            }
            frames = 0;
            duration = 0f;
            bestDuration = float.MaxValue;
            worstDuration = 0f;
        }

    }
}
