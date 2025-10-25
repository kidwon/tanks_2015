using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WebGLBuild
{
    private const string OutputPath = "BuildWebGL";

    public static void Build()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(scene => scene != null && scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("WebGL build failed: no scenes enabled in EditorBuildSettings.");
            return;
        }

        var buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"WebGL build failed: {report.summary.result}");
        }
        else
        {
            Debug.Log($"WebGL build succeeded: {report.summary.outputPath}");
        }
    }
}
