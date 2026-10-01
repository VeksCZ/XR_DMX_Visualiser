#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// Jednorázové nastavení projektu: Forward+, scéna Visualizer se všemi komponentami, Bloom.
// Menu: Tools → Visualizer → Setup Scene (nebo -executeMethod SetupVisualizer.Run)
public static class SetupVisualizer
{
    const string Root = "Assets/Visualizer";
    const string ScenePath = Root + "/Visualizer.unity";

    [MenuItem("Tools/Visualizer/Setup Scene")]
    public static void Run()
    {
        // 1) Forward+ ve všech URP rendererech (kvůli počtu světel)
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            var rd = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
            if (rd == null) continue;
            rd.renderingMode = RenderingMode.ForwardPlus;
            EditorUtility.SetDirty(rd);
        }

        // 2) Nová scéna
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        var cam = Object.FindFirstObjectByType<Camera>();
        if (cam != null)
        {
            var data = cam.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            cam.allowHDR = true;
        }

        var go = new GameObject("Visualizer");
        var sb = go.AddComponent<SceneBuilder>();
        sb.beamShader = Shader.Find("Visualizer/Beam");
        sb.emissiveShader = Shader.Find("Visualizer/Emissive");
        go.AddComponent<DemoDriver>();
        go.AddComponent<ArtNetReceiver>();
        go.AddComponent<DmxPatch>();

        // 3) Global Volume s Bloomem a tonemappingem
        string profilePath = Root + "/VisualizerVolume.asset";
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, profilePath);
            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(1.2f);
            bloom.scatter.Override(0.7f);
            var tm = profile.Add<Tonemapping>(true);
            tm.mode.Override(TonemappingMode.ACES);
            foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            EditorUtility.SetDirty(profile);
        }
        var vol = new GameObject("Global Volume").AddComponent<Volume>();
        vol.isGlobal = true;
        vol.sharedProfile = profile;

        // 4) Uložit a dát do Build Settings
        EditorSceneManager.SaveScene(scene, ScenePath);
        var list = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
        list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("Visualizer: scéna připravena -> " + ScenePath);
    }
}
#endif
