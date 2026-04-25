namespace TwinsRemoteHost
{
    partial class ProcessSelector
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
            flowLayoutPanel = new FlowLayoutPanel();
            OkButton = new Button();
            cancelButton = new Button();
            reloadButton = new Button();
            SuspendLayout();
            // 
            // flowLayoutPanel
            // 
            flowLayoutPanel.AutoScroll = true;
            flowLayoutPanel.Location = new Point(12, 12);
            flowLayoutPanel.Name = "flowLayoutPanel";
            flowLayoutPanel.Size = new Size(819, 898);
            flowLayoutPanel.TabIndex = 0;
            // 
            // OkButton
            // 
            OkButton.Location = new Point(719, 950);
            OkButton.Name = "OkButton";
            OkButton.Size = new Size(112, 42);
            OkButton.TabIndex = 2;
            OkButton.Text = "OK";
            OkButton.UseVisualStyleBackColor = true;
            OkButton.Click += OkButton_Click;
            // 
            // cancelButton
            // 
            cancelButton.Location = new Point(586, 950);
            cancelButton.Name = "cancelButton";
            cancelButton.Size = new Size(112, 42);
            cancelButton.TabIndex = 3;
            cancelButton.Text = "Cancel";
            cancelButton.UseVisualStyleBackColor = true;
            cancelButton.Click += CancelButton_Click;
            // 
            // reloadButton
            // 
            reloadButton.Location = new Point(12, 950);
            reloadButton.Name = "reloadButton";
            reloadButton.Size = new Size(112, 42);
            reloadButton.TabIndex = 4;
            reloadButton.Text = "Reload";
            reloadButton.UseVisualStyleBackColor = true;
            reloadButton.Click += ReloadButton_Click;
            // 
            // ProcessSelector
            // 
            AutoScaleDimensions = new SizeF(10F, 25F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(843, 1004);
            Controls.Add(reloadButton);
            Controls.Add(cancelButton);
            Controls.Add(flowLayoutPanel);
            Controls.Add(OkButton);
            Name = "ProcessSelector";
            Text = "ProcessSelector";
            ResumeLayout(false);
        }

        #endregion

        private ListView listView1;
        private FlowLayoutPanel flowLayoutPanel;
        private Button OkButton;
        private Button cancelButton;
        private Button reloadButton;
    }
}