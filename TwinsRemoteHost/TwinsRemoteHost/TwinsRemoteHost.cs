using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net.Http.Json;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.ComponentModel;

namespace TwinsRemoteHost
{
    public partial class Host : Form
    {
        private readonly string version = "1.0.1";

        private bool init = true;
        private List<string> notifications = [];
        private string updateNotification = string.Empty;
        private int notificationCount = 0;
        private readonly string releaseUrl = "https://github.com/Eye94Mask/TwinsRemote/releases";

        private readonly string signalBaseUrl = "https://play.twins-remote.com";
        private Process? _hostProcess;
        private Locale locale;
        private ProcessSelectorForm? pSelector = null;
        private ModeCreatorForm? mCreator = null;
        private ModeEditorForm? mEditor = null;
        private NotificationsForm? notificationsForm = null;
        private string pId = string.Empty;
        private Status status = Status.Stop;

        public Host()
        {
            InitializeComponent();

            if (LicenseManager.UsageMode == LicenseUsageMode.Designtime)
            {
                return;
            }

            _ = InitializeUi();

            FormClosing += Host_FormClosing;
        }

        private void Host_Load(object sender, EventArgs e)
        {

        }

        private int OrganizeNotificationAndGetCount()
        {
            List<string> notifications = [];
            int count = 0;
            foreach (string notification in this.notifications)
            {
                string notice = notification.Split('@')[0];
                notifications.Add(notice);
                DateTime dt = DateTime.Parse(notification.Split('@')[1]);

                // 取得したお知らせが前回のチェック時以降に追加されていた場合
                if (Properties.Settings.Default.NotificationDate.Date < dt.Date)
                {
                    count++;
                }
            }
            this.notifications.Clear();
            this.notifications = notifications;

            return count;
        }

        private void SetNotificationCounter()
        {
            this.notificationCount = OrganizeNotificationAndGetCount();

            if (this.notifications.Count > 0)
            {
                infoBellPictureBox.Enabled = true;
            }

            if (this.notificationCount > 0)
            {
                notificationCountLabel.Text = this.notificationCount.ToString();

                return;
            }

            if (this.updateNotification != string.Empty)
            {
                infoBellPictureBox.Enabled = true;
            }

            notificationCountLabel.Text = string.Empty;
        }

        private async Task InitializeNotifications()
        {
            this.notificationCount = 0;
            this.notifications.Clear();
            this.updateNotification = string.Empty;

            // notices format: "{Notification}@{NotificationDate}"
            string[] notices = await InformNotifications();
            foreach (string notice in notices)
            {
                if (!this.notifications.Contains(notice)) { this.notifications.Add(notice); }
            }
            await InformUpdate();

            SetNotificationCounter();
        }

        private async Task<string[]> InformNotifications()
        {
            using var http = new HttpClient();
            string url = this.signalBaseUrl.TrimEnd('/') + "/notifications";

            Notifications? resp = await http.GetFromJsonAsync<Notifications>(url);
            if (resp == null) { return []; }

            switch (languageComboBox.SelectedValue)
            {
                case "ja-JP": return resp.Japanese;
                case "en-US": return resp.English;
                default: break;
            }

            return resp.Japanese;
        }

        private async Task InformUpdate()
        {
            (int major, int minor, int patch) = await GetLatestVersion();
            (int currentMajor, int currentMinor, int currentPatch) = GetCurrentVersion();

            if (major > currentMajor) { NeedToUpdate(); return; }
            if (minor > currentMinor) { InformNewFeature(); return; }
            if (patch > currentPatch) { InformNewPatch(); return; }
        }

        private void NeedToUpdate()
        {
            MessageBox.Show($"{this.locale.NewMajorUpdate}\n", locale.Notice,
                        MessageBoxButtons.OK, MessageBoxIcon.Information);

            var ps = new Process();
            ps.StartInfo.UseShellExecute = true;
            ps.StartInfo.FileName = this.releaseUrl;
            ps.Start();

            this.Close();
        }

        private void InformNewFeature()
        {
            this.updateNotification = this.locale.NewMinorUpdate;
            updateLabel.Text = "↺";
        }

        private void InformNewPatch()
        {
            this.updateNotification = this.locale.NewPatchUpdate;
            updateLabel.Text = "↺";
        }

