using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static FunctionLibrary;

public class GPUGraph : MonoBehaviour
{
    // Static
    const int minResolution = 16;
    const int maxResolution = 3000;

    // Tunables
    [SerializeField] ComputeShader computeShader = null;
    [SerializeField] Material material = null;
    [SerializeField] Mesh mesh = null;
    [SerializeField] [Range(minResolution, maxResolution)] int resolution = 10;
    [SerializeField] FunctionName function = default;
    [SerializeField] [Min(0f)] float functionDuration = 1.0f;
    [SerializeField] TransitionMode transitionMode = default;
    [SerializeField] [Min(0f)] float transitionDuration = 1.0f;

    // State
    float duration = 0f;
    bool transitioning = false;
    FunctionName transitionFunction = default;

    // GPU
    ComputeBuffer positionsBuffer;
    static int stride = 3 * 4; // three float numbers, each float at 4 bytes
    static int xyThreadNumber = 8;
    static readonly int positionsID = Shader.PropertyToID("_Positions");
    static readonly int resolutionID = Shader.PropertyToID("_Resolution");
    static readonly int stepID = Shader.PropertyToID("_Step");
    static readonly int timeID = Shader.PropertyToID("_Time");
    static readonly int transitionProgressID = Shader.PropertyToID("_TransitionProgress");

    // Data Structures
    public enum TransitionMode
    {
        Cycle,
        Random
    }

    private void OnEnable()
    {
        positionsBuffer = new ComputeBuffer(maxResolution * maxResolution, stride);
    }

    private void OnDisable()
    {
        positionsBuffer.Release();
        positionsBuffer = null;
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

        UpdateFunctionOnGPU();
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

    private void UpdateFunctionOnGPU()
    {
        resolution = Mathf.Clamp(resolution, minResolution, maxResolution);

        float step = 2f / resolution;
        computeShader.SetInt(resolutionID, resolution);
        computeShader.SetFloat(stepID, step);
        computeShader.SetFloat(timeID, Time.time);
        if (transitioning)
        {
            computeShader.SetFloat(transitionProgressID,
                Mathf.SmoothStep(0f, 1f, duration / transitionDuration));
        }

        int kernelIndex = (int)function +
            (int)(transitioning ? transitionFunction : function) * FunctionLibrary.FunctionCount;

        computeShader.SetBuffer(kernelIndex, positionsID, positionsBuffer);
        int groups = Mathf.CeilToInt(resolution / xyThreadNumber);
        computeShader.Dispatch(kernelIndex, groups, groups, 1);

        material.SetBuffer(positionsID, positionsBuffer);
        material.SetFloat(stepID, step);
        Bounds bounds = new Bounds(Vector3.zero, Vector3.one * (2f + 2f / resolution));
        Graphics.DrawMeshInstancedProcedural(
            mesh, 0, material, bounds, resolution * resolution
        );
    }
}
