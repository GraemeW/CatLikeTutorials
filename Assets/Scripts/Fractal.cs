using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    // Data Structures
    private struct FractalPart
    {
        public Vector3 direction;
        public Quaternion rotation;
        public Transform transform;
    }

    // State
    FractalPart[][] parts;

    private void Awake()
    {
        InitializeFractalParts();
        SpawnFractal();
    }

    private void Update()
    {
        Quaternion deltaRotation = Quaternion.Euler(0f, 22.5f * Time.deltaTime, 0f);

        FractalPart rootPart = parts[0][0];
        rootPart.rotation *= deltaRotation;
        rootPart.transform.localRotation = rootPart.rotation;
        parts[0][0] = rootPart;

        for (int levelIndex = 1; levelIndex < parts.Length; levelIndex++)
        {
            FractalPart[] parentParts = parts[levelIndex - 1];
            FractalPart[] levelParts = parts[levelIndex];
            for (int fractalPartIndex = 0; fractalPartIndex < levelParts.Length; fractalPartIndex++)
            {
                Transform parentTransform = parentParts[fractalPartIndex / 5].transform;
                FractalPart part = levelParts[fractalPartIndex];
                part.rotation *= deltaRotation;

                part.transform.localRotation =
                    parentTransform.localRotation * part.rotation;

                part.transform.localPosition =
                    parentTransform.localPosition + 
                    parentTransform.localRotation *
                    (1.5f * part.transform.localScale.x * part.direction);
                levelParts[fractalPartIndex] = part;
            }
        }
    }

    private void InitializeFractalParts()
    {
        parts = new FractalPart[depth][];
        for (int i = 0, length = 1; i < parts.Length; i++, length *= 5)
        {
            parts[i] = new FractalPart[length];
        }
    }

    private void SpawnFractal()
    {
        float scale = 1f;
        parts[0][0] = CreatePart(0, 0, scale);
        for (int levelIndex = 1; levelIndex < parts.Length; levelIndex++)
        {
            scale *= 0.5f;
            FractalPart[] levelParts = parts[levelIndex];
            for (int fractalPartIndex = 0; fractalPartIndex < levelParts.Length; fractalPartIndex += 5)
            {
                for (int childIndex = 0; childIndex < 5; childIndex++)
                {
                    levelParts[fractalPartIndex + childIndex] = CreatePart(levelIndex, childIndex, scale);
                }
            }
        }
    }

    FractalPart CreatePart(int levelIndex, int childIndex, float scale)
    {
        GameObject go = new GameObject($"Fractal Part L{levelIndex} C{childIndex}");
        go.transform.SetParent(transform, false);
        go.transform.localScale = scale * Vector3.one;
        go.AddComponent<MeshFilter>().mesh = mesh;
        go.AddComponent<MeshRenderer>().material = material;

        FractalPart fractalPart = new FractalPart
        {
            direction = directions[childIndex],
            rotation = rotations[childIndex],
            transform = go.transform
        };
        return fractalPart;    
    }

}