        private static (int, int, int) StringVersionToIntVersion(string version)
        {
            string[] versions = version.Split('.');
            if (versions.Length != 3) { return (-1, -1, -1); }

            try
            {
                int major = int.Parse(versions[0]);
                int minor = int.Parse(versions[1]);
                int patch = int.Parse(versions[2]);

                return (major, minor, patch);
            }
            catch { }

            return (-1, -1, -1);
        }

        private (int, int, int) GetCurrentVersion()
        {
            return StringVersionToIntVersion(this.version);
        }

        private async Task<(int, int, int)> GetLatestVersion()
        {
            using var http = new HttpClient();
            string url = this.signalBaseUrl.TrimEnd('/') + "/latest-host-version";

            LatestHostVersionResponse? resp = await http.GetFromJsonAsync<LatestHostVersionResponse>(url);
            if (resp == null) { return (0, 0, 0); }

            return StringVersionToIntVersion(resp.Version);
        }

        private void ResetModeList(String? previousModeValue = null)
        {
            List<string> customNames = GetCustomModeList();

            modeComboBox.Items.Clear();
            List<object> modes = [];
            foreach (string customName in customNames)
            {
                modeComboBox.Items.Add(new VideoPresetItem { DisplayName = customName, Key = customName });
            }
            modeComboBox.Items.Add(new VideoPresetItem { DisplayName = this.locale.BalancedMode, Key = "balanced" });
            modeComboBox.Items.Add(new VideoPresetItem { DisplayName = this.locale.QualityMode, Key = "quality" });
            modeComboBox.Items.Add(new VideoPresetItem { DisplayName = this.locale.StableMode, Key = "stable" });
            modeComboBox.Items.Add(new VideoPresetItem { DisplayName = this.locale.MobileMode, Key = "mobile" });

            if (previousModeValue != null || previousModeValue == String.Empty)
            {
                int i = 0;
                foreach (VideoPresetItem item in modeComboBox.Items)
                {
                    if (item.DisplayName == previousModeValue)
                    {
                        modeComboBox.SelectedIndex = i;
                        return;
                    }
                    i++;
                }

                if (i >= modeComboBox.Items.Count) { modeComboBox.SelectedIndex = 0; }
            }
            else modeComboBox.SelectedIndex = 0;
        }

        private async Task InitializeUi()
        {
            string language = Properties.Settings.Default.Language;
            InitializeLanguageComboBox();
            languageComboBox.SelectedIndex = languageComboBox.FindStringExact(language);
            ApplyLanguage();

            versionLabel.Text = "v" + this.version;
            ResetModeList();

            statusValueLabel.Text = locale.StatusStopped;
            SetRunningState(false);

            SetCustomEditorButton();

            modeComboBox.SelectedIndex = modeComboBox.FindString(Properties.Settings.Default.Mode);

            await InitializeNotifications();
            this.init = false;
        }

        private void InitializeLanguageComboBox()
        {
            var languages = new[]
            {
                new { Name = "日本語", Code = "ja-JP" },
                new { Name = "English", Code = "en-US" }
            };
            languageComboBox.DisplayMember = "Name";
            languageComboBox.ValueMember = "Code";
            languageComboBox.DataSource = languages;
        }

        private void Host_FormClosing(object? sender, FormClosingEventArgs e)
        {
            Process[] systemMixCapture = Process.GetProcessesByName("SystemMixCapture");
            Process[] processAudioCapture = Process.GetProcessesByName("ProcessAudioCapture");
            Process[] nvEnc = Process.GetProcessesByName("NvEnc");
            Process[] twinsRemoteHost = Process.GetProcessesByName("twins_remote_host");

            KillProcesses(systemMixCapture);
            KillProcesses(processAudioCapture);
            KillProcesses(nvEnc);
            KillProcesses(twinsRemoteHost);
        }

        private void KillProcesses(Process[] processes)
        {
            foreach (Process process in processes)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                        process.WaitForExit(2000);
                    }
                }
                catch (Exception ex)
                {
                    AppendLog(this.locale.ProcessExitError + ex.Message);
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        private void SetRunningState(bool running)
        {
            modeComboBox.Enabled = !running;
            sessionIdTextBox.Enabled = !running;
            connectButton.Enabled = !running;

            audioOnButton.Enabled = running;
            audioOffButton.Enabled = running;
            audioSystemButton.Enabled = running;
        }

        private void AppendLog(string message)
        {
            logTextBox.AppendText(
                $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}"
            );
        }

