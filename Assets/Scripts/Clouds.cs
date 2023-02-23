using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class Clouds : MonoBehaviour
{
    [SerializeField] private Shader shader;
    [SerializeField] private Transform cloudsBox;
    [Tooltip("Counters banding caused by long step lengths in ray marcher")]
    [SerializeField] private Texture2D blueNoise;

    public enum March {
        _2 = 2,
        _4 = 4,
        _8 = 8
    }

    [Tooltip("Should it interpolate every other pixel?")]
    public bool useInterpolation;

    [Tooltip("Only march every nth pixel")]
    public March marchInterval;

    [Header("Movement")]
    [SerializeField] private bool movement = false;
    [Range(0.0f, 10.0f)]
    [SerializeField] private float moveSpeed = 0.1f;
    
    [Header("Cloud Shape")]
    [SerializeField] private int numSteps = 10;
    [SerializeField] private float cloudScale;

    [SerializeField] private float densityMultiplier;
    [Range(0, 1)]
    public float densityThreshold;

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

    public void Render(RenderTexture source, RenderTexture destination) {
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
        material.SetTexture("blueNoise", blueNoise);

        if (useInterpolation) {
            RunShaderTwice(source, destination);
        } else {
            Graphics.Blit(source, destination, material);
        }
    }

    private void RunShaderTwice(RenderTexture source, RenderTexture destination) {
        RenderTexture tmp = RenderTexture.GetTemporary(source.width, source.height, source.depth);

        material.SetInt("isFirstIteration", 1);
        Graphics.Blit(source, tmp, material);

        material.SetInt("isFirstIteration", 0);
        Graphics.Blit(tmp, destination, material);

        RenderTexture.ReleaseTemporary(tmp);
    }

    private void SetProperties() {
        // Need to convert to int due to no support for setting a material's bool
        int interpolate = useInterpolation ? 1 : 0;
        material.SetInt("useInterpolation", interpolate);
        material.SetInt("marchInterval", (int) marchInterval);
    
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
