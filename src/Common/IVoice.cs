using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Common {
    public interface IVoice {
        string Name { get; set; }
        string Language { get; set; }
        Gender Gender { get; set; }
    }
}
