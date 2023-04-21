using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

[ExecuteAlways]
public class Noise : MonoBehaviour {
    private const int threadGroupSize = 8;

    // Noise textures
    [SerializeField] private RenderTexture shapeNoise;
    [SerializeField] private RenderTexture detailNoise;

    [SerializeField] private ComputeShader computeShader;
    

    [Header("Shape Noise Parameters")]
    [SerializeField] private int shapeResolution = 128; // Tex resolution
    [SerializeField] private int shapeCellCount = 4; // Number of cells for Worley
    public int shapeSeed = 0; // Noise seed

    [Header("Detail Noise Parameters")]
    [SerializeField] private int detailResolution = 128;
    [SerializeField] private int detailCellCount = 4;
    public int detailSeed = 0;

    private ComputeBuffer computeBuffer;

    private bool needsUpdate = true; // Should we recalculate the noise tex?

    // Params for the texture currently being calculated (shape or detail)
    private int textureResolution;
    private int cellCount;
    private int seed;

    void Update() {
        // If we should update the textures
        if (needsUpdate) {
            needsUpdate = false;
            GenerateShapeNoise();
            GenerateDetailNoise();
        }
    }

    // Update noise when parameter is changed in inspector
    private void OnValidate() {
        needsUpdate = true;
    }

    // Manually update noise 
    public void UpdateNoise() {
        needsUpdate = true;
    }

    // Generate the shape noise
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

    /*
     * Generate a noise texture (RGBA)
     * renderTexture - Reference to render texture where we store noise
     * chNum - Number of RGBA channels to use
     * chMult - Factor to multiply cell count by for each channel
     */
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

        // Release buffer at the end
        computeBuffer.Release();
        computeBuffer = null;
    }

    /*
     * Dispatch the compute shader and update relevant parameters
     * channel - Current RGBA channel to use
     * ccc - Current Cell Count, number of cells to use for Worley
     */
    private void DispatchShader(int channel, int ccc) {
        UpdateParameters(channel, ccc);

        // Set number of thread groups based on res and group size
        int numGroups = Mathf.CeilToInt(textureResolution / (float) threadGroupSize);
        computeShader.Dispatch(0, numGroups, numGroups, numGroups);
    }

    /*
     * Update parameters that might change between channels
     * channel - Current RGBA channel to use
     * ccc - Current Cell Count, number of cells to use for Worley
     */
    private void UpdateParameters(int channel, int ccc) {
        CreatePointsBuffer(ccc); // Create buffer for random points
        computeShader.SetBuffer(0, "_Points", computeBuffer);

        computeShader.SetInt("_CellCount", ccc);
        computeShader.SetInt("_CellResolution", textureResolution / ccc);
        computeShader.SetInt("_Channel", channel);
    }

    // Initialize the texture for the shape noise
    private void CreateShapeNoiseTexture() {
        if (shapeNoise != null)
            shapeNoise.Release();

        CreateRenderTexture(ref shapeNoise);
    }

    // Initialize the texture for the detail noise
    private void CreateDetailNoiseTexture() {
        if (detailNoise != null)
            detailNoise.Release();

        CreateRenderTexture(ref detailNoise);
    }

    /*
     * Create a render texture with correct parameters
     * renderTexture - Ref to the texture we create
     */
    private void CreateRenderTexture(ref RenderTexture renderTexture) {
        // Release if it already exists
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

    /*
     * Create the buffer of points that determine Worley noise
     * currentCellCount - Num of cells to use for Worley noise
     */
    private void CreatePointsBuffer(int currentCellCount) {
        if (computeBuffer != null) {
            computeBuffer.Release();
            computeBuffer = null;
        }

        // One point in each cell, cell count used in each direction
        int numPoints = currentCellCount * currentCellCount * currentCellCount;
        List<Vector3> points = new List<Vector3>();

        // Create a point in a random position within each cell
        for (int i = 0; i < numPoints; i++) {
            // Point determined by three values 0 - 1, states position
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
