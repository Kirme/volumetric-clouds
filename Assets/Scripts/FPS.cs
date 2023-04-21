using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class FPS : MonoBehaviour {
    private float interval = 1.0f; // How often to evaluate FPS (in seconds)
    private float timeLeft = 0; // How much time left until new iteration
    private int frames = 0; // How many frames in current iteration

    private bool shouldCalculate; // Should we calculate FPS?
    private bool skippedFirst; // Have we already skipped one iteration?

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

    // Open a file to write in
    public void OpenFile() {
        string folder = eval.GetFolder() + "/fps/";

        string destination = folder + GetComponent<Camera>().GetFileName() + ".txt";

        writer = new StreamWriter(destination, true);
        shouldCalculate = true;
        skippedFirst = false; // Skip first every iteration

        ResetFPS();
    }

    // Write fps to file
    private void WriteToFile(int fps) {
        writer.WriteLine(fps);
    }

    // Reset parameters before new iteration
    private void ResetFPS() {
        timeLeft = interval;
        frames = 0;
    }

    // Close file and stop calculating FPS
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
