using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;

namespace PlayBrwsr
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string baseDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
            if (args.Length > 0)
            {
                string mode = args[0].ToLowerInvariant();
                if (mode == "--launcher")
                {
                    Application.Run(new MainForm(baseDir));
                    return;
                }
                if (mode == "--video")
                {
                    LauncherCore.LaunchBrowser(baseDir, LaunchMode.Video);
                    return;
                }
                if (mode == "--music")
                {
                    LauncherCore.LaunchBrowser(baseDir, LaunchMode.Music);
                    return;
                }
                if (mode == "--hub")
                {
                    LauncherCore.LaunchBrowser(baseDir, LaunchMode.Hub);
                    return;
                }
            }

            // Default: launch Desktop Media Player directly (YouTube Video & Music)
            LauncherCore.LaunchBrowser(baseDir, LaunchMode.Both);
        }
    }

    enum LaunchMode
    {
        Both,
        Video,
        Music,
        Hub
    }

    static class LauncherCore
    {
        public static string GetFirefoxPath(string baseDir)
        {
            string portable = Path.Combine(baseDir, "runtime", "firefox", "firefox.exe");
            if (File.Exists(portable)) return portable;

            string sys64 = @"C:\Program Files\Mozilla Firefox\firefox.exe";
            if (File.Exists(sys64)) return sys64;

            string sys86 = @"C:\Program Files (x86)\Mozilla Firefox\firefox.exe";
            if (File.Exists(sys86)) return sys86;

            return null;
        }

        public static string GetExtensionPath(string baseDir)
        {
            return Path.Combine(baseDir, "extensions", "browsec@browsec.com.xpi");
        }

        public static string GetProfilePath(string baseDir)
        {
            return Path.Combine(baseDir, "data", "profile");
        }

        public static string GetHubUrl(string baseDir)
        {
            string hubPath = Path.Combine(baseDir, "app", "hub.html");
            return new Uri(hubPath).AbsoluteUri;
        }

        public static void SyncPolicies(string baseDir, string firefoxExe)
        {
            string extPath = GetExtensionPath(baseDir);
            if (!File.Exists(extPath)) return;

            string ffDir = Path.GetDirectoryName(firefoxExe);
            string distDir = Path.Combine(ffDir, "distribution");

            // Only write policies.json inside our portable runtime folder (never touch Program Files)
            if (!ffDir.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

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

        public static bool LaunchBrowser(string baseDir, LaunchMode mode)
        {
            string ff = GetFirefoxPath(baseDir);
            if (ff == null)
            {
                MessageBox.Show(
                    "Runtime Firefox portable tidak ditemukan di:\n" + Path.Combine(baseDir, "runtime", "firefox", "firefox.exe"),
                    "PlayBrwsr - Runtime Tidak Ditemukan",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return false;
            }

            string profileDir = GetProfilePath(baseDir);
            Directory.CreateDirectory(profileDir);

            string extSrc = GetExtensionPath(baseDir);
            if (File.Exists(extSrc))
            {
                string profileExtDir = Path.Combine(profileDir, "extensions");
                Directory.CreateDirectory(profileExtDir);
                string extDst = Path.Combine(profileExtDir, "browsec@browsec.com.xpi");
                if (!File.Exists(extDst))
                {
                    File.Copy(extSrc, extDst, true);
                }
            }

            SyncPolicies(baseDir, ff);

            string urls;
            switch (mode)
            {
                case LaunchMode.Video:
                    urls = "\"https://www.youtube.com\"";
                    break;
                case LaunchMode.Music:
                    urls = "\"https://music.youtube.com\"";
                    break;
                case LaunchMode.Hub:
                    urls = "\"" + GetHubUrl(baseDir) + "\"";
                    break;
                case LaunchMode.Both:
                default:
                    urls = "\"https://www.youtube.com\" \"https://music.youtube.com\"";
                    break;
            }

            string sysRoaming = Path.Combine(baseDir, "data", "sys", "roaming");
            string sysLocal = Path.Combine(baseDir, "data", "sys", "local");
            string sysTemp = Path.Combine(baseDir, "data", "sys", "temp");
            Directory.CreateDirectory(sysRoaming);
            Directory.CreateDirectory(sysLocal);
            Directory.CreateDirectory(sysTemp);

            var psi = new ProcessStartInfo
            {
                FileName = ff,
                Arguments = "-no-remote -profile \"" + profileDir + "\" " + urls,
                WorkingDirectory = Path.GetDirectoryName(ff),
                UseShellExecute = false
            };
            psi.EnvironmentVariables["APPDATA"] = sysRoaming;
            psi.EnvironmentVariables["LOCALAPPDATA"] = sysLocal;
            psi.EnvironmentVariables["TEMP"] = sysTemp;
            psi.EnvironmentVariables["TMP"] = sysTemp;

            Process.Start(psi);
            return true;
        }
    }

    class MainForm : Form
    {
        private readonly string _baseDir;

        public MainForm(string baseDir)
        {
            _baseDir = baseDir;
            InitializeUI();
        }

        private void InitializeUI()
        {
            Text = "PlayBrwsr Portable — YouTube & Music (Browsec VPN)";
            ClientSize = new Size(460, 410);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(14, 14, 20);
            ForeColor = Color.FromArgb(243, 244, 246);
            Font = new Font("Segoe UI", 9.5f);

            var title = new Label
            {
                Text = "PlayBrwsr Portable",
                Font = new Font("Segoe UI", 18f, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(28, 22)
            };

            var subtitle = new Label
            {
                Text = "YouTube Video & YouTube Music dengan koneksi default Browsec VPN",
                Font = new Font("Segoe UI", 9.5f),
                ForeColor = Color.FromArgb(156, 163, 175),
                AutoSize = false,
                Size = new Size(404, 22),
                Location = new Point(28, 56)
            };

            var btnBoth = CreateActionButton(
                "Buka YouTube Video & Music",
                Color.FromArgb(225, 29, 72),
                Color.FromArgb(244, 63, 94),
                new Point(28, 96),
                new Size(404, 48),
                true
            );
            btnBoth.Click += (s, e) => LaunchAndMinimize(LaunchMode.Both);

            var btnVideo = CreateActionButton(
                "YouTube Video Saja",
                Color.FromArgb(30, 30, 44),
                Color.FromArgb(42, 42, 60),
                new Point(28, 156),
                new Size(196, 42),
                false
            );
            btnVideo.Click += (s, e) => LaunchAndMinimize(LaunchMode.Video);

            var btnMusic = CreateActionButton(
                "YouTube Music Saja",
                Color.FromArgb(30, 30, 44),
                Color.FromArgb(42, 42, 60),
                new Point(236, 156),
                new Size(196, 42),
                false
            );
            btnMusic.Click += (s, e) => LaunchAndMinimize(LaunchMode.Music);

            var btnHub = CreateActionButton(
                "Buka Hub & Cek IP Koneksi VPN",
                Color.FromArgb(24, 36, 32),
                Color.FromArgb(32, 50, 44),
                new Point(28, 210),
                new Size(404, 38),
                false
            );
            btnHub.ForeColor = Color.FromArgb(134, 239, 172);
            btnHub.Click += (s, e) => LaunchAndMinimize(LaunchMode.Hub);

            bool hasFf = LauncherCore.GetFirefoxPath(_baseDir) != null;
            bool hasExt = File.Exists(LauncherCore.GetExtensionPath(_baseDir));

            var statusPanel = new Panel
            {
                Location = new Point(28, 264),
                Size = new Size(404, 118),
                BackColor = Color.FromArgb(22, 22, 32)
            };
            statusPanel.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(42, 42, 58), 1))
                    e.Graphics.DrawRectangle(pen, 0, 0, statusPanel.Width - 1, statusPanel.Height - 1);
            };

            var lblStatus = new Label
            {
                Location = new Point(14, 12),
                Size = new Size(376, 94),
                ForeColor = Color.FromArgb(209, 213, 219),
                Font = new Font("Segoe UI", 8.8f),
                Text =
                    "Status Portable:\n" +
                    "• Engine Browser: " + (hasFf ? "Siap (Portable Firefox ESR)" : "TIDAK DITEMUKAN") + "\n" +
                    "• Extension VPN: " + (hasExt ? "Siap (Browsec VPN v4.1.1)" : "TIDAK DITEMUKAN") + "\n" +
                    "• Profil Portable: data\\profile\n\n" +
                    "Tip: Klik ikon perisai Browsec di kanan atas browser untuk ON/OFF atau pilih negara."
            };
            statusPanel.Controls.Add(lblStatus);

            Controls.Add(title);
            Controls.Add(subtitle);
            Controls.Add(btnBoth);
            Controls.Add(btnVideo);
            Controls.Add(btnMusic);
            Controls.Add(btnHub);
            Controls.Add(statusPanel);
        }

        private void LaunchAndMinimize(LaunchMode mode)
        {
            if (LauncherCore.LaunchBrowser(_baseDir, mode))
            {
                WindowState = FormWindowState.Minimized;
            }
        }

        private Button CreateActionButton(string text, Color bg, Color hoverBg, Point loc, Size size, bool bold)
        {
            var btn = new Button
            {
                Text = text,
                Location = loc,
                Size = size,
                FlatStyle = FlatStyle.Flat,
                BackColor = bg,
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", bold ? 10.5f : 9.5f, bold ? FontStyle.Bold : FontStyle.Regular)
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = Color.FromArgb(55, 55, 75);
            btn.MouseEnter += (s, e) => btn.BackColor = hoverBg;
            btn.MouseLeave += (s, e) => btn.BackColor = bg;
            return btn;
        }
    }
}
