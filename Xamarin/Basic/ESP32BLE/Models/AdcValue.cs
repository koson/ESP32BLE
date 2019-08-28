using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ESP32BLE.Models
{
    public class AdcValue
    {
        public int Value { get; set; }
        public DateTime Time { get; set; }
        public int Tick { get; set; }
    }

    public class AdcValues : ObservableCollection<AdcValue>
    {

    }
}
