using System;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Common;
using log4net;
using Sentry;
using SoundpadConnector;
using SoundpadConnector.Response;
using Squirrel;
using TTSApp.Extensions;
using TTSApp.Properties;

namespace TTSApp.Forms
{
    /// <summary>
    ///     Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static readonly ILog Log = LogManager.GetLogger(typeof(MainWindow));
        public const string UpdateUrl = "https://soundpadcontrol.blob.core.windows.net/soundpad-tts";
        private static readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1);
        private Soundpad _soundpad;


        public MainWindow()
        {
            InitializeComponent();
        }

        public static MainWindowViewModel Model { get; set; }

        protected override async void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            await InitializeViewModel();
            await InitializeSoundpad();
            await CheckAndInstallUpdate();
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            InputTextBox.Focus();
        }

        private async Task InitializeViewModel()
        {
            Model = new MainWindowViewModel();
            await Model.Reset();
            DataContext = Model;
        }

        private async Task InitializeSoundpad()
        {
            _soundpad = new Soundpad
            {
                AutoReconnect = true
            };
            _soundpad.StatusChanged += SoundpadOnStatusChanged;
            _soundpad.ConnectAsync();
        }

        private async Task CheckAndInstallUpdate()
        {
            try
            {
                UpdateSpinner.Visibility = Visibility.Visible;
                VersionTextBlock.Text = Properties.Resources.MainWindowUpdateChecking;
                using (var mgr = new UpdateManager(UpdateUrl))
                {
                    var update = await mgr.CheckForUpdate(true);
                    VersionTextBlock.Text = update.CurrentlyInstalledVersion.Version.ToString();
                    if (update.CurrentlyInstalledVersion.EntryAsString != update.FutureReleaseEntry.EntryAsString)
                    {
                        VersionTextBlock.Text = Properties.Resources.MainWindowUpdateInstalling;
                        await mgr.UpdateApp();
                        VersionTextBlock.Text = Properties.Resources.MainWindowUpdateInstalled;
                        BackupSettings();
                    }
                }
            }
            catch (Exception)
            {
                VersionTextBlock.Text = Properties.Resources.MainWindowUpdateError;
            }
            finally
            {
                UpdateSpinner.Visibility = Visibility.Hidden;
            }
        }

        private void SoundpadOnStatusChanged(object sender, EventArgs e)
        {
            UpdateButtomStatusBar();
        }

        private void UpdateButtomStatusBar()
        {
            FooterTextBlock.Text = _soundpad.ConnectionStatus.ToString();
            if (_soundpad.ConnectionStatus != ConnectionStatus.Connected)
            {
                FooterTextBlock.Text= FooterTextBlock.Text + $". {Properties.Resources.MainWindowFooterSoundpadRunning}";
            }

            FooterTextBlock.Foreground = _soundpad.ConnectionStatus == ConnectionStatus.Connected
                ? new SolidColorBrush(Colors.Black)
                : new SolidColorBrush(Colors.Red);
        }

        private async Task Play(string text)
        {
            Log.Debug($"Playing: {text}");

            PlaySoundpadButton.IsEnabled = false;
            await SemaphoreSlim.WaitAsync();
            try
            {
                var uniqueId = Guid.NewGuid().ToString().Replace("-", "").Clip(10);

                // Sanitize Filename
                var fileName = Regex.Replace(text.Clip(20), @"[^0-9A-Za-z ,]", "_", RegexOptions.Compiled);
                fileName = $"{fileName}_{uniqueId}.{Model.SelectedProvider.FileExtension}";
                var filePath = Path.Combine(GetSavePath(), fileName);

                var stream =
                    await Model.SelectedProvider.SynthesizeTextToStreamAsync(Model.SelectedVoice, text);

                using (var fileStream = File.Create(filePath))
                {
                    if (stream.CanSeek) stream.Seek(0, SeekOrigin.Begin);
                    stream.CopyTo(fileStream);
                }

                var countResult = await _soundpad.GetSoundFileCount();
                int count;
                await _soundpad.AddSound(filePath);
                while (true)
                {
                    var result = await _soundpad.GetSoundFileCount();
                    if (result.Value != countResult.Value)
                    {
                        count = (int) result.Value;
                        break;
                    }
                }

                await _soundpad.PlaySound(count, Settings.Default.PlayRenderLine, Settings.Default.PlayCaptureLine);
                while (true)
                {
                    var status = await _soundpad.GetPlayStatus();
                    if (status.Value == PlayStatus.Playing) break;
                }


                if (DeleteFromSoundpadCheckbox.IsChecked.HasValue && DeleteFromSoundpadCheckbox.IsChecked.Value)
                {
                    await _soundpad.DoSelectIndex(count + 1);
                    await Task.Delay(100);
                    await _soundpad.RemoveSelectedEntries();
                }
            }
            catch (Exception e)
            {
                Log.Error("Cannot play sound", e);
                SentrySdk.CaptureException(e);
                MessageBox.Show("There was an error during playback", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SemaphoreSlim.Release();
                PlaySoundpadButton.IsEnabled = true;
            }
        }

        private string GetSavePath()
        {
            if (!Settings.Default.DeleteFromSoundpadAfterPlay && Directory.Exists(Settings.Default.SaveFilePath))
            {
                return Settings.Default.SaveFilePath;
            }

            return Path.GetTempPath();

        }

        private void VoiceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(e.AddedItems[0] is IVoice voice)) return;
            Model.SetVoice(voice);
        }

        private async void ProviderComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (e.AddedItems.Count == 0 || !(e.AddedItems[0] is ITextToSpeechProvider item)) return;

                await Model.SetProvider(item);
            }
            catch (Exception ex)
            {
                Log.Error("Cannot select Provider", ex);
                SentrySdk.CaptureException(ex);
                MessageBox.Show("Provider is not available", "Error", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                await Model.Reset();
            }
        }

        private async void PlaySoundpadButton_Click(object sender, RoutedEventArgs e)
        {
            var hasText = !string.IsNullOrWhiteSpace(InputTextBox.Text);
            InputTextBox.Background = hasText ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Colors.Red);
            if (!hasText) return;

            if (_soundpad.ConnectionStatus != ConnectionStatus.Connected)
            {
                MessageBox.Show("Soundpad is not running", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }


            var text = InputTextBox.Text;
            if (Settings.Default.EmptyTextAfterPlay) InputTextBox.Text = "";
            InputTextBox.Focus();
            await Play(text);
        }

        private void MenuItemQuit_OnClick(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MenuItemSettings_OnClick(object sender, RoutedEventArgs e)
        {
            var settings = new OptionsWindow();
            settings.ShowDialog();
        }

        private void MenuItemAbout_OnClick(object sender, RoutedEventArgs e)
        {
            var about = new AboutWindow();
            about.ShowDialog();
        }

        private void InputTextBox_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) PlaySoundpadButton_Click(sender, e);
        }

        private void SettingsCheckboxChanged(object sender, RoutedEventArgs e)
        {
            Settings.Default.Save();
        }

        private void ProviderHelpIcon_OnClick(object sender, RoutedEventArgs e)
        {
            var window = new OptionsWindow();
            window.ShowProviderTab();
        }

        private void MenuItemWebsite_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/medokin/soundpad-text-to-speech");
        }

        private void MenuItemReportAnIssue_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/medokin/soundpad-text-to-speech/issues/new");
        }

        private void MenuItemDonate_OnClick(object sender, RoutedEventArgs e) {
            Process.Start("https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=ZBEPDYES8CEH8");
        }

        public void BackupSettings()
        {
            try
            {
                var settingsFile = ConfigurationManager
                    .OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal).FilePath;
                var destination = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\..\\last.config";
                File.Copy(settingsFile, destination, true);
            }
            catch (Exception e) 
            {
            }
        }

        private void MenuItemLogs_OnClick(object sender, RoutedEventArgs e)
        {
            var window = new LogWindow();
            window.Show();
        }
    }
}