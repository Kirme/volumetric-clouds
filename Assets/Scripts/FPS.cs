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

        if (shouldCalculate)
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

    public void OpenFile() {
        Debug.Log("File opened");
        string destination = "./eval/fps/" + GetComponent<Camera>().GetFileName() + ".txt";

        writer = new StreamWriter(destination, true);
        shouldCalculate = true;
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
}
