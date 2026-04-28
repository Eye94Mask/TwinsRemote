using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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

namespace TwinsRemoteHost
{
    public partial class Host : Form
    {
        private Process? _hostProcess;
        private Locale locale;
        private ProcessSelector? pSelector = null;
        private ModeCreator? mCreator = null;
        private string pId = "";
        private Status status = Status.Stop;

        public Host()
        {
            InitializeComponent();
            InitializeUi();

            FormClosing += Host_FormClosing;
        }

        private void Host_Load(object sender, EventArgs e)
        {

        }

        private void InitializeUi()
        {
            comboBoxMode.Items.Clear();
            comboBoxMode.Items.Add(new VideoPresetItem { DisplayName = "Balanced", Key = "balanced" });
            comboBoxMode.Items.Add(new VideoPresetItem { DisplayName = "Quality", Key = "quality" });
            comboBoxMode.Items.Add(new VideoPresetItem { DisplayName = "Stable", Key = "stable" });
            comboBoxMode.Items.Add(new VideoPresetItem { DisplayName = "Mobile", Key = "mobile" });
            comboBoxMode.Items.Add(new VideoPresetItem { DisplayName = "IPv4", Key = "ipv4" });
            comboBoxMode.SelectedIndex = 0;

            string language = Properties.Settings.Default.Language;
            InitializeLanguageComboBox();
            comboBoxLanguage.SelectedIndex = comboBoxLanguage.FindStringExact(language);
            ApplyLanguage();

            labelStatusValue.Text = locale.StatusStopped;
            SetRunningState(false);
        }

        private void InitializeLanguageComboBox()
        {
            var languages = new[]
            {
                new { Name = "日本語", Code = "ja-JP" },
                new { Name = "English", Code = "en-US" }
            };
            comboBoxLanguage.DisplayMember = "Name";
            comboBoxLanguage.ValueMember = "Code";
            comboBoxLanguage.DataSource = languages;
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
                    AppendLog(locale.ProcessExitError + ex.Message);
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        private void SetRunningState(bool running)
        {
            comboBoxMode.Enabled = !running;
            textBoxSessionId.Enabled = !running;
            buttonStart.Enabled = !running;

            buttonAudioOn.Enabled = running;
            buttonAudioOff.Enabled = running;
            buttonAudioSystem.Enabled = running;
        }

        private void AppendLog(string message)
        {
            textBoxLog.AppendText(
                $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}"
            );
        }

        private void SetStatus(string status)
        {
            labelStatusValue.Text = status;
        }

