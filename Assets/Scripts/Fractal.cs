using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

public class Fractal : MonoBehaviour
{
    // Tunables
    [SerializeField, Range(1, 8)] int depth = 4;
    [SerializeField] Mesh mesh;
    [SerializeField] Material material;

    // Static
    static Vector3[] directions = { Vector3.up, Vector3.right, Vector3.left, 
        Vector3.forward, Vector3.back };
    static Quaternion[] rotations = {Quaternion.identity, Quaternion.Euler(0f, 0f, -90f), Quaternion.Euler(0f, 0f, 90f),
        Quaternion.Euler(90f, 0f, 0f), Quaternion.Euler(-90f, 0f, 0f)};
    static readonly int matricesID = Shader.PropertyToID("_Matrices");
    static MaterialPropertyBlock propertyBlock;

    // Data Structures
    private struct FractalPart
    {
        public Vector3 direction;
        public Vector3 worldPosition;
        public Quaternion rotation;
        public Quaternion worldRotation;
        public float spinAngle;
    }

    // State
    FractalPart[][] parts;
    Matrix4x4[][] matrices;
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
        parts = new FractalPart[depth][];
        matrices = new Matrix4x4[depth][];
        matricesBuffer = new ComputeBuffer[depth];
        int stride = 16 * 4;
        for (int i = 0, length = 1; i < parts.Length; i++, length *= 5)
        {
            parts[i] = new FractalPart[length];
            matrices[i] = new Matrix4x4[length];
            matricesBuffer[i] = new ComputeBuffer(length, stride);
        }
    }

    private void ReleaseMemory()
    {
        for (int i = 0; i < matricesBuffer.Length; i++)
        {
            matricesBuffer[i].Release();
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
            FractalPart[] levelParts = parts[levelIndex];
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
        float spinAngleDelta = 22.5f * Time.deltaTime;

        rootPart.spinAngle += spinAngleDelta;
        rootPart.worldRotation = transform.rotation * 
            (rootPart.rotation * Quaternion.Euler(0f, rootPart.spinAngle, 0f));
        rootPart.worldPosition = transform.position;
        parts[0][0] = rootPart;
        matrices[0][0] = Matrix4x4.TRS(
            rootPart.worldPosition, rootPart.worldRotation, objectScale * Vector3.one);

        float scale = objectScale;
        for (int levelIndex = 1; levelIndex < parts.Length; levelIndex++)
        {
            scale *= 0.5f;
            FractalPart[] parentParts = parts[levelIndex - 1];
            FractalPart[] levelParts = parts[levelIndex];
            Matrix4x4[] levelMatrices = matrices[levelIndex];
            for (int fractalPartIndex = 0; fractalPartIndex < levelParts.Length; fractalPartIndex++)
            {
                FractalPart parent = parentParts[fractalPartIndex / 5];
                FractalPart part = levelParts[fractalPartIndex];

                part.spinAngle += spinAngleDelta;
                part.worldRotation =
                    parent.worldRotation *
                    (part.rotation * Quaternion.Euler(0f, part.spinAngle, 0f));

                part.worldPosition =
                    parent.worldPosition +
                    parent.worldRotation *
                    (1.5f * scale * part.direction);

                levelParts[fractalPartIndex] = part;
                levelMatrices[fractalPartIndex] = Matrix4x4.TRS(
                    part.worldPosition, part.worldRotation, scale * Vector3.one);
            }
        }
    }
}