using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Common;
using SoundpadRemote;
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
        private SoundpadRemoteControl _soundpad;


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
            _soundpad = new SoundpadRemoteControl {
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

        private async Task Play()
        {
            PlaySoundpadButton.IsEnabled = false;
            await SemaphoreSlim.WaitAsync();
            try
            {
                var fileName = Regex.Replace(InputTextBox.Text, @"[^0-9A-Za-z ,]", "_", RegexOptions.Compiled);
                var filePath = Path.GetTempPath() + $"{fileName}.{Model.SelectedProvider.FileExtension}";

                var stream =
                    await Model.SelectedProvider.SynthesizeTextToStreamAsync(Model.SelectedVoice, InputTextBox.Text);

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
                    if (result.Result != countResult.Result)
                    {
                        count = (int) result.Result;
                        break;
                    }
                }

                await _soundpad.PlaySound(count, Settings.Default.PlayRenderLine, Settings.Default.PlayCaptureLine);
                while (true)
                {
                    var status = await _soundpad.GetPlayStatus();
                    if (status.PlayStatus == PlayStatus.Playing) break;
                }


                if (DeleteFromSoundpadCheckbox.IsChecked.HasValue && DeleteFromSoundpadCheckbox.IsChecked.Value)
                {
                    await _soundpad.DoSelectIndex(count + 1);
                    await Task.Delay(100);
                    await _soundpad.RemoveSelectedEntries();
                }
            }
            catch (Exception)
            {
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

            InputTextBox.Focus();
            await Play();
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

        private void MenuItemAbout_OnClick(object sender, RoutedEventArgs e) {
            var about = new AboutWindow();
            about.ShowDialog();
        }

        private async void InputTextBox_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                await Play();
            }
        }

        private void SettingsCheckboxChanged(object sender, RoutedEventArgs e) {
            Settings.Default.Save();
        }

        private void ProviderHelpIcon_OnClick(object sender, RoutedEventArgs e)
        {
            var window = new OptionsWindow();
            window.ShowDialog();
        }

        private void MenuItemWebsite_OnClick(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/medokin/soundpad-text-to-speech");
        }

        private void MenuItemReportAnIssue_OnClick(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/medokin/soundpad-text-to-speech/issues/new");
        }
    }

    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public MainWindowViewModel()
        {
        }

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

        public ITextToSpeechProvider SelectedProvider { get; set; }
        public IList<ITextToSpeechProvider> ProviderList { get; set; }
        public IList<IVoice> VoiceList { get; set; }
        public IVoice SelectedVoice { get; set; }
        public event PropertyChangedEventHandler PropertyChanged;

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
                ProviderList.FirstOrDefault(provider => provider.Name == Settings.Default.LastProvider && provider.IsAvailable);

            return lastUsedProvider ?? ProviderList.First();
        }
    }
}