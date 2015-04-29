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

﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;


namespace Shield.Services
{

    /// <summary>
    /// Class to control the bluetooth connection to the Arduino.
    /// </summary>
    public class ConnectionManager
    {
        /// <summary>
        /// Socket used to communicate with Arduino.
        /// </summary>
        private StreamSocket socket;

        /// <summary>
        /// DataWriter used to send commands easily.
        /// </summary>
        private DataWriter dataWriter;

        /// <summary>
        /// DataReader used to receive messages easily.
        /// </summary>
        private DataReader dataReader;

        /// <summary>
        /// Thread used to keep reading data from socket.
        /// </summary>
        private bool isListening = true;

        /// <summary>
        /// Delegate used by event handler.
        /// </summary>
        /// <param name="message">The message received.</param>
        public delegate void CharReceivedHandler(char message);

        /// <summary>
        /// Event fired when a new message is received from Arduino.
        /// </summary>
        public event CharReceivedHandler CharReceived;

        /// <summary>
        /// Initialize the manager, should be called in OnNavigatedTo of main page.
        /// </summary>
        public void Initialize()
        {
            socket = new StreamSocket();
        }

        /// <summary>
        /// Finalize the connection manager, should be called in OnNavigatedFrom of main page.
        /// </summary>
        public void Terminate()
        {
            if (socket != null)
            {
                socket.Dispose();
            }

            isListening = false;
        }

        /// <summary>
        /// Connect to the given host device.
        /// </summary>
        /// <param name="deviceHostName">The host device name.</param>
        public async Task<bool> Connect(HostName deviceHostName)
        {
            if (socket != null)
            {
                try
                {
                    await socket.ConnectAsync(deviceHostName, "1");
                    dataReader = new DataReader(socket.InputStream);
                    Task.Run(() => { ReceiveMessages(); });
                    dataWriter = new DataWriter(socket.OutputStream);
                    return true;
                }
                catch (Exception)
                {
                    //ignore
                }
            }

            return false;
        }

        /// <summary>
        /// Receive messages from the Arduino through bluetooth.
        /// </summary>
        private async void ReceiveMessages()
        {
            try
            {
                while (isListening)
                {
                    // Read first byte (length of the subsequent message, 255 or less). 
                    uint sizeFieldCount = await dataReader.LoadAsync(1);
                    if (sizeFieldCount != 1)
                    {
                        // The underlying socket was closed before we were able to read the whole data. 
                        return;
                    }

                    // Read the message. 
                    uint val = dataReader.ReadByte();
                    if (val < 255)
                    {
                        CharReceived((char)val);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

        }

        /// <summary>
        /// Send command to the Arduino through bluetooth.
        /// </summary>
        /// <param name="command">The sent command.</param>
        /// <returns>The number of bytes sent</returns>
        public async Task<uint> SendCommand(string command)
        {
            uint sentCommandSize = 0;
            if (dataWriter != null)
            {
                //uint commandSize = dataWriter.MeasureString(command);
                dataWriter.WriteString("ABCDEFG");
                //dataWriter.WriteByte((byte)0x41);
                //dataWriter.WriteByte((byte)0x41);
                //sentCommandSize = dataWriter.WriteString(command);
                await dataWriter.StoreAsync();
                await dataWriter.FlushAsync();
            }
            return sentCommandSize;
        }

        /// <summary>
        /// Send command to the Arduino through bluetooth.
        /// </summary>
        /// <param name="command">The sent command.</param>
        /// <returns>The number of bytes sent</returns>
        public async Task<uint> SendCommand(byte b)
        {
            uint sentCommandSize = 0;
            if (dataWriter != null)
            {
                uint commandSize = 1;
                //dataWriter.WriteByte((byte)commandSize);
                dataWriter.WriteByte(b);
                await dataWriter.StoreAsync();
            }

            return 1;
        }
    }
}
