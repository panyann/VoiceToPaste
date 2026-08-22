using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace VoiceToPaste.Models
{
    public class DGV_KeyWords
    {
        [DisplayName("Fraza w transkrypcji")]
        public string Key { get; set; } = "";

        [DisplayName("Zamień na")]
        public string Word { get; set; } = "";
    }
}
