using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

using static Unity.Mathematics.math;
using quaternion = Unity.Mathematics.quaternion;
using Random = UnityEngine.Random;

public class Fractal : MonoBehaviour
{
    // Tunables
    [SerializeField, Range(3, 10)] int depth = 6;
    [SerializeField] Mesh mesh;
    [SerializeField] Mesh leafMesh;
    [SerializeField] Material material;
    [SerializeField] Gradient gradientA;
    [SerializeField] Gradient gradientB;
    [SerializeField] Color leafColorA;
    [SerializeField] Color leafColorB;
    [SerializeField, Range(0f, 90f)] float maxSagAngleA = 15f;
    [SerializeField, Range(0f, 90f)] float maxSagAngleB = 25f;
    [SerializeField, Range(0f, 90f)] float spinSpeedA = 20f;
    [SerializeField, Range(0f, 90f)] float spinSpeedB = 40f;
    [SerializeField, Range(0f, 1f)] float reverseSpinChance = 0.25f;

    // Static
    static quaternion[] rotations = {quaternion.identity,
        quaternion.RotateZ(-0.5f * PI), quaternion.RotateZ(0.5f * PI),
        quaternion.RotateX(0.5f * PI), quaternion.RotateX(-0.5f * PI)
    };
    static readonly int colorAID = Shader.PropertyToID("_ColorA");
    static readonly int colorBID = Shader.PropertyToID("_ColorB");
    static readonly int sequenceNumbersID = Shader.PropertyToID("_SequenceNumbers");
    static readonly int matricesID = Shader.PropertyToID("_Matrices");
    static MaterialPropertyBlock propertyBlock;

    // Data Structures
    private struct FractalPart
    {
        public float3 worldPosition;
        public quaternion rotation;
        public quaternion worldRotation;
        public float maxSagAngle;
        public float spinAngle;
        public float spinVelocity;
    }

    [BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
    private struct UpdateFractalLevelJob : IJobFor
    {
        public float scale;
        public float deltaTime;

        [ReadOnly]
        public NativeArray<FractalPart> parents;
        public NativeArray<FractalPart> parts;

        [WriteOnly]
        public NativeArray<float3x4> matrices;

        public void Execute(int i)
        {
            FractalPart parent = parents[i / 5];
            FractalPart part = parts[i];

            part.spinAngle += part.spinVelocity * deltaTime;

            float3 upAxis = mul(mul(parent.worldRotation, part.rotation), up());
            float3 sagAxis = cross(up(), upAxis);

            float sagMagnitude = length(sagAxis);
            quaternion baseRotation;
            if (sagMagnitude > 0f)
            {
                sagAxis /= sagMagnitude;
                quaternion sagRotation = quaternion.AxisAngle(sagAxis, part.maxSagAngle *sagMagnitude);
                baseRotation = mul(sagRotation, parent.worldRotation);
            }
            else
            {
                baseRotation = parent.worldRotation;
            }

            part.worldRotation = mul(baseRotation, mul(part.rotation, quaternion.RotateY(part.spinAngle)));

            part.worldPosition =
                parent.worldPosition +
                mul(part.worldRotation, float3(0f, 1.5f * scale, 0f));

            parts[i] = part;
            float3x3 r = float3x3(part.worldRotation) * scale;
            matrices[i] = float3x4(r.c0, r.c1, r.c2, part.worldPosition);
        }
    }

    // State
    NativeArray<FractalPart>[] parts;
    NativeArray<float3x4>[] matrices;
    ComputeBuffer[] matricesBuffer;
    Vector4[] sequenceNumbers;

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
        int leafIndex = matricesBuffer.Length - 1;
        for (int i = 0; i < matricesBuffer.Length; i++)
        {
            Color colorA;
            Color colorB;
            Mesh instanceMesh;
            if (i == leafIndex)
            {
                colorA = leafColorA;
                colorB = leafColorB;
                instanceMesh = leafMesh;
            }
            else
            {
                float gradientInterpolator = i / (matricesBuffer.Length - 1f);
                colorA = gradientA.Evaluate(gradientInterpolator);
                colorB = gradientB.Evaluate(gradientInterpolator);
                instanceMesh = mesh;
            }

            ComputeBuffer buffer = matricesBuffer[i];
            buffer.SetData(matrices[i]);
            propertyBlock.SetVector(sequenceNumbersID, sequenceNumbers[i]);
            propertyBlock.SetColor(colorAID, colorA);
            propertyBlock.SetColor(colorBID, colorB);
            propertyBlock.SetBuffer(matricesID, buffer);
            Graphics.DrawMeshInstancedProcedural(instanceMesh, 0, material, bounds, buffer.count, propertyBlock);
        }
    }

    private void InitializeMemory()
    {
        parts = new NativeArray<FractalPart>[depth];
        matrices = new NativeArray<float3x4>[depth];
        matricesBuffer = new ComputeBuffer[depth];
        sequenceNumbers = new Vector4[depth];
        int stride = 12 * 4;
        for (int i = 0, length = 1; i < parts.Length; i++, length *= 5)
        {
            parts[i] = new NativeArray<FractalPart>(length, Allocator.Persistent);
            matrices[i] = new NativeArray<float3x4>(length, Allocator.Persistent);
            matricesBuffer[i] = new ComputeBuffer(length, stride);
            sequenceNumbers[i] = new Vector4(Random.value, Random.value, Random.value, Random.value);
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
        sequenceNumbers = null;
    }

    FractalPart CreatePart(int childIndex)
    {
        FractalPart fractalPart = new FractalPart
        {
            maxSagAngle = radians(Random.Range(maxSagAngleA, maxSagAngleB)),
            rotation = rotations[childIndex],
            spinVelocity = (Random.value < reverseSpinChance ? -1f : 1f) * radians(Random.Range(spinSpeedA, spinSpeedB))
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
        float deltaTime = Time.deltaTime;

        rootPart.spinAngle += rootPart.spinVelocity * deltaTime;
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
                deltaTime = deltaTime,
                scale = scale,
                parents = parts[levelIndex - 1],
                parts = parts[levelIndex],
                matrices = matrices[levelIndex]
            }.ScheduleParallel(parts[levelIndex].Length, 5, jobHandle);
        }
        jobHandle.Complete();
    }
}