using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Common;
using TTSAmazonPolly;
using TTSApp.Properties;
using TTSGoogle;
using TTSUniversal;
using TTSWindows;

namespace TTSApp.Forms
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        public ITextToSpeechProvider SelectedProvider { get; set; }
        public IList<ITextToSpeechProvider> ProviderList { get; set; }
        public IList<IVoice> VoiceList { get; set; }
        public IVoice SelectedVoice { get; set; }
        public bool CanDeselectRemoveFile { get; set; }
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
            CanDeselectRemoveFile = Directory.Exists(Settings.Default.SaveFilePath);
            OnPropertyChanged(nameof(CanDeselectRemoveFile));

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
            var lastUsedProvider =
                ProviderList.FirstOrDefault(provider =>
                    provider.Name == Settings.Default.LastProvider && provider.IsAvailable);

            return lastUsedProvider ?? ProviderList.First();
        }
    }
}