using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Text;
using System.Threading.Tasks;
using Common;

namespace TTSWindows
{
    public class WindowsSpeechToTextProvider : ITextToSpeechProvider
    {
        public string Name => "Windows";
        bool ITextToSpeechProvider.IsAvailable => Task.Run(IsAvailable).Result;

        public async Task<Stream> SynthesizeTextToStreamAsync(IVoice voice, string text)
        {
            return await Task.Run<Stream>(() =>
            {
                using (var synth = new SpeechSynthesizer())
                {
                    synth.SelectVoice(voice.Name);
                    var stream = new MemoryStream();
                    synth.SetOutputToWaveStream(stream);
                    synth.Speak(text);
                    return stream;
                }
            });

        }

        public async Task<IList<IVoice>> GetVoicesAsync()
        {
            using (var synth = new SpeechSynthesizer()) {
                var voices = synth.GetInstalledVoices().Select(voice => new WindowsVoice()
                {
                    Gender = (Gender)Enum.Parse(typeof(Gender), voice.VoiceInfo.Gender.ToString()),
                    Language = voice.VoiceInfo.Culture.DisplayName,
                    Name = voice.VoiceInfo.Name
                }).Cast<IVoice>().ToList();

                return await Task.FromResult<IList<IVoice>>(voices);
            }
            
        }

        public Task<bool> IsAvailable()
        {
            return Task.FromResult(true);
        }
    }

    public class WindowsVoice : IVoice
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
