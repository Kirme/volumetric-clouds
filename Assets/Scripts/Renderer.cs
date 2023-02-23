using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways, ImageEffectAllowedInSceneView]
public class Renderer : MonoBehaviour {
    [SerializeField] private Clouds clouds;

    private void OnRenderImage(RenderTexture source, RenderTexture destination) {
        clouds.Render(source, destination);
    }
}
