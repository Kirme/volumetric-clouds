using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class Clouds : MonoBehaviour
{
    [SerializeField] private Shader shader;
    [SerializeField] private Transform cloudsBox;

    [Header("Movement")]
    [SerializeField] private bool movement = false;
    [Range(0.0f, 10.0f)]
    [SerializeField] private float moveSpeed = 0.1f;
    
    [Header("Cloud Shape")]
    [SerializeField] private int numSteps = 10;
    [SerializeField] private float cloudScale;

    [SerializeField] private float densityMultiplier;
    [Range(0, 1)]
    [SerializeField] private float densityThreshold;

    [SerializeField] private Vector3 offset;

    [Range(0, 1)]
    [SerializeField] private float globalCoverage;

    [SerializeField] private Vector4 shapeNoiseWeights;

    [Header("Cloud Detail")]
    [SerializeField] private Vector3 detailNoiseWeights;
    [SerializeField] private float detailScale;
    [SerializeField] private Vector3 detailOffset;

    [Header("Lighting")]
    [Range(0, 0.1f)]
    [SerializeField] private float lightAbsorption;
    [SerializeField] private int numSunSteps = 4;

    private Material material;

    private void Update() {
        if (movement) {
            offset.x += moveSpeed * Time.deltaTime;
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        if (material == null) {
            material = new Material(shader);
        }

        // Get box for clouds
        material.SetVector("boundsMin", cloudsBox.position - cloudsBox.localScale / 2);
        material.SetVector("boundsMax", cloudsBox.position + cloudsBox.localScale / 2);

        SetProperties();

        // Get noise
        Noise noise = FindObjectOfType<Noise>();

        material.SetTexture("ShapeNoise", noise.GetShapeNoise());
        material.SetTexture("DetailNoise", noise.GetDetailNoise());

        Graphics.Blit(source, destination, material);
    }

    private void SetProperties() {
        // Shape
        material.SetVector("cloudOffset", offset);
        material.SetFloat("cloudScale", cloudScale);
        material.SetFloat("densityThreshold", densityThreshold);
        material.SetFloat("densityMultiplier", densityMultiplier);
        material.SetInt("numSteps", numSteps);
        material.SetFloat("gc", globalCoverage);
        material.SetVector("shapeNoiseWeights", shapeNoiseWeights);

        // Detail
        material.SetVector("detailNoiseWeights", detailNoiseWeights);
        material.SetVector("detailOffset", detailOffset);
        material.SetFloat("detailScale", detailScale);

        // Lighting
        material.SetFloat("lightAbsorption", lightAbsorption);
        material.SetInt("numSunSteps", numSunSteps);
    }
}
