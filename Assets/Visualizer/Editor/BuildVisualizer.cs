#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools → Visualizer → Build Windows EXE
// Výstup: Build/Windows/DMXVisualiser.exe (složka Build je v .gitignore)
public static class BuildVisualizer
{
    const string ScenePath = "Assets/Visualizer/Visualizer.unity";
    const string OutPath = "Build/Windows/DMXVisualiser.exe";
    // Verze aplikace – zvyšovat s každým vydáním (tag na GitHubu = "v" + Version)
    public const string Version = "0.5.3";

    [MenuItem("Tools/Visualizer/Build Windows EXE")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // Shadery musí být ve scéně přiřazené, jinak je build vynechá
        var scene = EditorSceneManager.OpenScene(ScenePath);
        var sb = Object.FindFirstObjectByType<SceneBuilder>();
        if (sb != null)
        {
            if (sb.beamShader == null) sb.beamShader = Shader.Find("Visualizer/Beam");
            if (sb.emissiveShader == null) sb.emissiveShader = Shader.Find("Visualizer/Emissive");
            if (sb.litShader == null) sb.litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (sb.GetComponent<VisualizerMenu>() == null) sb.gameObject.AddComponent<VisualizerMenu>();
            EditorUtility.SetDirty(sb);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        PlayerSettings.companyName = "VeksCZ";
        PlayerSettings.productName = "DMX Visualiser";
        PlayerSettings.bundleVersion = Version;
        PlayerSettings.runInBackground = true;      // přijímat Art-Net i bez fokusu
        PlayerSettings.visibleInBackground = true;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.resizableWindow = true;

        var opts = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = OutPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        var report = BuildPipeline.BuildPlayer(opts);
        Debug.Log("Visualizer build: " + report.summary.result + " -> " + OutPath +
                  " (" + (report.summary.totalSize / (1024 * 1024)) + " MB, chyb: " + report.summary.totalErrors + ")");
    }
}
#endif
