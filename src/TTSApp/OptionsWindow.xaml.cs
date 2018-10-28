using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;
using NuGet;
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

        private void AmazonHelpIcon_OnClick(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://docs.aws.amazon.com/general/latest/gr/managing-aws-access-keys.html");
        }

        private void GoogleHelpIcon_OnClick(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://cloud.google.com/text-to-speech/docs/quickstart-client-libraries");
        }
    }
}
