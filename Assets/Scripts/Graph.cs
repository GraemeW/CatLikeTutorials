using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static FunctionLibrary;

public class Graph : MonoBehaviour
{
    // Tunables
    [SerializeField] Transform pointPrefab = null;
    [SerializeField][Range(0,100)] int resolution = 10;
    [SerializeField] FunctionName function = default;
    [SerializeField] [Min(0f)] float functionDuration = 1.0f;
    [SerializeField] TransitionMode transitionMode = default;
    [SerializeField] [Min(0f)] float transitionDuration = 1.0f;

    // State
    Transform[] points;
    float duration = 0f;
    bool transitioning = false;
    FunctionName transitionFunction = default;

    // Data Structures
    public enum TransitionMode
    {
        Cycle,
        Random
    }

    private void Awake()
    {
        points = new Transform[resolution * resolution];

        float step = 2f / resolution;
        Vector3 scale = Vector3.one * step;
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = Instantiate(pointPrefab, transform);
            points[i].localScale = scale;
        }
    }

    private void Update()
    {
        duration += Time.deltaTime;
        if (transitioning)
        {
            if (duration >= transitionDuration)
            {
                duration -= transitionDuration;
                transitioning = false;
            }
        }
        else if (duration >= functionDuration)
        {
            duration -= functionDuration;
            transitioning = true;
            transitionFunction = function;
            PickNextMethod();
        }

        if (transitioning)
        {
            UpdateFunctionTransition();
        }
        else
        {
            UpdateFunction();
        }
    }

    private void PickNextMethod()
    {
        switch (transitionMode)
        {
            case TransitionMode.Cycle:
                function = GetNextFunctionName(function);
                break;
            case TransitionMode.Random:
            default:
                function = GetRandomFunctionName(function);
                break;
        }
    }

    private void UpdateFunction()
    {
        Function f = GetFunction(function);

        float currentTime = Time.time;
        float step = 2f / resolution;

        float v = 0.5f * step - 1f;
        for (int i = 0, x = 0, z = 0; i < points.Length; i++, x++)
        {
            if (x == resolution)
            {
                x = 0;
                z++;
                v = (z + 0.5f) * step - 1f;
            }

            float u = (x + 0.5f) * step - 1f;
            points[i].localPosition = f(u, v, currentTime);
        }
    }

    private void UpdateFunctionTransition()
    {
        Function from = GetFunction(transitionFunction);
        Function to = GetFunction(function);
        float progress = duration / transitionDuration;

        float currentTime = Time.time;
        float step = 2f / resolution;

        float v = 0.5f * step - 1f;
        for (int i = 0, x = 0, z = 0; i < points.Length; i++, x++)
        {
            if (x == resolution)
            {
                x = 0;
                z++;
                v = (z + 0.5f) * step - 1f;
            }

            float u = (x + 0.5f) * step - 1f;
            points[i].localPosition = Morph(u, v, currentTime, from, to, progress);
        }
    }
}
