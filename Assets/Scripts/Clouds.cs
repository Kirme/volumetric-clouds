using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class Clouds : MonoBehaviour
{
    [SerializeField] private Shader shader;
    [SerializeField] private Transform cloudsBox;
    private Material material;

    [SerializeField] private int numSteps = 10;
    [SerializeField] private float cloudScale;

    [SerializeField] private float densityMultiplier;
    [Range(0, 1)]
    [SerializeField] private float densityThreshold;

    [SerializeField] private Vector3 offset;

    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        if (material == null) {
            material = new Material(shader);
        }

        // Get box for clouds
        material.SetVector("boundsMin", cloudsBox.position - cloudsBox.localScale / 2);
        material.SetVector("boundsMax", cloudsBox.position + cloudsBox.localScale / 2);
        material.SetVector("cloudOffset", offset);
        material.SetFloat("cloudScale", cloudScale);
        material.SetFloat("densityThreshold", densityThreshold);
        material.SetFloat("densityMultiplier", densityMultiplier);
        material.SetInt("numSteps", numSteps);

        // Get noise
        RenderTexture noise = FindObjectOfType<Noise>().GetNoise();
        material.SetTexture("ShapeNoise", noise);

        Graphics.Blit(source, destination, material);
    }
}
