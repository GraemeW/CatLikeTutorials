using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Mathematics.math;
using quaternion = Unity.Mathematics.quaternion;

public class Fractal : MonoBehaviour
{
    // Tunables
    [SerializeField, Range(1, 10)] int depth = 6;
    [SerializeField] Mesh mesh;
    [SerializeField] Material material;

    // Static
    static float3[] directions = { up(), 
        right(), left(), 
        forward(), back() };
    static quaternion[] rotations = {quaternion.identity,
        quaternion.RotateZ(-0.5f * PI), quaternion.RotateZ(0.5f * PI),
        quaternion.RotateX(0.5f * PI), quaternion.RotateX(-0.5f * PI)
    };
    static readonly int matricesID = Shader.PropertyToID("_Matrices");
    static MaterialPropertyBlock propertyBlock;

    // Data Structures
    private struct FractalPart
    {
        public float3 direction;
        public float3 worldPosition;
        public quaternion rotation;
        public quaternion worldRotation;
        public float spinAngle;
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    private struct UpdateFractalLevelJob : IJobFor
    {

        public float spinAngleDelta;
        public float scale;

        [ReadOnly]
        public NativeArray<FractalPart> parents;
        public NativeArray<FractalPart> parts;

        [WriteOnly]
        public NativeArray<float3x4> matrices;

        public void Execute(int i)
        {
            FractalPart parent = parents[i / 5];
            FractalPart part = parts[i];

            part.spinAngle += spinAngleDelta;
            part.worldRotation = mul(parent.worldRotation, mul(part.rotation, quaternion.RotateY(part.spinAngle)));

            part.worldPosition =
                parent.worldPosition +
                mul(parent.worldRotation, (1.5f * scale * part.direction));

            parts[i] = part;
            float3x3 r = float3x3(part.worldRotation) * scale;
            matrices[i] = float3x4(r.c0, r.c1, r.c2, part.worldPosition);
        }
    }

    // State
    NativeArray<FractalPart>[] parts;
    NativeArray<float3x4>[] matrices;
    ComputeBuffer[] matricesBuffer;

    private void OnEnable()
    {
        InitializeMemory();
        SpawnFractal();

        propertyBlock ??= new MaterialPropertyBlock();
    }

    private void OnDisable()
    {
        ReleaseMemory();
    }

    private void OnValidate()
    {
        if (parts != null && enabled)
        {
            OnDisable();
            OnEnable();
        }
    }

    private void Update()
    {
        FractalPart rootPart = parts[0][0];
        float objectScale = transform.lossyScale.x;
        RotateFractal(rootPart, objectScale);

        Bounds bounds = new Bounds(rootPart.worldPosition, 3f * objectScale * Vector3.one);
        for (int i = 0; i < matricesBuffer.Length; i++)
        {
            ComputeBuffer buffer = matricesBuffer[i];
            buffer.SetData(matrices[i]);
            propertyBlock.SetBuffer(matricesID, buffer);
            Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, buffer.count, propertyBlock);
        }
    }

    private void InitializeMemory()
    {
        parts = new NativeArray<FractalPart>[depth];
        matrices = new NativeArray<float3x4>[depth];
        matricesBuffer = new ComputeBuffer[depth];
        int stride = 12 * 4;
        for (int i = 0, length = 1; i < parts.Length; i++, length *= 5)
        {
            parts[i] = new NativeArray<FractalPart>(length, Allocator.Persistent);
            matrices[i] = new NativeArray<float3x4>(length, Allocator.Persistent);
            matricesBuffer[i] = new ComputeBuffer(length, stride);
        }
    }

    private void ReleaseMemory()
    {
        for (int i = 0; i < matricesBuffer.Length; i++)
        {
            matricesBuffer[i].Release();
            parts[i].Dispose();
            matrices[i].Dispose();
        }
        parts = null;
        matrices = null;
        matricesBuffer = null;
    }

    FractalPart CreatePart(int childIndex)
    {
        FractalPart fractalPart = new FractalPart
        {
            direction = directions[childIndex],
            rotation = rotations[childIndex]
        };
        return fractalPart;
    }

    private void SpawnFractal()
    {
        parts[0][0] = CreatePart(0);
        for (int levelIndex = 1; levelIndex < parts.Length; levelIndex++)
        {
            NativeArray<FractalPart> levelParts = parts[levelIndex];
            for (int fractalPartIndex = 0; fractalPartIndex < levelParts.Length; fractalPartIndex += 5)
            {
                for (int childIndex = 0; childIndex < 5; childIndex++)
                {
                    levelParts[fractalPartIndex + childIndex] = CreatePart(childIndex);
                }
            }
        }
    }

    private void RotateFractal(FractalPart rootPart, float objectScale)
    {
        float spinAngleDelta = 0.125f * PI * Time.deltaTime;

        rootPart.spinAngle += spinAngleDelta;
        rootPart.worldRotation = mul(transform.rotation,
            mul(rootPart.rotation, quaternion.RotateY(rootPart.spinAngle)));
        rootPart.worldPosition = transform.position;
        parts[0][0] = rootPart;
        float3x3 r = float3x3(rootPart.worldRotation) * objectScale;
        matrices[0][0] = float3x4(r.c0, r.c1, r.c2, rootPart.worldPosition);

        float scale = objectScale;
        JobHandle jobHandle = default;
        for (int levelIndex = 1; levelIndex < parts.Length; levelIndex++)
        {
            scale *= 0.5f;
            jobHandle = new UpdateFractalLevelJob
            {
                spinAngleDelta = spinAngleDelta,
                scale = scale,
                parents = parts[levelIndex - 1],
                parts = parts[levelIndex],
                matrices = matrices[levelIndex]
            }.ScheduleParallel(parts[levelIndex].Length, 5, jobHandle);
        }
        jobHandle.Complete();
    }
}