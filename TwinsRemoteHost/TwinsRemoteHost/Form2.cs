using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace TwinsRemoteHost
{
    public partial class ProcessSelector : Form
    {
        private FlowLayoutPanel? selectedPanel;
        private String processId = "";

        public ProcessSelector()
        {
            InitializeComponent();
            DisplayAudioProcess();
        }

        private List<Tuple<int, string>> GetAudioProcesses()
        {
            var audioProcesses = new List<Tuple<int, string>>();
            var enumerator = new MMDeviceEnumerator();
            var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);

            foreach (var device in devices)
            {
                var sessions = device.AudioSessionManager.Sessions;

                for (int i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];
                    var processId = session.GetProcessID;

                    var process = GetProcessFromId((int)processId);

                    if (process != null && processId != 0)
                    {
                        audioProcesses.Add(new Tuple<int, string>((int)processId, process.ProcessName));
                    }
                }
            }

            return audioProcesses;
        }

        private Process? GetProcessFromId(int processId)
        {
            try
            {
                return Process.GetProcessById(processId);
            }
            catch (ArgumentException) { }


            return null;
        }

        private void DisplayAudioProcess()
        {
            flowLayoutPanel.Controls.Clear();

            var audioProcesses = GetAudioProcesses();

            foreach (var audioProcess in audioProcesses)
            {
                if (IsExcludeFromList(audioProcess.Item1, audioProcess.Item2)) { continue; }

                var radioButton = new RadioButton()
                {
                    Name = "radioButton",
                    Text = " ",
                    Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128),
                    Checked = false,
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoSize = true,
                    TabIndex = 0,
                    TabStop = true,
                    UseVisualStyleBackColor = true
                };
                radioButton.Click += Panel_Click;

                var idLabel = new Label
                {
                    Name = "pId",
                    Text = $"pid {audioProcess.Item1}",
                    Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128),
                    AutoSize = true,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                idLabel.Click += Panel_Click;

                var nameLabel = new Label
                {
                    Name = "appName",
                    Text = $"{audioProcess.Item2}",
                    Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128),
                    AutoSize = true,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                nameLabel.Click += Panel_Click;


                var panel = new FlowLayoutPanel
                {
                    FlowDirection = FlowDirection.LeftToRight,
                    Dock = DockStyle.Top,
                    Padding = new Padding(5),
                    Margin = new Padding(5),
                    Width = flowLayoutPanel.Width - 25,
                    Height = 60
                };
                panel.Controls.Add(radioButton);
                panel.Controls.Add(idLabel);
                panel.Controls.Add(nameLabel);
                panel.Click += Panel_Click;
                flowLayoutPanel.Controls.Add(panel);
            }
        }

        private bool IsExcludeFromList(int audioProcessId, string audioProcessName)
        {
            foreach (FlowLayoutPanel panel in flowLayoutPanel.Controls.OfType<FlowLayoutPanel>())
            {
                Label? idLabel = panel.Controls["pId"] as Label;

                if (idLabel == null) { continue; }
                string pidStr = idLabel.Text.Replace("pid ", "");
                
                try
                {
                    int pid = int.Parse(pidStr);
                    if (pid == audioProcessId) { return true; }
                }
                catch
                {
                    continue;
                }

                // TwinsRemoteのアプリは除外
                if (audioProcessName == "SystemMixCapture"
                    || audioProcessName == "ProcessAudioCapture")
                {
                    return true;
                }
            }

            return false;
        }

        private void Panel_Click(object? sender, EventArgs e)
        {
            if (sender == null) { return; }
            FlowLayoutPanel? panel;
            
            if (sender.GetType() == typeof(FlowLayoutPanel))
            {
                panel = sender as FlowLayoutPanel;
            }
            else
            {
                Control? control = sender as Control;
                if (control == null) { return; }

                panel = control.Parent as FlowLayoutPanel;
            }
            if (panel == null) { return; }

            RadioButton? RadioBtn = panel.Controls["radioButton"] as RadioButton;
            Label? PIdLabel = panel.Controls["pId"] as Label;
            if (RadioBtn != null && PIdLabel != null)
            {
                ResetAllRadioButton();
                RadioBtn.Checked = true;
                this.processId = PIdLabel.Text;
            }
        }

        private void ResetAllRadioButton()
        {
            foreach(FlowLayoutPanel panel in flowLayoutPanel.Controls.OfType<FlowLayoutPanel>())
            {
                RadioButton? btn = panel.Controls["radioButton"] as RadioButton;
                
                if (btn == null) { continue; }
                btn.Checked = false;
            }
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            Reset();
            this.Close();
        }

        private void ReloadButton_Click(object sender, EventArgs e)
        {
            DisplayAudioProcess();
        }

        private void Reset()
        {
            flowLayoutPanel.Controls.Clear();
            this.selectedPanel = null;
            this.processId = "";
        }

        public string GetProcessId()
        {
            return this.processId;
        }
    }
}
