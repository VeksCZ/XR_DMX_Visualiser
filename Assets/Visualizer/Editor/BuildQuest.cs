#if UNITY_EDITOR
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using Debug = UnityEngine.Debug;

// Quest 3 build: OpenXR jen pro Android (Windows exe zůstává bez VR), nastavení přehrávače
// a sestavení APK + instalace přes ADB.
// Menu: Tools → Visualizer → Quest: Setup / Build + Install
public static class BuildQuest
{
    const string ApkPath = "Build/Quest/DMXVisualiser.apk";
    const string AppId = "cz.veks.dmxvisualiser";

    [MenuItem("Tools/Visualizer/Quest: Setup Project")]
    public static void Setup()
    {
        var android = NamedBuildTarget.Android;
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
        PlayerSettings.SetApplicationIdentifier(android, AppId);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan });
        PlayerSettings.colorSpace = ColorSpace.Linear;
        PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.GameActivity; // Meta Quest Support vyžaduje pro Unity 6
        PlayerSettings.Android.forceInternetPermission = true;

        // XR Management: OpenXR jen pro Android
        var perTarget = (XRGeneralSettingsPerBuildTarget)typeof(XRGeneralSettingsPerBuildTarget)
            .GetMethod("GetOrCreate", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
            .Invoke(null, null);
        if (!perTarget.HasSettingsForBuildTarget(BuildTargetGroup.Android)) perTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
        if (!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android)) perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        var gs = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
        gs.InitManagerOnStart = true;
        XRPackageMetadataStore.AssignLoader(gs.Manager, typeof(OpenXRLoader).FullName, BuildTargetGroup.Android);
        EditorUtility.SetDirty(gs);
        EditorUtility.SetDirty(perTarget);

        // OpenXR funkce pro Quest: podpora Questu, ovladače Touch / Touch Plus (Quest 3),
        // Meta session + kamera (passthrough) pro pozdější mixed reality.
        FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
        var oxr = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
        string[] want = { "MetaQuestFeature", "OculusTouchControllerProfile", "MetaQuestTouchPlusControllerProfile", "ARSessionFeature", "ARCameraFeature", "ARPlaneFeature" };
        foreach (var f in oxr.GetFeatures())
        {
            if (f == null) continue;
            if (want.Contains(f.GetType().Name)) { f.enabled = true; EditorUtility.SetDirty(f); }
        }
        oxr.renderMode = OpenXRSettings.RenderMode.SinglePassInstanced;
        EditorUtility.SetDirty(oxr);

        // OpenXR doporučení (validace projektu): nové typy ovládacích prvků Input Systemu
        foreach (var t in new[] { NamedBuildTarget.Android, NamedBuildTarget.Standalone })
        {
            var defs = PlayerSettings.GetScriptingDefineSymbols(t).Split(';').Where(s => s.Length > 0).ToList();
            foreach (var d in new[] { "USE_INPUT_SYSTEM_POSE_CONTROL", "USE_STICK_CONTROL_THUMBSTICKS" })
                if (!defs.Contains(d)) defs.Add(d);
            PlayerSettings.SetScriptingDefineSymbols(t, string.Join(";", defs));
        }

        RemoveSSAO();
        SetDebugSymbolsSymbolTable();
        LinkVRAssets();
        AssetDatabase.SaveAssets();
        Debug.Log("Quest: projekt nastaven (Android = OpenXR + Meta Quest, Windows bez XR)");
    }

    [MenuItem("Tools/Visualizer/Quest: Build + Install")]
    public static void BuildAndInstall()
    {
        Setup();
        Directory.CreateDirectory(Path.GetDirectoryName(ApkPath));
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        PlayerSettings.bundleVersion = BuildVisualizer.Version;
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = ApkPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        });
        bool ok = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
        Debug.Log("Quest build: " + report.summary.result + " -> " + ApkPath + " (" + report.summary.totalSize / (1024 * 1024) + " MB, chyb: " + report.summary.totalErrors + ")");
        if (!ok) { if (Application.isBatchMode) EditorApplication.Exit(1); return; }

        Install();
    }

    [MenuItem("Tools/Visualizer/Quest: Install last APK")]
    public static void Install()
    {
        string adb = FindAdb();
        Run(adb, "install -r -g \"" + Path.GetFullPath(ApkPath) + "\"");
        Run(adb, "shell monkey -p " + AppId + " 1");
    }

    // Modely ovladačů Quest 3 (Touch Plus) z balíčku Meta XR Core SDK – v repu je jen odkaz, ne samotné modely.
    static void LinkVRAssets()
    {
        const string dir = "Assets/Visualizer/Resources";
        const string path = dir + "/VRAssets.asset";
        const string meshes = "Packages/com.meta.xr.sdk.core/Meshes/MetaQuestTouchPlus/";
        Directory.CreateDirectory(dir);
        var a = AssetDatabase.LoadAssetAtPath<VRAssets>(path);
        if (a == null) { a = ScriptableObject.CreateInstance<VRAssets>(); AssetDatabase.CreateAsset(a, path); }
        a.leftController = AssetDatabase.LoadAssetAtPath<GameObject>(meshes + "MetaQuestTouchPlus_Left.fbx");
        a.rightController = AssetDatabase.LoadAssetAtPath<GameObject>(meshes + "MetaQuestTouchPlus_Right.fbx");
        EditorUtility.SetDirty(a);
        if (a.leftController == null) Debug.LogWarning("Quest: modely ovladačů z Meta XR SDK nenalezeny – použije se kvádr");
    }

    // SSAO ze šablony URP: v tmavém sále s aditivními paprsky není vidět, na Questu i slabším PC jen žere výkon.
    static void RemoveSSAO()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRendererData"))
        {
            var rd = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
            if (rd == null) continue;
            int i = rd.rendererFeatures.FindIndex(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion");
            if (i < 0) continue;
            var feat = rd.rendererFeatures[i];
            rd.rendererFeatures.RemoveAt(i);
            var mapField = typeof(UnityEngine.Rendering.Universal.ScriptableRendererData).GetField("m_RendererFeatureMap", BindingFlags.Instance | BindingFlags.NonPublic);
            if (mapField?.GetValue(rd) is System.Collections.Generic.List<long> map && i < map.Count) map.RemoveAt(i);
            AssetDatabase.RemoveObjectFromAsset(feat);
            Object.DestroyImmediate(feat, true);
            rd.SetDirty();
            EditorUtility.SetDirty(rd);
            Debug.Log("Quest: odstraněn SSAO z " + rd.name);
        }
    }

    // Diagnostika je zapnutá → Unity chce symboly aspoň v režimu SymbolTable (čitelné crash reporty).
    static void SetDebugSymbolsSymbolTable()
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType("UnityEditor.Android.UserBuildSettings+DebugSymbols");
            var p = t?.GetProperty("level", BindingFlags.Static | BindingFlags.Public);
            if (p == null) continue;
            p.SetValue(null, System.Enum.Parse(p.PropertyType, "SymbolTable"));
            return;
        }
        Debug.LogWarning("Quest: nastavení Debug Symbols nenalezeno");
    }

    static string FindAdb()
    {
        string sdk = EditorPrefs.GetString("AndroidSdkRoot");
        if (!string.IsNullOrEmpty(sdk) && File.Exists(Path.Combine(sdk, "platform-tools", "adb.exe")))
            return Path.Combine(sdk, "platform-tools", "adb.exe");
        string unitySdk = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines/AndroidPlayer/SDK/platform-tools/adb.exe");
        return File.Exists(unitySdk) ? unitySdk : "adb";
    }

    // Art-Net na Questu: příjem broadcastu (ArtPoll) potřebuje multicast lock → oprávnění v manifestu.
    class ManifestPatch : UnityEditor.Android.IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 999;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string mf = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(mf)) return;
            string x = File.ReadAllText(mf);
            string[] perms = { "android.permission.INTERNET", "android.permission.ACCESS_WIFI_STATE",
                               "android.permission.ACCESS_NETWORK_STATE", "android.permission.CHANGE_WIFI_MULTICAST_STATE" };
            string add = "";
            foreach (var p in perms)
                if (!x.Contains("\"" + p + "\"")) add += "\n  <uses-permission android:name=\"" + p + "\" />";
            if (add.Length == 0) return;
            int m = x.IndexOf("<manifest");
            int end = m >= 0 ? x.IndexOf('>', m) : -1;
            if (end < 0) return;
            File.WriteAllText(mf, x.Insert(end + 1, add));
        }
    }

    static void Run(string exe, string args)
    {
        try
        {
            var p = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true });
            string o = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            p.WaitForExit();
            Debug.Log("Quest: " + Path.GetFileName(exe) + " " + args + "\n" + o);
        }
        catch (System.Exception e) { Debug.LogWarning("Quest: " + exe + " – " + e.Message); }
    }
}
#endif
