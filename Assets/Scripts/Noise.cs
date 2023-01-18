using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

public class Noise : MonoBehaviour {
    [SerializeField] private RenderTexture renderTexture;

    [SerializeField] private int textureResolution = 128;
    [SerializeField] int cellCount = 4;

    [SerializeField] private ComputeShader computeShader;
    private ComputeBuffer computeBuffer;

    private int threads = 8;

    void Start() {
        GenerateNoise();
    }

    
    private void GenerateNoise() {
        CreateRenderTexture();

        Random.InitState(5);

        CreatePointsBuffer();

        computeShader.SetInt("_TextureResolution", textureResolution);
        computeShader.SetInt("_CellResolution", textureResolution / cellCount);
        computeShader.SetInt("_CellCount", cellCount);
        computeShader.SetBuffer(0, "_Points", computeBuffer);
        computeShader.SetTexture(0, "_Result", renderTexture);

        computeShader.Dispatch(0, threads, threads, threads);

        computeBuffer.Release();
        computeBuffer = null;
    }

    private void CreateRenderTexture() {
        if (renderTexture != null)
            renderTexture.Release();

        renderTexture = new RenderTexture(textureResolution, textureResolution, 0)
        {
            enableRandomWrite = true,
            dimension = TextureDimension.Tex3D,
            volumeDepth = textureResolution,
            name = "_NoiseTex",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };

        renderTexture.Create();
    }

    private void CreatePointsBuffer() {
        int numPoints = cellCount * cellCount * cellCount;
        Vector3[] points = new Vector3[numPoints];

        for (int i = 0; i < numPoints; i++) {
            Vector3 point = new Vector3(Random.value, Random.value, Random.value);
            points[i] = point;
        }

        computeBuffer = new ComputeBuffer(numPoints, sizeof(float) * 3, ComputeBufferType.Structured);
        computeBuffer.SetData(points);
    }
}
