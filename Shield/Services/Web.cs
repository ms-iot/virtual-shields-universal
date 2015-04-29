using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Streams;
using Windows.Web.Http;
using Windows.Web.Http.Headers;
using Shield.Core.Models;

namespace Shield.Services
{
    public class Web
    {
        private MainPage mainPage;

        public Web(MainPage mainPage)
        {
            this.mainPage = mainPage;
        }

        public async Task RequestUrl(WebMessage web)
        {
            var request = new HttpClient();
            HttpResponseMessage response = null;

            request.DefaultRequestHeaders.Accept.Add(HttpMediaTypeWithQualityHeaderValue.Parse("text/html"));
            request.DefaultRequestHeaders.Accept.Add(HttpMediaTypeWithQualityHeaderValue.Parse("application/xhtml+xml"));
            request.DefaultRequestHeaders.Accept.Add(HttpMediaTypeWithQualityHeaderValue.Parse("application/xml"));
            request.DefaultRequestHeaders.Accept.Add(HttpMediaTypeWithQualityHeaderValue.Parse("application/json"));
            request.DefaultRequestHeaders.Accept.Add(HttpMediaTypeWithQualityHeaderValue.Parse("*/*"));

            request.DefaultRequestHeaders.UserAgent.Add(HttpProductInfoHeaderValue.Parse("Mozilla/5.0"));

            try
            {
                if (web.Action != null)
                {
                    switch (web.Action.ToUpperInvariant())
                    {
                        case "POST":
                            {
                                var content = new HttpStringContent(web.Data, UnicodeEncoding.Utf8, "application/json");

                                request.DefaultRequestHeaders.Accept.Add(
                                    new HttpMediaTypeWithQualityHeaderValue("application/json"));

                                response = await request.PostAsync(new Uri(web.Url), content);
                                break;
                            }

                        case "GET":
                            {
                                request.DefaultRequestHeaders.Accept.Add(
                                    new HttpMediaTypeWithQualityHeaderValue("application/json"));
                                response = await request.GetAsync(new Uri(web.Url));

                                break;
                            }
                    }

                    response?.EnsureSuccessStatusCode();
                }

                if (response != null)
                {
                    var result = new ResultMessage(web) { ResultId = (int)response.StatusCode, Type = 'W' };

                    if (!string.IsNullOrWhiteSpace(web.Parse) || web.Len > 0)
                    {
                        var temp = await response.Content.ReadAsStringAsync();

                        if (!string.IsNullOrWhiteSpace(web.Parse))
                        {
                            var parser = new Parser { Content = temp, Instructions = web.Parse };
                            if (parser.Parse())
                            {
                                if (parser.IsDictionary)
                                {
                                    mainPage.mainDictionary[parser.Tablename] = parser.Dictionary;
                                    temp = parser.Tablename;
                                    result.ResultId = parser.Dictionary.Count();
                                }
                                else
                                {
                                    temp = parser.Result;
                                }
                            }
                        }

                        result.Result = string.IsNullOrWhiteSpace(temp)
                            ? string.Empty
                            : temp.Substring(0, Math.Min(temp.Length, web.Len == 0 ? 200 : web.Len));
                    }

                    await mainPage.SendResult(result);
                }
            }
            catch (Exception e)
            {
                await mainPage.SendResult(new ResultMessage(web) { ResultId = e.HResult, Result = e.Message });
            }
        }

    }
}