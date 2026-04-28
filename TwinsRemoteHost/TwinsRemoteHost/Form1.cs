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
            languageComboBox.SelectedIndex = languageComboBox.FindStringExact(language);
            ApplyLanguage();

            statusValueLabel.Text = locale.StatusStopped;
            SetRunningState(false);
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
            comboBoxMode.Enabled = !running;
            textBoxSessionId.Enabled = !running;
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
                        statusValueLabel.Text = locale.StatusConnected;
                    }
                    if (line.Contains("ICE_DISCONNECTED"))
                    {
                        this.status = Status.Disconnected;
                        statusValueLabel.Text = locale.StatusDisconnected;
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
            string lang = languageComboBox.Text.ToString() ?? "Japanese";

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
            AppendLog("Test: " + languageComboBox.SelectedItem.ToString());
            if (languageComboBox.SelectedItem == null) { return; }
            string? language = languageComboBox.SelectedItem.ToString();
            if (language == null) { return; }

            string[] words = language.Split(' ');
            if (words.Length != 8 && words[4] != "Code")
            {
                AppendLog(this.locale.InvalidLanguage);
                return;
            }

            string code = words[6];
            if (!isValidLanguage(code))
            {
                AppendLog(this.locale.InvalidLanguage);
                return;
            }

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

            modeLabel.Text = locale.LabelModeText;
            sessionIdLabel.Text = locale.LabelSessionIdText;
            statusTitleLabel.Text = locale.LabelStatusTitleText;
            statusValueLabel.Text = GetCurrentStatus();
            connectButton.Text = locale.ButtonStartText;
            audioOnButton.Text = locale.ButtonAudioOnText;
            audioOffButton.Text = locale.ButtonAudioOffText;
            audioSystemButton.Text = locale.ButtonAudioSystemText;
            createCustomModeButton.Text = locale.CreateCustomMode;
        }

        private async void buttonStart_Click(object sender, EventArgs e)
        {
            if (_hostProcess != null && !_hostProcess.HasExited)
            {
                AppendLog(this.locale.HostExeAlreadyStarted);
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
                mCreator = new ModeCreator(this.locale);
                mCreator.Left = this.Left + 200;
                mCreator.Top = this.Top + 200;
                mCreator.StartPosition = FormStartPosition.Manual;
                mCreator.ShowDialog();
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

        [JsonProperty("balancedMode")]
        public required string BalancedMode { get; set; }

        [JsonProperty("qualityMode")]
        public required string QualityMode { get; set; }

        [JsonProperty("stableMode")]
        public required string StableMode { get; set; }

        [JsonProperty("mobileMode")]
        public required string MobileMode { get; set; }

        [JsonProperty("resolutionDescription")]
        public required string ResolutionDescription { get; set; }

        [JsonProperty("fpsDescription")]
        public required string FpsDescription { get; set; }

        [JsonProperty("balancedModeDescription")]
        public required string BalancedModeDescription { get; set; }

        [JsonProperty("qualityModeDescription")]
        public required string QualityModeDescription { get; set; }

        [JsonProperty("stableModeDescription")]
        public required string StableModeDescription { get; set; }

        [JsonProperty("mobileModeDescription")]
        public required string MobileModeDescription { get; set; }

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

        [JsonProperty("enableLookAheadDescription")]
        public required string EnableLookAheadDescription { get; set; }

        [JsonProperty("lookAheadDepthDescription")]
        public required string LookAheadDepthDescription { get; set; }

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

        [JsonProperty("alertNoLookAheadDepth")]
        public required string AlertNoLookAheadDepth { get; set; }

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

        [JsonProperty("alertInvalidLookAheadDepth")]
        public required string AlertInvalidLookAheadDepth { get; set; }

        [JsonProperty("customModeSaved")]
        public required string CustomModeSaved { get; set; }
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