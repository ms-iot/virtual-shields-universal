using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Shield.Core.Models;

namespace Shield.Core
{
    public class Manager
    {
        public delegate void StringReceivedHandler(string message);
        public delegate void MessageReceivedHandler(MessageBase message);

        public event StringReceivedHandler StringReceived;
        public event MessageReceivedHandler MessageReceived;

        private int braceCount = 0;
        private bool quoted = false;
        private char currentQuoteChar = ' ';
        private char quotechar1 = '\'';
        private char quotechar2 = '\"';
        private bool isEscaped = false;
        private StringBuilder buffer = new StringBuilder();
        private long lastGoodMessage = 0;
        private long lastStartMessage = 0;
        private long maximumTimeToFullMessage = 1000;

        public void Test()
        {
            //OnStringReceived("{ 'Service': 'SMS', 'To': '+14255330004', 'Message': 'Abc' }");
            //OnStringReceived("{ 'Service': 'LCDT', 'Message': 'Hi There. Testing Line 0.', 'X': 0, 'Y': 0 }");
            //OnCharsReceived("{ 'Service': 'LCDT', 'Message': 'Testing Line 1', 'X': 0, 'Y': 1 }");
            //OnStringReceived("{ 'Service': 'SPEECH', 'Message': 'Hi There. Speech to text works.' }");
            //OnCharsReceived("{ 'Service': 'URL', 'Address': 'http://www.cnn.com' }");
            //OnCharsReceived("{ 'Service': 'SENSORS', 'Sensors': [ {'A' : true} ] }");
            //OnCharsReceived("{ 'Service': 'CAMERA' }");
            //OnCharsReceived("{ 'Service': 'RECOGNIZE' }");
            //OnCharsReceived("{ 'Service': 'RECORDAUDIO', 'Ms': 5000 }");
            OnStringReceived("{ 'Service': 'PLAY', 'Url': 'Videos:wildlife.mp4' }");
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
            else if (quoted && c == currentQuoteChar)
            {
                quoted = !quoted;
            }
            else if (!quoted && c == quotechar1)
            {
                quoted = true;
                currentQuoteChar = quotechar1;
            }
            else if (!quoted && c == quotechar2)
            {
                quoted = true;
                currentQuoteChar = quotechar2;
            }
            else if (!quoted && c == '{')
            {
                if (braceCount++ == 1)
                {
                    lastStartMessage = System.Environment.TickCount;
                }
            }
            else if (!quoted && c == '}')
            {
                if (--braceCount < 1)
                {
                    lastGoodMessage = System.Environment.TickCount;
                    braceCount = 0;
                    isComplete = true;
                }
                else if (lastStartMessage + maximumTimeToFullMessage > System.Environment.TickCount)
                {
                    //timeout of messages
                    lastGoodMessage = System.Environment.TickCount;
                    braceCount = 0;
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

        public void OnStringReceived(string message)
        {
            Debug.WriteLine(message);
            StringReceived?.Invoke(message);

            if (message.Length > 2)
            {
                //eliminate badly embedded characters if found
                message = message.Replace("{}", "");

                var msg = MessageFactory.FromMessage(message);
                if (msg != null)
                {
                    OnMessageReceived(msg);
                }
            }
        }

        private object messageLock = new object();

        protected void OnMessageReceived(MessageBase message)
        {
            // If the message was valid, invoke MessageReceived
            if (message != null)
            {
                MessageReceived?.Invoke(message);
            }
        }
    }
}
