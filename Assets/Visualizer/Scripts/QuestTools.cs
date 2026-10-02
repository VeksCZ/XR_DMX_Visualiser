#if UNITY_STANDALONE_WIN || UNITY_EDITOR
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

// Windows: správa Quest aplikace přes ADB – detekce Questu, instalace APK (z releasu na GitHubu
// nebo ze složky aplikace) a přenos nastavení světel do Questu.
// ADB se hledá ve složce aplikace (platform-tools), v PATH a v Android SDK; jinak jde stáhnout
// oficiální platform-tools od Googlu do složky aplikace.
public class QuestTools : MonoBehaviour
{
    public const string Package = "cz.veks.dmxvisualiser";
    const string RemoteSettings = "/sdcard/Android/data/" + Package + "/files/settings.json";
    const string PlatformToolsUrl = "https://dl.google.com/android/repository/platform-tools-latest-windows.zip";

    [NonSerialized] public string adbPath;          // null = nenalezeno
    [NonSerialized] public string device;           // sériové číslo připojeného Questu
    [NonSerialized] public string deviceModel;
    [NonSerialized] public string deviceState;      // device / unauthorized / offline / null
    [NonSerialized] public string installedVersion; // verze aplikace v Questu, null = není nainstalovaná
    [NonSerialized] public bool busy;
    [NonSerialized] public string status = "";
    [NonSerialized] public bool statusError;
    [NonSerialized] public float progress = -1f;

    string appDir, tmpDir;

    void Awake()
    {
        appDir = Path.GetDirectoryName(Application.dataPath);
        tmpDir = Path.Combine(Path.GetTempPath(), "DMXVisualiserQuest");
    }

    void Set(string msg, bool err = false) { status = msg; statusError = err; }

