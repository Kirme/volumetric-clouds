using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class Clouds : MonoBehaviour
{
    [Tooltip("Ray march shader")]
    [SerializeField] private Shader shader;
    [Tooltip("Box that contains the clouds")]
    [SerializeField] private Transform cloudsBox;
    [Tooltip("Counters banding caused by long step lengths in ray marcher")]
    [SerializeField] private Texture2D blueNoise;

    [SerializeField] private Evaluation eval;

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
        // Move clouds in x direction, if applicable
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

        // Set the noise textures in the ray march shader
        material.SetTexture("ShapeNoise", noise.GetShapeNoise());
        material.SetTexture("DetailNoise", noise.GetDetailNoise());
        material.SetTexture("blueNoise", blueNoise);

        material.SetTexture("_SourceTex", source); // Used for ray marching

        if (eval.useInterpolation) { // If should interpolate, run the shader twice
            RunShaderTwice(source, destination);
        } else { // Otherwise, just run shader
            Graphics.Blit(source, destination, material);
        }
    }

    // Runs the ray march shader twice, to allow for interpolation
    private void RunShaderTwice(RenderTexture source, RenderTexture destination) {
        // create a temp texture to hold result of first shader execution
        RenderTexture tmp = RenderTexture.GetTemporary(source.width, source.height, source.depth);

        material.SetInt("isFirstIteration", 1); // Parameter so shader knows current iteration
        Graphics.Blit(source, tmp, material);

        material.SetInt("isFirstIteration", 0);
        Graphics.Blit(tmp, destination, material);

        RenderTexture.ReleaseTemporary(tmp);
    }

    // Set the different properties required for ray marching
    private void SetProperties() {
        // Need to convert bool to int due to no support for setting a material's bool
        int interpolate = eval.useInterpolation ? 1 : 0;
        material.SetInt("useInterpolation", interpolate);

        material.SetInt("marchInterval", (int) eval.marchInterval);

        material.SetFloat("maxPixelDiff", eval.maxPixelDifference);
        material.SetInt("showInterpolation", eval.showInterpolation ? 1 : 0);
    
        // Shape
        material.SetVector("cloudOffset", offset);
        material.SetFloat("cloudScale", cloudScale);
        material.SetFloat("densityThreshold", densityThreshold);
        material.SetFloat("densityMultiplier", densityMultiplier);
        material.SetInt("numSteps", numSteps);
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
