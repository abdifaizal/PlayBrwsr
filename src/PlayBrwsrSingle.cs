using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace PlayBrwsrSingle
{
    static class Program
    {
        private const string BundleVersion = "1.0.1-slim";

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                Run(args);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Gagal menjalankan PlayBrwsr:\n" + ex.Message,
                    "PlayBrwsr Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        static string ResolveWorkDir()
        {
            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (!string.IsNullOrEmpty(exeDir) && IsDirWritable(exeDir))
            {
                string workDir = Path.Combine(exeDir, ".playbrwsr_runtime");
                Directory.CreateDirectory(workDir);
                try
                {
                    File.SetAttributes(workDir, File.GetAttributes(workDir) | FileAttributes.Hidden);
                }
                catch { }
                return workDir;
            }

            string fallback = Path.Combine(Path.GetTempPath(), "PlayBrwsrRuntime");
            Directory.CreateDirectory(fallback);
            return fallback;
        }

        static bool IsDirWritable(string dir)
        {
            string probe = Path.Combine(dir, ".playbrwsr_write_test");
            try
            {
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        static void EnsureExtracted(string workDir)
        {
            string marker = Path.Combine(workDir, ".bundle_version");
            string ffExe = Path.Combine(workDir, "runtime", "firefox", "firefox.exe");
            if (File.Exists(ffExe) && File.Exists(marker) && File.ReadAllText(marker).Trim() == BundleVersion)
            {
                return;
            }

            string tarPath = Path.Combine(workDir, "_bundle.tar.xz");
            using (Stream res = Assembly.GetExecutingAssembly().GetManifestResourceStream("bundle.tar.xz"))
            {
                if (res == null)
                    throw new InvalidOperationException("Embedded bundle.tar.xz resource not found.");
                using (FileStream fs = File.Create(tarPath))
                {
                    res.CopyTo(fs);
                }
            }

            try
            {
                var tarPsi = new ProcessStartInfo
                {
                    FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "tar.exe"),
                    Arguments = "-xJf \"" + tarPath + "\" -C \"" + workDir + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true
                };
                using (Process p = Process.Start(tarPsi))
                {
                    string err = p.StandardError.ReadToEnd();
                    p.WaitForExit();
                    if (p.ExitCode != 0)
                        throw new InvalidOperationException("Ekstraksi gagal: " + err);
                }
            }
            finally
            {
                if (File.Exists(tarPath))
                {
                    try { File.Delete(tarPath); } catch { }
                }
            }

            File.WriteAllText(marker, BundleVersion);
        }

        static void SyncPolicies(string workDir)
        {
            string extPath = Path.Combine(workDir, "extensions", "browsec@browsec.com.xpi");
            string distDir = Path.Combine(workDir, "runtime", "firefox", "distribution");
            Directory.CreateDirectory(distDir);

            string extUri = new Uri(extPath).AbsoluteUri;
            string policiesJson = "{\n" +
                "  \"policies\": {\n" +
                "    \"DisableAppUpdate\": true,\n" +
                "    \"DontCheckDefaultBrowser\": true,\n" +
                "    \"DisableTelemetry\": true,\n" +
                "    \"DisablePocket\": true,\n" +
                "    \"DisableFirefoxAccounts\": true,\n" +
                "    \"OverrideFirstRunPage\": \"\",\n" +
                "    \"OverridePostUpdatePage\": \"\",\n" +
                "    \"ExtensionSettings\": {\n" +
                "      \"browsec@browsec.com\": {\n" +
                "        \"installation_mode\": \"force_installed\",\n" +
                "        \"install_url\": \"" + extUri.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\",\n" +
                "        \"default_area\": \"navbar\"\n" +
                "      }\n" +
                "    }\n" +
                "  }\n" +
                "}\n";

            File.WriteAllText(Path.Combine(distDir, "policies.json"), policiesJson, System.Text.Encoding.UTF8);
        }

        static void Run(string[] args)
        {
            string workDir = ResolveWorkDir();
            EnsureExtracted(workDir);
            SyncPolicies(workDir);

            string ffExe = Path.Combine(workDir, "runtime", "firefox", "firefox.exe");
            string ffDir = Path.Combine(workDir, "runtime", "firefox");
            string profileDir = Path.Combine(workDir, "data", "profile");

            string sysRoaming = Path.Combine(workDir, "data", "sys", "roaming");
            string sysLocal = Path.Combine(workDir, "data", "sys", "local");
            string sysTemp = Path.Combine(workDir, "data", "sys", "temp");
            Directory.CreateDirectory(sysRoaming);
            Directory.CreateDirectory(sysLocal);
            Directory.CreateDirectory(sysTemp);
            Directory.CreateDirectory(profileDir);

            string extSrc = Path.Combine(workDir, "extensions", "browsec@browsec.com.xpi");
            string profileExtDir = Path.Combine(profileDir, "extensions");
            string extDst = Path.Combine(profileExtDir, "browsec@browsec.com.xpi");
            if (File.Exists(extSrc) && !File.Exists(extDst))
            {
                Directory.CreateDirectory(profileExtDir);
                File.Copy(extSrc, extDst, true);
            }

            string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "";
            string urls;
            if (mode == "--video")
                urls = "\"https://www.youtube.com\"";
            else if (mode == "--music")
                urls = "\"https://music.youtube.com\"";
            else if (mode == "--hub")
                urls = "\"" + new Uri(Path.Combine(workDir, "app", "hub.html")).AbsoluteUri + "\"";
            else
                urls = "\"https://www.youtube.com\" \"https://music.youtube.com\"";

            var psi = new ProcessStartInfo
            {
                FileName = ffExe,
                Arguments = "-no-remote -profile \"" + profileDir + "\" " + urls,
                WorkingDirectory = ffDir,
                UseShellExecute = false
            };
            psi.EnvironmentVariables["APPDATA"] = sysRoaming;
            psi.EnvironmentVariables["LOCALAPPDATA"] = sysLocal;
            psi.EnvironmentVariables["TEMP"] = sysTemp;
            psi.EnvironmentVariables["TMP"] = sysTemp;

            Process.Start(psi);
        }
    }
}
