using Common;
using Gender = Common.Gender;

namespace TTSAmazonPolly {
    public class AmazonPollyVoice : IVoice {
        public string Name { get; set; }
        public string Language { get; set; }
        public Gender Gender { get; set; }
        public override string ToString() {
            return $"{Name} - {Language}";
        }
    }
}
