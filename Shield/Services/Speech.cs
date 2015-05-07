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
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Globalization;
using Windows.Media.SpeechRecognition;
using Windows.Media.SpeechSynthesis;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.Web.Http;
using Shield.Core;
using Shield.Core.Models;

namespace Shield.Services
{
    public struct RecognizedSpeech
    {
        public string text;
        public int index;
        public string action;
        public int confidence;
        public SpeechRecognitionResultStatus status;
        internal int error;
    }

    public enum SpeechStatus
    {
        None,
        Listening,
        Stopped
    }

    public class SpeechArgs : EventArgs
    {
        public SpeechStatus Status { get; set; }
    }

    public class Speech
    {
        public delegate void SpeechStatusChangedHandler(object sender, SpeechArgs args);
        public event SpeechStatusChangedHandler SpeechStatusChanged;

        private SpeechRecognizer recognizer;
        private bool isRecognizing = false;
        private bool isUserStopped = false;

        public async void Speak(MediaElement audioPlayer, SpeechMessage speech)
        {
            var synth = new SpeechSynthesizer();
            var ttsStream = await synth.SynthesizeTextToStreamAsync(speech.Message);
            audioPlayer.SetSource(ttsStream, "");
            audioPlayer.CurrentStateChanged += async (object sender, Windows.UI.Xaml.RoutedEventArgs e) =>
            {
                await MainPage.Instance.SendResult(new ResultMessage(speech) { ResultId = (int)audioPlayer.CurrentState, Result = Enum.GetName(typeof(MediaElementState), audioPlayer.CurrentState) });
            };
        }

        public async Task<RecognizedSpeech> Recognize(string constraints, bool ui)
        {
            SpeechRecognitionGrammarFileConstraint grammarFileConstraint = null;
            var result = new RecognizedSpeech();
            bool isTable = false;
            Dictionary<string, string> dictionary = null;

            if (!string.IsNullOrWhiteSpace(constraints))
            {
                isTable = constraints.StartsWith("{table:");

                if (isTable)
                {
                    var name = constraints.Substring(7);
                    var i = name.IndexOf("}", StringComparison.CurrentCultureIgnoreCase);
                    name = name.Substring(0, i);

                    var constraintBuilder = new StringBuilder();
                    dictionary = MainPage.Instance.mainDictionary[name];

                    Debug.WriteLine("table "+name+" count=" + dictionary.Count);

                    foreach (var key in dictionary.Keys)
                    {
                        constraintBuilder.Append(key.Replace(","," "));
                        constraintBuilder.Append(",");
                    }

                    if (constraintBuilder.Length < 2)
                    {
                        result.error = -3;
                        return result;
                    }

                    constraints = constraintBuilder.ToString(0, constraintBuilder.Length - 1);
                    constraints = constraints.Replace(";", "-").Replace("&amp"," and ").Replace("&"," and ");
                }

                //build grammar constraints
                var grammarFileTemplate =
                    await
                        StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///GrammarConstraintTemplate.grxml"));

                const string wordTemplate = "<item>{0}</item>";
                const string itemTemplate = "<item><one-of>{0}</one-of><tag>out=\"{1}\";</tag></item>";

                var itemBuilder = new StringBuilder();
                var items = constraints.Split(';');
                string keyword = null;
                foreach (var itemPart in items)
                {
                    var item = itemPart;

                    var equals = item.IndexOf('=');
                    if (equals > -1)
                    {
                        keyword = item.Substring(0, equals);
                        item = item.Substring(equals + 1);
                    }

                    var words = item.Split(',');
                    var wordBuilder = new StringBuilder();
                    foreach (var word in words)
                    {
                        wordBuilder.AppendFormat(wordTemplate, word);
                    }

                    if (!string.IsNullOrWhiteSpace(keyword))
                    {
                        itemBuilder.AppendFormat(itemTemplate, wordBuilder, keyword);
                    }
                    else
                    {
                        itemBuilder.Append(wordBuilder);
                    }
                }

                var localFolder = ApplicationData.Current.LocalFolder;

                var grammarTemplate = await FileIO.ReadTextAsync(grammarFileTemplate);
                var grammarFile =
                    await
                        localFolder.CreateFileAsync("GrammarConstraint.grxml", CreationCollisionOption.ReplaceExisting);
                var finalGrammarText = string.Format(grammarTemplate, itemBuilder);
                await FileIO.WriteTextAsync(grammarFile, finalGrammarText);

                grammarFileConstraint = new SpeechRecognitionGrammarFileConstraint(grammarFile, "constraints");
            }

            if (isRecognizing && recognizer != null)
            {
                await recognizer.StopRecognitionAsync();
            }

            recognizer = new SpeechRecognizer();

            //if (recognizer != null)
            //{
            //}
            //else
            //{
            //    //recognizer.Constraints?.Clear();
            //    //await recognizer.CompileConstraintsAsync();
            //}

            if (grammarFileConstraint != null)
            {
                recognizer.Constraints.Add(grammarFileConstraint);
            }

            SpeechRecognitionResult recognize = null;

            try
            {
                isRecognizing = false;
                SpeechStatusChanged?.Invoke(this, new SpeechArgs { Status = SpeechStatus.None });

                await recognizer.CompileConstraintsAsync();

                isRecognizing = true;
                SpeechStatusChanged?.Invoke(this, new SpeechArgs { Status = SpeechStatus.Listening });

                recognize = await (ui ? recognizer.RecognizeWithUIAsync() : recognizer.RecognizeAsync());
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.GetType() + ":" + e.Message);

                if (recognize != null)
                {
                    result.status = recognize.Status;
                }

                result.confidence = 5;
                return result;
            }
            finally
            {
                isRecognizing = false;
                SpeechStatusChanged?.Invoke(this, new SpeechArgs { Status = isUserStopped ? SpeechStatus.Stopped : SpeechStatus.None });
            }

            result.status = isUserStopped ? SpeechRecognitionResultStatus.UserCanceled : recognize.Status;

            if (constraints == null)
            {
                result.text = recognize.Text;
                return result;
            }

            result.confidence = (int) recognize.Confidence;

            var text = recognize.Text.ToUpperInvariant();

            var items2 = constraints.Split(';');
            string keyword2 = null;
            var index = 1;
            foreach (var itemPart in items2)
            {
                var item = itemPart;

                var equals = item.IndexOf('=');
                if (equals > -1)
                {
                    keyword2 = item.Substring(0, equals);
                    item = item.Substring(equals + 1);
                }

                var words = item.Split(',');
                var innerIndex = 1;
                foreach (var word in words)
                {
                    if (word.ToUpperInvariant().Equals(text))
                    {
                        result.text = keyword2 ?? word;
                        if (isTable)
                        {
                            result.action = dictionary[result.text];
                        }

                        result.index = items2.Length == 1 ? innerIndex : index;
                        return result;
                    }

                    innerIndex++;
                }

                index++;
            }

            result.text = recognize.Text;
            return result;
        }

        public void Stop()
        {
            if (isRecognizing)
            {
                isRecognizing = false;
                isUserStopped = true;
                try
                {
                    recognizer?.StopRecognitionAsync();
                }
                catch (InvalidOperationException)
                {
                    //ignore invalid stops
                }
            }
        }
    }
}