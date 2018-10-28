using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public interface ITextToSpeechProvider
    {
        string Name { get; }
        string FileExtension { get; }
        Task<Stream> SynthesizeTextToStreamAsync(IVoice voice, string text);
        Task<IList<IVoice>> GetVoicesAsync();
        bool IsAvailable { get; }
    }
}
