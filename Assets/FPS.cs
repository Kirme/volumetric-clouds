using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class FPS : MonoBehaviour {
    private float interval = 1.0f;
    private float timeLeft = 0;
    private int frames = 0;

    private bool shouldCalculate;

    private StreamWriter writer;

    private void Start() {
        timeLeft = interval;
        shouldCalculate = true;

        OpenFile();
    }

    private void Update() {
        if (!shouldCalculate) return;
    
        timeLeft -= Time.deltaTime;
        frames++;

        if (timeLeft <= 0) {
            WriteToFile(frames);
            ResetFPS();
        }
    }

    private void OpenFile() {
        string destination = "./eval/fps/" + GetComponent<Camera>().fileName + ".txt";

        writer = new StreamWriter(destination, true);
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
        writer.Close();
    }
}
