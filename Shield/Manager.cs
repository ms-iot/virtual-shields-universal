using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Shields
{
    public class Manager
    {
        public delegate void StringReceivedHandler(string message);
        public delegate void MessageReceivedHandler(MessageBase message);

        public event StringReceivedHandler StringReceived;
        public event MessageReceivedHandler MessageReceived;

        private int braceCount = 0;
        private bool quoted = false;
        private char quotechar = '\'';
        private bool isEscaped = false;
        private StringBuilder buffer = new StringBuilder();

        public void Test()
        {
            //OnStringReceived("{ 'Service': 'SMS', 'Address': '+14255330004', 'Message': 'Abc' }");
            OnCharsReceived("{ 'Service': 'LCDT', 'Message': 'Hi There. Testing Line 0.', 'X': 0, 'Y': 0 }");
            //OnCharsReceived("{ 'Service': 'LCDT', 'Message': 'Testing Line 1', 'X': 0, 'Y': 1 }");
            //OnCharsReceived("{ 'Service': 'SPEECH', 'Message': 'Hi There. Speech to text works.' }");
            //OnCharsReceived("{ 'Service': 'URL', 'Address': 'http://www.cnn.com' }");
            //OnCharsReceived("{ 'Service': 'SENSORS', 'Sensors': [ {'A' : true} ] }");
            OnCharsReceived("{ 'Service': 'CAMERA' }");
        }

        public void OnCharsReceived(string part)
        {
            foreach (var c in part)
            {
                OnCharReceived(c);
            }
        }

        private void OnCharReceived(char c)
        {
            var isComplete = false;

            if (isEscaped)
            {
                isEscaped = false;
            }
            else if (c == quotechar)
            {
                quoted = !quoted;
            }
            else if (!quoted && c == '{')
            {
                braceCount++;
            }
            else if (!quoted && c == '}')
            {
                if (--braceCount < 1)
                {
                    braceCount = 0;
                    isComplete = true;
                }
            }
            else if (c == '\\')
            {
                isEscaped = true;
            }

            buffer.Append(c);
            if (isComplete)
            {
                OnStringReceived(buffer.ToString());
                buffer.Clear();
            }
        }

        protected void OnStringReceived(string message)
        {
            if (StringReceived != null)
            {
                StringReceived(message);
            }

            OnMessageReceived(MessageFactory.FromMessage(message));
        }

        protected void OnMessageReceived(MessageBase message)
        {
            if (MessageReceived != null)
            {
                MessageReceived(message);
            }
        }
    }
}
