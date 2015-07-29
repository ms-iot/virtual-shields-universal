/*
    Copyright(c) Microsoft Open Technologies, Inc. All rights reserved.

    The MIT License(MIT)

    Permission is hereby granted, free of charge, to any person obtaining a copy
    of this software and associated documentation files(the "Software"), to deal
    in the Software without restriction, including without limitation the rights
    to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
    copies of the Software, and to permit persons to whom the Software is
    furnished to do so, subject to the following conditions :

    The above copyright notice and this permission notice shall be included in
    all copies or substantial portions of the Software.

    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
    IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
    FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
    AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
    LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
    OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
    THE SOFTWARE.
*/

using System.Diagnostics;
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
        private long lastCompleteMessage = System.Environment.TickCount;
        private bool isClosedMessage = false;

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
            var isPreClosedMessage = false;

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
                isPreClosedMessage = true;
                if (braceCount++ == 1)
                {
                    lastStartMessage = System.Environment.TickCount;
                }
            }
            else if (!quoted && c == '}')
            {
                if (isClosedMessage && buffer.Length > 1 && (System.Environment.TickCount - lastCompleteMessage > 1000*5))
                {
                    //reset
                    buffer.Clear();
                    braceCount = 0;
                    isClosedMessage = false;
                    return;
                }

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

            isClosedMessage = isPreClosedMessage;

            buffer.Append(c);
            if (isComplete)
            {
                OnStringReceived(buffer.ToString());
                buffer.Clear();
            }

            if (c == '}' && (System.Environment.TickCount - lastCompleteMessage > 1000 * 10))
            {
                //reset
                buffer.Clear();
                braceCount = 0;
                isClosedMessage = false;
                return;
            }
        }

        public void OnStringReceived(string message)
        {
            lastCompleteMessage = System.Environment.TickCount;
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
