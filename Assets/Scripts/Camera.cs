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
    
    private string fileName;
    private CinemachineTrackedDolly dolly;
    private int iteration = 0;

    private FPS fps;

    private void Awake() {
        SetFileName();
        fps = GetComponent<FPS>();

        fps.SetShouldCalculate(shouldEvalFPS);
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

        // Name is "densiyThreshold_marchInterval_pathPosition", for eval of coverage
        //fileName = clouds.densityThreshold + "_" + march;
    }

    private bool HasNextIteration() {
        if (SetSeed()) {
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

        Debug.Log("Screenshot taken: " + name);
        ScreenCapture.CaptureScreenshot("./eval/img/" + name);
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

    public string GetFileName() {
        return fileName;
    }
}
