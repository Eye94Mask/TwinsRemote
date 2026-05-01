using Newtonsoft.Json;
using System;
using System.IO;

namespace TwinsRemoteHost
{
    public partial class ModeCreator : Form
    {
        private readonly Locale locale;
        private CustomMode customMode = new();

        // ====================
        // Presets
        // ====================
        // Casual Presets
        private readonly PresetMode balanced = new(
            1920, 1080, 60,
            12, 16, 12, 6,
            60, 60,
            true, false,
            3,
            "NV_ENC_PRESET_P4_GUID", "NV_ENC_TUNING_INFO_LOW_LATENCY",
            false, 0,
            true, true
        );
        private readonly PresetMode stable = new(
            1280, 720, 30,
            4, 5, 4, 2,
            60, 60,
            true, false,
            1,
            "NV_ENC_PRESET_P3_GUID", "NV_ENC_TUNING_INFO_LOW_LATENCY",
            false, 0,
            true, true
        );

        // Low Latency Presets
        private readonly PresetMode lowLatency = new(
            1920, 1080, 60,
            10, 12, 40, 20,
            999999, 999999,
            true, false,
            1,
            "NV_ENC_PRESET_P2_GUID", "NV_ENC_TUNING_INFO_LOW_LATENCY",
            false, 0,
            true, true
        );
        private readonly PresetMode ultraLowLatency = new(
            1920, 1080, 60,
            10, 12, 40, 20,
            999999, 999999,
            true, false,
            1,
            "NV_ENC_PRESET_P1_GUID", "NV_ENC_TUNING_INFO_ULTRA_LOW_LATENCY",
            false, 0,
            true, true
        );

        // Quality Presets
        private readonly PresetMode highFps = new(
            1920, 1080, 120,
            12, 16, 12, 6,
            60, 60,
            true, false,
            3,
            "NV_ENC_PRESET_P4_GUID", "NV_ENC_TUNING_INFO_LOW_LATENCY",
            false, 0,
            true, true
        );
        private readonly PresetMode fourK = new(
            3840, 2160, 30,
            12, 16, 12, 6,
            60, 60,
            true, false,
            3,
            "NV_ENC_PRESET_P4_GUID", "NV_ENC_TUNING_INFO_LOW_LATENCY",
            false, 0,
            true, true
        );

        // Reducing Network Load Presets
        private readonly PresetMode mobile = new(
            1280, 720, 30,
            0.25F, 3, 0.25F, 0.12F,
            30, 30,
            true, false,
            1,
            "NV_ENC_PRESET_P3_GUID", "NV_ENC_TUNING_INFO_LOW_LATENCY",
            false, 0,
            true, true
        );
        private readonly PresetMode ipv4 = new(
            960, 540, 30,
            0.14F, 0.18F, 0.14F, 0.07F,
            30, 30,
            true, false,
            1,
            "NV_ENC_PRESET_P2_GUID", "NV_ENC_TUNING_INFO_LOW_LATENCY",
            false, 0,
            true, true
        );
        private readonly PresetMode restrictedIpv4 = new(
            854, 480, 30,
            0.09F, 0.12F, 0.09F, 0.045F,
            30, 30,
            true, false,
            1,
            "NV_ENC_PRESET_P1_GUID", "NV_ENC_TUNING_INFO_LOW_LATENCY",
            false, 0,
            true, true
        );

        public ModeCreator(Locale locale)
        {
            this.locale = locale;
            InitializeComponent();
            InitializePreset();
            InitializeUi();
            ApplyLanguage();
        }

        private void InitializePreset()
        {
            resolutionWidthTextBox.Text = this.balanced.Width.ToString();
            resolutionHeightTextBox.Text = this.balanced.Height.ToString();
            fpsTextBox.Text = this.balanced.Fps.ToString();

            averageBitrateTextBox.Text = this.balanced.AverageBitrate.ToString();
            maxBitrateTextBox.Text = this.balanced.MaxBitrate.ToString();
            vbvBufferSizeTextBox.Text = this.balanced.VbvBufferSize.ToString();
            vbvInitialDelayTextBox.Text = this.balanced.VbvInitialDelay.ToString();

            gopLengthTextBox.Text = this.balanced.GopLength.ToString();
            idrPeriodTextBox.Text = this.balanced.IdrPeriod.ToString();

            repeatSpsPpsCheckBox.Checked = this.balanced.RepeatSpsPps;
            outputAudCheckBox.Checked = this.balanced.OutputAud;

            maxRefFramesTextBox.Text = this.balanced.MaxRefFrames.ToString();

            presetGuidComboBox.SelectedIndex = presetGuidComboBox.FindString(this.balanced.PresetGuid.ToString());
            tuningInfoComboBox.SelectedIndex = tuningInfoComboBox.FindString(this.balanced.TuningInfo.ToString());

            enableLookaheadCheckBox.Checked = this.balanced.EnableLookahead;
            lookaheadDepthTextBox.Text = this.balanced.LookaheadDepth.ToString();

            disableIadaptCheckBox.Checked = this.balanced.DisableIadapt;
            disableBadaptCheckBox.Checked = this.balanced.DisableBadapt;
        }

