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
    public partial class NotificationsForm : Form
    {
        private readonly List<string> notifications;
        private readonly string updateNotification;
        private readonly string releaseUrl;

        public NotificationsForm(List<string> notifications, string updateNotification, string releaseUrl)
        {
            this.notifications = notifications;
            this.updateNotification = updateNotification;
            this.releaseUrl = releaseUrl;
            InitializeComponent();
            SetNotifications();
        }

        private void SetNotifications()
        {
            foreach (string notification in this.notifications)
            {
                Label notificationLabel = new()
                {
                    Name = "notificaitonLabel",
                    Text = notification + "\n------------------------------------------------",
                    Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128),
                    AutoSize = true,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                flowLayoutPanel.Controls.Add(notificationLabel);
            }

            if (this.updateNotification != String.Empty)
            {
                Label notificationLabel = new()
                {
                    Name = "updateNotificationLabel",
                    Text = this.updateNotification,
                    Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128),
                    AutoSize = true,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                LinkLabel releaseUrlLinkLabel = new()
                {
                    Name = "releaseUrlLinkLabel",
                    Text = this.releaseUrl,
                    Font = new Font("メイリオ", 14F, FontStyle.Regular, GraphicsUnit.Point, 128),
                    AutoSize = true,
                    TextAlign = ContentAlignment.MiddleLeft,
                    LinkVisited = false,
                };
                releaseUrlLinkLabel.LinkClicked += releaseUrlLinkLabel_Clicked;
                
                flowLayoutPanel.Controls.Add(notificationLabel);
                flowLayoutPanel.Controls.Add(releaseUrlLinkLabel);
            }
        }

        private void releaseUrlLinkLabel_Clicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var ps = new Process();
            ps.StartInfo.UseShellExecute = true;
            ps.StartInfo.FileName = this.releaseUrl;
            ps.Start();
        }
    }
}
