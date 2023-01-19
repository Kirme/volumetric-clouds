using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

public class Noise : MonoBehaviour {
    [SerializeField] private RenderTexture renderTexture;

    [SerializeField] private ComputeShader computeShader;

    [Header("Worley Parameters")]
    [SerializeField] private int textureResolution = 128;
    [SerializeField] int cellCount = 4;
    [SerializeField] int seed = 5;

    [Range(0, 1)]
    [SerializeField] private float noiseThreshold = 0;

    private ComputeBuffer computeBuffer;
    private int threads = 8;

    void Start() {
        GenerateNoise();
    }

    private void GenerateNoise() {
        // Initialization
        CreateRenderTexture();
        Random.InitState(seed);
        
        computeShader.SetInt("_TextureResolution", textureResolution);
        computeShader.SetTexture(0, "_Result", renderTexture);
        computeShader.SetFloat("_Threshold", noiseThreshold);

        // Run compute shader once for each channel (rgba), w. increasing freq
        int currentCellCount = cellCount;
        for (int i = 0; i < 4; i++) {
            DispatchShader(i, currentCellCount);
            if (i != 3)
                currentCellCount *= 2;
        }

        computeBuffer.Release();
        computeBuffer = null;
    }

    // Dispatch the compute shader and update relevant parameters
    private void DispatchShader(int channel, int ccc) {
        UpdateParameters(channel, ccc);

        computeShader.Dispatch(0, threads, threads, threads);
    }

    // Update parameters that might change between channels
    private void UpdateParameters(int channel, int ccc) {
        CreatePointsBuffer(ccc);
        computeShader.SetBuffer(0, "_Points", computeBuffer);

        computeShader.SetInt("_CellCount", ccc);
        computeShader.SetInt("_CellResolution", textureResolution / ccc);
        computeShader.SetInt("_Channel", channel);
    }

    // Create the render texture
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

    // Create the buffer of points that determine worley noise
    private void CreatePointsBuffer(int currentCellCount) {
        int numPoints = currentCellCount * currentCellCount * currentCellCount;
        Vector3[] points = new Vector3[numPoints];

        for (int i = 0; i < numPoints; i++) {
            Vector3 point = new Vector3(Random.value, Random.value, Random.value);
            points[i] = point;
        }

        computeBuffer = new ComputeBuffer(numPoints, sizeof(float) * 3, ComputeBufferType.Structured);
        computeBuffer.SetData(points);
    }
}
