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
