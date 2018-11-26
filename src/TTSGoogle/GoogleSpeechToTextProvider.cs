using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Common;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.TextToSpeech.V1;
using Grpc.Auth;
using Grpc.Core;
using log4net;


namespace TTSGoogle
{
    public class GoogleSpeechToTextProvider : ITextToSpeechProvider {

        private readonly string _secret;
        private static readonly ILog Log = LogManager.GetLogger(typeof(GoogleSpeechToTextProvider));
        public bool IsAvailable => Task.Run(CheckAvailable).Result;

        private TextToSpeechClient Client
        {
            get
            {
                var cred = GoogleCredential.FromJson(_secret);
                var channel = new Channel(
                    TextToSpeechClient.DefaultEndpoint.Host, TextToSpeechClient.DefaultEndpoint.Port, cred.ToChannelCredentials());
                return TextToSpeechClient.Create(channel);
            }
        }    

        public GoogleSpeechToTextProvider(string secret)
        {
            _secret = secret?.Replace("\\r\\n", "");
        }

        public string Name => "Google";
        public string FileExtension => "mp3";

        public async Task<Stream> SynthesizeTextToStreamAsync(IVoice voice, string text) {

            var input = new SynthesisInput {
                Text = text
            };

            var config = new AudioConfig {
                AudioEncoding = AudioEncoding.Mp3
            };

            var response = await Client.SynthesizeSpeechAsync(new SynthesizeSpeechRequest {
                Input = input,
                Voice = new VoiceSelectionParams()
                {
                    Name = voice.Name,
                    LanguageCode = voice.Language
                },
                AudioConfig = config,
            });

            return new MemoryStream(response.AudioContent.ToByteArray());
        }

        public async Task<IList<IVoice>> GetVoicesAsync()
        {
            var voices = await Client.ListVoicesAsync(new ListVoicesRequest());
            return voices.Voices.Select(voice => new GoogleVoice()
            {
                Language = voice.LanguageCodes.First(),
                Name = voice.Name,
                Gender = (Gender)Enum.Parse(typeof(Gender),
                voice.SsmlGender.ToString())
            }).Cast<IVoice>().ToList();
        }

        public async Task<bool> CheckAvailable()
        {
            try
            {
                await Client.ListVoicesAsync(new ListVoicesRequest());
                return true;
            }
            catch (Exception e)
            {
                Log.Error("Cannot connect to Google", e);
                return false;
            }
        }
    }

    public class GoogleVoice : IVoice {
        public string Name { get; set; }
        public string Language { get; set; }
        public Gender Gender { get; set; }
        public override string ToString() {
            return $"{Name}- {Gender}";
        }
    }
}
