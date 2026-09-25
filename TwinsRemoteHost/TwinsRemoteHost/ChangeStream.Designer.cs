namespace TwinsRemoteHost
{
    partial class ChangeStream
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
            modeLabel = new Label();
            modeComboBox = new ComboBox();
            selectScreenLabel = new Label();
            screenOptionFlowLayoutPanel = new FlowLayoutPanel();
            cancelButton = new Button();
            okButton = new Button();
            SuspendLayout();
            // 
            // modeLabel
            // 
            modeLabel.Font = new Font("メイリオ", 14F);
            modeLabel.Location = new Point(12, 22);
            modeLabel.Name = "modeLabel";
            modeLabel.RightToLeft = RightToLeft.Yes;
            modeLabel.Size = new Size(207, 42);
            modeLabel.TabIndex = 0;
            modeLabel.Text = "label1";
            modeLabel.TextAlign = ContentAlignment.MiddleRight;
            // 
            // modeComboBox
            // 
            modeComboBox.Font = new Font("メイリオ", 14F);
            modeComboBox.FormattingEnabled = true;
            modeComboBox.Location = new Point(225, 14);
            modeComboBox.Name = "modeComboBox";
            modeComboBox.Size = new Size(322, 50);
            modeComboBox.TabIndex = 1;
            // 
            // selectScreenLabel
            // 
            selectScreenLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            selectScreenLabel.AutoSize = true;
            selectScreenLabel.Font = new Font("メイリオ", 14F);
            selectScreenLabel.Location = new Point(12, 518);
            selectScreenLabel.Name = "selectScreenLabel";
            selectScreenLabel.Size = new Size(98, 42);
            selectScreenLabel.TabIndex = 2;
            selectScreenLabel.Text = "label1";
            selectScreenLabel.TextAlign = ContentAlignment.MiddleRight;
            // 
            // screenOptionFlowLayoutPanel
            // 
            screenOptionFlowLayoutPanel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            screenOptionFlowLayoutPanel.AutoScroll = true;
            screenOptionFlowLayoutPanel.Location = new Point(12, 563);
            screenOptionFlowLayoutPanel.Name = "screenOptionFlowLayoutPanel";
            screenOptionFlowLayoutPanel.Size = new Size(891, 724);
            screenOptionFlowLayoutPanel.TabIndex = 3;
            // 
            // cancelButton
            // 
            cancelButton.Font = new Font("メイリオ", 14F);
            cancelButton.Location = new Point(623, 1334);
            cancelButton.Name = "cancelButton";
            cancelButton.Size = new Size(131, 52);
            cancelButton.TabIndex = 100;
            cancelButton.Text = "Cancel";
            cancelButton.UseVisualStyleBackColor = true;
            cancelButton.Click += cancelButton_Click;
            // 
            // okButton
            // 
            okButton.Font = new Font("メイリオ", 14F);
            okButton.Location = new Point(774, 1334);
            okButton.Name = "okButton";
            okButton.Size = new Size(131, 52);
            okButton.TabIndex = 101;
            okButton.Text = "OK";
            okButton.UseVisualStyleBackColor = true;
            okButton.Click += okButton_Click;
            // 
            // ChangeStream
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(915, 1398);
            Controls.Add(okButton);
            Controls.Add(cancelButton);
            Controls.Add(screenOptionFlowLayoutPanel);
            Controls.Add(selectScreenLabel);
            Controls.Add(modeComboBox);
            Controls.Add(modeLabel);
            Name = "ChangeStream";
            Text = "ChangeStream";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label modeLabel;
        private ComboBox modeComboBox;
        private Label selectScreenLabel;
        private FlowLayoutPanel screenOptionFlowLayoutPanel;
        private Button cancelButton;
        private Button okButton;
    }
}