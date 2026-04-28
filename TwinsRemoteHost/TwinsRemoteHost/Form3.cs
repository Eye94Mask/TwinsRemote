using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace TwinsRemoteHost
{
    public partial class ModeCreator : Form
    {
        private Locale locale;
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

        private bool ValidateCustomMode()
        {
            string message = "";

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
                !decimal.TryParse(resolutionWidthTextBox.Text, out decimal width) ||
                !decimal.TryParse(resolutionHeightTextBox.Text, out decimal height)
                )
            {
                message += this.locale.AlertInvalidResolution + "\n";
            }

            // FPS
            if (fpsTextBox.Text == string.Empty)
            {
                message += this.locale.AlertNoFps + "\n";
            }
            else if (!decimal.TryParse(fpsTextBox.Text, out decimal fps))
            {
                message += this.locale.AlertInvalidFps + "\n";
            }

            // averageBitrate
            if (averageBitrateTextBox.Text == string.Empty)
            {
                message += this.locale.AlertNoAverageBitrate + "\n";
            }
            else if (!decimal.TryParse(averageBitrateTextBox.Text, out decimal averageBitrate))
            {
                message += this.locale.AlertInvalidAverageBitrate + "\n";
            }

            // maxBitrate
            if (maxBitrateTextBox.Text == string.Empty)
            {
                message += this.locale.AlertNoMaxBitrate + "\n";
            }
            else if (!decimal.TryParse(maxBitrateTextBox.Text, out decimal maxBitrate))
            {
                message += this.locale.AlertInvalidMaxBitrate + "\n";
            }

            // vbvBufferSize
            if (vbvBufferSizeTextBox.Text == string.Empty)
            {
                message += this.locale.AlertNoVbvBufferSize + "\n";
            }
            else if (!decimal.TryParse(vbvBufferSizeTextBox.Text, out decimal vbvBufferSize))
            {
                message += this.locale.AlertInvalidVbvBufferSize + "\n";
            }

            // gopLength
            if (gopLengthTextBox.Text == string.Empty)
            {
                message += this.locale.AlertNoGopLength + "\n";
            }
            else if (!decimal.TryParse(gopLengthTextBox.Text, out decimal gopLength))
            {
                message += this.locale.AlertInvalidGopLength + "\n";
            }

            // idrPeriod
            if (idrPeriodTextBox.Text == string.Empty)
            {
                message += this.locale.AlertNoIdrPeriod + "\n";
            }
            else if (!decimal.TryParse(idrPeriodTextBox.Text, out decimal idrPeriod))
            {
                message += this.locale.AlertInvalidIdrPeriod + "\n";
            }

            // maxRefFrames
            if (maxRefFramesTextBox.Text == string.Empty)
            {
                message += this.locale.AlertInvalidMaxRefFrames + "\n";
            }
            else if (!decimal.TryParse(maxRefFramesTextBox.Text, out decimal maxRefFrames))
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
                message += this.locale.AlertNoLookAheadDepth + "\n";
            }
            else if (!decimal.TryParse(lookAheadDepthTextBox.Text, out decimal lookAheadDepth))
            {
                message += this.locale.AlertInvalidLookAheadDepth + "\n";
            }

            // 入力に不正な値があった場合
            if (message != string.Empty)
            {
                MessageBox.Show(message, locale.Confirm,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

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

        private void Save_Click(object sender, EventArgs e)
        {
            if (ValidateCustomMode())
            {

            }
        }
    }

    class CustomMode
    {

    }
}
