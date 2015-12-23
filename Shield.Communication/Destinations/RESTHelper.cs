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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.Core;
using Windows.Web.Http;
using Windows.Web.Http.Headers;

namespace Shield.Communication
{
    public class RESTHelper
    {
        public RESTHelper(string endpoint, string storageAccount, string storageKey)
        {
            Endpoint = endpoint;
            StorageAccount = storageAccount;
            StorageKey = storageKey;
        }

        protected bool IsTableStorage { get; set; }

        public string Endpoint { get; internal set; }

        public string StorageAccount { get; internal set; }

        public string StorageKey { get; internal set; }

        //{

        //public static T Retry<T>(RetryDelegate<T> del)

        //// Retry delegate with default retry settings.
        //const int retryIntervalMS = 200;

        //const int retryCount = 3;
        //public delegate void RetryDelegate();

        //public delegate T RetryDelegate<T>();

        //#region Retry Delegate

        #region REST HTTP Request Helper Methods

        // Construct and issue a REST request and return the response.

        public HttpRequestMessage CreateRESTRequest(string method, string resource, string requestBody = null,
            Dictionary<string, string> headers = null,
            string ifMatch = "", string md5 = "")
        {
            byte[] byteArray = null;
            var now = DateTime.UtcNow;
            var uri = new Uri(Endpoint + resource);
            var httpMethod = new HttpMethod(method);
            var contentLength = 0;

            var httpClient = new HttpClient();
            var request = new HttpRequestMessage(httpMethod, uri);
            request.Headers.Add("x-ms-date", now.ToString("R", CultureInfo.InvariantCulture));
            request.Headers.Add("x-ms-version", "2009-09-19");
            //Debug.WriteLine(now.ToString("R", System.Globalization.CultureInfo.InvariantCulture));

            if (IsTableStorage)
            {
                request.Content.Headers.ContentType = new HttpMediaTypeHeaderValue("application/atom+xml");

                request.Headers.Add("DataServiceVersion", "1.0;NetFx");
                request.Headers.Add("MaxDataServiceVersion", "1.0;NetFx");
            }

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            if (!string.IsNullOrEmpty(requestBody))
            {
                request.Headers.Add("Accept-Charset", "UTF-8");

                byteArray = Encoding.UTF8.GetBytes(requestBody);
                var stream = new MemoryStream(byteArray);
                var streamContent = stream.AsInputStream();
                var content = new HttpStreamContent(streamContent);
                request.Content = content;

                contentLength = byteArray.Length;
            }

            var authorizationHeader = AuthorizationHeader(method, now, request, contentLength, ifMatch, md5);
            request.Headers.Authorization = authorizationHeader;

            return request;
        }

        public HttpRequestMessage CreateStreamRESTRequest(string method, string resource,
            MemoryStream requestBody = null, Dictionary<string, string> headers = null,
            string ifMatch = "", string md5 = "")
        {
            var now = DateTime.UtcNow;
            var uri = new Uri(Endpoint + resource);
            var httpMethod = new HttpMethod(method);
            long contentLength = 0;

            var httpClient = new HttpClient();
            var request = new HttpRequestMessage(httpMethod, uri);
            request.Headers.Add("x-ms-date", now.ToString("R", CultureInfo.InvariantCulture));
            request.Headers.Add("x-ms-version", "2009-09-19");
            //Debug.WriteLine(now.ToString("R", System.Globalization.CultureInfo.InvariantCulture));

            if (IsTableStorage)
            {
                request.Content.Headers.ContentType = new HttpMediaTypeHeaderValue("application/atom+xml");

                request.Headers.Add("DataServiceVersion", "1.0;NetFx");
                request.Headers.Add("MaxDataServiceVersion", "1.0;NetFx");
            }

            if (null != headers)
            {
                foreach (var header in headers)
                {
                    request.Headers.Add(header.Key, header.Value);
                }
            }

            if (null != requestBody)
            {
                request.Headers.Add("Accept-Charset", "UTF-8");

                var streamContent = requestBody.AsInputStream();
                var content = new HttpStreamContent(streamContent);
                request.Content = content;

                contentLength = requestBody.Length;
            }

            var authorizationHeader = AuthorizationHeader(method, now, request, contentLength, ifMatch, md5);
            request.Headers.Authorization = authorizationHeader;

            return request;
        }


        // Generate an authorization header.

        public HttpCredentialsHeaderValue AuthorizationHeader(string method, DateTime now, HttpRequestMessage request,
            long contentLength, string ifMatch = "", string md5 = "")
        {
            string MessageSignature;

            if (IsTableStorage)
            {
                MessageSignature = string.Format("{0}\n\n{1}\n{2}\n{3}",
                    method,
                    "application/atom+xml",
                    now.ToString("R", CultureInfo.InvariantCulture),
                    GetCanonicalizedResource(request.RequestUri, StorageAccount)
                    );
            }
            else
            {
                MessageSignature = string.Format("{0}\n\n\n{1}\n{5}\n\n\n\n{2}\n\n\n\n{3}{4}",
                    method,
                    (method == "GET" || method == "HEAD") ? string.Empty : contentLength.ToString(),
                    ifMatch,
                    GetCanonicalizedHeaders(request),
                    GetCanonicalizedResource(request.RequestUri, StorageAccount),
                    md5
                    );
            }

            //Debug.WriteLine(MessageSignature);
            var key = CryptographicBuffer.DecodeFromBase64String(StorageKey);
            var msg = CryptographicBuffer.ConvertStringToBinary(MessageSignature, BinaryStringEncoding.Utf8);

            var objMacProv = MacAlgorithmProvider.OpenAlgorithm(MacAlgorithmNames.HmacSha256);
            //CryptographicKey cryptKey = objMacProv.CreateKey(key);
            //var buff = CryptographicEngine.Sign(cryptKey, msg);
            var hash = objMacProv.CreateHash(key);
            hash.Append(msg);

            var authorizationHeader = new HttpCredentialsHeaderValue("SharedKey",
                StorageAccount + ":" + CryptographicBuffer.EncodeToBase64String(hash.GetValueAndReset()));
            //Debug.WriteLine(authorizationHeader.ToString());
            return authorizationHeader;
        }