        private void InitializeUi()
        {
            var presetGuids = new[]
            {
                new { Name = "NV_ENC_PRESET_P1_GUID", Code = "NV_ENC_PRESET_P1_GUID" },
                new { Name = "NV_ENC_PRESET_P2_GUID", Code = "NV_ENC_PRESET_P2_GUID" },
                new { Name = "NV_ENC_PRESET_P3_GUID", Code = "NV_ENC_PRESET_P3_GUID" },
                new { Name = "NV_ENC_PRESET_P4_GUID", Code = "NV_ENC_PRESET_P4_GUID" },
                new { Name = "NV_ENC_PRESET_P5_GUID", Code = "NV_ENC_PRESET_P5_GUID" },
                new { Name = "NV_ENC_PRESET_P6_GUID", Code = "NV_ENC_PRESET_P6_GUID" },
                new { Name = "NV_ENC_PRESET_P7_GUID", Code = "NV_ENC_PRESET_P7_GUID" }
            };

            presetGuidComboBox.DisplayMember = "Name";
            presetGuidComboBox.ValueMember = "Code";
            presetGuidComboBox.DataSource = presetGuids;

            var tuningInfo = new[]
            {
                new { Name = "NV_ENC_TUNING_INFO_HIGH_QUALITY", Code = "NV_ENC_TUNING_INFO_HIGH_QUALITY" },
                new { Name = "NV_ENC_TUNING_INFO_LOW_LATENCY", Code = "NV_ENC_TUNING_INFO_LOW_LATENCY" },
                new { Name = "NV_ENC_TUNING_INFO_ULTRA_LOW_LATENCY", Code = "NV_ENC_TUNING_INFO_ULTRA_LOW_LATENCY" }
            };

            tuningInfoComboBox.DisplayMember = "Name";
            tuningInfoComboBox.ValueMember = "Code";
            tuningInfoComboBox.DataSource = tuningInfo;
        }

        private void ApplyLanguage()
        {
            modeNameLabel.Text = this.locale.ModeName;
            resolusionLabel.Text = this.locale.Resolution;
            presetModeLabel.Text = this.locale.Presets;
            casualPresetLabel.Text = this.locale.CasualPresets;
            lowLatencyLabel.Text = this.locale.LowLatencyPresets;
            qualityLabel.Text = this.locale.QualityPresets;
            reducingNetworkLoadLabel.Text = this.locale.ReducingNetworkLoadPresets;

            // ====================
            // Preset Buttons
            // ====================
            // CasualPresets
            balancedButton.Text = this.locale.BalancedMode;
            stableButton.Text = this.locale.StableMode;

            // Low Latency Presets
            lowLatencyButton.Text = this.locale.LowLatencyMode;
            ultraLowLatencyButton.Text = this.locale.UltraLowLatencyMode;

            // Quality Presets
            highFpsButton.Text = this.locale.HighFpsMode;
            fourKButton.Text = this.locale.FourKMode;

            // Reducing Network Load Presets
            mobileButton.Text = this.locale.MobileMode;
            ipv4Button.Text = this.locale.Ipv4Mode;
            restrictedIpv4Button.Text = this.locale.RestrictedIpv4Mode;

            // Detail Settings
            detailSettingLabel.Text = this.locale.ToggleDescriptionClose;
        }

        private bool ValidateModeName()
        {
            string[] InvalidCharactors = ["\\", "/", ":", "*", "?", "\"", "<", ">", "|"];

            foreach (string text in InvalidCharactors)
            {
                if (modeNameTextBox.Text.Contains(text)) { return false; }
            }

            return true;
        }

