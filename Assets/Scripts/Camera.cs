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
        dolly.m_PathPosition = Mathf.Round(dolly.m_PathPosition * 100f) / 100f;

        if (IsAtWholeValue(dolly.m_PathPosition)) {
            TakeScreenshot();
        }
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        clouds.Render(source, destination);
    }

    private void TakeScreenshot() {
        int march = clouds.useInterpolation ? (int) clouds.marchInterval : 0;

        // Name is "shapeSeed_detailSeed_marchInterval_pathPosition", for eval of seeds
        string name = noise.shapeSeed + "_" + noise.detailSeed + "_" + march + "_" + dolly.m_PathPosition + ".png";

        // Name is "densiyThreshold_marchInterval_pathPosition", for eval of coverage
        //string name = clouds.densityThreshold + "_" + march + "_" + dolly.m_PathPosition + ".png";

        Debug.Log("Screenshot taken: " + name);
        ScreenCapture.CaptureScreenshot(name);
    }

    private bool IsAtWholeValue(float pos) {
        return pos == (int) pos;
    }
}
