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
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.IO;
using Windows.Web;
using System.Xml;
using System.Xml.Linq;

using Windows.Web.Http;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Shield.Communication
{
    public class BlobHelper : RESTHelper
    {
        // Constructor.
    
        public BlobHelper(string storageAccount, string storageKey) : base("http://" + storageAccount + ".blob.core.windows.net/", storageAccount, storageKey)
        {
        }
    
    
        public async Task<List<string>> ListContainers()
        {
            List<string> containers = new List<string>();
    
            try
            {
                HttpRequestMessage request = CreateRESTRequest("GET", "?comp=list");
                HttpClient httpClient = new HttpClient();
                HttpResponseMessage response = await httpClient.SendRequestAsync(request);
                if ((int)response.StatusCode == 200)
                {
                    var inputStream = await response.Content.ReadAsInputStreamAsync();
                    var memStream = new MemoryStream();
                    Stream testStream = inputStream.AsStreamForRead();
                    await testStream.CopyToAsync(memStream);
    
                    memStream.Position = 0;
                    using (StreamReader reader = new StreamReader(memStream))
                    {
                        string result = reader.ReadToEnd();
    
                        XElement x = XElement.Parse(result);
                        foreach (XElement container in x.Element("Containers").Elements("Container"))
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
                HttpRequestMessage request = CreateRESTRequest("PUT", container + "?restype=container");
                HttpClient httpClient = new HttpClient();
                HttpResponseMessage response = await httpClient.SendRequestAsync(request);
    
                if (response.StatusCode == HttpStatusCode.Created)
                {
                    return true;
                }
                else
                {
                    Debug.WriteLine("ERROR: " + response.StatusCode + " - " + response.ReasonPhrase);
                    return false;
                }
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
            List<string> blobs = new List<string>();
    
            try
            {
                HttpRequestMessage request = CreateRESTRequest("GET", container + "?restype=container&comp=list&include=snapshots&include=metadata");
                HttpClient httpClient = new HttpClient();
                HttpResponseMessage response = await httpClient.SendRequestAsync(request);
    
                if ((int)response.StatusCode == 200)
                {
                    var inputStream = await response.Content.ReadAsInputStreamAsync();
                    var memStream = new MemoryStream();
                    Stream testStream = inputStream.AsStreamForRead();
                    await testStream.CopyToAsync(memStream);
    
                    memStream.Position = 0;
                    using (StreamReader reader = new StreamReader(memStream))
                    {
                        string result = reader.ReadToEnd();
    
                        XElement x = XElement.Parse(result);
                        foreach (XElement blob in x.Element("Blobs").Elements("Blob"))
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
                HttpRequestMessage request = CreateRESTRequest("GET", container + "/" + blob);
                HttpClient httpClient = new HttpClient();
                HttpResponseMessage response = await httpClient.SendRequestAsync(request);
    
                if ((int)response.StatusCode == 200)
                {
                    var inputStream = await response.Content.ReadAsInputStreamAsync();
                    var memStream = new MemoryStream();
                    Stream testStream = inputStream.AsStreamForRead();
                    await testStream.CopyToAsync(memStream);
    
                    memStream.Position = 0;
                    using (StreamReader reader = new StreamReader(memStream))
                    {
                        content = reader.ReadToEnd();
                    }
    
                    return content;
                }
                else
                {
                    Debug.WriteLine("ERROR: " + response.StatusCode + " - " + response.ReasonPhrase);
                    return null;
                }
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
                HttpRequestMessage request = CreateRESTRequest("GET", container + "/" + blob);
                HttpClient httpClient = new HttpClient();
                HttpResponseMessage response = await httpClient.SendRequestAsync(request);
    
                if ((int)response.StatusCode == 200)
                {
                    var inputStream = await response.Content.ReadAsInputStreamAsync();
                    var memStream = new MemoryStream();
                    Stream testStream = inputStream.AsStreamForRead();
                    await testStream.CopyToAsync(memStream);
    
                    memStream.Position = 0;
    
                    return memStream;
                }
                else
                {
                    Debug.WriteLine("ERROR: " + response.StatusCode + " - " + response.ReasonPhrase);
                    return null;
                }
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
                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("x-ms-blob-type", "BlockBlob");
    
                HttpRequestMessage request = CreateRESTRequest("PUT", container + "/" + blob, content, headers);
                HttpClient httpClient = new HttpClient();
                HttpResponseMessage response = await httpClient.SendRequestAsync(request);
    
                if (response.StatusCode == HttpStatusCode.Created)
                {
                    return true;
                }
                else
                {
                    Debug.WriteLine("ERROR: " + response.StatusCode + " - " + response.ReasonPhrase);
                    return false;
                }
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
                Dictionary<string, string> headers = new Dictionary<string, string>();
                headers.Add("x-ms-blob-type", "BlockBlob");
    
                HttpRequestMessage request = CreateStreamRESTRequest("PUT", container + "/" + blob, content, headers);
                HttpClient httpClient = new HttpClient();
                HttpResponseMessage response = await httpClient.SendRequestAsync(request);
    
                if (response.StatusCode == HttpStatusCode.Created)
                {
                    return true;
                }
                else
                {
                    Debug.WriteLine("ERROR: " + response.StatusCode + " - " + response.ReasonPhrase);
                    return false;
                }
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
            Dictionary<string, string> propertiesList = new Dictionary<string, string>();
    
            try
            {
                HttpRequestMessage request = CreateStreamRESTRequest("HEAD", container + "/" + blob);
                HttpClient httpClient = new HttpClient();
                HttpResponseMessage response = await httpClient.SendRequestAsync(request);
    
                if ((int)response.StatusCode == 200)
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
            Dictionary<string, string> metadata = new Dictionary<string, string>();
    
            try
            {
                HttpRequestMessage request = CreateStreamRESTRequest("HEAD", container + "/" + blob + "?comp=metadata");
                HttpClient httpClient = new HttpClient();
                HttpResponseMessage response = await httpClient.SendRequestAsync(request);
    
                if ((int)response.StatusCode == 200)
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
