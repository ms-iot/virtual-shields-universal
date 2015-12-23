// /*
//     Copyright(c) Microsoft Open Technologies, Inc. All rights reserved.
// 
//     The MIT License(MIT)
// 
//     Permission is hereby granted, free of charge, to any person obtaining a copy
//     of this software and associated documentation files(the "Software"), to deal
//     in the Software without restriction, including without limitation the rights
//     to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
//     copies of the Software, and to permit persons to whom the Software is
//     furnished to do so, subject to the following conditions :
// 
//     The above copyright notice and this permission notice shall be included in
//     all copies or substantial portions of the Software.
// 
//     THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//     IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//     FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
//     AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//     LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//     OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
//     THE SOFTWARE.
// */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using Windows.Web.Http;

namespace Shield.Communication
{
    public class BlobHelper : RESTHelper
    {
        // Constructor.

        public BlobHelper(string storageAccount, string storageKey)
            : base("http://" + storageAccount + ".blob.core.windows.net/", storageAccount, storageKey)
        {
        }


        public async Task<List<string>> ListContainers()
        {
            var containers = new List<string>();

            try
            {
                var request = CreateRESTRequest("GET", "?comp=list");
                var httpClient = new HttpClient();
                var response = await httpClient.SendRequestAsync(request);
                if ((int) response.StatusCode == 200)
                {
                    var inputStream = await response.Content.ReadAsInputStreamAsync();
                    var memStream = new MemoryStream();
                    var testStream = inputStream.AsStreamForRead();
                    await testStream.CopyToAsync(memStream);

                    memStream.Position = 0;
                    using (var reader = new StreamReader(memStream))
                    {
                        var result = reader.ReadToEnd();

                        var x = XElement.Parse(result);
                        foreach (var container in x.Element("Containers").Elements("Container"))
                        {
                            containers.Add(container.Element("Name").Value);
                            //Debug.WriteLine(container.Element("Name").Value);
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("Request: " + response.RequestMessage.ToString());
                    Debug.WriteLine("Response: " + response);
                }
                return containers;
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                return null;
            }
        }

        // Create a blob container. 
        // Return true on success, false if already exists, throw exception on error.

        public async Task<bool> CreateContainer(string container)
        {
            try
            {
                var request = CreateRESTRequest("PUT", container + "?restype=container");
                var httpClient = new HttpClient();
                var response = await httpClient.SendRequestAsync(request);

                if (response.StatusCode == HttpStatusCode.Created)
                {
                    return true;
                }
                Debug.WriteLine("ERROR: " + response.StatusCode + " - " + response.ReasonPhrase);
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return false;
                throw;
            }
        }

        //// List blobs in a container.
        public async Task<List<string>> ListBlobs(string container)
        {
            var blobs = new List<string>();

            try
            {
                var request = CreateRESTRequest("GET",
                    container + "?restype=container&comp=list&include=snapshots&include=metadata");
                var httpClient = new HttpClient();
                var response = await httpClient.SendRequestAsync(request);

                if ((int) response.StatusCode == 200)
                {
                    var inputStream = await response.Content.ReadAsInputStreamAsync();
                    var memStream = new MemoryStream();
                    var testStream = inputStream.AsStreamForRead();
                    await testStream.CopyToAsync(memStream);

                    memStream.Position = 0;
                    using (var reader = new StreamReader(memStream))
                    {
                        var result = reader.ReadToEnd();

                        var x = XElement.Parse(result);
                        foreach (var blob in x.Element("Blobs").Elements("Blob"))
                        {
                            blobs.Add(blob.Element("Name").Value);
                            //Debug.WriteLine(blob.Element("Name").Value);
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("ERROR: " + response.StatusCode + " - " + response.ReasonPhrase);
                    return null;
                }

                return blobs;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
                throw;
            }
        }

        // Retrieve the content of a blob. 
        // Return true on success, false if not found, throw exception on error.

        public async Task<string> GetBlob(string container, string blob)
        {
            string content = null;

            try
            {
                var request = CreateRESTRequest("GET", container + "/" + blob);
                var httpClient = new HttpClient();
                var response = await httpClient.SendRequestAsync(request);

                if ((int) response.StatusCode == 200)
                {
                    var inputStream = await response.Content.ReadAsInputStreamAsync();
                    var memStream = new MemoryStream();
                    var testStream = inputStream.AsStreamForRead();
                    await testStream.CopyToAsync(memStream);

                    memStream.Position = 0;
                    using (var reader = new StreamReader(memStream))
                    {
                        content = reader.ReadToEnd();
                    }

                    return content;
                }
                Debug.WriteLine("ERROR: " + response.StatusCode + " - " + response.ReasonPhrase);
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
                throw;
            }
        }

        public async Task<MemoryStream> GetBlobStream(string container, string blob)
        {
            try
            {
                var request = CreateRESTRequest("GET", container + "/" + blob);
                var httpClient = new HttpClient();
                var response = await httpClient.SendRequestAsync(request);

                if ((int) response.StatusCode == 200)
                {
                    var inputStream = await response.Content.ReadAsInputStreamAsync();
                    var memStream = new MemoryStream();
                    var testStream = inputStream.AsStreamForRead();
                    await testStream.CopyToAsync(memStream);

                    memStream.Position = 0;

                    return memStream;
                }
                Debug.WriteLine("ERROR: " + response.StatusCode + " - " + response.ReasonPhrase);
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
                throw;
            }
        }

        // Create or update a blob. 
        // Return true on success, false if not found, throw exception on error.

        public async Task<bool> PutBlob(string container, string blob, string content)
        {
            try
            {
                var headers = new Dictionary<string, string>();
                headers.Add("x-ms-blob-type", "BlockBlob");

                var request = CreateRESTRequest("PUT", container + "/" + blob, content, headers);
                var httpClient = new HttpClient();
                var response = await httpClient.SendRequestAsync(request);

                if (response.StatusCode == HttpStatusCode.Created)
                {
                    return true;
                }
                Debug.WriteLine("ERROR: " + response.StatusCode + " - " + response.ReasonPhrase);
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return false;
                throw;
            }
        }

        // Create or update a blob (Image). 
        // Return true on success, false if not found, throw exception on error.

        public async Task<bool> PutBlob(string container, string blob, MemoryStream content)
        {
            try
            {
                var headers = new Dictionary<string, string>();
                headers.Add("x-ms-blob-type", "BlockBlob");

                var request = CreateStreamRESTRequest("PUT", container + "/" + blob, content, headers);
                var httpClient = new HttpClient();
                var response = await httpClient.SendRequestAsync(request);

                if (response.StatusCode == HttpStatusCode.Created)
                {
                    return true;
                }
                Debug.WriteLine("ERROR: " + response.StatusCode + " - " + response.ReasonPhrase);
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return false;
                throw;
            }
        }

        // Retrieve a blob's properties.
        // Return true on success, false if not found, throw exception on error.

        public async Task<Dictionary<string, string>> GetBlobProperties(string container, string blob)
        {
            var propertiesList = new Dictionary<string, string>();

            try
            {
                var request = CreateStreamRESTRequest("HEAD", container + "/" + blob);
                var httpClient = new HttpClient();
                var response = await httpClient.SendRequestAsync(request);

                if ((int) response.StatusCode == 200)
                {
                    if (response.Headers != null)
                    {
                        foreach (var kvp in response.Headers)
                        {
                            propertiesList.Add(kvp.Key, kvp.Value);
                        }
                    }
                }
                else
                {
                    Debug.WriteLine("ERROR: " + response.StatusCode + " - " + response.ReasonPhrase);
                    return null;
                }

                return propertiesList;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
                throw;
            }
        }

        // Retrieve a blob's metadata.
        // Return true on success, false if not found, throw exception on error.

        public async Task<Dictionary<string, string>> GetBlobMetadata(string container, string blob)
        {
            var metadata = new Dictionary<string, string>();

            try
            {
                var request = CreateStreamRESTRequest("HEAD", container + "/" + blob + "?comp=metadata");
                var httpClient = new HttpClient();
                var response = await httpClient.SendRequestAsync(request);

                if ((int) response.StatusCode == 200)
                {
                    if (response.Headers != null)
                    {
                        foreach (var kvp in response.Headers)
                        {
                            if (kvp.Key.StartsWith("x-ms-meta-"))
                            {
                                metadata.Add(kvp.Key, kvp.Value);
                            }
                        }
                    }
                }

                return metadata;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return null;
                throw;
            }
        }
    }
}