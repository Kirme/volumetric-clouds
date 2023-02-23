using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

[ExecuteAlways]
public class Noise : MonoBehaviour {
    private const int threadGroupSize = 8;

    [SerializeField] private RenderTexture shapeNoise;
    [SerializeField] private RenderTexture detailNoise;

    [SerializeField] private ComputeShader computeShader;
    

    [Header("Shape Noise Parameters")]
    [SerializeField] private int shapeResolution = 128;
    [SerializeField] private int shapeCellCount = 4;
    public int shapeSeed = 0;

    [Header("Detail Noise Parameters")]
    [SerializeField] private int detailResolution = 128;
    [SerializeField] private int detailCellCount = 4;
    public int detailSeed = 0;

    private ComputeBuffer computeBuffer;

    private bool needsUpdate = true;

    private int textureResolution;
    private int cellCount;
    private int seed;

    void Update() {
        if (needsUpdate) {
            needsUpdate = false;
            GenerateShapeNoise();
            GenerateDetailNoise();
        }
    }

    private void OnValidate() {
        needsUpdate = true;
    }

    public void UpdateNoise() {
        needsUpdate = true;
    }

    private void GenerateShapeNoise() {
        textureResolution = shapeResolution;
        cellCount = shapeCellCount;
        CreateShapeNoiseTexture();
        seed = shapeSeed;

        GenerateNoise(ref shapeNoise, 4, 2);
    }

    private void GenerateDetailNoise() {
        textureResolution = detailResolution;
        cellCount = detailCellCount;
        CreateDetailNoiseTexture();
        seed = detailSeed;

        GenerateNoise(ref detailNoise, 3, 2);
    }

    public void GenerateNoise(ref RenderTexture renderTexture, int chNum, int chMult) {
        // Initialization
        Random.InitState(seed);
        
        computeShader.SetInt("_TextureResolution", textureResolution);
        computeShader.SetTexture(0, "_Result", renderTexture);

        // Run compute shader once for each channel (rgba), w. increasing freq
        int currentCellCount = cellCount;
        for (int i = 0; i < chNum; i++) {
            DispatchShader(i, currentCellCount);
            currentCellCount *= chMult;
        }
        computeBuffer.Release();
        computeBuffer = null;
    }

    // Dispatch the compute shader and update relevant parameters
    private void DispatchShader(int channel, int ccc) {
        UpdateParameters(channel, ccc);

        int numGroups = Mathf.CeilToInt(textureResolution / (float) threadGroupSize);
        computeShader.Dispatch(0, numGroups, numGroups, numGroups);
    }

    // Update parameters that might change between channels
    private void UpdateParameters(int channel, int ccc) {
        CreatePointsBuffer(ccc);
        computeShader.SetBuffer(0, "_Points", computeBuffer);

        computeShader.SetInt("_CellCount", ccc);
        computeShader.SetInt("_CellResolution", textureResolution / ccc);
        computeShader.SetInt("_Channel", channel);
    }

    private void CreateShapeNoiseTexture() {
        if (shapeNoise != null)
            shapeNoise.Release();

        CreateRenderTexture(ref shapeNoise);
    }

    private void CreateDetailNoiseTexture() {
        if (detailNoise != null)
            detailNoise.Release();

        CreateRenderTexture(ref detailNoise);
    }

    // Create the render texture
    private void CreateRenderTexture(ref RenderTexture renderTexture) {
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
        if (computeBuffer != null) {
            computeBuffer.Release();
            computeBuffer = null;
        }

        int numPoints = currentCellCount * currentCellCount * currentCellCount;
        List<Vector3> points = new List<Vector3>();

        for (int i = 0; i < numPoints; i++) {
            Vector3 point = new Vector3(Random.value, Random.value, Random.value);
            points.Add(point);
        }

        computeBuffer = new ComputeBuffer(numPoints, sizeof(float) * 3, ComputeBufferType.Structured);
        computeBuffer.SetData(points);
    }

    public RenderTexture GetShapeNoise() {
        return shapeNoise;
    }

    public RenderTexture GetDetailNoise() {
        return detailNoise;
    }
}
