using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Evaluation : MonoBehaviour
{
    public enum Metric {
        IMG = 0,
        FPS = 1
    }

    public enum Parameter {
        Seed = 0,
        Coverage = 1,
        Coherence = 2
    }

    [Tooltip("Eval FPS or take screenshots")]
    public Metric metric;
    [Tooltip("Eval different coverages or seeds")]
    public Parameter parameter;
    
    [Tooltip("Should it interpolate every other pixel?")]
    public bool useInterpolation;

    public enum March {
        _2 = 2,
        _4 = 4,
        _8 = 8
    }

    [Tooltip("Raymarch every nth pixel")]
    public March marchInterval;

    [Range(0.0f, 0.6f)]
    public float coherence;

    public bool showInterpolation;

    private string folder;

    private void OnValidate() {
        switch(parameter) {
            case Parameter.Seed:
                folder = "./eval";
                break;
            case Parameter.Coverage:
                folder = "./eval/coverage";
                break;
            case Parameter.Coherence:
                folder = "./eval/coherence";
                break;
        }
    }

    public string GetFolder() {
        return folder;
    }
}
