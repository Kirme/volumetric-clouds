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
    [SerializeField] private Evaluation eval;

    private string fileName;
    private CinemachineTrackedDolly dolly;
    private int iteration = 0;

    private FPS fps;

    private void Awake() {
        if (eval.parameter == Evaluation.Parameter.Coverage)
            clouds.densityThreshold = 0.5f;

        if (eval.parameter == Evaluation.Parameter.Coherence)
            eval.coherence = 0.6f; // Since max stddev is 0.57...

        SetFileName();
        fps = GetComponent<FPS>();

        fps.SetShouldCalculate(eval.metric == Evaluation.Metric.FPS);
        fps.SetEval(eval);
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
            if (eval.metric == Evaluation.Metric.IMG)
                TakeScreenshot();

            // If on last position
            if (eval.metric == Evaluation.Metric.FPS &&
                dolly.m_PathPosition >= dolly.m_Path.MaxPos) {
                fps.CloseFile();
            }
        }
    }

    private void SetFileName() {
        int march = eval.useInterpolation ? (int) eval.marchInterval : 0;

        fileName = "";

        switch(eval.parameter) {
            case Evaluation.Parameter.Seed:
                // Name is "shapeSeed_detailSeed_marchInterval_pathPosition", for eval of seeds
                fileName = noise.shapeSeed + "_" + noise.detailSeed + "_" + march;
                break;
            case Evaluation.Parameter.Coverage:
                // Name is "pathPosition_marchInterval_densityThreshold", for eval of coverage
                fileName = march + "_" + clouds.densityThreshold;

                // To order correctly, name is reversed for eval of fps
                if (eval.metric == Evaluation.Metric.FPS)
                    fileName = clouds.densityThreshold + "_" + march;
                break;
            case Evaluation.Parameter.Coherence:
                fileName = eval.coherence + "_" + march;
                break;
        }
    }

    private bool HasNextIteration() {
        bool hasNext;

        switch(eval.parameter) {
            case Evaluation.Parameter.Seed:
                hasNext = SetSeed();
                break;
            case Evaluation.Parameter.Coverage:
                hasNext = SetCoverage();
                break;
            case Evaluation.Parameter.Coherence:
                hasNext = SetCoherence();
                break;
            default:
                hasNext = false;
                break;
        }

        if (hasNext) {
            if (eval.parameter == Evaluation.Parameter.Seed)
                noise.UpdateNoise();
            SetFileName();

            if (eval.metric == Evaluation.Metric.FPS)
                fps.OpenFile();

            dolly.m_PathPosition = 0; // Reset position

            return true;
        }

        return false;
    }

    private void TakeScreenshot() {
        string name = fileName + "_" + dolly.m_PathPosition + ".png";

        string folder = eval.GetFolder() + "/img/";

        if (eval.parameter == Evaluation.Parameter.Coverage) {
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

    private bool SetCoherence() {
        float minCoherence = 0.0f;
        float step = 0.05f;

        if (eval.coherence >= minCoherence + step) {
            // Rounded to avoid floating point errors
            eval.coherence = Mathf.Round((eval.coherence - step) * 100f) / 100f;

            return true;
        }

        return false;
    }

    public string GetFileName() {
        return fileName;
    }
}