    // ---------- ADB ----------
    public string FindAdb()
    {
        var cands = new System.Collections.Generic.List<string>
        {
            Path.Combine(appDir, "platform-tools", "adb.exe"),
        };
        string la = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (!string.IsNullOrEmpty(la)) cands.Add(Path.Combine(la, "Android", "Sdk", "platform-tools", "adb.exe"));
        foreach (var p in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            if (!string.IsNullOrWhiteSpace(p)) cands.Add(Path.Combine(p.Trim().Trim('"'), "adb.exe"));
        foreach (var c in cands)
            try { if (File.Exists(c)) return c; } catch { }
        return null;
    }

    static (int code, string output) Run(string exe, string args, int timeoutMs = 30000)
    {
        try
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true
            };
            using (var p = Process.Start(psi))
            {
                var o = p.StandardOutput.ReadToEndAsync();
                var e = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return (-1, "timeout"); }
                return (p.ExitCode, (o.Result + "\n" + e.Result).Trim());
            }
        }
        catch (Exception ex) { return (-1, ex.Message); }
    }

    (int code, string output) Adb(string args, int timeoutMs = 30000) =>
        Run(adbPath, (string.IsNullOrEmpty(device) ? "" : "-s " + device + " ") + args, timeoutMs);

    // Detekce: najít adb, připojený Quest a verzi aplikace v něm
    public void Detect()
    {
        if (busy) return;
        busy = true; Set(Loc.T("qDetecting"));
        Task.Run(() =>
        {
            try { DetectSync(); }
            finally { busy = false; }
        });
    }

    void DetectSync()
    {
        adbPath = FindAdb();
        device = deviceModel = deviceState = installedVersion = null;
        if (adbPath == null) { Set(Loc.T("qNoAdb"), true); return; }
        var r = Run(adbPath, "devices -l", 20000);
        if (r.code != 0) { Set(r.output, true); return; }
        foreach (var line in r.output.Split('\n'))
        {
            var t = line.Trim();
            if (t.Length == 0 || t.StartsWith("List of") || t.StartsWith("*")) continue;
            var parts = t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            string model = null;
            foreach (var p in parts) if (p.StartsWith("model:")) model = p.Substring(6).Replace('_', ' ');
            // přednost má Quest, jinak první zařízení
            if (device == null || (model != null && model.Contains("Quest")))
            { device = parts[0]; deviceState = parts[1]; deviceModel = model ?? parts[0]; }
        }
        if (device == null) { Set(Loc.T("qNoDevice"), true); return; }
        if (deviceState == "unauthorized") { Set(Loc.T("qUnauthorized"), true); return; }
        if (deviceState != "device") { Set(Loc.F("qDeviceState", deviceState), true); return; }
        var v = Adb("shell dumpsys package " + Package);
        foreach (var line in v.output.Split('\n'))
        {
            var t = line.Trim();
            if (t.StartsWith("versionName=")) { installedVersion = t.Substring(12); break; }
        }
        Set(installedVersion == null ? Loc.T("qNotInstalled") : Loc.F("qInstalled", installedVersion));
    }

    // Stáhnout oficiální Android platform-tools do složky aplikace
    public void DownloadAdb()
    {
        if (busy) return;
        StartCoroutine(DownloadAdbRoutine());
    }

    IEnumerator DownloadAdbRoutine()
    {
        busy = true; progress = 0f; Set(Loc.T("qDownloadingAdb"));
        string zip = Path.Combine(tmpDir, "platform-tools.zip");
        try { Directory.CreateDirectory(tmpDir); } catch { }
        using (var req = UnityWebRequest.Get(PlatformToolsUrl))
        {
            req.downloadHandler = new DownloadHandlerFile(zip) { removeFileOnAbort = true };
            var op = req.SendWebRequest();
            while (!op.isDone) { progress = req.downloadProgress; yield return null; }
            progress = -1f;
            if (req.result != UnityWebRequest.Result.Success) { busy = false; Set(req.error, true); yield break; }
        }
        var task = Task.Run(() =>
        {
            string dest = Path.Combine(appDir, "platform-tools");
            if (Directory.Exists(dest)) Directory.Delete(dest, true);
            ZipFile.ExtractToDirectory(zip, appDir);   // zip obsahuje složku platform-tools
        });
        while (!task.IsCompleted) yield return null;
        busy = false;
        if (task.IsFaulted) { Set(task.Exception.InnerException.Message, true); yield break; }
        Detect();
    }

    // ---------- Instalace APK ----------
    // Přednost má APK ve složce aplikace (DMXVisualiser*.apk), jinak se stáhne z GitHubu
    // z vydání stejné verze jako Windows aplikace.
    public void InstallApp()
    {
        if (busy || device == null) return;
        StartCoroutine(InstallRoutine());
    }

    IEnumerator InstallRoutine()
    {
        busy = true;
        string apk = null;
        try
        {
            foreach (var f in Directory.GetFiles(appDir, "*.apk")) { apk = f; break; }
        }
        catch { }
        if (apk == null)
        {
            string url = null;
            Set(Loc.T("qFindingApk"));
            string api = "https://api.github.com/repos/" + Updater.Repo + "/releases/tags/v" + Application.version;
            using (var req = UnityWebRequest.Get(api))
            {
                req.SetRequestHeader("User-Agent", "DMXVisualiser");
                req.timeout = 15;
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var rel = JsonUtility.FromJson<Rel>(req.downloadHandler.text);
                    if (rel?.assets != null)
                        foreach (var a in rel.assets)
                            if (a.name != null && a.name.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)) { url = a.browser_download_url; break; }
                }
            }
            if (url == null) { busy = false; Set(Loc.F("qNoApk", Application.version), true); yield break; }
            try { Directory.CreateDirectory(tmpDir); } catch { }
            apk = Path.Combine(tmpDir, "DMXVisualiser.apk");
            Set(Loc.T("qDownloadingApk")); progress = 0f;
            using (var req = UnityWebRequest.Get(url))
            {
                req.downloadHandler = new DownloadHandlerFile(apk) { removeFileOnAbort = true };
                var op = req.SendWebRequest();
                while (!op.isDone) { progress = req.downloadProgress; yield return null; }
                progress = -1f;
                if (req.result != UnityWebRequest.Result.Success) { busy = false; Set(req.error, true); yield break; }
            }
        }
        Set(Loc.T("qInstalling"));
        string apkPath = apk;
        var task = Task.Run(() =>
        {
            var r = Adb("install -r -g \"" + apkPath + "\"", 300000);
            if (r.code != 0 || !r.output.Contains("Success")) return r.output;
            Adb("shell monkey -p " + Package + " -c android.intent.category.LAUNCHER 1");
            return null;
        });
        while (!task.IsCompleted) yield return null;
        busy = false;
        if (task.Result != null) { Set(Loc.F("qInstallFailed", Last(task.Result)), true); yield break; }
        Detect();
        while (busy) yield return null;
        Set(Loc.F("qInstallDone", installedVersion ?? Application.version));
    }

    [Serializable] class Asset { public string name; public string browser_download_url; }
    [Serializable] class Rel { public Asset[] assets; }

    // ---------- Přenos nastavení ----------
    // Z PC se přenese patch světel, kalibrace a hazer; VR volby a pohled kamery v Questu zůstanou.
    public void PushSettings(VisualizerSettings pc)
    {
        if (busy || device == null || pc == null) return;
        busy = true; Set(Loc.T("qPushing"));
        string pcJson = JsonUtility.ToJson(pc);
        Task.Run(() =>
        {
            try { Set(PushSync(pcJson)); }
            catch (Exception e) { Set(e.Message, true); }
            finally { busy = false; }
        });
    }

    string PushSync(string pcJson)
    {
        Directory.CreateDirectory(tmpDir);
        string local = Path.Combine(tmpDir, "quest-settings.json");
        var pc = JsonUtility.FromJson<VisualizerSettings>(pcJson);
        VisualizerSettings q = null;
        if (File.Exists(local)) File.Delete(local);
        var pull = Adb("pull " + RemoteSettings + " \"" + local + "\"");
        bool pulled = pull.code == 0 && File.Exists(local);
        if (pulled)
            try { q = JsonUtility.FromJson<VisualizerSettings>(File.ReadAllText(local)); } catch { }
        if (q == null) q = new VisualizerSettings { cameraPreset = 0 };
        q.fixtures = pc.fixtures;
        q.hazeBuildRate = pc.hazeBuildRate;
        q.hazeDecay = pc.hazeDecay;
        q.roomLight = pc.roomLight;
        q.language = pc.language;
        q.version = pc.version;
        File.WriteAllText(local, JsonUtility.ToJson(q, true));

        // přes dočasný soubor a cat, aby soubor v Questu zůstal zapisovatelný pro aplikaci
        const string tmpRemote = "/data/local/tmp/dmxvis-settings.json";
        var push = Adb("push \"" + local + "\" " + tmpRemote);
        if (push.code != 0) throw new Exception(Last(push.output));
        if (!pulled)
        {
            // složku files vytváří aplikace sama – když v Questu ještě nastavení není, jednou ji spustit
            Adb("shell monkey -p " + Package + " -c android.intent.category.LAUNCHER 1");
            System.Threading.Thread.Sleep(4000);
        }
        Adb("shell am force-stop " + Package);
        var cp = Adb("shell \"cat " + tmpRemote + " > " + RemoteSettings + " || exit 1; chmod 666 " + RemoteSettings + " 2>/dev/null; rm " + tmpRemote + "; exit 0\"");
        if (cp.code != 0) throw new Exception(Last(cp.output));
        Adb("shell monkey -p " + Package + " -c android.intent.category.LAUNCHER 1");
        return Loc.T("qPushDone");
    }

    static string Last(string s)
    {
        var lines = (s ?? "").Trim().Split('\n');
        return lines.Length == 0 ? "" : lines[lines.Length - 1].Trim();
    }
}
#else
// Na Questu (Android) se ADB nástroje nepoužívají – prázdná náhrada, ať se do APK nedostanou Process/ZipFile.
public class QuestTools : UnityEngine.MonoBehaviour
{
    [System.NonSerialized] public string adbPath, device, deviceModel, deviceState, installedVersion, status = "";
    [System.NonSerialized] public bool busy, statusError;
    [System.NonSerialized] public float progress = -1f;
    public void Detect() { }
    public void DownloadAdb() { }
    public void InstallApp() { }
    public void PushSettings(VisualizerSettings s) { }
}
#endif
