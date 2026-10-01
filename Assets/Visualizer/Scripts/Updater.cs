using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

// Aktualizace přes GitHub Releases.
// Vydání musí obsahovat .zip se složkou buildu (DMXVisualiser.exe + DMXVisualiser_Data …).
// Instalace: stáhne zip, spustí PowerShell skript, aplikace se ukončí, skript rozbalí
// soubory přes starou verzi a aplikaci znovu spustí. Nastavení (settings.json) je jinde, zůstane.
public class Updater : MonoBehaviour
{
    public const string Repo = "VeksCZ/XR_DMX_Visualiser";

    public enum State { Idle, Checking, UpToDate, Available, Downloading, Installing, Error }
    public State state = State.Idle;
    public string latestVersion;
    public string releaseUrl;
    public string error;
    public float progress;

    string assetUrl;

    [Serializable] class GhAsset { public string name; public string browser_download_url; }
    [Serializable] class GhRelease { public string tag_name; public string html_url; public GhAsset[] assets; }

    public void Check()
    {
        if (state == State.Checking || state == State.Downloading || state == State.Installing) return;
        StartCoroutine(CheckRoutine());
    }

    IEnumerator CheckRoutine()
    {
        state = State.Checking;
        error = null;
        using (var req = UnityWebRequest.Get("https://api.github.com/repos/" + Repo + "/releases/latest"))
        {
            req.SetRequestHeader("User-Agent", "DMXVisualiser");
            req.SetRequestHeader("Accept", "application/vnd.github+json");
            req.timeout = 15;
            yield return req.SendWebRequest();

            if (req.responseCode == 404) { state = State.Error; error = Loc.T("noRelease"); yield break; }
            if (req.result != UnityWebRequest.Result.Success) { state = State.Error; error = req.error; yield break; }

            GhRelease rel = null;
            try { rel = JsonUtility.FromJson<GhRelease>(req.downloadHandler.text); }
            catch (Exception e) { state = State.Error; error = e.Message; yield break; }
            if (rel == null || string.IsNullOrEmpty(rel.tag_name)) { state = State.Error; error = Loc.T("noRelease"); yield break; }

            latestVersion = rel.tag_name.TrimStart('v', 'V');
            releaseUrl = rel.html_url;
            assetUrl = null;
            if (rel.assets != null)
                foreach (var a in rel.assets)
                    if (a.name != null && a.name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) { assetUrl = a.browser_download_url; break; }

            state = IsNewer(latestVersion, Application.version) ? State.Available : State.UpToDate;
        }
    }

    public static bool IsNewer(string remote, string local)
    {
        if (Version.TryParse(Normalize(remote), out var r) && Version.TryParse(Normalize(local), out var l)) return r > l;
        return !string.Equals(remote, local, StringComparison.OrdinalIgnoreCase);
    }

    static string Normalize(string v)
    {
        v = (v ?? "").Trim().TrimStart('v', 'V');
        int dash = v.IndexOf('-');
        if (dash >= 0) v = v.Substring(0, dash);
        if (!v.Contains(".")) v += ".0";
        return v;
    }

    public void OpenReleasePage()
    {
        Application.OpenURL(string.IsNullOrEmpty(releaseUrl) ? "https://github.com/" + Repo + "/releases" : releaseUrl);
    }

    public void DownloadAndInstall()
    {
        if (state != State.Available) return;
        if (string.IsNullOrEmpty(assetUrl)) { state = State.Error; error = Loc.T("noAsset"); return; }
        if (Application.isEditor) { state = State.Error; error = Loc.T("editorNoInstall"); return; }
        StartCoroutine(InstallRoutine());
    }

    IEnumerator InstallRoutine()
    {
        state = State.Downloading;
        progress = 0f;
        string tmp = Path.Combine(Path.GetTempPath(), "DMXVisualiserUpdate");
        try { if (Directory.Exists(tmp)) Directory.Delete(tmp, true); Directory.CreateDirectory(tmp); }
        catch (Exception e) { state = State.Error; error = e.Message; yield break; }
        string zip = Path.Combine(tmp, "update.zip");

        using (var req = UnityWebRequest.Get(assetUrl))
        {
            req.SetRequestHeader("User-Agent", "DMXVisualiser");
            req.downloadHandler = new DownloadHandlerFile(zip) { removeFileOnAbort = true };
            var op = req.SendWebRequest();
            while (!op.isDone) { progress = req.downloadProgress; yield return null; }
            if (req.result != UnityWebRequest.Result.Success) { state = State.Error; error = req.error; yield break; }
        }

        state = State.Installing;
        string appDir = Path.GetDirectoryName(Application.dataPath);           // složka s .exe
        string exeName = Path.GetFileName(appDir) + ".exe";                      // fallback
        foreach (var f in Directory.GetFiles(appDir, "*.exe"))
            if (!Path.GetFileName(f).StartsWith("UnityCrashHandler", StringComparison.OrdinalIgnoreCase)) { exeName = Path.GetFileName(f); break; }
        int pid = Process.GetCurrentProcess().Id;

        string script = Path.Combine(tmp, "update.ps1");
        string ps =
            "$ErrorActionPreference = 'Stop'\n" +
            "try { Wait-Process -Id " + pid + " -Timeout 30 -ErrorAction SilentlyContinue } catch {}\n" +
            "$tmp = '" + Esc(tmp) + "'\n" +
            "$app = '" + Esc(appDir) + "'\n" +
            "$out = Join-Path $tmp 'unpacked'\n" +
            "Expand-Archive -LiteralPath (Join-Path $tmp 'update.zip') -DestinationPath $out -Force\n" +
            "$exe = Get-ChildItem -Path $out -Recurse -Filter '" + Esc(exeName) + "' | Select-Object -First 1\n" +
            "if (-not $exe) { $exe = Get-ChildItem -Path $out -Recurse -Filter '*.exe' | Where-Object { $_.Name -notlike 'UnityCrashHandler*' } | Select-Object -First 1 }\n" +
            "$src = $exe.DirectoryName\n" +
            "robocopy $src $app /E /R:3 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null\n" +
            "Start-Process -FilePath (Join-Path $app '" + Esc(exeName) + "')\n";
        try
        {
            File.WriteAllText(script, ps);
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"" + script + "\"",
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch (Exception e) { state = State.Error; error = e.Message; yield break; }

        yield return new WaitForSecondsRealtime(0.5f);
        Application.Quit();
    }

    static string Esc(string s) => s.Replace("'", "''");
}
