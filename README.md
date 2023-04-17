# Volumetric Clouds
This repo contains an implementation of a real-time volumetric cloud renderer in Unity using shaders. It was made for a Master's Thesis at KTH Royal Institute of Technology, Sweden. The repo also includes Python files for generating graphs of evaluation data generated within Unity.  
This README describes both the features of the implementation, and what you can do with it.

## Program Versions
Unity version: 2021.3.16f1 (LTS)  
Python version: 3.8.10

## Volumetric Cloud Renderer
* Developed in Unity (3D)
* Uses C# scripts, HLSL shaders, and Compute shaders
* Rendered using a ray marching algorithm
* Pseudo-random cloud shapes generated with Worley noise
* Lighting calculated using Beer's law

The specifics of the implementation and algorithms used is found in the related Master's thesis, which can be found in the KTH Publication Database DiVA.
  
The renderer was developed in order to investigate the effects of bilinearly interpolation using adaptive sampling on the performance and visual quality of the program. Therefore, there exists tools within the program to enable these features. These are described below:
* Ability to only ray march every nth (2nd, 4th or 8th) pixel and interpolate the rest
* Ability to enable adaptive sampling, choosing to only interpolate when differences between surrounding pixels are below a threshold
* Ability to evaluate either performance or visual quality over a parameter

## Instructions
Firstly, a note on the usage of this repo. There are several limitations to the renderer. The purpose of the program is to be suitably advanced for the purposes of the thesis. Thus, it does not contain many features that are likely found in programs more suited for direct use in e.g. video games. The main purpose of this program is to be used as a reference for the thesis, or to replicate the results. It can, however, also be used as inspiration or as a baseline for further implementations or research.

In order to use this program, one simply needs to clone it and open it in its entirety within Unity. The eval folder located in the repo is not strictly necessary for use of the renderer. It is where Unity stores the data gathered from evaluation, if one wishes to evaluate the implementation. It is also where the Python scripts required for generating graphs are located.
