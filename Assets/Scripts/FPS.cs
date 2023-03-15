using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class FPS : MonoBehaviour {
    private float interval = 1.0f;
    private float timeLeft = 0;
    private int frames = 0;

    private bool shouldCalculate;
    private bool skippedFirst;

    private Evaluation eval;
    private StreamWriter writer;

    private void Start() {
        timeLeft = interval;
        skippedFirst = false;

        if (shouldCalculate)
            OpenFile();
    }

    private void Update() {
        if (!shouldCalculate) return;
    
        timeLeft -= Time.deltaTime;
        frames++;

        if (timeLeft <= 0) {
            // Skip first to avoid FPS outliars on startup
            if (skippedFirst) WriteToFile(frames);
            else skippedFirst = true;

            ResetFPS();
        }
    }

    public void OpenFile() {
        string folder = eval.GetFolder() + "/fps/";

        string destination = folder + GetComponent<Camera>().GetFileName() + ".txt";

        writer = new StreamWriter(destination, true);
        shouldCalculate = true;
        skippedFirst = false; // Skip first every iteration

        ResetFPS();
    }

    private void WriteToFile(int fps) {
        writer.WriteLine(fps);
    }

    private void ResetFPS() {
        timeLeft = interval;
        frames = 0;
    }

    public void CloseFile() {
        shouldCalculate = false;
        writer.Flush();
        writer.Close();
    }

    public void SetShouldCalculate(bool set) {
        shouldCalculate = set;
    }

    public void SetEval(Evaluation set) {
        eval = set;
    }
}
