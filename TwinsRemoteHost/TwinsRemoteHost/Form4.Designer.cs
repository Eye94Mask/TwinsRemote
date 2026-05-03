namespace TwinsRemoteHost
{
    partial class ModeEditor
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            detailSettingsContainerFlowLayoutPanel = new FlowLayoutPanel();
            detailSettingsFlowLayoutPanel1 = new FlowLayoutPanel();
            averageBitrateLabel = new Label();
            averageBitrateTextBox = new TextBox();
            averageBitrateUnitLabel = new Label();
            maxBitrateLabel = new Label();
            maxBitrateTextBox = new TextBox();
            maxBitrateUnitLabel = new Label();
            vbvBufferSizeLabel = new Label();
            vbvBufferSizeTextBox = new TextBox();
            vbvBufferSizeUnitLabel = new Label();
            vbvInitialDelayLabel = new Label();
            vbvInitialDelayTextBox = new TextBox();
            vbvInitialDelayUnitLabel = new Label();
            gopLengthLabel = new Label();
            gopLengthTextBox = new TextBox();
            idrPeriodLabel = new Label();
            idrPeriodTextBox = new TextBox();
            repeatSpsPpsLabel = new Label();
            repeatSpsPpsCheckBox = new CheckBox();
            outputAudLabel = new Label();
            outputAudCheckBox = new CheckBox();
            detailSettingsFlowLayoutPanel2 = new FlowLayoutPanel();
            maxRefFramesLabel = new Label();
            maxRefFramesTextBox = new TextBox();
            presetGuidLabel = new Label();
            presetGuidComboBox = new ComboBox();
            tuningInfoLabel = new Label();
            tuningInfoComboBox = new ComboBox();
            enableLookaheadLabel = new Label();
            enableLookaheadCheckBox = new CheckBox();
            lookaheadDepthLabel = new Label();
            lookaheadDepthTextBox = new TextBox();
            disableIadaptLabel = new Label();
            disableIadaptCheckBox = new CheckBox();
            disableBadaptLabel = new Label();
            disableBadaptCheckBox = new CheckBox();
            resolutionHeightTextBox = new TextBox();
            resolutionXLabel = new Label();
            fpsTextBox = new TextBox();
            resolutionWidthTextBox = new TextBox();
            closeButton = new Button();
            saveButton = new Button();
            detailSettingLabel = new Label();
            selectModeLabel = new Label();
            fpsLabel = new Label();
            resolutionLabel = new Label();
            customModeComboBox = new ComboBox();
            modeNameLabel = new Label();
            modeNameTextBox = new TextBox();
            deleteButton = new Button();
            technicalTermToolTip = new ToolTip(components);
            detailSettingsContainerFlowLayoutPanel.SuspendLayout();
            detailSettingsFlowLayoutPanel1.SuspendLayout();
            detailSettingsFlowLayoutPanel2.SuspendLayout();
            SuspendLayout();
            // 
            // detailSettingsContainerFlowLayoutPanel
            // 
            detailSettingsContainerFlowLayoutPanel.BorderStyle = BorderStyle.FixedSingle;
            detailSettingsContainerFlowLayoutPanel.Controls.Add(detailSettingsFlowLayoutPanel1);
            detailSettingsContainerFlowLayoutPanel.Controls.Add(detailSettingsFlowLayoutPanel2);
            detailSettingsContainerFlowLayoutPanel.Location = new Point(17, 691);
            detailSettingsContainerFlowLayoutPanel.Name = "detailSettingsContainerFlowLayoutPanel";
            detailSettingsContainerFlowLayoutPanel.Size = new Size(1234, 493);
            detailSettingsContainerFlowLayoutPanel.TabIndex = 65;
            // 
            // detailSettingsFlowLayoutPanel1
            // 
            detailSettingsFlowLayoutPanel1.Controls.Add(averageBitrateLabel);
            detailSettingsFlowLayoutPanel1.Controls.Add(averageBitrateTextBox);
            detailSettingsFlowLayoutPanel1.Controls.Add(averageBitrateUnitLabel);
            detailSettingsFlowLayoutPanel1.Controls.Add(maxBitrateLabel);
            detailSettingsFlowLayoutPanel1.Controls.Add(maxBitrateTextBox);
            detailSettingsFlowLayoutPanel1.Controls.Add(maxBitrateUnitLabel);
            detailSettingsFlowLayoutPanel1.Controls.Add(vbvBufferSizeLabel);
            detailSettingsFlowLayoutPanel1.Controls.Add(vbvBufferSizeTextBox);
            detailSettingsFlowLayoutPanel1.Controls.Add(vbvBufferSizeUnitLabel);
            detailSettingsFlowLayoutPanel1.Controls.Add(vbvInitialDelayLabel);
            detailSettingsFlowLayoutPanel1.Controls.Add(vbvInitialDelayTextBox);
            detailSettingsFlowLayoutPanel1.Controls.Add(vbvInitialDelayUnitLabel);
            detailSettingsFlowLayoutPanel1.Controls.Add(gopLengthLabel);
            detailSettingsFlowLayoutPanel1.Controls.Add(gopLengthTextBox);
            detailSettingsFlowLayoutPanel1.Controls.Add(idrPeriodLabel);
            detailSettingsFlowLayoutPanel1.Controls.Add(idrPeriodTextBox);
            detailSettingsFlowLayoutPanel1.Controls.Add(repeatSpsPpsLabel);
            detailSettingsFlowLayoutPanel1.Controls.Add(repeatSpsPpsCheckBox);
            detailSettingsFlowLayoutPanel1.Controls.Add(outputAudLabel);
            detailSettingsFlowLayoutPanel1.Controls.Add(outputAudCheckBox);
            detailSettingsFlowLayoutPanel1.Location = new Point(3, 3);
            detailSettingsFlowLayoutPanel1.Name = "detailSettingsFlowLayoutPanel1";
            detailSettingsFlowLayoutPanel1.Size = new Size(542, 471);
            detailSettingsFlowLayoutPanel1.TabIndex = 16;
            // 
            // averageBitrateLabel
            // 
            averageBitrateLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            averageBitrateLabel.Location = new Point(3, 0);
            averageBitrateLabel.Name = "averageBitrateLabel";
            averageBitrateLabel.Size = new Size(240, 52);
            averageBitrateLabel.TabIndex = 12;
            averageBitrateLabel.Text = "averageBitrate";
            averageBitrateLabel.TextAlign = ContentAlignment.MiddleLeft;
            averageBitrateLabel.MouseLeave += common_MouseLeave;
            averageBitrateLabel.MouseHover += averageBitrateLabel_MouseHover;
            // 
            // averageBitrateTextBox
            // 
            averageBitrateTextBox.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            averageBitrateTextBox.Location = new Point(249, 3);
            averageBitrateTextBox.Name = "averageBitrateTextBox";
            averageBitrateTextBox.Size = new Size(148, 49);
            averageBitrateTextBox.TabIndex = 20;
            // 
            // averageBitrateUnitLabel
            // 
            averageBitrateUnitLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            averageBitrateUnitLabel.Location = new Point(403, 0);
            averageBitrateUnitLabel.Name = "averageBitrateUnitLabel";
            averageBitrateUnitLabel.Size = new Size(120, 52);
            averageBitrateUnitLabel.TabIndex = 14;
            averageBitrateUnitLabel.Text = "Mbyte";
            averageBitrateUnitLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // maxBitrateLabel
            // 
            maxBitrateLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            maxBitrateLabel.Location = new Point(3, 55);
            maxBitrateLabel.Name = "maxBitrateLabel";
            maxBitrateLabel.Size = new Size(240, 52);
            maxBitrateLabel.TabIndex = 15;
            maxBitrateLabel.Text = "maxBitrate";
            maxBitrateLabel.TextAlign = ContentAlignment.MiddleLeft;
            maxBitrateLabel.MouseLeave += common_MouseLeave;
            maxBitrateLabel.MouseHover += maxBitrateLabel_MouseHover;
            // 
            // maxBitrateTextBox
            // 
            maxBitrateTextBox.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            maxBitrateTextBox.Location = new Point(249, 58);
            maxBitrateTextBox.Name = "maxBitrateTextBox";
            maxBitrateTextBox.Size = new Size(148, 49);
            maxBitrateTextBox.TabIndex = 21;
            // 
            // maxBitrateUnitLabel
            // 
            maxBitrateUnitLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            maxBitrateUnitLabel.Location = new Point(403, 55);
            maxBitrateUnitLabel.Name = "maxBitrateUnitLabel";
            maxBitrateUnitLabel.Size = new Size(120, 52);
            maxBitrateUnitLabel.TabIndex = 17;
            maxBitrateUnitLabel.Text = "Mbyte";
            maxBitrateUnitLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // vbvBufferSizeLabel
            // 
            vbvBufferSizeLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            vbvBufferSizeLabel.Location = new Point(3, 110);
            vbvBufferSizeLabel.Name = "vbvBufferSizeLabel";
            vbvBufferSizeLabel.Size = new Size(240, 52);
            vbvBufferSizeLabel.TabIndex = 18;
            vbvBufferSizeLabel.Text = "vbvBufferSize";
            vbvBufferSizeLabel.TextAlign = ContentAlignment.MiddleLeft;
            vbvBufferSizeLabel.MouseLeave += common_MouseLeave;
            vbvBufferSizeLabel.MouseHover += vbvBufferSizeLabel_MouseHover;
            // 
            // vbvBufferSizeTextBox
            // 
            vbvBufferSizeTextBox.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            vbvBufferSizeTextBox.Location = new Point(249, 113);
            vbvBufferSizeTextBox.Name = "vbvBufferSizeTextBox";
            vbvBufferSizeTextBox.Size = new Size(148, 49);
            vbvBufferSizeTextBox.TabIndex = 22;
            // 
            // vbvBufferSizeUnitLabel
            // 
            vbvBufferSizeUnitLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            vbvBufferSizeUnitLabel.Location = new Point(403, 110);
            vbvBufferSizeUnitLabel.Name = "vbvBufferSizeUnitLabel";
            vbvBufferSizeUnitLabel.Size = new Size(120, 52);
            vbvBufferSizeUnitLabel.TabIndex = 20;
            vbvBufferSizeUnitLabel.Text = "Mbyte";
            vbvBufferSizeUnitLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // vbvInitialDelayLabel
            // 
            vbvInitialDelayLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            vbvInitialDelayLabel.Location = new Point(3, 165);
            vbvInitialDelayLabel.Name = "vbvInitialDelayLabel";
            vbvInitialDelayLabel.Size = new Size(240, 52);
            vbvInitialDelayLabel.TabIndex = 21;
            vbvInitialDelayLabel.Text = "vbvInitialDelay";
            vbvInitialDelayLabel.TextAlign = ContentAlignment.MiddleLeft;
            vbvInitialDelayLabel.MouseLeave += common_MouseLeave;
            vbvInitialDelayLabel.MouseHover += vbvInitialDelayLabel_MouseHover;
            // 
            // vbvInitialDelayTextBox
            // 
            vbvInitialDelayTextBox.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            vbvInitialDelayTextBox.Location = new Point(249, 168);
            vbvInitialDelayTextBox.Name = "vbvInitialDelayTextBox";
            vbvInitialDelayTextBox.Size = new Size(148, 49);
            vbvInitialDelayTextBox.TabIndex = 23;
            // 
            // vbvInitialDelayUnitLabel
            // 
            vbvInitialDelayUnitLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            vbvInitialDelayUnitLabel.Location = new Point(403, 165);
            vbvInitialDelayUnitLabel.Name = "vbvInitialDelayUnitLabel";
            vbvInitialDelayUnitLabel.Size = new Size(120, 52);
            vbvInitialDelayUnitLabel.TabIndex = 23;
            vbvInitialDelayUnitLabel.Text = "Mbyte";
            vbvInitialDelayUnitLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // gopLengthLabel
            // 
            gopLengthLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            gopLengthLabel.Location = new Point(3, 220);
            gopLengthLabel.Name = "gopLengthLabel";
            gopLengthLabel.Size = new Size(240, 52);
            gopLengthLabel.TabIndex = 24;
            gopLengthLabel.Text = "gopLength";
            gopLengthLabel.TextAlign = ContentAlignment.MiddleLeft;
            gopLengthLabel.MouseLeave += common_MouseLeave;
            gopLengthLabel.MouseHover += gopLengthLabel_MouseHover;
            // 
            // gopLengthTextBox
            // 
            gopLengthTextBox.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            gopLengthTextBox.Location = new Point(249, 223);
            gopLengthTextBox.Name = "gopLengthTextBox";
            gopLengthTextBox.Size = new Size(148, 49);
            gopLengthTextBox.TabIndex = 24;
            // 
            // idrPeriodLabel
            // 
            idrPeriodLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            idrPeriodLabel.Location = new Point(3, 275);
            idrPeriodLabel.Name = "idrPeriodLabel";
            idrPeriodLabel.Size = new Size(240, 52);
            idrPeriodLabel.TabIndex = 26;
            idrPeriodLabel.Text = "idrPeriod";
            idrPeriodLabel.TextAlign = ContentAlignment.MiddleLeft;
            idrPeriodLabel.MouseLeave += common_MouseLeave;
            idrPeriodLabel.MouseHover += idrPeriodLabel_MouseHover;
            // 
            // idrPeriodTextBox
            // 
            idrPeriodTextBox.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            idrPeriodTextBox.Location = new Point(249, 278);
            idrPeriodTextBox.Name = "idrPeriodTextBox";
            idrPeriodTextBox.Size = new Size(148, 49);
            idrPeriodTextBox.TabIndex = 25;
            // 
            // repeatSpsPpsLabel
            // 
            repeatSpsPpsLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            repeatSpsPpsLabel.Location = new Point(3, 330);
            repeatSpsPpsLabel.Name = "repeatSpsPpsLabel";
            repeatSpsPpsLabel.Size = new Size(240, 52);
            repeatSpsPpsLabel.TabIndex = 28;
            repeatSpsPpsLabel.Text = "repeatSpsPps";
            repeatSpsPpsLabel.TextAlign = ContentAlignment.MiddleLeft;
            repeatSpsPpsLabel.MouseLeave += common_MouseLeave;
            repeatSpsPpsLabel.MouseHover += repeatSpsPpsLabel_MouseHover;
            // 
            // repeatSpsPpsCheckBox
            // 
            repeatSpsPpsCheckBox.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            repeatSpsPpsCheckBox.Location = new Point(249, 333);
            repeatSpsPpsCheckBox.Name = "repeatSpsPpsCheckBox";
            repeatSpsPpsCheckBox.Size = new Size(182, 44);
            repeatSpsPpsCheckBox.TabIndex = 26;
            repeatSpsPpsCheckBox.Text = " ";
            repeatSpsPpsCheckBox.UseVisualStyleBackColor = true;
            // 
            // outputAudLabel
            // 
            outputAudLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            outputAudLabel.Location = new Point(3, 382);
            outputAudLabel.Name = "outputAudLabel";
            outputAudLabel.Size = new Size(240, 52);
            outputAudLabel.TabIndex = 30;
            outputAudLabel.Text = "outputAud";
            outputAudLabel.TextAlign = ContentAlignment.MiddleLeft;
            outputAudLabel.MouseLeave += common_MouseLeave;
            outputAudLabel.MouseHover += outputAudLabel_MouseHover;
            // 
            // outputAudCheckBox
            // 
            outputAudCheckBox.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            outputAudCheckBox.Location = new Point(249, 385);
            outputAudCheckBox.Name = "outputAudCheckBox";
            outputAudCheckBox.Size = new Size(182, 44);
            outputAudCheckBox.TabIndex = 27;
            outputAudCheckBox.Text = " ";
            outputAudCheckBox.UseVisualStyleBackColor = true;
            // 
            // detailSettingsFlowLayoutPanel2
            // 
            detailSettingsFlowLayoutPanel2.Controls.Add(maxRefFramesLabel);
            detailSettingsFlowLayoutPanel2.Controls.Add(maxRefFramesTextBox);
            detailSettingsFlowLayoutPanel2.Controls.Add(presetGuidLabel);
            detailSettingsFlowLayoutPanel2.Controls.Add(presetGuidComboBox);
            detailSettingsFlowLayoutPanel2.Controls.Add(tuningInfoLabel);
            detailSettingsFlowLayoutPanel2.Controls.Add(tuningInfoComboBox);
            detailSettingsFlowLayoutPanel2.Controls.Add(enableLookaheadLabel);
            detailSettingsFlowLayoutPanel2.Controls.Add(enableLookaheadCheckBox);
            detailSettingsFlowLayoutPanel2.Controls.Add(lookaheadDepthLabel);
            detailSettingsFlowLayoutPanel2.Controls.Add(lookaheadDepthTextBox);
            detailSettingsFlowLayoutPanel2.Controls.Add(disableIadaptLabel);
            detailSettingsFlowLayoutPanel2.Controls.Add(disableIadaptCheckBox);
            detailSettingsFlowLayoutPanel2.Controls.Add(disableBadaptLabel);
            detailSettingsFlowLayoutPanel2.Controls.Add(disableBadaptCheckBox);
            detailSettingsFlowLayoutPanel2.Location = new Point(551, 3);
            detailSettingsFlowLayoutPanel2.Name = "detailSettingsFlowLayoutPanel2";
            detailSettingsFlowLayoutPanel2.Size = new Size(666, 471);
            detailSettingsFlowLayoutPanel2.TabIndex = 32;
            // 
            // maxRefFramesLabel
            // 
            maxRefFramesLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            maxRefFramesLabel.Location = new Point(3, 0);
            maxRefFramesLabel.Name = "maxRefFramesLabel";
            maxRefFramesLabel.Size = new Size(258, 52);
            maxRefFramesLabel.TabIndex = 12;
            maxRefFramesLabel.Text = "maxRefFrames";
            maxRefFramesLabel.TextAlign = ContentAlignment.MiddleLeft;
            maxRefFramesLabel.MouseLeave += common_MouseLeave;
            maxRefFramesLabel.MouseHover += maxRefFramesLabel_MouseHover;
            // 
            // maxRefFramesTextBox
            // 
            maxRefFramesTextBox.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            maxRefFramesTextBox.Location = new Point(267, 3);
            maxRefFramesTextBox.Name = "maxRefFramesTextBox";
            maxRefFramesTextBox.Size = new Size(182, 49);
            maxRefFramesTextBox.TabIndex = 28;
            // 
            // presetGuidLabel
            // 
            presetGuidLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            presetGuidLabel.Location = new Point(3, 55);
            presetGuidLabel.Name = "presetGuidLabel";
            presetGuidLabel.Size = new Size(258, 52);
            presetGuidLabel.TabIndex = 18;
            presetGuidLabel.Text = "presetGuid";
            presetGuidLabel.TextAlign = ContentAlignment.MiddleLeft;
            presetGuidLabel.MouseLeave += common_MouseLeave;
            presetGuidLabel.MouseHover += presetGuidLabel_MouseHover;
            // 
            // presetGuidComboBox
            // 
            presetGuidComboBox.Font = new Font("Yu Gothic UI", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            presetGuidComboBox.FormattingEnabled = true;
            presetGuidComboBox.Location = new Point(3, 110);
            presetGuidComboBox.Name = "presetGuidComboBox";
            presetGuidComboBox.Size = new Size(653, 46);
            presetGuidComboBox.TabIndex = 29;
            // 
            // tuningInfoLabel
            // 
            tuningInfoLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            tuningInfoLabel.Location = new Point(3, 159);
            tuningInfoLabel.Name = "tuningInfoLabel";
            tuningInfoLabel.Size = new Size(258, 52);
            tuningInfoLabel.TabIndex = 21;
            tuningInfoLabel.Text = "tuningInfo";
            tuningInfoLabel.TextAlign = ContentAlignment.MiddleLeft;
            tuningInfoLabel.MouseLeave += common_MouseLeave;
            tuningInfoLabel.MouseHover += tuningInfoLabel_MouseHover;
            // 
            // tuningInfoComboBox
            // 
            tuningInfoComboBox.Font = new Font("Yu Gothic UI", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            tuningInfoComboBox.FormattingEnabled = true;
            tuningInfoComboBox.Location = new Point(3, 214);
            tuningInfoComboBox.Name = "tuningInfoComboBox";
            tuningInfoComboBox.Size = new Size(653, 46);
            tuningInfoComboBox.TabIndex = 30;
            // 
            // enableLookaheadLabel
            // 
            enableLookaheadLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            enableLookaheadLabel.Location = new Point(3, 263);
            enableLookaheadLabel.Name = "enableLookaheadLabel";
            enableLookaheadLabel.Size = new Size(258, 52);
            enableLookaheadLabel.TabIndex = 24;
            enableLookaheadLabel.Text = "enableLookahead";
            enableLookaheadLabel.TextAlign = ContentAlignment.MiddleLeft;
            enableLookaheadLabel.MouseLeave += common_MouseLeave;
            enableLookaheadLabel.MouseHover += enableLookaheadLabel_MouseHover;
            // 
            // enableLookaheadCheckBox
            // 
            enableLookaheadCheckBox.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            enableLookaheadCheckBox.Location = new Point(267, 266);
            enableLookaheadCheckBox.Name = "enableLookaheadCheckBox";
            enableLookaheadCheckBox.Size = new Size(182, 44);
            enableLookaheadCheckBox.TabIndex = 31;
            enableLookaheadCheckBox.Text = " ";
            enableLookaheadCheckBox.UseVisualStyleBackColor = true;
            // 
            // lookaheadDepthLabel
            // 
            lookaheadDepthLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            lookaheadDepthLabel.Location = new Point(3, 315);
            lookaheadDepthLabel.Name = "lookaheadDepthLabel";
            lookaheadDepthLabel.Size = new Size(258, 52);
            lookaheadDepthLabel.TabIndex = 26;
            lookaheadDepthLabel.Text = "lookaheadDepth";
            lookaheadDepthLabel.TextAlign = ContentAlignment.MiddleLeft;
            lookaheadDepthLabel.MouseLeave += common_MouseLeave;
            lookaheadDepthLabel.MouseHover += lookaheadDepthLabel_MouseHover;
            // 
            // lookaheadDepthTextBox
            // 
            lookaheadDepthTextBox.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            lookaheadDepthTextBox.Location = new Point(267, 318);
            lookaheadDepthTextBox.Name = "lookaheadDepthTextBox";
            lookaheadDepthTextBox.Size = new Size(182, 49);
            lookaheadDepthTextBox.TabIndex = 32;
            // 
            // disableIadaptLabel
            // 
            disableIadaptLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            disableIadaptLabel.Location = new Point(3, 370);
            disableIadaptLabel.Name = "disableIadaptLabel";
            disableIadaptLabel.Size = new Size(258, 52);
            disableIadaptLabel.TabIndex = 28;
            disableIadaptLabel.Text = "disableIadpt";
            disableIadaptLabel.TextAlign = ContentAlignment.MiddleLeft;
            disableIadaptLabel.MouseLeave += common_MouseLeave;
            disableIadaptLabel.MouseHover += disableIadaptLabel_MouseHover;
            // 
            // disableIadaptCheckBox
            // 
            disableIadaptCheckBox.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            disableIadaptCheckBox.Location = new Point(267, 373);
            disableIadaptCheckBox.Name = "disableIadaptCheckBox";
            disableIadaptCheckBox.Size = new Size(182, 44);
            disableIadaptCheckBox.TabIndex = 33;
            disableIadaptCheckBox.Text = " ";
            disableIadaptCheckBox.UseVisualStyleBackColor = true;
            // 
            // disableBadaptLabel
            // 
            disableBadaptLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            disableBadaptLabel.Location = new Point(3, 422);
            disableBadaptLabel.Name = "disableBadaptLabel";
            disableBadaptLabel.Size = new Size(258, 52);
            disableBadaptLabel.TabIndex = 30;
            disableBadaptLabel.Text = "disableBadapt";
            disableBadaptLabel.TextAlign = ContentAlignment.MiddleLeft;
            disableBadaptLabel.MouseLeave += common_MouseLeave;
            disableBadaptLabel.MouseHover += disableBadaptLabel_MouseHover;
            // 
            // disableBadaptCheckBox
            // 
            disableBadaptCheckBox.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            disableBadaptCheckBox.Location = new Point(267, 425);
            disableBadaptCheckBox.Name = "disableBadaptCheckBox";
            disableBadaptCheckBox.Size = new Size(182, 44);
            disableBadaptCheckBox.TabIndex = 34;
            disableBadaptCheckBox.Text = " ";
            disableBadaptCheckBox.UseVisualStyleBackColor = true;
            // 
            // resolutionHeightTextBox
            // 
            resolutionHeightTextBox.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            resolutionHeightTextBox.Location = new Point(377, 194);
            resolutionHeightTextBox.Name = "resolutionHeightTextBox";
            resolutionHeightTextBox.Size = new Size(139, 49);
            resolutionHeightTextBox.TabIndex = 4;
            // 
            // resolutionXLabel
            // 
            resolutionXLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            resolutionXLabel.Location = new Point(336, 197);
            resolutionXLabel.Name = "resolutionXLabel";
            resolutionXLabel.RightToLeft = RightToLeft.Yes;
            resolutionXLabel.Size = new Size(35, 38);
            resolutionXLabel.TabIndex = 64;
            resolutionXLabel.Text = "x";
            // 
            // fpsTextBox
            // 
            fpsTextBox.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            fpsTextBox.Location = new Point(191, 267);
            fpsTextBox.Name = "fpsTextBox";
            fpsTextBox.Size = new Size(139, 49);
            fpsTextBox.TabIndex = 5;
            // 
            // resolutionWidthTextBox
            // 
            resolutionWidthTextBox.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            resolutionWidthTextBox.Location = new Point(191, 194);
            resolutionWidthTextBox.Name = "resolutionWidthTextBox";
            resolutionWidthTextBox.Size = new Size(139, 49);
            resolutionWidthTextBox.TabIndex = 3;
            // 
            // closeButton
            // 
            closeButton.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            closeButton.Location = new Point(941, 1190);
            closeButton.Name = "closeButton";
            closeButton.Size = new Size(131, 52);
            closeButton.TabIndex = 63;
            closeButton.TabStop = false;
            closeButton.Text = "Close";
            closeButton.UseVisualStyleBackColor = true;
            closeButton.Click += closeButton_Click;
            // 
            // saveButton
            // 
            saveButton.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            saveButton.Location = new Point(1104, 1190);
            saveButton.Name = "saveButton";
            saveButton.Size = new Size(131, 52);
            saveButton.TabIndex = 59;
            saveButton.TabStop = false;
            saveButton.Text = "Save";
            saveButton.UseVisualStyleBackColor = true;
            saveButton.Click += saveButton_Click;
            // 
            // detailSettingLabel
            // 
            detailSettingLabel.BackColor = SystemColors.Control;
            detailSettingLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            detailSettingLabel.Location = new Point(17, 650);
            detailSettingLabel.Name = "detailSettingLabel";
            detailSettingLabel.Size = new Size(190, 38);
            detailSettingLabel.TabIndex = 62;
            detailSettingLabel.Text = "label1";
            // 
            // selectModeLabel
            // 
            selectModeLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            selectModeLabel.Location = new Point(8, 22);
            selectModeLabel.Name = "selectModeLabel";
            selectModeLabel.RightToLeft = RightToLeft.Yes;
            selectModeLabel.Size = new Size(167, 38);
            selectModeLabel.TabIndex = 57;
            selectModeLabel.Text = "label1";
            // 
            // fpsLabel
            // 
            fpsLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            fpsLabel.Location = new Point(8, 270);
            fpsLabel.Name = "fpsLabel";
            fpsLabel.RightToLeft = RightToLeft.Yes;
            fpsLabel.Size = new Size(167, 38);
            fpsLabel.TabIndex = 51;
            fpsLabel.Text = "FPS";
            fpsLabel.MouseHover += fpsLabel_MouseHover;
            // 
            // resolutionLabel
            // 
            resolutionLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            resolutionLabel.Location = new Point(8, 200);
            resolutionLabel.Name = "resolutionLabel";
            resolutionLabel.RightToLeft = RightToLeft.Yes;
            resolutionLabel.Size = new Size(167, 38);
            resolutionLabel.TabIndex = 49;
            resolutionLabel.Text = "label1";
            resolutionLabel.MouseHover += resolutionLabel_MouseHover;
            // 
            // customModeComboBox
            // 
            customModeComboBox.Font = new Font("Yu Gothic UI", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            customModeComboBox.FormattingEnabled = true;
            customModeComboBox.Location = new Point(181, 18);
            customModeComboBox.Name = "customModeComboBox";
            customModeComboBox.Size = new Size(653, 46);
            customModeComboBox.TabIndex = 1;
            customModeComboBox.SelectedIndexChanged += customModeComboBox_SelectedIndexChanged;
            // 
            // modeNameLabel
            // 
            modeNameLabel.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            modeNameLabel.Location = new Point(8, 130);
            modeNameLabel.Name = "modeNameLabel";
            modeNameLabel.RightToLeft = RightToLeft.Yes;
            modeNameLabel.Size = new Size(167, 38);
            modeNameLabel.TabIndex = 66;
            modeNameLabel.Text = "label1";
            // 
            // modeNameTextBox
            // 
            modeNameTextBox.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            modeNameTextBox.Location = new Point(191, 127);
            modeNameTextBox.Name = "modeNameTextBox";
            modeNameTextBox.Size = new Size(440, 49);
            modeNameTextBox.TabIndex = 2;
            // 
            // deleteButton
            // 
            deleteButton.Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128);
            deleteButton.Location = new Point(887, 17);
            deleteButton.Name = "deleteButton";
            deleteButton.Size = new Size(131, 52);
            deleteButton.TabIndex = 67;
            deleteButton.TabStop = false;
            deleteButton.Text = "Delete";
            deleteButton.UseVisualStyleBackColor = true;
            deleteButton.Click += deleteButton_Click;
            // 
            // ModeEditor
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1258, 1260);
            Controls.Add(deleteButton);
            Controls.Add(modeNameTextBox);
            Controls.Add(modeNameLabel);
            Controls.Add(detailSettingsContainerFlowLayoutPanel);
            Controls.Add(resolutionHeightTextBox);
            Controls.Add(resolutionXLabel);
            Controls.Add(fpsTextBox);
            Controls.Add(resolutionWidthTextBox);
            Controls.Add(customModeComboBox);
            Controls.Add(closeButton);
            Controls.Add(saveButton);
            Controls.Add(detailSettingLabel);
            Controls.Add(selectModeLabel);
            Controls.Add(fpsLabel);
            Controls.Add(resolutionLabel);
            Name = "ModeEditor";
            Text = "ModeEditor";
            Load += ModeEditor_Load;
            detailSettingsContainerFlowLayoutPanel.ResumeLayout(false);
            detailSettingsFlowLayoutPanel1.ResumeLayout(false);
            detailSettingsFlowLayoutPanel1.PerformLayout();
            detailSettingsFlowLayoutPanel2.ResumeLayout(false);
            detailSettingsFlowLayoutPanel2.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private FlowLayoutPanel detailSettingsContainerFlowLayoutPanel;
        private FlowLayoutPanel detailSettingsFlowLayoutPanel1;
        private Label averageBitrateLabel;
        private TextBox averageBitrateTextBox;
        private Label averageBitrateUnitLabel;
        private Label maxBitrateLabel;
        private TextBox maxBitrateTextBox;
        private Label maxBitrateUnitLabel;
        private Label vbvBufferSizeLabel;
        private TextBox vbvBufferSizeTextBox;
        private Label vbvBufferSizeUnitLabel;
        private Label vbvInitialDelayLabel;
        private TextBox vbvInitialDelayTextBox;
        private Label vbvInitialDelayUnitLabel;
        private Label gopLengthLabel;
        private TextBox gopLengthTextBox;
        private Label idrPeriodLabel;
        private TextBox idrPeriodTextBox;
        private Label repeatSpsPpsLabel;
        private CheckBox repeatSpsPpsCheckBox;
        private Label outputAudLabel;
        private CheckBox outputAudCheckBox;
        private FlowLayoutPanel detailSettingsFlowLayoutPanel2;
        private Label maxRefFramesLabel;
        private TextBox maxRefFramesTextBox;
        private Label presetGuidLabel;
        private ComboBox presetGuidComboBox;
        private Label tuningInfoLabel;
        private ComboBox tuningInfoComboBox;
        private Label enableLookaheadLabel;
        private CheckBox enableLookaheadCheckBox;
        private Label lookaheadDepthLabel;
        private TextBox lookaheadDepthTextBox;
        private Label disableIadaptLabel;
        private CheckBox disableIadaptCheckBox;
        private Label disableBadaptLabel;
        private CheckBox disableBadaptCheckBox;
        private TextBox resolutionHeightTextBox;
        private Label resolutionXLabel;
        private TextBox fpsTextBox;
        private TextBox resolutionWidthTextBox;
        private Button closeButton;
        private Button saveButton;
        private Label detailSettingLabel;
        private Label selectModeLabel;
        private Label fpsLabel;
        private Label resolutionLabel;
        private ComboBox customModeComboBox;
        private Label modeNameLabel;
        private TextBox modeNameTextBox;
        private Button deleteButton;
        private ToolTip technicalTermToolTip;
    }
}