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
namespace Shield.Core
{
    using System;
    using System.Diagnostics;
    using System.Text;

    using Shield.Core.Models;

    public class Manager
    {
        public delegate void MessageReceivedHandler(MessageBase message);

        public delegate void StringReceivedHandler(string message);

        private readonly StringBuilder buffer = new StringBuilder();

        private readonly long maximumTimeToFullMessage = 1000;

        private readonly char quotechar1 = '\'';

        private readonly char quotechar2 = '\"';

        private int braceCount;

        private char currentQuoteChar = ' ';

        private bool isClosedMessage;

        private bool isEscaped;

        private long lastCompleteMessage = Environment.TickCount;

        private long lastGoodMessage;

        private long lastStartMessage;

        private object messageLock = new object();

        private bool quoted;

        public event StringReceivedHandler StringReceived;

        public event MessageReceivedHandler MessageReceived;

        public void Test()
        {
            // OnStringReceived("{ 'Service': 'SMS', 'To': '+14255330004', 'Message': 'Abc' }");
            // OnStringReceived("{ 'Service': 'LCDT', 'Message': 'Hi There. Testing Line 0.', 'X': 0, 'Y': 0 }");
            // OnCharsReceived("{ 'Service': 'LCDT', 'Message': 'Testing Line 1', 'X': 0, 'Y': 1 }");
            // OnStringReceived("{ 'Service': 'SPEECH', 'Message': 'Hi There. Speech to text works.' }");
            // OnCharsReceived("{ 'Service': 'URL', 'Address': 'http://www.cnn.com' }");
            // OnCharsReceived("{ 'Service': 'SENSORS', 'Sensors': [ {'A' : true} ] }");
            // OnCharsReceived("{ 'Service': 'CAMERA' }");
            // OnCharsReceived("{ 'Service': 'RECOGNIZE' }");
            // OnCharsReceived("{ 'Service': 'RECORDAUDIO', 'Ms': 5000 }");
            this.OnStringReceived("{ 'Service': 'PLAY', 'Url': 'Videos:wildlife.mp4' }");
        }

        public void OnCharsReceived(string part)
        {
            foreach (var c in part)
            {
                this.OnCharReceived(c);
            }
        }

        private void OnCharReceived(char c)
        {
            var isComplete = false;
            var isPreClosedMessage = false;

            if (this.isEscaped)
            {
                this.isEscaped = false;
            }
            else if (this.quoted && c == this.currentQuoteChar)
            {
                this.quoted = !this.quoted;
            }
            else if (!this.quoted && c == this.quotechar1)
            {
                this.quoted = true;
                this.currentQuoteChar = this.quotechar1;
            }
            else if (!this.quoted && c == this.quotechar2)
            {
                this.quoted = true;
                this.currentQuoteChar = this.quotechar2;
            }
            else if (!this.quoted && c == '{')
            {
                isPreClosedMessage = true;
                if (this.braceCount++ == 1)
                {
                    this.lastStartMessage = Environment.TickCount;
                }
            }
            else if (!this.quoted && c == '}')
            {
                if (this.isClosedMessage && this.buffer.Length > 1
                    && (Environment.TickCount - this.lastCompleteMessage > 1000 * 5))
                {
                    // reset
                    this.buffer.Clear();
                    this.braceCount = 0;
                    this.isClosedMessage = false;
                    return;
                }

                if (--this.braceCount < 1)
                {
                    this.lastGoodMessage = Environment.TickCount;
                    this.braceCount = 0;
                    isComplete = true;
                }
                else if (this.lastStartMessage + this.maximumTimeToFullMessage > Environment.TickCount)
                {
                    // timeout of messages
                    this.lastGoodMessage = Environment.TickCount;
                    this.braceCount = 0;
                }
            }
            else if (c == '\\')
            {
                this.isEscaped = true;
            }

            this.isClosedMessage = isPreClosedMessage;

            this.buffer.Append(c);
            if (isComplete)
            {
                this.OnStringReceived(this.buffer.ToString());
                this.buffer.Clear();
            }

            if (c == '}' && (Environment.TickCount - this.lastCompleteMessage > 1000 * 10))
            {
                // reset
                this.buffer.Clear();
                this.braceCount = 0;
                this.isClosedMessage = false;
            }
        }

        public void OnStringReceived(string message)
        {
            this.lastCompleteMessage = Environment.TickCount;
            Debug.WriteLine(message);
            this.StringReceived?.Invoke(message);

            if (message.Length > 2)
            {
                // eliminate badly embedded characters if found
                message = message.Replace("{}", string.Empty);

                var msg = MessageFactory.FromMessage(message);
                if (msg != null)
                {
                    this.OnMessageReceived(msg);
                }
            }
        }

        protected void OnMessageReceived(MessageBase message)
        {
            // If the message was valid, invoke MessageReceived
            if (message != null)
            {
                this.MessageReceived?.Invoke(message);
            }
        }
    }
}