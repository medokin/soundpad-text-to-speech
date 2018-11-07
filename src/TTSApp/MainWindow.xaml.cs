using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Common;
using Sentry;
using SoundpadConnector;
using SoundpadConnector.Response;
using Squirrel;
using TTSAmazonPolly;
using TTSApp.Properties;
using TTSGoogle;
using TTSUniversal;
using TTSWindows;

namespace TTSApp
{
    /// <summary>
    ///     Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public const string UpdateUrl = "https://soundpadcontrol.blob.core.windows.net/soundpad-tts";
        private static readonly SemaphoreSlim SemaphoreSlim = new SemaphoreSlim(1);
        private Soundpad _soundpad;


        public MainWindow()
        {
            InitializeComponent();
            RestoreSettings();
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
            await _soundpad.ConnectAsync();
        }

        private async Task CheckAndInstallUpdate()
        {
            try
            {
                UpdateSpinner.Visibility = Visibility.Visible;
                VersionTextBlock.Text = "Checking for Updates...";
                using (var mgr = new UpdateManager(UpdateUrl))
                {
                    var update = await mgr.CheckForUpdate(true);
                    VersionTextBlock.Text = update.CurrentlyInstalledVersion.Version.ToString();
                    if (update.CurrentlyInstalledVersion.EntryAsString != update.FutureReleaseEntry.EntryAsString)
                    {
                        VersionTextBlock.Text = "Installing Updates...";
                        await mgr.UpdateApp();
                        VersionTextBlock.Text = "Updates installed. Please restart.";
                        BackupSettings();
                    }
                }
            }
            catch (Exception)
            {
                VersionTextBlock.Text = "Error checking updates.";
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
            FooterTextBlock.Foreground = _soundpad.ConnectionStatus == ConnectionStatus.Connected
                ? new SolidColorBrush(Colors.Black)
                : new SolidColorBrush(Colors.Red);
        }

        private async Task Play(string text)
        {
            PlaySoundpadButton.IsEnabled = false;
            await SemaphoreSlim.WaitAsync();
            try
            {
                var uniqueId = Guid.NewGuid().ToString().Replace("-", "").Clip(10);


                // Sanitize Filename
                var fileName = Regex.Replace(text.Clip(20), @"[^0-9A-Za-z ,]", "_", RegexOptions.Compiled);
                fileName = $"{fileName}_{uniqueId}";
                var filePath = Path.GetTempPath() + $"{fileName}.{Model.SelectedProvider.FileExtension}";

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
            window.ShowDialog();
        }

        private void MenuItemWebsite_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/medokin/soundpad-text-to-speech");
        }

        private void MenuItemReportAnIssue_OnClick(object sender, RoutedEventArgs e)
        {
            Process.Start("https://github.com/medokin/soundpad-text-to-speech/issues/new");
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

        private void RestoreSettings()
        {
            //Restore settings after application update            
            var destFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal)
                .FilePath;
            var sourceFile = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\..\\last.config";
            // Check if we have settings that we need to restore
            if (!File.Exists(sourceFile)) return;
            // Create directory as needed
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(destFile));
            }
            catch (Exception)
            {
            }

            // Copy our backup file in place 
            try
            {
                File.Copy(sourceFile, destFile, true);
                Settings.Default.Reload();
            }
            catch (Exception)
            {
            }

            // Delete backup file
            try
            {
                File.Delete(sourceFile);
            }
            catch (Exception)
            {
            }
        }
    }

    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public ITextToSpeechProvider SelectedProvider { get; set; }
        public IList<ITextToSpeechProvider> ProviderList { get; set; }
        public IList<IVoice> VoiceList { get; set; }
        public IVoice SelectedVoice { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;

        private void ReloadProviderList()
        {
            ProviderList = new List<ITextToSpeechProvider>
            {
                new WindowsSpeechToTextProvider(),
                new UniversalTextToSpeechProvider(),
                new AmazonPollySpeechToTextProvider(Settings.Default.AmazonAccessKey, Settings.Default.AmazonSecretKey),
                new GoogleSpeechToTextProvider(Settings.Default.GoogleJson)
            };

            OnPropertyChanged(nameof(ProviderList));
        }

        [NotifyPropertyChangedInvocator]
        public virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void SetVoice(IVoice voice)
        {
            SelectedVoice = voice;

            Settings.Default.LastVoice = SelectedVoice.Name;
            Settings.Default.Save();

            OnPropertyChanged(nameof(SelectedVoice));
        }

        public async Task SetProvider(ITextToSpeechProvider item)
        {
            SelectedProvider = item;
            SelectedVoice = await DefaultVoice();

            Settings.Default.LastProvider = item.Name;
            Settings.Default.LastVoice = SelectedVoice.Name;
            Settings.Default.Save();

            OnPropertyChanged(nameof(SelectedProvider));
            OnPropertyChanged(nameof(SelectedVoice));
            OnPropertyChanged(nameof(VoiceList));
        }

        public async Task Reset()
        {
            ReloadProviderList();
            await SetProvider(await DefaultProvider());
        }

        public async Task<IVoice> DefaultVoice()
        {
            VoiceList = await SelectedProvider.GetVoicesAsync();
            var lastUsedVoice = VoiceList.FirstOrDefault(voice => Settings.Default.LastVoice == voice.Name);
            return lastUsedVoice ?? VoiceList.First();
        }

        public async Task<ITextToSpeechProvider> DefaultProvider()
        {
            //var tasks = ProviderList.Select(async provider => new
            // {
            //    Item = provider,
            //    IsAvailable = await provider.IsAvailable
            //});

            //var tuples = await Task.WhenAll(tasks);
            //var lastUsedProvider =
            //    tuples.Where(provider => provider.Item.Name == Settings.Default.LastProvider && provider.IsAvailable)
            //        .Select(arg => arg.Item).FirstOrDefault();


            var lastUsedProvider =
                ProviderList.FirstOrDefault(provider =>
                    provider.Name == Settings.Default.LastProvider && provider.IsAvailable);

            return lastUsedProvider ?? ProviderList.First();
        }
    }
}