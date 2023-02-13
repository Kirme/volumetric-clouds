using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class Camera : MonoBehaviour {
    [SerializeField] private Clouds clouds;
    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        clouds.Render(source, destination);
    }
}