        private void CreateCustomMode(
            UInt32 width,
            UInt32 height,
            UInt32 fps,
            UInt32 averageBitrate,
            UInt32 maxBitrate,
            UInt32 vbvBufferSize,
            UInt32 vbvInitialDelay,
            UInt32 gopLength,
            UInt32 idrPeriod,
            UInt32 maxRefFrames,
            UInt32 lookaheadDepth
            )
        {
            this.customMode.Width = width;
            this.customMode.Height = height;
            this.customMode.Fps = fps;
            this.customMode.AverageBitrate = averageBitrate;
            this.customMode.MaxBitrate = maxBitrate;
            this.customMode.VbvBufferSize = vbvBufferSize;
            this.customMode.VbvInitialDelay = vbvInitialDelay;
            this.customMode.GopLength = gopLength;
            this.customMode.IdrPeriod = idrPeriod;
            this.customMode.RepeatSpsPps = repeatSpsPpsCheckBox.Checked;
            this.customMode.OutputAud = outputAudCheckBox.Checked;
            this.customMode.MaxRefFrames = maxRefFrames;
            this.customMode.ProfileGuid = "NV_ENC_H264_PROFILE_HIGH_GUID";  // 基本これ一択なので固定
            this.customMode.PresetGuid = presetGuidComboBox.SelectedValue.ToString();
            this.customMode.TuningInfo = tuningInfoComboBox.SelectedValue.ToString();
            this.customMode.EnableLookahead = enableLookaheadCheckBox.Checked;
            this.customMode.LookaheadDepth = lookaheadDepth;
            this.customMode.DisableIadapt = disableIadaptCheckBox.Checked;
            this.customMode.DisableBadapt = disableBadaptCheckBox.Checked;
        }

