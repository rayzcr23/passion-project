using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.IO;

/// <summary>
/// Command-line build script for WebGL.
/// Invoked via: Unity.exe -batchmode -executeMethod WebGLBuild.Build ...
/// </summary>
public static class WebGLBuild
{
    public static void Build()
    {
        // Output folder passed as -buildPath, default to Builds/WebGL
        string outputPath = GetArg("-buildPath") ?? "Builds/WebGL";

        Debug.Log("[WebGLBuild] Output path: " + outputPath);

        // Make sure the directory exists
        Directory.CreateDirectory(outputPath);

        // Gather all scenes from build settings
        var scenes = new System.Collections.Generic.List<string>();
        foreach (var s in EditorBuildSettings.scenes)
        {
            if (s.enabled)
                scenes.Add(s.path);
        }

        if (scenes.Count == 0)
        {
            Debug.LogError("[WebGLBuild] No scenes in Build Settings!");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log("[WebGLBuild] Building " + scenes.Count + " scene(s): " + string.Join(", ", scenes));

        var options = new BuildPlayerOptions
        {
            scenes           = scenes.ToArray(),
            locationPathName = outputPath,
            target           = BuildTarget.WebGL,
            options          = BuildOptions.None,
        };

        BuildReport  report  = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log("[WebGLBuild] Build succeeded! Size: " + summary.totalSize + " bytes.");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError("[WebGLBuild] Build FAILED: " + summary.result);
            EditorApplication.Exit(1);
        }
    }

    static string GetArg(string name)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name)
                return args[i + 1];
        return null;
    }
}
