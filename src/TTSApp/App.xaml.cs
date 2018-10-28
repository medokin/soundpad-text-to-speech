using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using Sentry;

namespace TTSApp {
    /// <summary>
    /// Interaktionslogik für "App.xaml"
    /// </summary>
    public partial class App : Application {
        public App()
        {
            SentrySdk.Init("https://b656626b3ff74cf6a7cb73ba91dd0be4@sentry.io/1310740");
            this.Dispatcher.UnhandledException += OnDispatcherUnhandledException;
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            SentrySdk.CaptureException(e.Exception);
            string errorMessage = $"An unhandled exception occurred: {e.Exception.Message}";
            MessageBox.Show(errorMessage, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
