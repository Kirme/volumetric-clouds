using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class Camera : MonoBehaviour {
    [SerializeField] private Clouds clouds;
    [SerializeField] private CinemachineVirtualCamera virtualCamera;
    [SerializeField] private float cameraSpeed;

    [Tooltip("For Evaluation")]
    [SerializeField] private Noise noise;
    [SerializeField] private List<Vector2Int> seeds;
    [SerializeField] private bool shouldEvalFPS;
    [SerializeField] private bool shouldEvalCoverage;

    private string fileName;
    private CinemachineTrackedDolly dolly;
    private int iteration = 0;

    private FPS fps;

    private void Awake() {
        if (shouldEvalCoverage)
            clouds.densityThreshold = 0.5f;

        SetFileName();
        fps = GetComponent<FPS>();

        fps.SetShouldCalculate(shouldEvalFPS);
        fps.SetShouldEvalCoverage(shouldEvalCoverage);
    }

    private void Start() {
        dolly = virtualCamera.GetCinemachineComponent<CinemachineTrackedDolly>();
        dolly.m_PathPosition = 0.0f;

        SetSeed();
    }

    private void FixedUpdate() {
        // Should only move and evaluate when playing
        if (dolly.m_PathPosition >= dolly.m_Path.MaxPos) {
            if (!HasNextIteration()) {
                return;
            }
        }

        // Update position on path
        dolly.m_PathPosition += cameraSpeed;

        // Avg to two decimal points
        dolly.m_PathPosition = Mathf.Round(dolly.m_PathPosition * 100f) / 100f;

        if (IsAtWholeValue(dolly.m_PathPosition)) {
            if (!shouldEvalFPS)
                TakeScreenshot();

            // If on last position
            if (shouldEvalFPS && dolly.m_PathPosition >= dolly.m_Path.MaxPos) {
                fps.CloseFile();
            }
        }
    }

    private void SetFileName() {
        int march = clouds.useInterpolation ? (int)clouds.marchInterval : 0;

        // Name is "shapeSeed_detailSeed_marchInterval_pathPosition", for eval of seeds
        fileName = noise.shapeSeed + "_" + noise.detailSeed + "_" + march;

        // Name is "pathPosition_marchInterval_densityThreshold", for eval of coverage
        if (shouldEvalCoverage) {
            fileName = march + "_" + clouds.densityThreshold;

            // To order correctly, name is reversed for eval of fps
            if (shouldEvalFPS)
                fileName = clouds.densityThreshold + "_" + march;
        }
            
    }

    private bool HasNextIteration() {
        bool hasNext = shouldEvalCoverage ? SetCoverage() : SetSeed();

        if (hasNext) {
            if (!shouldEvalCoverage)
                noise.UpdateNoise();
            SetFileName();

            if (shouldEvalFPS)
                fps.OpenFile();

            dolly.m_PathPosition = 0; // Reset position

            return true;
        }

        return false;
    }

    private void TakeScreenshot() {
        string name = fileName + "_" + dolly.m_PathPosition + ".png";

        string folder = "./eval/img/";

        if (shouldEvalCoverage) {
            folder = "./eval/coverage/img/";
            // Different name order due to different ordering in eval
            name = dolly.m_PathPosition + "_" + fileName + ".png";
        }
            

        ScreenCapture.CaptureScreenshot(folder + name);
    }

    private bool IsAtWholeValue(float pos) {
        return pos == (int) pos;
    }

    private bool SetSeed() {
        if (iteration < seeds.Count) {
            Vector2Int seed = seeds[iteration];

            noise.shapeSeed = seed.x;
            noise.detailSeed = seed.y;

            iteration++;

            return true;
        }

        return false;
    }

    private bool SetCoverage() {
        float minDensity = 0.1f;
        
        if (clouds.densityThreshold >= minDensity + 0.1f) {
            clouds.densityThreshold -= 0.1f;
            return true;
        }

        return false;
    }

    public string GetFileName() {
        return fileName;
    }
}
