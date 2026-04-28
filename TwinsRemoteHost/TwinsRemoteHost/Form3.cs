using Newtonsoft.Json;
using System;
using System.IO;

namespace TwinsRemoteHost
{
    public partial class ModeCreator : Form
    {
        private readonly Locale locale;
        private CustomMode customMode = new();

        public ModeCreator(Locale locale)
        {
            this.locale = locale;
            InitializeComponent();
            InitializeUi();
            ApplyLanguage();
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
            balancedButton.Text = this.locale.BalancedMode;
            qualityButton.Text = this.locale.QualityMode;
            stableButton.Text = this.locale.StableMode;
            mobileButton.Text = this.locale.MobileMode;

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
            this.customMode.ModeName = modeNameTextBox.Text;
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
            this.customMode.EnableLookahead = enableLookAheadCheckBox.Checked;
            this.customMode.LookaheadDepth = lookaheadDepth;
        }

        private bool ValidateCustomMode()
        {
            string message = "";
            decimal width = 0;
            decimal height = 0;
            decimal fps = 0;
            decimal averageBitrate = 0;
            decimal maxBitrate = 0;
            decimal vbvBufferSize = 0;
            decimal vbvInitialDelay = 0;
            decimal gopLength = 0;
            decimal idrPeriod = 0;
            decimal maxRefFrames = 0;
            decimal lookaheadDepth = 0;

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
            else if (
                (!decimal.TryParse(resolutionWidthTextBox.Text, out width) &&
                width <= UInt32.MaxValue && width >= UInt32.MinValue
                )
                ||
                (!decimal.TryParse(resolutionHeightTextBox.Text, out height) &&
                height <= UInt32.MaxValue && height >= UInt32.MinValue
                )
                )
            {
                message += this.locale.AlertInvalidResolution + "\n";
            }

            // FPS
            if (fpsTextBox.Text == string.Empty)
            {
                message += this.locale.AlertNoFps + "\n";
            }
            else if (
                !decimal.TryParse(fpsTextBox.Text, out fps) &&
                fps <= UInt32.MaxValue && fps >= UInt32.MinValue
                )
            {
                message += this.locale.AlertInvalidFps + "\n";
            }

            // averageBitrate
            if (averageBitrateTextBox.Text == string.Empty)
            {
                message += this.locale.AlertNoAverageBitrate + "\n";
            }
            else if (
                !decimal.TryParse(averageBitrateTextBox.Text, out averageBitrate) &&
                averageBitrate <= UInt32.MaxValue && averageBitrate >= UInt32.MinValue
                )
            {
                message += this.locale.AlertInvalidAverageBitrate + "\n";
            }

            // maxBitrate
            if (maxBitrateTextBox.Text == string.Empty)
            {
                message += this.locale.AlertNoMaxBitrate + "\n";
            }
            else if (
                !decimal.TryParse(maxBitrateTextBox.Text, out maxBitrate) &&
                maxBitrate <= UInt32.MaxValue && maxBitrate >= UInt32.MinValue
                )
            {
                message += this.locale.AlertInvalidMaxBitrate + "\n";
            }

            // vbvBufferSize
            if (vbvBufferSizeTextBox.Text == string.Empty)
            {
                message += this.locale.AlertNoVbvBufferSize + "\n";
            }
            else if (
                !decimal.TryParse(vbvBufferSizeTextBox.Text, out vbvBufferSize) &&
                vbvBufferSize <= UInt32.MaxValue && vbvBufferSize >= UInt32.MinValue
                )
            {
                message += this.locale.AlertInvalidVbvBufferSize + "\n";
            }

            // gopLength
            if (gopLengthTextBox.Text == string.Empty)
            {
                message += this.locale.AlertNoGopLength + "\n";
            }
            else if (
                !decimal.TryParse(gopLengthTextBox.Text, out gopLength) &&
                gopLength <= UInt32.MaxValue && gopLength >= UInt32.MinValue
                )
            {
                message += this.locale.AlertInvalidGopLength + "\n";
            }

            // idrPeriod
            if (idrPeriodTextBox.Text == string.Empty)
            {
                message += this.locale.AlertNoIdrPeriod + "\n";
            }
            else if (
                !decimal.TryParse(idrPeriodTextBox.Text, out idrPeriod) &&
                idrPeriod <= UInt32.MaxValue && idrPeriod >= UInt32.MinValue
                )
            {
                message += this.locale.AlertInvalidIdrPeriod + "\n";
            }

            // maxRefFrames
            if (maxRefFramesTextBox.Text == string.Empty)
            {
                message += this.locale.AlertInvalidMaxRefFrames + "\n";
            }
            else if (
                !decimal.TryParse(maxRefFramesTextBox.Text, out maxRefFrames) &&
                maxRefFrames <= UInt32.MaxValue && maxRefFrames <= UInt32.MinValue
                )
            {
                message += this.locale.AlertInvalidMaxRefFrames + "\n";
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

            // lookAheadDepth
            if (lookAheadDepthTextBox.Text == string.Empty)
            {
                message += this.locale.AlertNoLookaheadDepth + "\n";
            }
            else if (
                !decimal.TryParse(lookAheadDepthTextBox.Text, out lookaheadDepth) &&
                lookaheadDepth <= UInt32.MaxValue && lookaheadDepth >= UInt32.MinValue
                )
            {
                message += this.locale.AlertInvalidLookaheadDepth + "\n";
            }

            // 入力に不正な値があった場合
            if (message != string.Empty)
            {
                MessageBox.Show(message, locale.Confirm,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            CreateCustomMode(
                decimal.ToUInt32(width),
                decimal.ToUInt32(height),
                decimal.ToUInt32(fps),
                decimal.ToUInt32(averageBitrate),
                decimal.ToUInt32(maxBitrate),
                decimal.ToUInt32(vbvInitialDelay),
                decimal.ToUInt32(vbvInitialDelay),
                decimal.ToUInt32(gopLength),
                decimal.ToUInt32(idrPeriod),
                decimal.ToUInt32(maxRefFrames),
                decimal.ToUInt32(lookaheadDepth)
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
    }

    [JsonObject("customModes")]
    class CustomMode
    {
        [JsonProperty("modeName")]
        public string ModeName { get; set; }

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
    }
}
