using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NuGet;
using Path = System.Windows.Shapes.Path;
using Settings = TTSApp.Properties.Settings;

namespace TTSApp {
    /// <summary>
    /// Interaction logic for OptionsWindow.xaml
    /// </summary>
    public partial class OptionsWindow : Window {
        public OptionsWindow() {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e) {
            Settings.Default.Save();
            MainWindow.Model.Reset();
            Close();
        }

        public void ShowProviderTab()
        {
            SettingsTabControl.SelectedIndex = 1;
            ShowDialog();        
        }

        private void AmazonHelpIcon_OnClick(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://docs.aws.amazon.com/general/latest/gr/managing-aws-access-keys.html");
        }

        private void GoogleHelpIcon_OnClick(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://cloud.google.com/text-to-speech/docs/quickstart-client-libraries");
        }

        private void SelectFilePathButton_OnClick(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                var currentPath = Settings.Default.SaveFilePath;
                if (!string.IsNullOrEmpty(currentPath))
                {
                    dialog.SelectedPath = currentPath;
                }
                var result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {

                    if (!HasWriteAccessToDirectory(dialog.SelectedPath))
                    {
                        MessageBox.Show("Cannot access selected directory", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    Settings.Default.SaveFilePath = dialog.SelectedPath;
                }
            }
        }

        private bool HasWriteAccessToDirectory(string folderPath) {
            try {
                var ds = Directory.GetAccessControl(folderPath);
                return true;
            } catch (UnauthorizedAccessException) {
                return false;
            }
        }
    }
}