        // Get canonicalized headers.

        public string GetCanonicalizedHeaders(HttpRequestMessage request)
        {
            var headerNameList = new List<string>();
            var sb = new StringBuilder();
            foreach (var headerName in request.Headers.Keys)
            {
                if (headerName.ToLowerInvariant().StartsWith("x-ms-", StringComparison.Ordinal))
                {
                    headerNameList.Add(headerName.ToLowerInvariant());
                }
            }
            headerNameList.Sort();
            foreach (var headerName in headerNameList)
            {
                var builder = new StringBuilder(headerName);
                var separator = ":";
                foreach (var headerValue in GetHeaderValues(request.Headers, headerName))
                {
                    var trimmedValue = headerValue.Replace("\r\n", string.Empty);
                    builder.Append(separator);
                    builder.Append(trimmedValue);
                    separator = ",";
                }
                sb.Append(builder);
                sb.Append("\n");
            }
            return sb.ToString();
        }

        // Get header values.

        public List<string> GetHeaderValues(HttpRequestHeaderCollection headers, string headerName)
        {
            var list = new List<string>();

            var headerList = headers.ToList();
            var values = headerList.Where(kvp => kvp.Key == headerName).Select(kvp => kvp.Value).Distinct().ToList();
            foreach (var str in values)
            {
                list.Add(str.TrimStart(null));
            }
            return list;
        }

        // Get canonicalized resource.

        public string GetCanonicalizedResource(Uri address, string accountName)
        {
            var str = new StringBuilder();
            var builder = new StringBuilder("/");
            builder.Append(accountName);
            builder.Append(address.AbsolutePath);
            str.Append(builder);
            var values2 = new Dictionary<string, string>();

            if (!IsTableStorage)
            {
                var values = new List<KeyValuePair<string, string>>();

                // Split the address query string into parts
                var querySegments = address.Query.Split('&');
                foreach (var segment in querySegments)
                {
                    var parts = segment.Split('=');
                    if (parts.Length > 1)
                    {
                        var key = parts[0].Trim('?', ' ');
                        var val = parts[1].Trim();

                        values.Add(new KeyValuePair<string, string>(key, val));
                    }
                }

                foreach (var str2 in values.Select(kvp => kvp.Key).Distinct())
                {
                    var list = values.Where(kvp => kvp.Key == str2).Select(kvp => kvp.Value).ToList();
                    list.Sort();
                    var builder2 = new StringBuilder();
                    foreach (object obj2 in list)
                    {
                        if (builder2.Length > 0)
                        {
                            builder2.Append(",");
                        }
                        builder2.Append(obj2);
                    }
                    values2.Add((str2 == null) ? str2 : str2.ToLowerInvariant(), builder2.ToString());
                }
            }
            var list2 = new List<string>(values2.Keys);
            list2.Sort();
            foreach (var str3 in list2)
            {
                var builder3 = new StringBuilder(string.Empty);
                builder3.Append(str3);
                builder3.Append(":");
                builder3.Append(values2[str3]);
                str.Append("\n");
                str.Append(builder3);
            }
            return str.ToString();
        }

        #endregion

        //    return Retry<T>(del, retryCount, retryIntervalMS);
        //}

        //// Retry delegate.

        //public static T Retry<T>(RetryDelegate<T> del, int numberOfRetries, int msPause)
        //{
        //    int counter = 0;
        //    RetryLabel:

        //    try
        //    {
        //        counter++;
        //        return del.Invoke();
        //    }
        //    catch (Exception ex)
        //    {
        //        if (counter > numberOfRetries)
        //        {
        //            throw ex;
        //        }
        //        else
        //        {
        //            if (msPause > 0)
        //            {
        //                Thread.Sleep(msPause);
        //            }
        //            goto RetryLabel;
        //        }
        //    }
        //}


        //// Retry delegate with default retry settings.

        //public static bool Retry(RetryDelegate del)
        //{
        //    return Retry(del, retryCount, retryIntervalMS);
        //}


        //public static bool Retry(RetryDelegate del, int numberOfRetries, int msPause)
        //{
        //    int counter = 0;

        //    RetryLabel:
        //    try
        //    {
        //        counter++;
        //        del.Invoke();
        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        if (counter > numberOfRetries)
        //        {
        //            throw ex;
        //        }
        //        else
        //        {
        //            if (msPause > 0)
        //            {
        //                Thread.Sleep(msPause);
        //            }
        //            goto RetryLabel;
        //        }
        //    }
        //}

        //#endregion
    }
}