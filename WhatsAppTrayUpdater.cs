// WhatsApp Tray Updater
// Monitors for new WhatsApp Desktop versions via store.rg-adguard.net
// Runs silently in the system tray, notifies on new versions
// Credit: Mario Dengg @ techwiz-services.eu

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("WhatsApp Tray Updater")]
[assembly: AssemblyDescription("WhatsApp Desktop updater without Microsoft Store")]
[assembly: AssemblyCompany("techwiz-services.eu")]
[assembly: AssemblyProduct("WhatsApp Tray Updater")]
[assembly: AssemblyCopyright("Mario Dengg @ techwiz-services.eu")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace WhatsAppTrayUpdater
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            bool createdNew;
            using (var mutex = new Mutex(true, "WhatsAppTrayUpdater_SingleInstance_v1", out createdNew))
            {
                if (!createdNew) return;
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }
    }

    /// <summary>
    /// Hidden form — acts as the UI thread message pump and sync context.
    /// The tray icon, menu, and all UI updates live here.
    /// </summary>
    class MainForm : Form
    {
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _menu;
        private ToolStripMenuItem _installedVersionItem;
        private ToolStripMenuItem _newVersionItem;
        private ToolStripMenuItem _downloadItem;
        private ToolStripMenuItem _statusItem;
        private ToolStripMenuItem _checkNowItem;

        private System.Windows.Forms.Timer _checkTimer;
        private const int CHECK_INTERVAL_MS = 3600000; // 1 hour

        private const string STORE_API    = "https://store.rg-adguard.net/api/GetFiles";
        private const string WHATSAPP_URL = "https://apps.microsoft.com/detail/9NKSQGP7F2NH";

        private string _installedVersion = "";
        private string _latestVersion    = "";
        private string _downloadUrl      = "";
        private bool   _updateAvailable  = false;
        private bool   _checking         = false;

        // Icon cache
        private Icon _iconNormal;
        private Icon _iconUpdate;

        public MainForm()
        {
            // Make this form invisible
            this.WindowState   = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size          = new Size(1, 1);
            this.Opacity       = 0;

            BuildIcons();
            BuildMenu();
            BuildTrayIcon();

            // First check after 3s, then every hour
            _checkTimer = new System.Windows.Forms.Timer();
            _checkTimer.Interval = CHECK_INTERVAL_MS;
            _checkTimer.Tick += (s, e) => CheckForUpdate();
            _checkTimer.Start();

            // Initial check on a short delay
            var startup = new System.Windows.Forms.Timer();
            startup.Interval = 3000;
            startup.Tick += (s, e) => { startup.Stop(); startup.Dispose(); CheckForUpdate(); };
            startup.Start();
        }

        protected override void SetVisibleCore(bool value)
        {
            // Never show the window
            base.SetVisibleCore(false);
        }

        void BuildIcons()
        {
            _iconNormal = MakeIcon(false);
            _iconUpdate = MakeIcon(true);
        }

        // Generates a small WhatsApp-style icon purely in GDI — no embedded resources needed
        static Icon MakeIcon(bool hasUpdate)
        {
            var bmp = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // Background circle
                Color bg = hasUpdate ? Color.FromArgb(255, 37, 211, 102) : Color.FromArgb(255, 18, 140, 126);
                using (var b = new SolidBrush(bg))
                    g.FillEllipse(b, 0, 0, 15, 15);

                // W glyph
                using (var p = new Pen(Color.White, 1.5f))
                {
                    g.DrawLine(p, 2,  4, 5, 11);
                    g.DrawLine(p, 5, 11, 7,  7);
                    g.DrawLine(p, 7,  7, 10, 11);
                    g.DrawLine(p, 10, 11, 13, 4);
                }

                // Orange dot for update
                if (hasUpdate)
                {
                    using (var b2 = new SolidBrush(Color.OrangeRed))
                        g.FillEllipse(b2, 10, 0, 6, 6);
                    using (var p2 = new Pen(Color.White, 1f))
                        g.DrawEllipse(p2, 10, 0, 6, 6);
                }
            }

            IntPtr hIcon = bmp.GetHicon();
            bmp.Dispose();
            return Icon.FromHandle(hIcon);
        }

        void BuildMenu()
        {
            _installedVersionItem = new ToolStripMenuItem("Installed: detecting...") { Enabled = false };
            _newVersionItem       = new ToolStripMenuItem("")                         { Enabled = false, Visible = false };
            _downloadItem         = new ToolStripMenuItem("Download && Install Update", null, OnDownloadClick)
            {
                Visible = false,
                Font    = new Font(SystemFonts.MenuFont, FontStyle.Bold)
            };
            _statusItem   = new ToolStripMenuItem("Checking for updates...") { Enabled = false };
            _checkNowItem = new ToolStripMenuItem("Check Now", null, (s, e) => CheckForUpdate());

            _menu = new ContextMenuStrip();
            _menu.Items.Add(new ToolStripMenuItem("WhatsApp Tray Updater") { Enabled = false, Font = new Font(SystemFonts.MenuFont, FontStyle.Bold) });
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_installedVersionItem);
            _menu.Items.Add(_newVersionItem);
            _menu.Items.Add(_downloadItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_statusItem);
            _menu.Items.Add(_checkNowItem);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(new ToolStripMenuItem("Credit: Mario Dengg | techwiz-services.eu") { Enabled = false });
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(new ToolStripMenuItem("Exit", null, (s, e) => ExitApp()));
        }

        void BuildTrayIcon()
        {
            _trayIcon = new NotifyIcon
            {
                Icon             = _iconNormal,
                Text             = "WhatsApp Tray Updater",
                ContextMenuStrip = _menu,
                Visible          = true
            };
            _trayIcon.MouseClick      += (s, e) => { if (e.Button == MouseButtons.Left) _menu.Show(Cursor.Position); };
            _trayIcon.BalloonTipClicked += (s, e) => { if (_updateAvailable) ShowInstallDialog(); };
        }

        // ─── Version check ───────────────────────────────────────────────────────

        void CheckForUpdate()
        {
            if (_checking) return;
            _checking = true;
            _statusItem.Text = "Checking for updates...";

            ThreadPool.QueueUserWorkItem(_ =>
            {
                string installed = GetInstalledVersion();
                string latestVer, latestUrl;
                GetLatestFromStore(out latestVer, out latestUrl);

                this.Invoke((Action)(() =>
                {
                    _installedVersion = installed;
                    _latestVersion    = latestVer;
                    _downloadUrl      = latestUrl;

                    _installedVersionItem.Text = string.IsNullOrEmpty(installed)
                        ? "Installed: not found"
                        : "Installed: " + installed;

                    bool isNewer = IsVersionNewer(latestVer, installed);
                    _updateAvailable = isNewer;

                    if (isNewer)
                    {
                        _newVersionItem.Text    = "New version: " + latestVer;
                        _newVersionItem.Visible = true;
                        _downloadItem.Visible   = true;
                        _statusItem.Text        = "Update available!";
                        _trayIcon.Icon          = _iconUpdate;
                        _trayIcon.Text          = "WhatsApp Update: " + latestVer;

                        _trayIcon.BalloonTipTitle = "WhatsApp Update Available";
                        _trayIcon.BalloonTipText  =
                            "New: "       + latestVer + "  |  " +
                            "Installed: " + (string.IsNullOrEmpty(installed) ? "not found" : installed) +
                            "\nClick to download & install.";
                        _trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                        _trayIcon.ShowBalloonTip(8000);
                    }
                    else
                    {
                        _newVersionItem.Visible = false;
                        _downloadItem.Visible   = false;
                        _trayIcon.Icon          = _iconNormal;
                        _trayIcon.Text          = "WhatsApp Tray Updater \u2014 Up to date";
                        _statusItem.Text        = string.IsNullOrEmpty(latestVer)
                            ? "Could not reach update server"
                            : "Up to date  |  Latest: " + latestVer;
                    }

                    _checking = false;
                }));
            });
        }

        static string GetInstalledVersion()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "powershell.exe",
                    Arguments              = "-NoProfile -NonInteractive -Command \"(Get-AppxPackage -Name '5319275A.WhatsAppDesktop').Version\"",
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow         = true
                };
                using (var p = Process.Start(psi))
                {
                    string v = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit(10000);
                    return v;
                }
            }
            catch { return ""; }
        }

        static void GetLatestFromStore(out string version, out string url)
        {
            version = "";
            url     = "";
            try
            {
                using (var wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                    wc.Headers[HttpRequestHeader.UserAgent]   = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
                    string body = "type=url&url=" + Uri.EscapeDataString(WHATSAPP_URL) + "&ring=Retail";
                    string html = wc.UploadString(STORE_API, body);

                    // Match msixbundle links: href="https://...5319275A.WhatsAppDesktop_VERSION_neutral...msixbundle"
                    var rx = new Regex(
                        @"href=""(https://[^""]+5319275A\.WhatsAppDesktop_(\d+\.\d+\.\d+\.\d+)_neutral[^""]+\.msixbundle)""",
                        RegexOptions.IgnoreCase);

                    string bestVer = "";
                    string bestUrl = "";

                    foreach (Match m in rx.Matches(html))
                    {
                        string mUrl = m.Groups[1].Value;
                        string mVer = m.Groups[2].Value;
                        if (string.IsNullOrEmpty(bestVer) || IsVersionNewer(mVer, bestVer))
                        {
                            bestVer = mVer;
                            bestUrl = mUrl;
                        }
                    }

                    version = bestVer;
                    url     = bestUrl;
                }
            }
            catch { }
        }

        static bool IsVersionNewer(string candidate, string current)
        {
            if (string.IsNullOrEmpty(candidate)) return false;
            if (string.IsNullOrEmpty(current))   return true;
            try { return new Version(candidate) > new Version(current); }
            catch { return false; }
        }

        // ─── Download & Install ──────────────────────────────────────────────────

        void OnDownloadClick(object sender, EventArgs e)
        {
            if (_updateAvailable) ShowInstallDialog();
        }

        void ShowInstallDialog()
        {
            if (string.IsNullOrEmpty(_downloadUrl)) return;

            string inst = string.IsNullOrEmpty(_installedVersion) ? "not installed" : _installedVersion;
            string msg  = "WhatsApp Desktop Update\n\n" +
                          "Installed version:   " + inst + "\n" +
                          "Available version:  " + _latestVersion + "\n\n" +
                          "Download and install now?\n" +
                          "(The app will be downloaded to %TEMP% and installed silently.)";

            if (MessageBox.Show(msg, "WhatsApp Tray Updater",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _downloadItem.Enabled = false;
            _statusItem.Text      = "Downloading...";

            ThreadPool.QueueUserWorkItem(_ => DownloadAndInstall());
        }

        void DownloadAndInstall()
        {
            string tempFile = Path.Combine(
                Path.GetTempPath(),
                "WhatsAppDesktop_" + _latestVersion + ".msixbundle");

            try
            {
                // Download
                using (var wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)";
                    long totalBytes = 0;

                    wc.DownloadProgressChanged += (s, e) =>
                    {
                        totalBytes = e.TotalBytesToReceive;
                        int pct = e.ProgressPercentage;
                        this.Invoke((Action)(() =>
                        {
                            _statusItem.Text = totalBytes > 0
                                ? string.Format("Downloading... {0}%", pct)
                                : string.Format("Downloading... {0} MB", e.BytesReceived / 1048576);
                        }));
                    };

                    var done = new ManualResetEventSlim(false);
                    Exception dlEx = null;
                    wc.DownloadFileCompleted += (s, e) => { dlEx = e.Error; done.Set(); };
                    wc.DownloadFileAsync(new Uri(_downloadUrl), tempFile);
                    done.Wait();
                    if (dlEx != null) throw dlEx;
                }

                this.Invoke((Action)(() => _statusItem.Text = "Installing..."));

                // Install via Add-AppxPackage
                var psi = new ProcessStartInfo
                {
                    FileName               = "powershell.exe",
                    Arguments              = "-NoProfile -NonInteractive -Command \"Add-AppxPackage -Path '" + tempFile + "'\"",
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true
                };

                using (var p = Process.Start(psi))
                {
                    string stderr = p.StandardError.ReadToEnd();
                    p.WaitForExit(120000);
                    int exit = p.ExitCode;

                    this.Invoke((Action)(() =>
                    {
                        _downloadItem.Enabled = true;
                        if (exit == 0)
                        {
                            _updateAvailable        = false;
                            _installedVersion       = _latestVersion;
                            _installedVersionItem.Text = "Installed: " + _installedVersion;
                            _newVersionItem.Visible = false;
                            _downloadItem.Visible   = false;
                            _trayIcon.Icon          = _iconNormal;
                            _trayIcon.Text          = "WhatsApp Tray Updater \u2014 Up to date";
                            _statusItem.Text        = "Installed " + _latestVersion + " successfully";
                            MessageBox.Show(
                                "WhatsApp " + _latestVersion + " installed successfully.",
                                "WhatsApp Tray Updater", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                        else
                        {
                            _statusItem.Text = "Installation failed";
                            string snippet = string.IsNullOrEmpty(stderr)
                                ? "(no error output)"
                                : stderr.Substring(0, Math.Min(stderr.Length, 400));
                            MessageBox.Show(
                                "Installation failed (exit code " + exit + "):\n\n" + snippet,
                                "WhatsApp Tray Updater", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }));
                }
            }
            catch (Exception ex)
            {
                this.Invoke((Action)(() =>
                {
                    _downloadItem.Enabled = true;
                    _statusItem.Text      = "Download/install failed";
                    MessageBox.Show(
                        "Failed:\n\n" + ex.Message,
                        "WhatsApp Tray Updater", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }));
            }
            finally
            {
                try { File.Delete(tempFile); } catch { }
            }
        }

        // ─── Cleanup ─────────────────────────────────────────────────────────────

        void ExitApp()
        {
            _trayIcon.Visible = false;
            _checkTimer?.Stop();
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // Suppress close; only exit via tray menu
            if (e.CloseReason == CloseReason.UserClosing)
                e.Cancel = true;
            else
                base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _checkTimer?.Dispose();
                _trayIcon?.Dispose();
                _menu?.Dispose();
                _iconNormal?.Dispose();
                _iconUpdate?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
