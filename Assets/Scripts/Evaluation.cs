using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Evaluation : MonoBehaviour
{
    // The metric being evaluated
    public enum Metric {
        IMG = 0,
        FPS = 1
    }

    // The parameter being evaluated
    public enum Parameter {
        Seed = 0,
        Coverage = 1,
        Coherence = 2
    }

    [Tooltip("Eval FPS or take screenshots")]
    public Metric metric;
    [Tooltip("Eval different coverages, seeds or coherences")]
    public Parameter parameter;
    
    [Tooltip("Should it interpolate pixels?")]
    public bool useInterpolation;

    public enum March {
        _2 = 2,
        _4 = 4,
        _8 = 8
    }

    [Tooltip("Raymarch every nth pixel")]
    public March marchInterval;

    [Range(0.0f, 0.6f)]
    public float maxPixelDifference;

    public bool showInterpolation;

    private string folder;

    // Executed when a value is altered in inspector
    private void OnValidate() {
        // Switch folder to separate evaluation data
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
