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
namespace Shield.Services
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using System.Threading.Tasks;

    using Shield.Core.Models;

    using Windows.Media.SpeechRecognition;
    using Windows.Media.SpeechSynthesis;
    using Windows.Storage;
    using Windows.UI.Xaml;
    using Windows.UI.Xaml.Controls;
    using Windows.UI.Xaml.Media;

    public struct RecognizedSpeech
    {
        public string action;

        public int confidence;

        internal int error;

        public int index;

        public SpeechRecognitionResultStatus status;

        public string text;
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

        private bool isRecognizing;

        private bool isUserStopped;

        private SpeechRecognizer recognizer;

        public event SpeechStatusChangedHandler SpeechStatusChanged;

        public async void Speak(MediaElement audioPlayer, SpeechMessage speech)
        {
            var synth = new SpeechSynthesizer();
            var ttsStream = await synth.SynthesizeTextToStreamAsync(speech.Message);
            audioPlayer.SetSource(ttsStream, string.Empty);
            audioPlayer.CurrentStateChanged +=
                async (object sender, RoutedEventArgs e) =>
                    {
                        await
                            MainPage.Instance.SendResult(
                                new ResultMessage(speech)
                                    {
                                        ResultId = (int)audioPlayer.CurrentState, 
                                        Result =
                                            Enum.GetName(
                                                typeof(MediaElementState), 
                                                audioPlayer.CurrentState)
                                    });
                    };
        }

        public async Task<RecognizedSpeech> Recognize(string constraints, bool ui)
        {
            SpeechRecognitionGrammarFileConstraint grammarFileConstraint = null;
            var result = new RecognizedSpeech();
            var isTable = false;
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

                    Debug.WriteLine("table " + name + " count=" + dictionary.Count);

                    foreach (var key in dictionary.Keys)
                    {
                        constraintBuilder.Append(key.Replace(",", " "));
                        constraintBuilder.Append(",");
                    }

                    if (constraintBuilder.Length < 2)
                    {
                        result.error = -3;
                        return result;
                    }

                    constraints = constraintBuilder.ToString(0, constraintBuilder.Length - 1);
                    constraints = constraints.Replace(";", "-").Replace("&amp", " and ").Replace("&", " and ");
                }

                // build grammar constraints
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

            if (this.isRecognizing && this.recognizer != null)
            {
                await this.recognizer.StopRecognitionAsync();
            }

            this.recognizer = new SpeechRecognizer();

            // if (recognizer != null)
            // {
            // }
            // else
            // {
            // //recognizer.Constraints?.Clear();
            // //await recognizer.CompileConstraintsAsync();
            // }
            if (grammarFileConstraint != null)
            {
                this.recognizer.Constraints.Add(grammarFileConstraint);
            }

            SpeechRecognitionResult recognize = null;

            try
            {
                this.isRecognizing = false;
                this.SpeechStatusChanged?.Invoke(this, new SpeechArgs { Status = SpeechStatus.None });

                await this.recognizer.CompileConstraintsAsync();

                this.isRecognizing = true;
                this.SpeechStatusChanged?.Invoke(this, new SpeechArgs { Status = SpeechStatus.Listening });

                recognize = await (ui ? this.recognizer.RecognizeWithUIAsync() : this.recognizer.RecognizeAsync());
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
                this.isRecognizing = false;
                this.SpeechStatusChanged?.Invoke(
                    this, 
                    new SpeechArgs { Status = this.isUserStopped ? SpeechStatus.Stopped : SpeechStatus.None });
            }

            result.status = this.isUserStopped ? SpeechRecognitionResultStatus.UserCanceled : recognize.Status;

            if (constraints == null)
            {
                result.text = recognize.Text;
                return result;
            }

            result.confidence = (int)recognize.Confidence;

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
            if (this.isRecognizing)
            {
                this.isRecognizing = false;
                this.isUserStopped = true;
                try
                {
                    this.recognizer?.StopRecognitionAsync();
                }
                catch (InvalidOperationException)
                {
                    // ignore invalid stops
                }
            }
        }
    }
}