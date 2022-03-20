using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Clock : MonoBehaviour
{
    // Tunables
    [SerializeField] Transform hoursPivot, minutesPivot, secondsPivot = null;

    // Static
    static float hoursToDegrees = 1f / 12 * 360;
    static float minutesToDegrees = 1f / 60 * 360;
    static float secondsToDegrees = 1f / 60 * 360;

    // State
    TimeSpan currentTime;

    private void Update()
    {
        currentTime = DateTime.Now.TimeOfDay;
        hoursPivot.localRotation = Quaternion.Euler(0, 0, -(float)currentTime.TotalHours * hoursToDegrees);
        minutesPivot.localRotation = Quaternion.Euler(0, 0, -(float)currentTime.TotalMinutes * minutesToDegrees);
        secondsPivot.localRotation = Quaternion.Euler(0, 0, -(float)currentTime.TotalSeconds * secondsToDegrees);
    }
}
