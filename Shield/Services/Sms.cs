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
    using System.Linq;

    using Windows.ApplicationModel.Chat;
    using Windows.ApplicationModel.Contacts;
    using Windows.Storage;
    using Windows.Storage.Streams;

    public class Sms
    {
        public void Send(Contact recipient, string messageBody, StorageFile attachmentFile, string mimeType)
        {
            var phone = recipient.Phones.FirstOrDefault();
            if (phone != null)
            {
                this.Send(phone.Number, messageBody, attachmentFile, mimeType);
            }
        }

        public async void Send(string phoneNumber, string messageBody, StorageFile attachmentFile, string mimeType)
        {
            var chat = new ChatMessage { Body = messageBody };

            if (attachmentFile != null)
            {
                var stream = RandomAccessStreamReference.CreateFromFile(attachmentFile);

                var attachment = new ChatMessageAttachment(mimeType, stream);

                chat.Attachments.Add(attachment);
            }

            chat.Recipients.Add(phoneNumber);

            await ChatMessageManager.ShowComposeSmsMessageAsync(chat);
        }
    }
}