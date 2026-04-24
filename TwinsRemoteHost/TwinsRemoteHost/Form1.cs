using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http.Json;
using System.Text;
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
        private LocaleData locale;

        public Host()
        {
            InitializeComponent();
            InitializeUi();

            FormClosing += Host_FormClosing;
        }

        private void InitializeUi()
        {
            comboBoxMode.Items.Clear();
            comboBoxMode.Items.Add(new VideoPresetItem { DisplayName = "Balanced", Key = "balanced" });
            comboBoxMode.Items.Add(new VideoPresetItem { DisplayName = "Quality", Key = "quality" });
            comboBoxMode.Items.Add(new VideoPresetItem { DisplayName = "Stable", Key = "stable" });
            comboBoxMode.Items.Add(new VideoPresetItem { DisplayName = "Mobile", Key = "mobile" });
            comboBoxMode.SelectedIndex = 0;

            labelStatusValue.Text = "停止中";
            SetRunningState(false);
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
                    try
                    {
                        AppendLog("プロセス終了失敗: " + ex.Message);
                    }
                    catch
                    {

                    }
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

        private async void buttonStart_Click(object sender, EventArgs e)
        {
            if (_hostProcess != null && !_hostProcess.HasExited)
            {
                AppendLog("すでに起動中です");
                return;
            }

            string mode = (comboBoxMode.SelectedItem?.ToString() ?? "Balanced").ToLower();
            string sessionId = textBoxSessionId.Text.Trim();

            if (string.IsNullOrWhiteSpace(sessionId))
            {
                MessageBox.Show("セッションIDを入力してください", "確認",
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
                MessageBox.Show($"twins_remote_host.exe が見つかりません\n{hostExePath}", "エラー",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!File.Exists(nvEncPath))
            {
                MessageBox.Show($"NvEnc.exe が見つかりません\n{nvEncPath}", "エラー",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!File.Exists(processAudioCapturePath))
            {
                MessageBox.Show($"ProcessAudioCapture.exe が見つかりません\n{processAudioCapturePath}", "エラー",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!File.Exists(systemMixCapture))
            {
                MessageBox.Show($"SystemMixCapture.exe が見つかりません\n{systemMixCapture}", "エラー",
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
                    AppendLog("host.exe の起動に失敗しました");
                    return;
                }

                AppendLog($"twins_remote_host.exe を起動しました: mode={mode}, session={sessionId}");
                SetStatus("起動中");
                SetRunningState(true);

                _ = Task.Run(() => ReadOutputLoop(_hostProcess));
                _ = Task.Run(() => ReadErrorLoop(_hostProcess));
            }
            catch (Exception ex)
            {
                AppendLog("起動例外: " + ex.Message);
                SetStatus("起動失敗");
                SetRunningState(false);
            }
        }

        private void HostProcess_Exited(object? sender, EventArgs e)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() => HostProcess_Exited(sender, e)));
                return;
            }

            AppendLog("twins_remote_host.exe が終了しました");
            SetStatus("停止中");
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
                    AppendLog("標準出力読み取りエラー: " + ex.Message);
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
                    AppendLog("標準エラー読み取りエラー: " + ex.Message);
                }));
            }
        }

        private void HandleOutputLine(string line)
        {
            AppendLog("[OUT] " + line);

            if (line.StartsWith("[STATE] "))
            {
                string state = line.Substring(8).Trim();

                switch (state)
                {
                    case "HOST_STARTING":
                        SetStatus("起動中");
                        break;
                    case "HOST_READY":
                        SetStatus("待機中");
                        break;
                    case "AUDIO_ON":
                        SetStatus("音声ON");
                        break;
                    case "AUDIO_OFF":
                        SetStatus("音声OFF");
                        break;
                    case "EXITING":
                        SetStatus("終了中");
                        break;
                }
            }
        }

        private void SendCommand(string command)
        {
            if (_hostProcess == null || _hostProcess.HasExited)
            {
                AppendLog("プロセスが起動していません。");
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
                AppendLog("コマンド送信失敗: " + ex.Message);
            }
        }

        private void buttonAudioOn_Click(object sender, EventArgs e)
        {
            SendCommand("audio on");
        }

        private void buttonAudioOff_Click(object sender, EventArgs e)
        {
            SendCommand("audio_stop");
        }

        private void buttonAudioSystem_Click(object sender, EventArgs e)
        {
            SendCommand("system");
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
    }

    public class VideoPresetItem
    {
        public string DisplayName { get; set; } = "";
        public string Key { get; set; } = "";

        public override string ToString() => DisplayName;
    }

    class LocaleData
    {
        [JsonObject("Japanese")]
        public sealed class Japanese
        {
            [JsonProperty("labelModeText")]
            public string labelModeText { get; }

            [JsonProperty("labelSessionIdText")]
            public string labelSessionIdText { get; }

            [JsonProperty("labelStatusTitleText")]
            public string labelStatusTitleText { get; }

            [JsonProperty("labelStatusValueText")]
            public string labelStatusValueText { get; }

            [JsonProperty("buttonStartText")]
            public string buttonStartText { get; }

            [JsonProperty("buttonAudioOnText")]
            public string buttonAudioOnText { get; }

            [JsonProperty("buttonAudioOffText")]
            public string buttonAudioOffText { get; }

            [JsonProperty("buttonAudioStopText")]
            public string buttonAudioStopText { get; }
        }

        [JsonObject("English")]
        public sealed class English
        {
            [JsonProperty("labelModeText")]
            public string labelModeText { get; }

            [JsonProperty("labelSessionIdText")]
            public string labelSessionIdText { get; }

            [JsonProperty("labelStatusTitleText")]
            public string labelStatusTitleText { get; }

            [JsonProperty("labelStatusValueText")]
            public string labelStatusValueText { get; }

            [JsonProperty("buttonStartText")]
            public string buttonStartText { get; }

            [JsonProperty("buttonAudioOnText")]
            public string buttonAudioOnText { get; }

            [JsonProperty("buttonAudioOffText")]
            public string buttonAudioOffText { get; }

            [JsonProperty("buttonAudioStopText")]
            public string buttonAudioStopText { get; }
        }
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