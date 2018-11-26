using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using log4net;
using TTSAmazonPolly;
using MessageBox = System.Windows.MessageBox;

namespace TTSApp.Forms {
    /// <summary>
    /// Interaction logic for LogWindow.xaml
    /// </summary>
    public partial class LogWindow : Window {

        public Timer Timer { get; set; }

        public LogWindow() {
            InitializeComponent();
            CreateUpdater();
            UpdateLog();
        }

        private void CreateUpdater()
        {
            Timer = new Timer {Interval = 1000};
            Timer.Tick += OnTick;
            Timer.Start();
        }

        private void OnTick(object sender, EventArgs e)
        {
            UpdateLog();
        }

        private void UpdateLog()
        {
            try
            {
                using (var fs = new FileStream("logs.log", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs, Encoding.Default))
                {
                    var newContent = sr.ReadToEnd();
                    if (LogsTextBox.Text != newContent)
                    {
                        LogsTextBox.Text = newContent;
                        LogsTextBox.ScrollToEnd();
                    }
                }
            }
            catch (Exception e)
            {
                //
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            Timer?.Dispose();
            base.OnClosing(e);
        }
    }

}
