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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shield.Communication.Destinations
{
    public class AzureBlob : IDestination
    {
        private string PREFIX = "BLOB://";
        private BlobHelper blobHelper;

        // Setup for Azure BLOB storage
        public string accountName { get; set; }
        public string accountKey { get; set; }

        string IDestination.PREFIX
        {
            get
            {
                return PREFIX;
            }
        }

        public AzureBlob(string accountName, string accountKey)
        {
            this.accountName = accountName;
            this.accountKey = accountKey;
            this.blobHelper = new BlobHelper(accountName, accountKey);
        }

        public string[] ParseAddress(string address)
        {
            string parsed = address.Substring(PREFIX.Length);
            return parsed.Split('/');
        }

        public string ParseAddressForFileName(string address)
        {
            string[] parsedSplit = ParseAddress(address);
            return parsedSplit[1];
        }

        public async Task<string> Send(MemoryStream memStream, string address)
        {
            string container = "";
            string blob = "";

            //Parse the BLOB Address
            string[] parsedSplit = ParseAddress(address);
            if (parsedSplit.Length == 2)
            {
                container = parsedSplit[0];
                blob = parsedSplit[1];
            }

            //stores the image in Azure BLOB Storage
            memStream.Position = 0;
            await blobHelper.PutBlob(container, blob, memStream);

            return "http://" + this.accountName + ".blob.core.windows.net/" + container + "/" + blob;
        }

        public bool CheckPrefix(string address)
        {
            return address.Contains(this.PREFIX);
        }
    }
}
