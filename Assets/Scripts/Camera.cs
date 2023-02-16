using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

[ExecuteAlways, ImageEffectAllowedInSceneView]
public class Camera : MonoBehaviour {
    [SerializeField] private Clouds clouds;
    [SerializeField] private CinemachineVirtualCamera virtualCamera;
    [SerializeField] private float cameraSpeed;

    [Tooltip("For Evaluation")]
    [SerializeField] private Noise noise;

    private CinemachineTrackedDolly dolly;

    public string fileName;

    private void Awake() {
        if (!Application.isPlaying) return;

        int march = clouds.useInterpolation ? (int)clouds.marchInterval : 0;

        // Name is "shapeSeed_detailSeed_marchInterval_pathPosition", for eval of seeds
        fileName = noise.shapeSeed + "_" + noise.detailSeed + "_" + march;

        // Name is "densiyThreshold_marchInterval_pathPosition", for eval of coverage
        //fileName = clouds.densityThreshold + "_" + march;
    }

    private void Start() {
        dolly = virtualCamera.GetCinemachineComponent<CinemachineTrackedDolly>();
        dolly.m_PathPosition = 0.0f;
    }

    private void FixedUpdate() {
        // Should only move and evaluate when playing
        if (!Application.isPlaying || dolly.m_PathPosition >= dolly.m_Path.MaxPos)
            return;

        // Update position on path
        dolly.m_PathPosition += cameraSpeed;

        // Avg to two decimal points
        dolly.m_PathPosition = Mathf.Round(dolly.m_PathPosition * 100f) / 100f;

        if (IsAtWholeValue(dolly.m_PathPosition)) {
            TakeScreenshot();

            // If on last position
            if (dolly.m_PathPosition >= dolly.m_Path.MaxPos) {
                GetComponent<FPS>().CloseFile();
            }
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        clouds.Render(source, destination);
    }

    private void TakeScreenshot() {
        string name = fileName + "_" + dolly.m_PathPosition + ".png";

        Debug.Log("Screenshot taken: " + name);
        ScreenCapture.CaptureScreenshot("./eval/img/" + name);
    }

    private bool IsAtWholeValue(float pos) {
        return pos == (int) pos;
    }
}