        private void HostProcess_Exited(object? sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => HostProcess_Exited(sender, e)));
                return;
            }

            AppendLog(locale.HostExeExit);
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
                        labelStatusValue.Text = locale.StatusConnected;
                    }
                    if (line.Contains("ICE_DISCONNECTED"))
                    {
                        this.status = Status.Disconnected;
                        labelStatusValue.Text = locale.StatusDisconnected;
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
                    AppendLog(locale.ReadLineFailed + ex.Message);
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
                    AppendLog(locale.ReadErrorFailed + ex.Message);
                }));
            }
        }

        public void SendCommand(string command)
        {
            if (_hostProcess == null || _hostProcess.HasExited)
            {
                AppendLog(locale.HostExeNotStarted);
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
                AppendLog(locale.SendCommandFailed + ex.Message);
            }
        }

        private async Task<string> IssueHostTokenAsync(string signalBaseUrl, string sessionId)
        {
            using var http = new HttpClient();

            string url = signalBaseUrl.TrimEnd('/') + "/issue-host-token";

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

        private void comboBoxLanguage_SelectedIndexChanged(object sender, EventArgs e)
        {
            string lang = comboBoxLanguage.Text.ToString() ?? "Japanese";

            if (lang != string.Empty)
            {
                Properties.Settings.Default.Language = lang;
                Properties.Settings.Default.Save();
            }

            ApplyLanguage();
        }

        private bool isValidLanguage(string code)
        {
            switch (code)
            {
                case "ja-JP":
                case "en-US":
                    {
                        return true;
                    }
            }

            return false;
        }

        private void SetLocale()
        {
            if (comboBoxLanguage.SelectedItem == null) { return; }
            string? language = comboBoxLanguage.SelectedItem.ToString();
            if (language == null) { return; }

            string[] words = language.Split(' ');
            if (words.Length != 8 && words[4] != "Code")
            {
                AppendLog(locale.InvalidLanguage);
                return;
            }

            string code = words[6];
            if (!isValidLanguage(code))
            {
                AppendLog(locale.InvalidLanguage);
                return;
            }

            string localeFile = $"{code}.json";
            string localePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "locales", localeFile);
            if (!File.Exists(localePath))
            {
                AppendLog(locale.LocaleNotFound + localeFile);
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
                AppendLog(locale.ReadLocaleFailed + ex);
            }

            return;
        }

        private string GetCurrentStatus()
        {
            switch (this.status)
            {
                case Status.Stop: return locale.StatusStopped;
                case Status.Start: return locale.StatusStart;
                case Status.Connected: return locale.StatusConnected;
                case Status.Disconnected: return locale.StatusDisconnected;
            }

            return "";
        }

        private void ApplyLanguage()
        {
            SetLocale();

            labelMode.Text = locale.LabelModeText;
            labelSessionId.Text = locale.LabelSessionIdText;
            labelStatusTitle.Text = locale.LabelStatusTitleText;
            labelStatusValue.Text = GetCurrentStatus();
            buttonStart.Text = locale.ButtonStartText;
            buttonAudioOn.Text = locale.ButtonAudioOnText;
            buttonAudioOff.Text = locale.ButtonAudioOffText;
            buttonAudioSystem.Text = locale.ButtonAudioSystemText;
        }

        private async void buttonStart_Click(object sender, EventArgs e)
        {
            if (_hostProcess != null && !_hostProcess.HasExited)
            {
                AppendLog(locale.HostExeAlreadyStarted);
                return;
            }

            string mode = (comboBoxMode.SelectedItem?.ToString() ?? "Balanced").ToLower();
            string sessionId = textBoxSessionId.Text.Trim();

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
                Arguments = $"--mode {mode} --session \"{sessionId}\"",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            string signalBaseUrl = "https://play.twins-remote.com";
            string hostToken = await IssueHostTokenAsync(signalBaseUrl, sessionId);

            psi.Environment["SIGNAL_BASE_URL"] = signalBaseUrl;
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
                    AppendLog(locale.HostExeFailedToStart);
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
                AppendLog(locale.statusStartException + ex.Message);
                SetStatus(locale.StatusFailed);
                SetRunningState(false);
            }
        }

        private void buttonAudioOn_Click(object sender, EventArgs e)
        {
            if (pSelector == null || pSelector.IsDisposed)
            {
                pSelector = new ProcessSelector();
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
            if (pId != "")
            {
                SendCommand(pId);
                pId = "";

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
            if (mCreator == null || mCreator.IsDisposed)
            {
                mCreator = new ModeCreator();
                mCreator.Left = this.Left + 200;
                mCreator.Top = this.Top + 200;
                mCreator.StartPosition = FormStartPosition.Manual;
                mCreator.Show();
            }
            else
            {
                mCreator.WindowState = FormWindowState.Normal;
                mCreator.Activate();
            }

            mCreator.Dispose();
            mCreator = null;
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
        public string DisplayName { get; set; } = "";
        public string Key { get; set; } = "";

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

        [JsonProperty("buttonAudioOnText")]
        public required string ButtonAudioOnText { get; set; }

        [JsonProperty("buttonAudioOffText")]
        public required string ButtonAudioOffText { get; set; }

        [JsonProperty("buttonAudioSystemText")]
        public required string ButtonAudioSystemText { get; set; }

        [JsonProperty("processExitError")]
        public required string ProcessExitError { get; set; }

        [JsonProperty("hostExeAlreadyStarted")]
        public required string HostExeAlreadyStarted {  get; set; }

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

        [JsonProperty("confim")]
        public required string Confirm { get; set; }

        [JsonProperty("error")]
        public required string Error { get; set; }
    }

    public class IssueHostTokenRequest
    {
        [JsonPropertyName("sessionId")]
        public string SessionId { get; set; } = "";
    }

    public class IssueHostTokenResponse
    {
        [JsonPropertyName("token")]
        public string Token { get; set; } = "";

        [JsonPropertyName("expiresIn")]
        public int ExpiresIn { get; set; }
    }
}