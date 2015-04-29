using System;
using System.Linq;
using Windows.ApplicationModel.Chat;
using Windows.ApplicationModel.Contacts;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Shield.Services
{
    public class Sms
    {
        public void Send(Contact recipient,
            string messageBody,
            StorageFile attachmentFile,
            string mimeType)

        {
            var phone = recipient.Phones.FirstOrDefault();
            if (phone != null)
            {
                Send(phone.Number, messageBody, attachmentFile, mimeType);
            }
        }

        public async void Send(string phoneNumber,
            string messageBody,
            StorageFile attachmentFile,
            string mimeType)
        {
            var chat = new ChatMessage {Body = messageBody};

            if (attachmentFile != null)
            {
                var stream = RandomAccessStreamReference.CreateFromFile(attachmentFile);

                var attachment = new ChatMessageAttachment(
                    mimeType,
                    stream);

                chat.Attachments.Add(attachment);
            }

            chat.Recipients.Add(phoneNumber);

            await ChatMessageManager.ShowComposeSmsMessageAsync(chat);
        }
    }
}