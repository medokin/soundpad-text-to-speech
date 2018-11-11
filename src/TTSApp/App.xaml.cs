using System;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Sentry;
using TTSApp.Properties;

namespace TTSApp {
    /// <summary>
    /// Interaktionslogik für "App.xaml"
    /// </summary>
    public partial class App : Application {
        public App()
        {
            SentrySdk.Init("https://b656626b3ff74cf6a7cb73ba91dd0be4@sentry.io/1310740");
            RestoreSettings();
            CheckSettings();

            System.Threading.Thread.CurrentThread.CurrentUICulture = Settings.Default.CultureInfo;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            SentrySdk.CaptureException(e.Exception);
            string errorMessage = $"An unhandled exception occurred: {e.Exception.Message}";
            MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void RestoreSettings() {
            //Restore settings after application update            
            var destFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal)
                .FilePath;
            var sourceFile = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\..\\last.config";
            // Check if we have settings that we need to restore
            if (!File.Exists(sourceFile)) return;
            // Create directory as needed
            try {
                Directory.CreateDirectory(Path.GetDirectoryName(destFile));
            } catch (Exception) {
            }

            // Copy our backup file in place 
            try {
                File.Copy(sourceFile, destFile, true);
                Settings.Default.Reload();
            } catch (Exception) {
            }

            // Delete backup file
            try {
                File.Delete(sourceFile);
            } catch (Exception) {
            }
        }

        /// <summary>
        /// Check and restore setting values if needed
        /// </summary>
        private void CheckSettings() {
            // Update 1.0.18 - Select DeleteFromSoundpadAfterPlay if file Path not set
            if (!Settings.Default.DeleteFromSoundpadAfterPlay && !Directory.Exists(Settings.Default.SaveFilePath)) {
                Settings.Default.DeleteFromSoundpadAfterPlay = true;
            }

            // Update 1.0.19 - Set UI Culture as Default Culture if invariant
            if (Equals(Settings.Default.CultureInfo, CultureInfo.InvariantCulture)) {
                Settings.Default.CultureInfo = Thread.CurrentThread.CurrentUICulture;
            }

            Settings.Default.Save();
        }
    }
}
