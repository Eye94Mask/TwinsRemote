using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace TwinsRemoteHost
{
    public partial class ChangeStream : Form
    {
        private readonly Locale locale;
        private bool isReady = false;
        private string primaryScreenName = "";
        private string screenName = "";
        private string currentModeValue = "";

        const int ENUM_CURRENT_SETTINGS = -1;

        [DllImport("user32.dll")]
        public static extern bool EnumDisplaySettingsA(string lpszDeviceName, int iModeNum, ref DEVMODE lpDevMode);

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVMODE
        {
            private const int CCHDEVICENAME = 0x20;
            private const int CCHFORMNAME = 0x20;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public ScreenOrientation dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 0x20)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        public ChangeStream(Locale locale, String currentModeValue)
        {
            this.locale = locale;
            this.currentModeValue = currentModeValue;
            InitializeComponent();
            InitializeUi();
            ApplyLanguage();
        }

        private void InitializeUi()
        {
            MakeSelectionOfMonitors();

            this.isReady = false;
        }

        private void ApplyLanguage()
        {
            selectScreenLabel.Text = this.locale.SelectScreen;
            modeLabel.Text = this.locale.LabelModeText;

            ResetModeList(this.currentModeValue);
        }

        private void ResetModeList(string currentModeValue)
        {
            List<string> customNames = Host.GetCustomModeList();

            modeComboBox.Items.Clear();
            List<object> modes = [];
            foreach (string customName in customNames)
            {
                modeComboBox.Items.Add(new VideoPresetItem { DisplayName = customName, Key = customName });
            }
            modeComboBox.Items.Add(new VideoPresetItem { DisplayName = this.locale.BalancedMode, Key = "balanced" });
            modeComboBox.Items.Add(new VideoPresetItem { DisplayName = this.locale.QualityMode, Key = "quality" });
            modeComboBox.Items.Add(new VideoPresetItem { DisplayName = this.locale.StableMode, Key = "stable" });
            modeComboBox.Items.Add(new VideoPresetItem { DisplayName = this.locale.MobileMode, Key = "mobile" });

            int i = 0;
            foreach (VideoPresetItem item in modeComboBox.Items)
            {
                if (item.DisplayName == this.currentModeValue)
                {
                    modeComboBox.SelectedIndex = i;
                    return;
                }
                i++;
            }

            if (i >= modeComboBox.Items.Count) { modeComboBox.SelectedIndex = 0; }
        }

        private Bitmap CaptureScreen(Rectangle screenBounds, DEVMODE dm, int scale)
        {
            Size srcSize = new Size(screenBounds.Width * scale / 100, screenBounds.Height * scale / 100);

            Bitmap bmp = new Bitmap(srcSize.Width, srcSize.Height);
            Graphics graphic = Graphics.FromImage(bmp);
            graphic.CopyFromScreen(new Point(dm.dmPositionX, dm.dmPositionY), new Point(0, 0), bmp.Size);
            graphic.Dispose();

            return bmp;
        }

        private void MakeSelectionOfMonitors()
        {
            FlowLayoutPanel[] monitorOptions = [];
            const string PRIORITY = "Priority";
            int tabIndex = 10;

            // メインモニターを先頭にFlowLayoutを登録
            foreach (System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens)
            {
                // dpi差の解消
                DEVMODE dm = new DEVMODE();
                dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                EnumDisplaySettingsA(screen.DeviceName, ENUM_CURRENT_SETTINGS, ref dm);
                int scale = (dm.dmPelsWidth * 100) / screen.Bounds.Width;

                // スクリーンの画面コピー
                Bitmap bitmap = CaptureScreen(screen.Bounds, dm, scale);

                // モニタのキャプチャ
                PictureBox capture = new()
                {
                    Name = "screenPicture",
                    Image = bitmap,
                    Size = new Size(400, 250),
                    SizeMode = PictureBoxSizeMode.StretchImage
                };
                capture.Click += Monitor_Click;

                // モニタの名前
                string text = "";
                if (screen.Primary)
                {
                    text = PRIORITY;
                    this.screenName = screen.DeviceName;
                }

                RadioButton monitorName = new()
                {
                    Name = "monitorButton",
                    Tag = screen.DeviceName,
                    Text = text,
                    Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128),
                    Checked = screen.Primary,
                    AutoSize = false,
                    Width = 420,
                    Height = 46
                };
                monitorName.Click += Monitor_Click;

                // モニター選択肢のフローレイアウト
                FlowLayoutPanel selectionContainer = new()
                {
                    Width = capture.Width,
                    Height = capture.Height + 50,
                    Margin = new Padding(3, 3, 3, 20),
                    AutoSize = false,
                    TabStop = true,
                    TabIndex = tabIndex
                };

                selectionContainer.Click += Monitor_Click;

                if (screen.Primary)
                {
                    this.primaryScreenName = screen.DeviceName;
                }

                selectionContainer.Controls.Add(capture);
                selectionContainer.Controls.Add(monitorName);

                if (screen.Primary)
                {
                    monitorOptions.Prepend(selectionContainer);
                }
                else
                {
                    monitorOptions.Append(selectionContainer);
                }

                screenOptionFlowLayoutPanel.Controls.Add(selectionContainer);
                tabIndex++;
            }

            int screenIndex = 2;
            // メインモニタを優先して表示
            foreach (Control monitorOption in screenOptionFlowLayoutPanel.Controls)
            {
                bool isPriority = false;
                foreach (Control c in monitorOption.Controls)
                {
                    if (c.GetType().Equals(typeof(RadioButton)))
                    {
                        if (c.Text == PRIORITY)
                        {
                            isPriority = true;
                            c.Text = this.locale.Screen + "1";
                        }
                        else
                        {
                            c.Text = this.locale.Screen + screenIndex;
                            screenIndex++;
                        }
                    }
                }

                if (isPriority)
                {
                    if (monitorOption.GetType().Equals(typeof(FlowLayoutPanel)))
                    {
                        screenOptionFlowLayoutPanel.Controls.SetChildIndex(monitorOption, 0);
                    }
                }
            }
        }

        private void Monitor_Click(object? sender, EventArgs e)
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

            RadioButton? monitorBtn = panel.Controls["monitorButton"] as RadioButton;
            if (monitorBtn != null)
            {
                ResetAllMonitorRadioButton();
                monitorBtn.Checked = true;
                if (monitorBtn.Tag != null)
                {
                    this.screenName = monitorBtn.Tag.ToString();
                }
                else
                {
                    this.screenName = this.primaryScreenName;
                }
            }
        }

        private void ResetAllMonitorRadioButton()
        {
            foreach (FlowLayoutPanel panel in screenOptionFlowLayoutPanel.Controls.OfType<FlowLayoutPanel>())
            {
                RadioButton? btn = panel.Controls["monitorButton"] as RadioButton;

                if (btn == null) { continue; }
                btn.Checked = false;
            }
        }

        public string GetChangeStreamCommnad()
        {
            if (this.screenName == string.Empty) { return ""; }

            string newMode = modeComboBox.SelectedItem?.ToString() ?? this.currentModeValue;
            return "change_stream " + this.screenName + " " + newMode;
        }

        public string GetNewMode()
        {
            return modeComboBox.SelectedItem?.ToString() ?? this.currentModeValue;
        }

        public bool IsChangingStreamReady()
        {
            return this.isReady;
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void okButton_Click(object sender, EventArgs e)
        {
            this.isReady = true;

            this.Close();
        }
    }
}
