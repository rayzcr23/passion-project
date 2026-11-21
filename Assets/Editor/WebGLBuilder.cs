using UnityEditor;
using UnityEngine;

/// Helper to configure and build WebGL specifically optimized for GitHub Pages / iPad Safari.
public static class WebGLBuilder
{
    [MenuItem("Tools/Build WebGL for iPad (GitHub Pages)")]
    public static void BuildForWeb()
    {
        Debug.Log("[WebGLBuilder] Configuring WebGL settings for GitHub Pages...");

        // Disable compression so GitHub Pages static server delivers files without gzip header errors
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.WebGL.decompressionFallback = false;

        string buildPath = "Builds/WebGL";
        string[] scenes = new string[] { "Assets/Scenes/SampleScene 1.unity" };

        Debug.Log($"[WebGLBuilder] Starting build to: {buildPath} with scene: {scenes[0]}");

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = buildPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"[WebGLBuilder] SUCCESS! WebGL build completed at: {buildPath}");
            EditorUtility.RevealInFinder(buildPath);
        }
        else
        {
            Debug.LogError($"[WebGLBuilder] Build failed with result: {report.summary.result}");
        }
    }
}
