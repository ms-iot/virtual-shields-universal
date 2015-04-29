using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Shields
{
    public static class MessageFactory
    {
        private static MessageBase Create<T>(string data) where T:MessageBase 
        {
            var result = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(data);
            result._Source = data;
            return result;
        }

        public static MessageBase FromMessage(string data)
        {
            MessageBase json;

            try
            {
                json = JsonConvert.DeserializeObject<MessageBase>(data);
            }
            catch (Exception e)
            {
                return null;
            }

            json._Source = data;

            switch (json.Service)
            {
                case "SMS" :
                    return Create<SmsMessage>(data);

                case "SMS2":
                    return Create<SmsMessage>(data);

                case "LCDT":
                case "LOG":
                    return Create<LcdTextMessage>(data);

                case "SPEECH":
                    return Create<SpeechMessage>(data);

                case "URL":
                    return Create<UrlMessage>(data);

                case "SENSORS":
                    return Create<SensorMessage>(data);
            }

            return json;
        } 
    }

    public class MessageBase
    {
        public string Service { get; set; }
        public bool Known { get; set; }

        public string _Source { get; set; }
        
    }

    public class SensorSwitches
    {
        public bool? A { get; set; }
        public bool? G { get; set; }
        public bool? M { get; set; }
        public bool? L { get; set; }

        public bool? Q { get; set; }
    }

    public class SensorMessage : MessageBase
    {
        public List<SensorSwitches> Sensors { get; set; } 
    }

    public class UrlMessage : MessageBase
    {
        public string Action { get; set; }
        public List<KeyValuePair<string, string>> Headers { get; set; }
        public string Address { get; set; }

        public string Message { get; set; }
    }

    public class SmsMessage : MessageBase
    {
        public string Address { get; set; }
        public string Message { get; set; }

        public SmsMessage()
        {
            this.Known = true;
        }
    }

    public class LcdTextMessage : MessageBase
    {
        public string Message { get; set; }
        public int? X { get; set; }
        public int? Y { get; set; }

        public string Action { get; set; }
    }

    public class SpeechMessage : MessageBase
    {
        public string Message { get; set; }
    }
}
