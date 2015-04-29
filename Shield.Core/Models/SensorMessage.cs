using System.Collections.Generic;

namespace Shield.Core.Models
{
    [Service("SENSORS")]
    public class SensorMessage : MessageBase
    {
        public List<SensorSwitches> Sensors { get; set; } 
    }
}