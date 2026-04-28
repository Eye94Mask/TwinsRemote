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
            ApplyLanguage();
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

        private void detailSettingLabel_Click(object sender, EventArgs e)
        {
            if (detailSettingLabel.Text == this.locale.ToggleDescriptionClose)
            {
                detailSettingLabel.Text = this.locale.ToggleDescriptionOpen;
                detailSettingsFlowLayoutPanel1.Visible = true;
                detailSettingsFlowLayoutPanel2.Visible = true;
            }
            else
            {
                detailSettingLabel.Text = this.locale.ToggleDescriptionClose;
                detailSettingsFlowLayoutPanel1.Visible = false;
                detailSettingsFlowLayoutPanel2.Visible = false;
            }
        }
    }
}
