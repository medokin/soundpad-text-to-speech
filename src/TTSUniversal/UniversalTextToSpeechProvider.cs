using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.SpeechSynthesis;
using Windows.Storage.Streams;
using Common;

namespace TTSUniversal
{
    /// <summary>
    /// HowTo Call UWP APIs: https://docs.microsoft.com/de-de/windows/uwp/porting/desktop-to-uwp-enhance
    /// </summary>
    public class UniversalTextToSpeechProvider : ITextToSpeechProvider
    {
        public string Name => "Universal";
        public string FileExtension => "wav";
        public bool IsAvailable => Task.Run(CheckAvailable).Result;

        public async Task<Stream> SynthesizeTextToStreamAsync(IVoice voice, string text)
        {
            using (var synth = new SpeechSynthesizer())
            {
                synth.Voice = SpeechSynthesizer.AllVoices.First(info => info.DisplayName == voice.Name);
                var synthStream = await synth.SynthesizeTextToStreamAsync(text);
                using (var reader = new DataReader(synthStream)) {
                    await reader.LoadAsync((uint)synthStream.Size);
                    var buffer = reader.ReadBuffer((uint)synthStream.Size);
                    var stream = buffer.AsStream();
                    return stream;
                }
            }
        }

        public Task<IList<IVoice>> GetVoicesAsync()
        {

            var voices = SpeechSynthesizer.AllVoices.Select(voice => new UniversalVoice()
                {
                    Language = voice.Language,
                    Name = voice.DisplayName,
                    Gender = (Gender) Enum.Parse(typeof(Gender), voice.Gender.ToString())
                })
                .Cast<IVoice>()
                .ToList();

            return Task.FromResult<IList<IVoice>>(voices);
        }

        public async Task<bool> CheckAvailable()
        {
            try
            {
                await GetVoicesAsync();
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }
    }

    public class UniversalVoice : IVoice
    {
        public string Name { get; set; }
        public string Language { get; set; }
        public Gender Gender { get; set; }
        public override string ToString()
        {
            return $"{Name} - {Language}";
        }
    }
}