        private bool ValidateCustomMode()
        {
            string message = "";
            UInt32 width = 0;
            UInt32 height = 0;
            UInt32 fps = 0;
            UInt32 customAverageBitrate = 0;
            UInt32 customMaxBitrate = 0;
            UInt32 customVbvBufferSize = 0;
            UInt32 customVbvInitialDelay = 0;
            UInt32 gopLength = 0;
            UInt32 idrPeriod = 0;
            UInt32 maxRefFrames = 0;
            UInt32 lookaheadDepth = 0;

            // Custom Mode Name
            if (modeNameTextBox.Text == string.Empty)
            {
                message += this.locale.AlretNoModeName + "\n";
            }
            else if (!ValidateModeName())
            {
                message += this.locale.AlertInvalidModeName + "\n";
            }

            // Resolution
            if (
                resolutionWidthTextBox.Text == string.Empty ||
                resolutionHeightTextBox.Text == string.Empty
                )
            {
                message += this.locale.AlertNoResolution + "\n";
            }
            else
            {
                try
                {
                    width = UInt32.Parse(resolutionWidthTextBox.Text);
                    height = UInt32.Parse(resolutionHeightTextBox.Text);
                }
                catch (Exception)
                {
                    message += this.locale.AlertInvalidResolution + "\n";
                }
            }

            // FPS
            if (fpsTextBox.Text == string.Empty)
            {
                message += this.locale.AlertNoFps + "\n";
            }
            else
            {
                try
                {
                    fps = UInt32.Parse(fpsTextBox.Text);
                }
                catch (Exception)
                {
                    message += this.locale.AlertInvalidFps + "\n";
                }
            }

            // averageBitrate
            if (averageBitrateTextBox.Text == string.Empty)
            {
                message += this.locale.AlertNoAverageBitrate + "\n";
            }
            else
            {
                try
                {
                    float averageBitrate = float.Parse(averageBitrateTextBox.Text);
                    customAverageBitrate = (UInt32)(averageBitrate * 1000 * 1000);
                }
                catch (Exception)
                {
                    message += this.locale.AlertInvalidAverageBitrate + "\n";
                }
            }

            // maxBitrate
            if (maxBitrateTextBox.Text == string.Empty)
            {
                message += this.locale.AlertNoMaxBitrate + "\n";
            }
            else
            {
                try
                {
                    float maxBitrate = float.Parse(maxBitrateTextBox.Text);
                    customMaxBitrate = (UInt32)(maxBitrate * 1000 * 1000);
                }
                catch (Exception)
                {
                    message += this.locale.AlertInvalidMaxBitrate + "\n";
                }
            }

            // vbvBufferSize
            if (vbvBufferSizeTextBox.Text == string.Empty)
            {
                message += this.locale.AlertNoVbvBufferSize + "\n";
            }
            else
            {
                try
                {
                    float vbvBufferSize = float.Parse(vbvBufferSizeTextBox.Text);
                    customVbvBufferSize = (UInt32)(vbvBufferSize * 1000 * 1000);
                }
                catch (Exception)
                {
                    message += this.locale.AlertInvalidVbvBufferSize + "\n";
                }
            }

            if (vbvInitialDelayTextBox.Text == string.Empty)
            {
                message += this.locale.AlertNoVbvInitialDelay + "\n";
            }
            else
            {
                try
                {
                    float vbvInitialDelay = float.Parse(vbvInitialDelayTextBox.Text);
                    customVbvInitialDelay = (UInt32)(vbvInitialDelay * 1000 * 1000);
                }
                catch (Exception)
                {
                    message += this.locale.AlertInvalidVbvInitialDelay + "\n";
                }
            }

            // gopLength
            if (gopLengthTextBox.Text == string.Empty)
            {
                message += this.locale.AlertNoGopLength + "\n";
            }
            else
            {
                try
                {
                    gopLength = UInt32.Parse(gopLengthTextBox.Text);
                }
                catch (Exception)
                {
                    message += this.locale.AlertInvalidGopLength + "\n";
                }
            }

            // idrPeriod
            if (idrPeriodTextBox.Text == string.Empty)
            {
                message += this.locale.AlertNoIdrPeriod + "\n";
            }
            else
            {
                try
                {
                    idrPeriod = UInt32.Parse(idrPeriodTextBox.Text);
                }
                catch (Exception)
                {
                    message += this.locale.AlertInvalidIdrPeriod + "\n";
                }
            }

            // maxRefFrames
            if (maxRefFramesTextBox.Text == string.Empty)
            {
                message += this.locale.AlertInvalidMaxRefFrames + "\n";
            }
            else
            {
                try
                {
                    maxRefFrames = UInt32.Parse(maxRefFramesTextBox.Text);
                }
                catch (Exception)
                {
                    message += this.locale.AlertInvalidMaxRefFrames + "\n";
                }
            }

            // presetGuid
            if (presetGuidComboBox.SelectedItem == null)
            {
                message += this.locale.AlertInvalidPresetGuid + "\n";
            }

            // tuningInfo
            if (tuningInfoComboBox.SelectedItem == null)
            {
                message += this.locale.AlertInvalidTuningInfo + "\n";
            }

            // lookaheadDepth
            if (lookaheadDepthTextBox.Text == string.Empty)
            {
                message += this.locale.AlertNoLookaheadDepth + "\n";
            }
            else
            {
                try
                {
                    lookaheadDepth = UInt32.Parse(lookaheadDepthTextBox.Text);
                }
                catch (Exception)
                {
                    message += this.locale.AlertInvalidLookaheadDepth + "\n";
                }
            }

            // 入力に不正な値があった場合
            if (message != string.Empty)
            {
                MessageBox.Show(message, locale.Confirm,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            CreateCustomMode(
                width,
                height,
                fps,
                customAverageBitrate,
                customMaxBitrate,
                customVbvBufferSize,
                customVbvInitialDelay,
                gopLength,
                idrPeriod,
                maxRefFrames,
                lookaheadDepth
            );
            return true;
        }

        private void detailSettingLabel_Click(object sender, EventArgs e)
        {
            if (detailSettingLabel.Text == this.locale.ToggleDescriptionClose)
            {
                detailSettingLabel.Text = this.locale.ToggleDescriptionOpen;
                detailSettingsContainerFlowLayoutPanel.Visible = true;
            }
            else
            {
                detailSettingLabel.Text = this.locale.ToggleDescriptionClose;
                detailSettingsContainerFlowLayoutPanel.Visible = false;
            }
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            if (!ValidateCustomMode()) { return; }

            var customMode = JsonConvert.SerializeObject(this.customMode);
            string customJsonName = modeNameTextBox.Text + ".json";

            string customsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exes", "customs");
            if (!Directory.Exists(customsDirectory))
            {
                DirectoryInfo di = new DirectoryInfo(customsDirectory);
                di.Create();
            }

            string customJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exes", "customs", customJsonName);
            if (!File.Exists(customJsonPath))
            {
                using FileStream fs = File.Create(customJsonPath);
            }

            using StreamWriter sw = new(customJsonPath, false, System.Text.Encoding.UTF8);
            sw.Write(customMode);

            this.Close();
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void balancedButton_Click(object sender, EventArgs e)
        {
            resolutionWidthTextBox.Text = this.balanced.Width.ToString();
            resolutionHeightTextBox.Text = this.balanced.Height.ToString();
            fpsTextBox.Text = this.balanced.Fps.ToString();

            averageBitrateTextBox.Text = this.balanced.AverageBitrate.ToString();
            maxBitrateTextBox.Text = this.balanced.MaxBitrate.ToString();
            vbvBufferSizeTextBox.Text = this.balanced.VbvBufferSize.ToString();
            vbvInitialDelayTextBox.Text = this.balanced.VbvInitialDelay.ToString();

            gopLengthTextBox.Text = this.balanced.GopLength.ToString();
            idrPeriodTextBox.Text = this.balanced.IdrPeriod.ToString();

            repeatSpsPpsCheckBox.Checked = this.balanced.RepeatSpsPps;
            outputAudCheckBox.Checked = this.balanced.OutputAud;

            maxRefFramesTextBox.Text = this.balanced.MaxRefFrames.ToString();

            presetGuidComboBox.SelectedIndex = presetGuidComboBox.FindString(this.balanced.PresetGuid.ToString());
            tuningInfoComboBox.SelectedIndex = tuningInfoComboBox.FindString(this.balanced.TuningInfo.ToString());

            enableLookaheadCheckBox.Checked = this.balanced.EnableLookahead;
            lookaheadDepthTextBox.Text = this.balanced.LookaheadDepth.ToString();

            disableIadaptCheckBox.Checked = this.balanced.DisableIadapt;
            disableBadaptCheckBox.Checked = this.balanced.DisableBadapt;
        }

        private void stableButton_Click(object sender, EventArgs e)
        {
            resolutionWidthTextBox.Text = this.stable.Width.ToString();
            resolutionHeightTextBox.Text = this.stable.Height.ToString();
            fpsTextBox.Text = this.stable.Fps.ToString();

            averageBitrateTextBox.Text = this.stable.AverageBitrate.ToString();
            maxBitrateTextBox.Text = this.stable.MaxBitrate.ToString();
            vbvBufferSizeTextBox.Text = this.stable.VbvBufferSize.ToString();
            vbvInitialDelayTextBox.Text = this.stable.VbvInitialDelay.ToString();

            gopLengthTextBox.Text = this.stable.GopLength.ToString();
            idrPeriodTextBox.Text = this.stable.IdrPeriod.ToString();

            repeatSpsPpsCheckBox.Checked = this.stable.RepeatSpsPps;
            outputAudCheckBox.Checked = this.stable.OutputAud;

            maxRefFramesTextBox.Text = this.stable.MaxRefFrames.ToString();

            presetGuidComboBox.SelectedIndex = presetGuidComboBox.FindString(this.stable.PresetGuid.ToString());
            tuningInfoComboBox.SelectedIndex = tuningInfoComboBox.FindString(this.stable.TuningInfo.ToString());

            enableLookaheadCheckBox.Checked = this.stable.EnableLookahead;
            lookaheadDepthTextBox.Text = this.stable.LookaheadDepth.ToString();

            disableIadaptCheckBox.Checked = this.stable.DisableIadapt;
            disableBadaptCheckBox.Checked = this.stable.DisableBadapt;
        }

        private void lowLatencyButton_Click(object sender, EventArgs e)
        {
            resolutionWidthTextBox.Text = this.lowLatency.Width.ToString();
            resolutionHeightTextBox.Text = this.lowLatency.Height.ToString();
            fpsTextBox.Text = this.lowLatency.Fps.ToString();

            averageBitrateTextBox.Text = this.lowLatency.AverageBitrate.ToString();
            maxBitrateTextBox.Text = this.lowLatency.MaxBitrate.ToString();
            vbvBufferSizeTextBox.Text = this.lowLatency.VbvBufferSize.ToString();
            vbvInitialDelayTextBox.Text = this.lowLatency.VbvInitialDelay.ToString();

            gopLengthTextBox.Text = this.lowLatency.GopLength.ToString();
            idrPeriodTextBox.Text = this.lowLatency.IdrPeriod.ToString();

            repeatSpsPpsCheckBox.Checked = this.lowLatency.RepeatSpsPps;
            outputAudCheckBox.Checked = this.lowLatency.OutputAud;

            maxRefFramesTextBox.Text = this.lowLatency.MaxRefFrames.ToString();

            presetGuidComboBox.SelectedIndex = presetGuidComboBox.FindString(this.lowLatency.PresetGuid.ToString());
            tuningInfoComboBox.SelectedIndex = tuningInfoComboBox.FindString(this.lowLatency.TuningInfo.ToString());

            enableLookaheadCheckBox.Checked = this.lowLatency.EnableLookahead;
            lookaheadDepthTextBox.Text = this.lowLatency.LookaheadDepth.ToString();

            disableIadaptCheckBox.Checked = this.lowLatency.DisableIadapt;
            disableBadaptCheckBox.Checked = this.lowLatency.DisableBadapt;
        }

        private void ultraLowLatencyButton_Click(object sender, EventArgs e)
        {
            resolutionWidthTextBox.Text = this.ultraLowLatency.Width.ToString();
            resolutionHeightTextBox.Text = this.ultraLowLatency.Height.ToString();
            fpsTextBox.Text = this.ultraLowLatency.Fps.ToString();

            averageBitrateTextBox.Text = this.ultraLowLatency.AverageBitrate.ToString();
            maxBitrateTextBox.Text = this.ultraLowLatency.MaxBitrate.ToString();
            vbvBufferSizeTextBox.Text = this.ultraLowLatency.VbvBufferSize.ToString();
            vbvInitialDelayTextBox.Text = this.ultraLowLatency.VbvInitialDelay.ToString();

            gopLengthTextBox.Text = this.ultraLowLatency.GopLength.ToString();
            idrPeriodTextBox.Text = this.ultraLowLatency.IdrPeriod.ToString();

            repeatSpsPpsCheckBox.Checked = this.ultraLowLatency.RepeatSpsPps;
            outputAudCheckBox.Checked = this.ultraLowLatency.OutputAud;

            maxRefFramesTextBox.Text = this.ultraLowLatency.MaxRefFrames.ToString();

            presetGuidComboBox.SelectedIndex = presetGuidComboBox.FindString(this.ultraLowLatency.PresetGuid.ToString());
            tuningInfoComboBox.SelectedIndex = tuningInfoComboBox.FindString(this.ultraLowLatency.TuningInfo.ToString());

            enableLookaheadCheckBox.Checked = this.ultraLowLatency.EnableLookahead;
            lookaheadDepthTextBox.Text = this.ultraLowLatency.LookaheadDepth.ToString();

            disableIadaptCheckBox.Checked = this.ultraLowLatency.DisableIadapt;
            disableBadaptCheckBox.Checked = this.ultraLowLatency.DisableBadapt;
        }

        private void highFpsButton_Click(object sender, EventArgs e)
        {
            resolutionWidthTextBox.Text = this.highFps.Width.ToString();
            resolutionHeightTextBox.Text = this.highFps.Height.ToString();
            fpsTextBox.Text = this.highFps.Fps.ToString();

            averageBitrateTextBox.Text = this.highFps.AverageBitrate.ToString();
            maxBitrateTextBox.Text = this.highFps.MaxBitrate.ToString();
            vbvBufferSizeTextBox.Text = this.highFps.VbvBufferSize.ToString();
            vbvInitialDelayTextBox.Text = this.highFps.VbvInitialDelay.ToString();

            gopLengthTextBox.Text = this.highFps.GopLength.ToString();
            idrPeriodTextBox.Text = this.highFps.IdrPeriod.ToString();

            repeatSpsPpsCheckBox.Checked = this.highFps.RepeatSpsPps;
            outputAudCheckBox.Checked = this.highFps.OutputAud;

            maxRefFramesTextBox.Text = this.highFps.MaxRefFrames.ToString();

            presetGuidComboBox.SelectedIndex = presetGuidComboBox.FindString(this.highFps.PresetGuid.ToString());
            tuningInfoComboBox.SelectedIndex = tuningInfoComboBox.FindString(this.highFps.TuningInfo.ToString());

            enableLookaheadCheckBox.Checked = this.highFps.EnableLookahead;
            lookaheadDepthTextBox.Text = this.highFps.LookaheadDepth.ToString();

            disableIadaptCheckBox.Checked = this.highFps.DisableIadapt;
            disableBadaptCheckBox.Checked = this.highFps.DisableBadapt;
        }

        private void fourKButton_Click(object sender, EventArgs e)
        {
            resolutionWidthTextBox.Text = this.fourK.Width.ToString();
            resolutionHeightTextBox.Text = this.fourK.Height.ToString();
            fpsTextBox.Text = this.fourK.Fps.ToString();

            averageBitrateTextBox.Text = this.fourK.AverageBitrate.ToString();
            maxBitrateTextBox.Text = this.fourK.MaxBitrate.ToString();
            vbvBufferSizeTextBox.Text = this.fourK.VbvBufferSize.ToString();
            vbvInitialDelayTextBox.Text = this.fourK.VbvInitialDelay.ToString();

            gopLengthTextBox.Text = this.fourK.GopLength.ToString();
            idrPeriodTextBox.Text = this.fourK.IdrPeriod.ToString();

            repeatSpsPpsCheckBox.Checked = this.fourK.RepeatSpsPps;
            outputAudCheckBox.Checked = this.fourK.OutputAud;

            maxRefFramesTextBox.Text = this.fourK.MaxRefFrames.ToString();

            presetGuidComboBox.SelectedIndex = presetGuidComboBox.FindString(this.fourK.PresetGuid.ToString());
            tuningInfoComboBox.SelectedIndex = tuningInfoComboBox.FindString(this.fourK.TuningInfo.ToString());

            enableLookaheadCheckBox.Checked = this.fourK.EnableLookahead;
            lookaheadDepthTextBox.Text = this.fourK.LookaheadDepth.ToString();

            disableIadaptCheckBox.Checked = this.fourK.DisableIadapt;
            disableBadaptCheckBox.Checked = this.fourK.DisableBadapt;
        }

        private void mobileButton_Click(object sender, EventArgs e)
        {
            resolutionWidthTextBox.Text = this.mobile.Width.ToString();
            resolutionHeightTextBox.Text = this.mobile.Height.ToString();
            fpsTextBox.Text = this.mobile.Fps.ToString();

            averageBitrateTextBox.Text = this.mobile.AverageBitrate.ToString();
            maxBitrateTextBox.Text = this.mobile.MaxBitrate.ToString();
            vbvBufferSizeTextBox.Text = this.mobile.VbvBufferSize.ToString();
            vbvInitialDelayTextBox.Text = this.mobile.VbvInitialDelay.ToString();

            gopLengthTextBox.Text = this.mobile.GopLength.ToString();
            idrPeriodTextBox.Text = this.mobile.IdrPeriod.ToString();

            repeatSpsPpsCheckBox.Checked = this.mobile.RepeatSpsPps;
            outputAudCheckBox.Checked = this.mobile.OutputAud;

            maxRefFramesTextBox.Text = this.mobile.MaxRefFrames.ToString();

            presetGuidComboBox.SelectedIndex = presetGuidComboBox.FindString(this.mobile.PresetGuid.ToString());
            tuningInfoComboBox.SelectedIndex = tuningInfoComboBox.FindString(this.mobile.TuningInfo.ToString());

            enableLookaheadCheckBox.Checked = this.mobile.EnableLookahead;
            lookaheadDepthTextBox.Text = this.mobile.LookaheadDepth.ToString();

            disableIadaptCheckBox.Checked = this.mobile.DisableIadapt;
            disableBadaptCheckBox.Checked = this.mobile.DisableBadapt;
        }

        private void ipv4Button_Click(object sender, EventArgs e)
        {
            resolutionWidthTextBox.Text = this.ipv4.Width.ToString();
            resolutionHeightTextBox.Text = this.ipv4.Height.ToString();
            fpsTextBox.Text = this.ipv4.Fps.ToString();

            averageBitrateTextBox.Text = this.ipv4.AverageBitrate.ToString();
            maxBitrateTextBox.Text = this.ipv4.MaxBitrate.ToString();
            vbvBufferSizeTextBox.Text = this.ipv4.VbvBufferSize.ToString();
            vbvInitialDelayTextBox.Text = this.ipv4.VbvInitialDelay.ToString();

            gopLengthTextBox.Text = this.ipv4.GopLength.ToString();
            idrPeriodTextBox.Text = this.ipv4.IdrPeriod.ToString();

            repeatSpsPpsCheckBox.Checked = this.ipv4.RepeatSpsPps;
            outputAudCheckBox.Checked = this.ipv4.OutputAud;

            maxRefFramesTextBox.Text = this.ipv4.MaxRefFrames.ToString();

            presetGuidComboBox.SelectedIndex = presetGuidComboBox.FindString(this.ipv4.PresetGuid.ToString());
            tuningInfoComboBox.SelectedIndex = tuningInfoComboBox.FindString(this.ipv4.TuningInfo.ToString());

            enableLookaheadCheckBox.Checked = this.ipv4.EnableLookahead;
            lookaheadDepthTextBox.Text = this.ipv4.LookaheadDepth.ToString();

            disableIadaptCheckBox.Checked = this.ipv4.DisableIadapt;
            disableBadaptCheckBox.Checked = this.ipv4.DisableBadapt;
        }

        private void restrictedIpv4Button_Click(object sender, EventArgs e)
        {
            resolutionWidthTextBox.Text = this.restrictedIpv4.Width.ToString();
            resolutionHeightTextBox.Text = this.restrictedIpv4.Height.ToString();
            fpsTextBox.Text = this.restrictedIpv4.Fps.ToString();

            averageBitrateTextBox.Text = this.restrictedIpv4.AverageBitrate.ToString();
            maxBitrateTextBox.Text = this.restrictedIpv4.MaxBitrate.ToString();
            vbvBufferSizeTextBox.Text = this.restrictedIpv4.VbvBufferSize.ToString();
            vbvInitialDelayTextBox.Text = this.restrictedIpv4.VbvInitialDelay.ToString();

            gopLengthTextBox.Text = this.restrictedIpv4.GopLength.ToString();
            idrPeriodTextBox.Text = this.restrictedIpv4.IdrPeriod.ToString();

            repeatSpsPpsCheckBox.Checked = this.restrictedIpv4.RepeatSpsPps;
            outputAudCheckBox.Checked = this.restrictedIpv4.OutputAud;

            maxRefFramesTextBox.Text = this.restrictedIpv4.MaxRefFrames.ToString();

            presetGuidComboBox.SelectedIndex = presetGuidComboBox.FindString(this.restrictedIpv4.PresetGuid.ToString());
            tuningInfoComboBox.SelectedIndex = tuningInfoComboBox.FindString(this.restrictedIpv4.TuningInfo.ToString());

            enableLookaheadCheckBox.Checked = this.restrictedIpv4.EnableLookahead;
            lookaheadDepthTextBox.Text = this.restrictedIpv4.LookaheadDepth.ToString();

            disableIadaptCheckBox.Checked = this.restrictedIpv4.DisableIadapt;
            disableBadaptCheckBox.Checked = this.restrictedIpv4.DisableBadapt;
        }
    }

    [JsonObject("customModes")]
    class CustomMode
    {
        [JsonProperty("width")]
        public UInt32 Width { get; set; }

        [JsonProperty("height")]
        public UInt32 Height { get; set; }

        [JsonProperty("fps")]
        public UInt32 Fps { get; set; }

        [JsonProperty("averageBitrate")]
        public UInt32 AverageBitrate { get; set; }

        [JsonProperty("maxBitrate")]
        public UInt32 MaxBitrate { get; set; }

        [JsonProperty("vbvBufferSize")]
        public UInt32 VbvBufferSize { get; set; }

        [JsonProperty("vbvInitialDelay")]
        public UInt32 VbvInitialDelay { get; set; }

        [JsonProperty("gopLength")]
        public UInt32 GopLength { get; set; }

        [JsonProperty("idrPeriod")]
        public UInt32 IdrPeriod { get; set; }

        [JsonProperty("repeatSpsPps")]
        public bool RepeatSpsPps { get; set; }

        [JsonProperty("outputAud")]
        public bool OutputAud { get; set; }

        [JsonProperty("maxRefFrames")]
        public UInt32 MaxRefFrames { get; set; }

        [JsonProperty("profileGuid")]
        public string ProfileGuid { get; set; }

        [JsonProperty("presetGuid")]
        public string PresetGuid { get; set; }

        [JsonProperty("tuningInfo")]
        public string TuningInfo { get; set; }

        [JsonProperty("enableLookahead")]
        public bool EnableLookahead { get; set; }

        [JsonProperty("lookaheadDepth")]
        public UInt32 LookaheadDepth { get; set; }

        [JsonProperty("disableIadapt")]
        public bool DisableIadapt { get; set; }

        [JsonProperty("disableBadapt")]
        public bool DisableBadapt { get; set; }
    }

    class PresetMode(
        UInt32 width, UInt32 height, UInt32 fps,
        float averageBitrate, float maxBitrate, float vbvBufferSize, float vbvInitialDelay,
        UInt32 gopLength, UInt32 idrPeriod,
        bool repeatSpsPps, bool outputAud,
        UInt32 maxRefFrames,
        string presetGuid, string tuningInfo,
        bool enableLookahead, UInt32 lookaheadDepth,
        bool disableIadapt, bool disableBadapt
        )
    {
        public UInt32 Width { get; set; } = width;

        public UInt32 Height { get; set; } = height;

        public UInt32 Fps { get; set; } = fps;

        public float AverageBitrate { get; set; } = averageBitrate;

        public float MaxBitrate { get; set; } = maxBitrate;

        public float VbvBufferSize { get; set; } = vbvBufferSize;

        public float VbvInitialDelay { get; set; } = vbvInitialDelay;

        public UInt32 GopLength { get; set; } = gopLength;

        public UInt32 IdrPeriod { get; set; } = idrPeriod;

        public bool RepeatSpsPps { get; set; } = repeatSpsPps;

        public bool OutputAud { get; set; } = outputAud;

        public UInt32 MaxRefFrames { get; set; } = maxRefFrames;

        public string PresetGuid { get; set; } = presetGuid;

        public string TuningInfo { get; set; } = tuningInfo;

        public bool EnableLookahead { get; set; } = enableLookahead;

        public UInt32 LookaheadDepth { get; set; } = lookaheadDepth;

        public bool DisableIadapt { get; set; } = disableIadapt;

        public bool DisableBadapt { get; set; } = disableBadapt;
    }
}