        private void SetStatus(string status)
        {
            statusValueLabel.Text = status;
        }

        private void HostProcess_Exited(object? sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => HostProcess_Exited(sender, e)));
                return;
            }

            AppendLog(this.locale.HostExeExit);
            SetStatus(locale.StatusStopped);
            SetRunningState(false);
        }

        private async Task ReadOutputLoop(Process process)
        {
            try
            {
                while (true)
                {
                    string? line = await process.StandardOutput.ReadLineAsync();
                    if (line == null) break;
                    if (line.Contains("ICE_CONNECTED"))
                    {
                        this.status = Status.Connected;
                        statusValueLabel.Text = this.locale.StatusConnected;
                    }
                    if (line.Contains("ICE_DISCONNECTED"))
                    {
                        this.status = Status.Disconnected;
                        statusValueLabel.Text = this.locale.StatusDisconnected;
                    }

                    BeginInvoke(new Action(() =>
                    {
                        AppendLog("[OUT] " + line);
                    }));
                }
            }
            catch (Exception ex)
            {
                BeginInvoke(new Action(() =>
                {
                    AppendLog(this.locale.ReadLineFailed + ex.Message);
                }));
            }
        }

        private async Task ReadErrorLoop(Process process)
        {
            try
            {
                while (true)
                {
                    string? line = await process.StandardError.ReadLineAsync();
                    if (line == null) break;

                    BeginInvoke(new Action(() =>
                    {
                        AppendLog("[ERR] " + line);
                    }));
                }
            }
            catch (Exception ex)
            {
                BeginInvoke(new Action(() =>
                {
                    AppendLog(this.locale.ReadErrorFailed + ex.Message);
                }));
            }
        }

        public void SendCommand(string command)
        {
            if (_hostProcess == null || _hostProcess.HasExited)
            {
                AppendLog(this.locale.HostExeNotStarted);
                return;
            }

            try
            {
                _hostProcess.StandardInput.WriteLine(command);
                _hostProcess.StandardInput.Flush();
                AppendLog("[CMD] " + command);
            }
            catch (Exception ex)
            {
                AppendLog(this.locale.SendCommandFailed + ex.Message);
            }
        }

        private async Task<string> IssueHostTokenAsync(string sessionId)
        {
            using var http = new HttpClient();

            string url = this.signalBaseUrl.TrimEnd('/') + "/issue-host-token";

            var resp = await http.PostAsJsonAsync(url, new IssueHostTokenRequest
            {
                SessionId = sessionId
            });

            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadFromJsonAsync<IssueHostTokenResponse>();
            if (body == null || string.IsNullOrWhiteSpace(body.Token))
                throw new Exception("host token response is empty");

            return body.Token;
        }

        private async void languageComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            string lang = languageComboBox.Text.ToString() ?? "Japanese";

            if (lang != string.Empty)
            {
                Properties.Settings.Default.Language = lang;
                Properties.Settings.Default.Save();
            }

            ApplyLanguage();

            if (!this.init) { await InitializeNotifications(); }
        }

        private void SetLocale()
        {
            if (languageComboBox.SelectedItem == null) { return; }
            string code = languageComboBox.SelectedValue.ToString();

            string localeFile = $"{code}.json";
            string localePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "locales", localeFile);
            if (!File.Exists(localePath))
            {
                AppendLog(this.locale.LocaleNotFound + localeFile);
                return;
            }

            try
            {
                string jsonStr;
                using (var sr = new StreamReader(localePath, Encoding.GetEncoding("utf-8")))
                {
                    jsonStr = sr.ReadToEnd();
                }

                this.locale = JsonConvert.DeserializeObject<Locale>(jsonStr);
            }
            catch (Exception ex)
            {
                AppendLog(this.locale.ReadLocaleFailed + ex);
            }

            return;
        }

        private string GetCurrentStatus()
        {
            switch (this.status)
            {
                case Status.Stop: return this.locale.StatusStopped;
                case Status.Start: return this.locale.StatusStart;
                case Status.Connected: return this.locale.StatusConnected;
                case Status.Disconnected: return this.locale.StatusDisconnected;
            }

            return string.Empty;
        }

        private void ApplyLanguage()
        {
            SetLocale();

            modeLabel.Text = this.locale.LabelModeText;
            sessionIdLabel.Text = this.locale.LabelSessionIdText;
            statusTitleLabel.Text = this.locale.LabelStatusTitleText;
            statusValueLabel.Text = GetCurrentStatus();
            connectButton.Text = this.locale.ButtonStartText;
            audioLabel.Text = this.locale.AudioSharingLabel;
            audioOnButton.Text = this.locale.ButtonAudioOnText;
            audioOffButton.Text = this.locale.ButtonAudioOffText;
            audioSystemButton.Text = this.locale.ButtonAudioSystemText;
            createCustomModeButton.Text = this.locale.CreateCustomMode;
            updateCustomMode.Text = this.locale.UpdateCustomMode;
            saveLogButton.Text = this.locale.SaveLog;

            int selectedIndex = modeComboBox.SelectedIndex;
            ResetModeList();
            modeComboBox.SelectedIndex = selectedIndex;
        }

        public static string GetCustomDirectory()
        {
            string customDir = System.Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "/Twins Remote/customs";
            return customDir;
        }

        public static List<string> GetCustomModeList()
        {
            List<string> customNames = [];

            if (!Directory.Exists(GetCustomDirectory()))
            {
                return customNames;
            }

            string[] customs = Directory.GetFiles(GetCustomDirectory());

            foreach (string custom in customs)
            {
                customNames.Add(
                    Path.GetFileNameWithoutExtension(custom)
                );
            }

            return customNames;
        }

        private void SetCustomEditorButton()
        {
            int customLength = GetCustomModeList().Count;
            if (customLength < 1)
            {
                updateCustomMode.Enabled = false;
                return;
            }
            updateCustomMode.Enabled = true;
        }

        private async void connectButton_Click(object sender, EventArgs e)
        {
            if (_hostProcess != null && !_hostProcess.HasExited)
            {
                AppendLog(this.locale.HostExeAlreadyStarted);
                return;
            }

            string mode = (modeComboBox.SelectedItem?.ToString() ?? "Balanced");
            AppendLog(mode);
            string sessionId = sessionIdTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(sessionId))
            {
                MessageBox.Show(locale.InputSessionIdMessage, locale.Confirm,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string hostExePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exes", "twins_remote_host.exe");
            string exeDir = Path.GetDirectoryName(hostExePath)!;

            string nvEncPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exes", "NvEnc.exe");
            string processAudioCapturePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exes", "ProcessAudioCapture.exe");
            string systemMixCapture = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exes", "SystemMixCapture.exe");

            if (!File.Exists(hostExePath))
            {
                MessageBox.Show($"{locale.HostExeNotFound}\n{hostExePath}", locale.Error,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!File.Exists(nvEncPath))
            {
                MessageBox.Show($"{locale.NvEncExeNotFound}\n{nvEncPath}", locale.Error,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!File.Exists(processAudioCapturePath))
            {
                MessageBox.Show($"{locale.ProcessAudioCaptureExeNotFound}\n{processAudioCapturePath}", locale.Error,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!File.Exists(systemMixCapture))
            {
                MessageBox.Show($"{locale.SystemMixCaptureExeNotFound}\n{systemMixCapture}", locale.Error,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = hostExePath,
                WorkingDirectory = exeDir,
                Arguments = $"--mode \"{mode}\" --session \"{sessionId}\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            string hostToken = await IssueHostTokenAsync(sessionId);

            psi.Environment["SIGNAL_BASE_URL"] = this.signalBaseUrl;
            psi.Environment["WEBRTC_CONFIG_TOKEN"] = hostToken;

            _hostProcess = new Process
            {
                StartInfo = psi,
                EnableRaisingEvents = true
            };

            _hostProcess.Exited += HostProcess_Exited;

            try
            {
                bool started = _hostProcess.Start();
                if (!started)
                {
                    AppendLog(this.locale.HostExeFailedToStart);
                    return;
                }

                status = Status.Start;
                AppendLog($"{locale.HostExeStart}mode={mode}, session={sessionId}");
                SetStatus(locale.StatusStart);
                SetRunningState(true);

                _ = Task.Run(() => ReadOutputLoop(_hostProcess));
                _ = Task.Run(() => ReadErrorLoop(_hostProcess));
            }
            catch (Exception ex)
            {
                AppendLog(this.locale.statusStartException + ex.Message);
                SetStatus(locale.StatusFailed);
                SetRunningState(false);
            }
        }

        private void buttonAudioOn_Click(object sender, EventArgs e)
        {
            if (pSelector == null || pSelector.IsDisposed)
            {
                pSelector = new ProcessSelectorForm();
                pSelector.Left = this.Left + 200;
                pSelector.Top = this.Top + 200;
                pSelector.StartPosition = FormStartPosition.Manual;
                pSelector.ShowDialog();
                pId = pSelector.GetProcessId();
            }
            else
            {
                pSelector.WindowState = FormWindowState.Normal;
                pSelector.Activate();
            }

            pSelector.Dispose();
            pSelector = null;
            if (pId != string.Empty)
            {
                SendCommand(pId);
                pId = string.Empty;

            }
        }

        private void buttonAudioOff_Click(object sender, EventArgs e)
        {
            SendCommand("audio_stop");
        }

        private void buttonAudioSystem_Click(object sender, EventArgs e)
        {
            SendCommand("system");
        }

        private void createMode_Click(object sender, EventArgs e)
        {
            String selectedModeValue = string.Empty;
            if (modeComboBox.SelectedItem != null)
            {
                selectedModeValue = modeComboBox.SelectedItem.ToString();
            }

            if (this.mCreator == null || this.mCreator.IsDisposed)
            {
                this.mCreator = new ModeCreatorForm(this.locale);
                this.mCreator.Left = this.Left + 200;
                this.mCreator.Top = this.Top + 200;
                this.mCreator.StartPosition = FormStartPosition.Manual;
                this.mCreator.ShowDialog();
            }
            else
            {
                this.mCreator.WindowState = FormWindowState.Normal;
                this.mCreator.Activate();
            }

            SetCustomEditorButton();
            ResetModeList(selectedModeValue);
            this.mCreator.Dispose();
            this.mCreator = null;
        }

        private void updateDeleteCustomMode_Click(object sender, EventArgs e)
        {
            String selectedModeValue = string.Empty;
            if (modeComboBox.SelectedItem != null)
            {
                selectedModeValue = modeComboBox.SelectedItem.ToString();
            }

            if (this.mEditor == null || this.mEditor.IsDisposed)
            {
                this.mEditor = new ModeEditorForm(this.locale);
                this.mEditor.Left = this.Left + 200;
                this.mEditor.Top = this.Top + 200;
                this.mEditor.StartPosition = FormStartPosition.Manual;
                this.mEditor.ShowDialog();
            }
            else
            {
                this.mEditor.WindowState = FormWindowState.Normal;
                this.mEditor.Activate();
            }

            SetCustomEditorButton();
            ResetModeList(selectedModeValue);
            this.mEditor.Dispose();
            this.mEditor = null;
        }

        private void infoBellPictureBox_Click(object sender, EventArgs e)
        {
            OpenNotificationForm();
        }

        private void notificationCountLabel_Click(object sender, EventArgs e)
        {
            OpenNotificationForm();
        }

        private void OpenNotificationForm()
        {
            if (this.notificationsForm == null || this.notificationsForm.IsDisposed)
            {
                this.notificationsForm = new NotificationsForm(this.notifications, this.updateNotification, this.releaseUrl);
                this.notificationsForm.Left = this.Left + 200;
                this.notificationsForm.Top = this.Top + 200;
                this.notificationsForm.ShowDialog();
            }
            else
            {
                this.notificationsForm.WindowState = FormWindowState.Normal;
                this.notificationsForm.Activate();
            }

            Properties.Settings.Default.NotificationDate = DateTime.Now;
            Properties.Settings.Default.Save();
            this.notificationCount = 0;
            notificationCountLabel.Text = "";

            SetCustomEditorButton();
            this.notificationsForm.Dispose();
            this.notificationsForm = null;
        }

        private void saveLogButton_Click(object sender, EventArgs e)
        {
            // ログファイル（例：TwinsRemote_Host_2020_01_01_12_00_00.txt）
            DateTime now = DateTime.Now;
            string dt = now.ToString("yyyy_MM_dd_HH_mm_ss");
            string fileName = "TwinsRemote_Host_" + dt + ".txt";
            string filePath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) + "/" + fileName;

            string log = logTextBox.Text;
            StreamWriter sw = new StreamWriter(filePath, false, Encoding.UTF8);
            sw.Write(log);
            sw.Close();

            // 保存先の通知
            MessageBox.Show(this.locale.LogHasBeenSaved + "\n" + filePath, locale.Confirm,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void modeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.init) return;

            Properties.Settings.Default.Mode = modeComboBox.SelectedItem.ToString();
            Properties.Settings.Default.Save();
        }
    }

    public enum Status
    {
        Stop,
        Start,
        Connected,
        Disconnected
    }

    public class Language
    {
        public required string Name { get; set; }
        public required string Code { get; set; }
    }

    public class VideoPresetItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;

        public override string ToString() => DisplayName;
    }

    public class Locale
    {
        [JsonProperty("labelModeText")]
        public required string LabelModeText { get; set; }

        [JsonProperty("labelSessionIdText")]
        public required string LabelSessionIdText { get; set; }

        [JsonProperty("labelStatusTitleText")]
        public required string LabelStatusTitleText { get; set; }

        [JsonProperty("buttonStartText")]
        public required string ButtonStartText { get; set; }

        [JsonProperty("audioSharingLabel")]
        public required string AudioSharingLabel { get; set; }

        [JsonProperty("buttonAudioOnText")]
        public required string ButtonAudioOnText { get; set; }

        [JsonProperty("buttonAudioOffText")]
        public required string ButtonAudioOffText { get; set; }

        [JsonProperty("buttonAudioSystemText")]
        public required string ButtonAudioSystemText { get; set; }

        [JsonProperty("processExitError")]
        public required string ProcessExitError { get; set; }

        [JsonProperty("hostExeAlreadyStarted")]
        public required string HostExeAlreadyStarted { get; set; }

        [JsonProperty("inputSessionIdMessage")]
        public required string InputSessionIdMessage { get; set; }

        [JsonProperty("hostExeNotFound")]
        public required string HostExeNotFound { get; set; }

        [JsonProperty("hostExeFailedToStart")]
        public required string HostExeFailedToStart { get; set; }

        [JsonProperty("hostExeStart")]
        public required string HostExeStart { get; set; }

        [JsonProperty("hostExeExit")]
        public required string HostExeExit { get; set; }

        [JsonProperty("hostExeNotStarted")]
        public required string HostExeNotStarted { get; set; }

        [JsonProperty("nvEncExeNotFound")]
        public required string NvEncExeNotFound { get; set; }

        [JsonProperty("processAudioCaptureExeNotFound")]
        public required string ProcessAudioCaptureExeNotFound { get; set; }

        [JsonProperty("systemMixCaptureExeNotFound")]
        public required string SystemMixCaptureExeNotFound { get; set; }

        [JsonProperty("statusStartException")]
        public required string statusStartException { get; set; }

        [JsonProperty("statusFailed")]
        public required string StatusFailed { get; set; }

        [JsonProperty("statusStart")]
        public required string StatusStart { get; set; }

        [JsonProperty("statusStopped")]
        public required string StatusStopped { get; set; }

        [JsonProperty("statusConnected")]
        public required string StatusConnected { get; set; }

        [JsonProperty("statusDisconnected")]
        public required string StatusDisconnected { get; set; }

        [JsonProperty("readLineFailed")]
        public required string ReadLineFailed { get; set; }

        [JsonProperty("readErrorFailed")]
        public required string ReadErrorFailed { get; set; }

        [JsonProperty("sendCommandFailed")]
        public required string SendCommandFailed { get; set; }

        [JsonProperty("invalidLanguage")]
        public required string InvalidLanguage { get; set; }

        [JsonProperty("localeNotFound")]
        public required string LocaleNotFound { get; set; }

        [JsonProperty("readLocaleFailed")]
        public required string ReadLocaleFailed { get; set; }

        [JsonProperty("notice")]
        public required string Notice{ get; set; }

        [JsonProperty("confirm")]
        public required string Confirm { get; set; }

        [JsonProperty("error")]
        public required string Error { get; set; }

        [JsonProperty("createCustomMode")]
        public required string CreateCustomMode { get; set; }

        [JsonProperty("modeName")]
        public required string ModeName { get; set; }

        [JsonProperty("resolution")]
        public required string Resolution { get; set; }

        [JsonProperty("presets")]
        public required string Presets { get; set; }

        [JsonProperty("everydayUsePresets")]
        public required string EverydayUsePresets { get; set; }

        [JsonProperty("stablePresets")]
        public required string StablePresets { get; set; }

        [JsonProperty("qualityPresets")]
        public required string QualityPresets { get; set; }

        [JsonProperty("reducingNetworkLoadPresets")]
        public required string ReducingNetworkLoadPresets { get; set; }

        [JsonProperty("balancedMode")]
        public required string BalancedMode { get; set; }

        [JsonProperty("qualityMode")]
        public required string QualityMode { get; set; }

        [JsonProperty("stableMode")]
        public required string StableMode { get; set; }

        [JsonProperty("mobileMode")]
        public required string MobileMode { get; set; }

        [JsonProperty("highFpsMode")]
        public required string HighFpsMode { get; set; }

        [JsonProperty("fourKMode")]
        public required string FourKMode { get; set; }

        [JsonProperty("wifiFriendlyMode")]
        public required string WifiFriendlyMode { get; set; }

        [JsonProperty("ipv4Mode")]
        public required string Ipv4Mode { get; set; }

        [JsonProperty("restrictedIpv4Mode")]
        public required string RestrictedIpv4Mode { get; set; }

        [JsonProperty("resolutionDescription")]
        public required string ResolutionDescription { get; set; }

        [JsonProperty("fpsDescription")]
        public required string FpsDescription { get; set; }

        [JsonProperty("casualModeDescription")]
        public required string CasualModeDescription { get; set; }

        [JsonProperty("lowLatencyModeDescription")]
        public required string LowLatencyModeDescription { get; set; }

        [JsonProperty("qualityModeDescription")]
        public required string QualityModeDescription { get; set; }

        [JsonProperty("reducingNetworkLoadModeDescription")]
        public required string ReducingNetworkLoadModeDescription { get; set; }

        [JsonProperty("toggleDescriptionClose")]
        public required string ToggleDescriptionClose { get; set; }

        [JsonProperty("toggleDescriptionOpen")]
        public required string ToggleDescriptionOpen { get; set; }

        [JsonProperty("averageBitrateDescription")]
        public required string AverageBitrateDescription { get; set; }

        [JsonProperty("maxBitrateDescription")]
        public required string MaxBitrateDescription { get; set; }

        [JsonProperty("vbvBufferSizeDescription")]
        public required string VbvBufferSizeDescription { get; set; }

        [JsonProperty("vbvInitialDelayDescription")]
        public required string VbvInitialDelayDescription { get; set; }

        [JsonProperty("gopLengthDescription")]
        public required string GopLengthDescription { get; set; }

        [JsonProperty("idrPeriodDescription")]
        public required string IdrPeriodDescription { get; set; }

        [JsonProperty("repeatSpsPpsDescription")]
        public required string RepeatSpsPpsDescription { get; set; }

        [JsonProperty("outputAudDescription")]
        public required string OutputAudDescription { get; set; }

        [JsonProperty("maxRefFramesDescription")]
        public required string MaxRefFramesDescription { get; set; }

        [JsonProperty("presetGuidDescription")]
        public required string PresetGuidDescription { get; set; }

        [JsonProperty("tuningInfoDescription")]
        public required string TuningInfoDescription { get; set; }

        [JsonProperty("enableLookaheadDescription")]
        public required string EnableLookaheadDescription { get; set; }

        [JsonProperty("lookaheadDepthDescription")]
        public required string LookaheadDepthDescription { get; set; }

        [JsonProperty("disableIadaptDescription")]
        public required string DisableIadaptDescription { get; set; }

        [JsonProperty("disableBadaptDescription")]
        public required string DisableBadaptDescription { get; set; }

        [JsonProperty("alretNoModeName")]
        public required string AlretNoModeName { get; set; }

        [JsonProperty("alertNoResolution")]
        public required string AlertNoResolution { get; set; }

        [JsonProperty("alertNoFps")]
        public required string AlertNoFps { get; set; }

        [JsonProperty("alertNoAverageBitrate")]
        public required string AlertNoAverageBitrate { get; set; }

        [JsonProperty("alertNoMaxBitrate")]
        public required string AlertNoMaxBitrate { get; set; }

        [JsonProperty("alertNoVbvBufferSize")]
        public required string AlertNoVbvBufferSize { get; set; }

        [JsonProperty("alertNoVbvInitialDelay")]
        public required string AlertNoVbvInitialDelay { get; set; }

        [JsonProperty("alertNoGopLength")]
        public required string AlertNoGopLength { get; set; }

        [JsonProperty("alertNoIdrPeriod")]
        public required string AlertNoIdrPeriod { get; set; }

        [JsonProperty("alertNoPresetGuid")]
        public required string AlertNoPresetGuid { get; set; }

        [JsonProperty("alertNoTuningInfo")]
        public required string AlertNoTuningInfo { get; set; }

        [JsonProperty("alertNoLookaheadDepth")]
        public required string AlertNoLookaheadDepth { get; set; }

        [JsonProperty("alertInvalidModeName")]
        public required string AlertInvalidModeName { get; set; }

        [JsonProperty("alertInvalidResolution")]
        public required string AlertInvalidResolution { get; set; }

        [JsonProperty("alertInvalidFps")]
        public required string AlertInvalidFps { get; set; }

        [JsonProperty("alertInvalidAverageBitrate")]
        public required string AlertInvalidAverageBitrate { get; set; }

        [JsonProperty("alertInvalidMaxBitrate")]
        public required string AlertInvalidMaxBitrate { get; set; }

        [JsonProperty("alertInvalidVbvBufferSize")]
        public required string AlertInvalidVbvBufferSize { get; set; }

        [JsonProperty("alertInvalidVbvInitialDelay")]
        public required string AlertInvalidVbvInitialDelay { get; set; }

        [JsonProperty("alertInvalidGopLength")]
        public required string AlertInvalidGopLength { get; set; }

        [JsonProperty("alertInvalidIdrPeriod")]
        public required string AlertInvalidIdrPeriod { get; set; }

        [JsonProperty("alertInvalidMaxRefFrames")]
        public required string AlertInvalidMaxRefFrames { get; set; }

        [JsonProperty("alertInvalidPresetGuid")]
        public required string AlertInvalidPresetGuid { get; set; }

        [JsonProperty("alertInvalidTuningInfo")]
        public required string AlertInvalidTuningInfo { get; set; }

        [JsonProperty("alertInvalidLookaheadDepth")]
        public required string AlertInvalidLookaheadDepth { get; set; }

        [JsonProperty("customModeSaved")]
        public required string CustomModeSaved { get; set; }

        [JsonProperty("customModeUpdated")]
        public required string CustomModeUpdated { get; set; }

        [JsonProperty("updateCustomMode")]
        public required string UpdateCustomMode { get; set; }

        [JsonProperty("selectModeLabel")]
        public required string SelectModeLabel { get; set; }

        [JsonProperty("failedToReadCustom")]
        public required string FailedToReadCustom { get; set; }

        [JsonProperty("customModeNameConflictsWithOthers")]
        public required string CustomModeNameConflictsWithOthers { get; set; }

        [JsonProperty("deleteCustomSuccess")]
        public required string DeleteCustomSuccess { get; set; }

        [JsonProperty("failedToDeleteCustom")]
        public required string FailedToDeleteCustom { get; set; }

        [JsonProperty("failedToUpdateCustom")]
        public required string FailedToUpdateCustom { get; set; }

        [JsonProperty("newMajorUpdate")]
        public required string NewMajorUpdate { get; set; }

        [JsonProperty("newMinorUpdate")]
        public required string NewMinorUpdate { get; set; }

        [JsonProperty("newPatchUpdate")]
        public required string NewPatchUpdate { get; set; }

        [JsonProperty("saveLog")]
        public required string SaveLog { get; set; }

        [JsonProperty("logHasBeenSaved")]
        public required string LogHasBeenSaved { get; set; }
    }

    public class IssueHostTokenRequest
    {
        [JsonPropertyName("sessionId")]
        public string SessionId { get; set; } = string.Empty;
    }

    public class IssueHostTokenResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        [JsonPropertyName("expiresIn")]
        public int ExpiresIn { get; set; }
    }

    public class LatestHostVersionResponse
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = string.Empty;
    }

    public class Notifications
    {
        [JsonPropertyName("japanese")]
        public string[] Japanese { get; set; } = [];

        [JsonPropertyName("english")]
        public string[] English { get; set; } = [];
    }
}