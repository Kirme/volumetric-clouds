using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class Clouds : MonoBehaviour
{
    [SerializeField] private Shader shader;
    [SerializeField] private Transform cloudsBox;
    private Material material;

    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        if (material == null) {
            material = new Material(shader);
        }

        // Get box for clouds
        material.SetVector("boundsMin", cloudsBox.position - cloudsBox.localScale / 2);
        material.SetVector("boundsMax", cloudsBox.position + cloudsBox.localScale / 2);
        // Get noise
        Noise noise = FindObjectOfType<Noise>();
        material.SetTexture("shapeNoise", noise.GetNoise());

        Graphics.Blit(source, destination, material);
    }
}
