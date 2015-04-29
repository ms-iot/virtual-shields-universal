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
