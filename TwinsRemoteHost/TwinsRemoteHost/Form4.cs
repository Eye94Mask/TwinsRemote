using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace TwinsRemoteHost
{
    public partial class ModeEditor : Form
    {
        private readonly Locale locale;
        private CustomMode editMode;
        private string currentMode;

        public ModeEditor(Locale locale)
        {
            this.locale = locale;
            InitializeComponent();
            InitializeUi();
            InitializeParameter();
            ApplyLanguage();
        }

        private void ModeEditor_Load(object sender, EventArgs e)
        {

        }

        private CustomMode? GetSelectedModeParameter()
        {
            if (customModeComboBox.SelectedValue == null) { return null; }
            string editJson = customModeComboBox.SelectedValue.ToString() + ".json";
            string customDirectory = @"./exes/customs/";

            if (File.Exists(customDirectory + editJson) is false)
            {
                MessageBox.Show(customDirectory + editJson, this.locale.Error,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
            try
            {
                using var fs = new FileStream(customDirectory + editJson, FileMode.Open);
                using var sr = new StreamReader(fs);
                return JsonConvert.DeserializeObject<CustomMode>(sr.ReadToEnd());
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void InitializeUi()
        {
            ResetCustomModes();

            var presetGuids = ModeCreator.GetPresetGuids();
            presetGuidComboBox.DataSource = null;
            presetGuidComboBox.Items.Clear();
            presetGuidComboBox.DisplayMember = "Name";
            presetGuidComboBox.ValueMember = "Value";
            presetGuidComboBox.DataSource = presetGuids;

            var tuningInfo = ModeCreator.GetTuningInfo();
            tuningInfoComboBox.DataSource = null;
            tuningInfoComboBox.Items.Clear();
            tuningInfoComboBox.DisplayMember = "Name";
            tuningInfoComboBox.ValueMember = "Value";
            tuningInfoComboBox.DataSource = tuningInfo;
        }

        private void ResetCustomModes()
        {
            List<string> customNames = Host.GetCustomModeList();

            var customModes = customNames
                .Select(name => new
                {
                    Name = name,
                    Value = name
                })
                .ToList();

            customModeComboBox.DataSource = null;
            customModeComboBox.DisplayMember = "Name";
            customModeComboBox.ValueMember = "Value";
            customModeComboBox.DataSource = customModes;
        }

        private void ApplyLanguage()
        {
            selectModeLabel.Text = this.locale.ModeName;
            modeNameLabel.Text = this.locale.ModeName;
            resolutionLabel.Text = this.locale.Resolution;
            selectModeLabel.Text = this.locale.SelectModeLabel;

            // Advanced Settings
            detailSettingLabel.Text = this.locale.ToggleDescriptionOpen;
        }

        private static bool DeleteMode(string path)
        {
            if (File.Exists(path) is false) { return false; }
            File.Delete(path);

            return true;
        }

        private bool ConflictsWithOtherModes(string updateModeName)
        {
            List<string> customNames = Host.GetCustomModeList();
            foreach (string customName in customNames)
            {
                // 更新するモードと同じならOK
                if (customName == this.currentMode) { continue; }

                // ほかのカスタムモードと被るのはNG
                if (customName == updateModeName) { return true; }
            }

            return false;
        }

        private static string ByteToMbyte(float b)
        {
            return (b / 1000 / 1000).ToString();
        }

        private void SetModeValiables()
        {
            modeNameTextBox.Text = customModeComboBox.Text;
            resolutionWidthTextBox.Text = this.editMode.Width.ToString();
            resolutionHeightTextBox.Text = this.editMode.Height.ToString();
            fpsTextBox.Text = this.editMode.Fps.ToString();

            // Advanced Settings
            int presetGuidIndex = presetGuidComboBox.FindStringExact(this.editMode.PresetGuid.ToString());
            int tuningInfoIndex = tuningInfoComboBox.FindStringExact(this.editMode.TuningInfo.ToString());
            averageBitrateTextBox.Text = ByteToMbyte(this.editMode.AverageBitrate);
            maxBitrateTextBox.Text = ByteToMbyte(this.editMode.MaxBitrate);
            vbvBufferSizeTextBox.Text = ByteToMbyte(this.editMode.VbvBufferSize);
            vbvInitialDelayTextBox.Text = ByteToMbyte(this.editMode.VbvInitialDelay);
            gopLengthTextBox.Text = this.editMode.GopLength.ToString();
            idrPeriodTextBox.Text = this.editMode.IdrPeriod.ToString();
            repeatSpsPpsCheckBox.Checked = this.editMode.RepeatSpsPps;
            outputAudCheckBox.Checked = this.editMode.OutputAud;
            maxRefFramesTextBox.Text = this.editMode.MaxRefFrames.ToString();
            presetGuidComboBox.SelectedIndex = presetGuidIndex;
            tuningInfoComboBox.SelectedIndex = tuningInfoIndex;
            enableLookaheadCheckBox.Checked = this.editMode.EnableLookahead;
            lookaheadDepthTextBox.Text = this.editMode.LookaheadDepth.ToString();
            disableIadaptCheckBox.Checked = this.editMode.DisableIadapt;
            disableBadaptCheckBox.Checked = this.editMode.DisableBadapt;

            this.currentMode = customModeComboBox.SelectedValue.ToString();
        }

        private void InitializeParameter()
        {
            customModeComboBox.SelectedIndex = 0;
            CustomMode? selectedMode = GetSelectedModeParameter();
            if (selectedMode == null)
            {
                return;
            }

            this.editMode = selectedMode;
            SetModeValiables();
        }

        private void customModeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            CustomMode? selectedMode = GetSelectedModeParameter();
            if (selectedMode == null)
            {
                InitializeParameter();
                return;
            }

            this.editMode = selectedMode;
            SetModeValiables();
        }

        private void saveButton_Click(object sender, EventArgs e)
        {
            List<UInt32>? validatedValues = ModeCreator.ValidateCustomMode(
                modeNameTextBox.Text,
                resolutionWidthTextBox.Text,
                resolutionHeightTextBox.Text,
                fpsTextBox.Text,
                averageBitrateTextBox.Text,
                maxBitrateTextBox.Text,
                vbvBufferSizeTextBox.Text,
                vbvInitialDelayTextBox.Text,
                gopLengthTextBox.Text,
                idrPeriodTextBox.Text,
                maxRefFramesTextBox.Text,
                presetGuidComboBox.SelectedItem,
                tuningInfoComboBox.SelectedItem,
                lookaheadDepthTextBox.Text,
                this.locale
            );

            if (validatedValues == null) { return; }

            UInt32 width = validatedValues[0];
            UInt32 height = validatedValues[1];
            UInt32 fps = validatedValues[2];
            UInt32 averageBitrate = validatedValues[3];
            UInt32 maxBitrate = validatedValues[4];
            UInt32 vbvBufferSize = validatedValues[5];
            UInt32 vbvInitialDelay = validatedValues[6];
            UInt32 gopLength = validatedValues[7];
            UInt32 idrPeriod = validatedValues[8];
            UInt32 maxRefFrames = validatedValues[9];
            UInt32 lookaheadDepth = validatedValues[10];

            this.editMode = ModeCreator.CreateCustomMode(
                width, height, fps,
                averageBitrate, maxBitrate, vbvBufferSize, vbvInitialDelay,
                gopLength, idrPeriod,
                repeatSpsPpsCheckBox.Checked, outputAudCheckBox.Checked,
                maxRefFrames,
                presetGuidComboBox.SelectedValue.ToString(),
                tuningInfoComboBox.SelectedValue.ToString(),
                enableLookaheadCheckBox.Checked, lookaheadDepth,
                disableIadaptCheckBox.Checked, disableBadaptCheckBox.Checked
            );

            var customMode = JsonConvert.SerializeObject(this.editMode);
            string customJsonName = modeNameTextBox.Text + ".json";

            string customDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exes", "customs");
            string customJsonPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "exes", "customs", customJsonName);

            // モード名がほかのカスタムモードと被っている場合
            if (ConflictsWithOtherModes(modeNameTextBox.Text))
            {
                MessageBox.Show(this.locale.CustomModeNameConflictsWithOthers, this.locale.Confirm,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // モード名を変更している場合があるため古いカスタムモードは削除
            string oldModePath = @"./exes/customs/" + this.currentMode + ".json";
            if (!DeleteMode(oldModePath))
            {
                MessageBox.Show(this.locale.FailedToUpdateCustom, this.locale.Error,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (!Directory.Exists(customDirectory))
            {
                DirectoryInfo di = new(customDirectory);
                di.Create();
            }

            using FileStream fs = File.Create(customJsonPath);
            fs.Close();
            using StreamWriter sw = new(customJsonPath, false, System.Text.Encoding.UTF8);
            sw.Write(customMode);
            sw.Close();

            MessageBox.Show(this.locale.CustomModeUpdated + modeNameTextBox.Text, this.locale.Confirm,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);

            InitializeUi();
        }

        private void deleteButton_Click(object sender, EventArgs e)
        {
            string modePath = @"./exes/customs/" + customModeComboBox.SelectedValue + ".json";
            if (!DeleteMode(modePath))
            {
                MessageBox.Show(this.locale.FailedToDeleteCustom, this.locale.Error,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            InitializeUi();
        }

        private void closeButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void resolutionLabel_MouseHover(object sender, EventArgs e)
        {
            technicalTermToolTip.SetToolTip(resolutionLabel, this.locale.ResolutionDescription);
        }

        private void fpsLabel_MouseHover(object sender, EventArgs e)
        {
            technicalTermToolTip.SetToolTip(fpsLabel, this.locale.FpsDescription);
        }

        private void averageBitrateLabel_MouseHover(object sender, EventArgs e)
        {
            technicalTermToolTip.SetToolTip(averageBitrateLabel, this.locale.AverageBitrateDescription);
        }

        private void maxBitrateLabel_MouseHover(object sender, EventArgs e)
        {
            technicalTermToolTip.SetToolTip(maxBitrateLabel, this.locale.MaxBitrateDescription);
        }

        private void vbvBufferSizeLabel_MouseHover(object sender, EventArgs e)
        {
            technicalTermToolTip.SetToolTip(vbvBufferSizeLabel, this.locale.VbvBufferSizeDescription);
        }

        private void vbvInitialDelayLabel_MouseHover(object sender, EventArgs e)
        {
            technicalTermToolTip.SetToolTip(vbvInitialDelayLabel, this.locale.VbvInitialDelayDescription);
        }

        private void gopLengthLabel_MouseHover(object sender, EventArgs e)
        {
            technicalTermToolTip.SetToolTip(gopLengthLabel, this.locale.GopLengthDescription);
        }

        private void idrPeriodLabel_MouseHover(object sender, EventArgs e)
        {
            technicalTermToolTip.SetToolTip(idrPeriodLabel, this.locale.IdrPeriodDescription);
        }

        private void repeatSpsPpsLabel_MouseHover(object sender, EventArgs e)
        {
            technicalTermToolTip.SetToolTip(repeatSpsPpsLabel, this.locale.RepeatSpsPpsDescription);
        }

        private void outputAudLabel_MouseHover(object sender, EventArgs e)
        {
            technicalTermToolTip.SetToolTip(outputAudLabel, this.locale.OutputAudDescription);
        }

        private void maxRefFramesLabel_MouseHover(object sender, EventArgs e)
        {
            technicalTermToolTip.SetToolTip(maxRefFramesLabel, this.locale.MaxRefFramesDescription);
        }

        private void presetGuidLabel_MouseHover(object sender, EventArgs e)
        {
            technicalTermToolTip.SetToolTip(presetGuidLabel, this.locale.PresetGuidDescription);
        }

        private void tuningInfoLabel_MouseHover(object sender, EventArgs e)
        {
            technicalTermToolTip.SetToolTip(tuningInfoLabel, this.locale.TuningInfoDescription);
        }

        private void enableLookaheadLabel_MouseHover(object sender, EventArgs e)
        {
            technicalTermToolTip.SetToolTip(enableLookaheadLabel, this.locale.EnableLookaheadDescription);
        }

        private void lookaheadDepthLabel_MouseHover(object sender, EventArgs e)
        {
            technicalTermToolTip.SetToolTip(lookaheadDepthLabel, this.locale.LookaheadDepthDescription);
        }

        private void disableIadaptLabel_MouseHover(object sender, EventArgs e)
        {
            technicalTermToolTip.SetToolTip(disableIadaptLabel, this.locale.DisableIadaptDescription);
        }

        private void disableBadaptLabel_MouseHover(object sender, EventArgs e)
        {
            technicalTermToolTip.SetToolTip(disableBadaptLabel, this.locale.DisableBadaptDescription);
        }

        // ToolTipが表示されないバグの回避策
        private void common_MouseLeave(object sender, EventArgs e)
        {
            technicalTermToolTip.Active = false;
            technicalTermToolTip.Active = true;
        }
    }
}