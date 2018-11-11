using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Common;
using IBM.WatsonDeveloperCloud.TextToSpeech.v1;
using IBM.WatsonDeveloperCloud.TextToSpeech.v1.Model;
using IBM.WatsonDeveloperCloud.Util;

namespace TTSWatson
{
    public class WatsonTextToSpeechProvider : ITextToSpeechProvider
    {
        private readonly string _apiKey;
        private readonly string _serviceUrl;
        public string Name => "IBM Watson";
        public string FileExtension => "mp3";

        private TextToSpeechService Service => new TextToSpeechService(new TokenOptions() {
            IamApiKey = _apiKey,
            ServiceUrl = _serviceUrl
        });

        public WatsonTextToSpeechProvider(string apiKey, string serviceUrl)
        {
            _apiKey = apiKey;
            _serviceUrl = serviceUrl;
        }

        public Task<Stream> SynthesizeTextToStreamAsync(IVoice voice, string text)
        {
            
            var result = Service.Synthesize(new Text()
            {
                _Text = text
            }, "audio/mp3", voice.Name);

            return Task.FromResult((Stream)result);
        }

        public Task<IList<IVoice>> GetVoicesAsync()
        {
            var result = Service.ListVoices()._Voices.Select(voice => new WatsonVoice()
            {
                Gender = GetGender(voice.Gender),
                Language = voice.Language,
                Name = voice.Name
            }).Cast<IVoice>().ToList();

            return Task.FromResult((IList<IVoice>)result);
        }

        private Gender GetGender(string gender)
        {
            switch (gender)
            {
                case "female":
                    return Gender.Female;
                default:
                    return Gender.Male;
            }
        }

        public bool IsAvailable => Task.Run(CheckAvailable).Result;

        private async Task<bool> CheckAvailable() {
            try
            {
                await GetVoicesAsync();
                return true;
            } catch (Exception e) {
                return false;
            }
        }
    }

    public class WatsonVoice : IVoice
    {
        public string Name { get; set; }
        public string Language { get; set; }
        public Gender Gender { get; set; }

        public override string ToString()
        {
            return Name;
        }
    }
}
