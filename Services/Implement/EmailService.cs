using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UTechEmailGateway.Services.Interface;
using UTechEmailGateway.Models;
using System.Net;
using System.Net.Mail;
using System.Net.Security;
using OpenPop.Pop3;
//using System.Windows.Forms;
using OpenPop.Mime;
using System.IO;
using System.Threading.Tasks;

namespace UTechEmailGateway.Services.Implement
{
    public class EmailService : IEmailService
    {
        public const string PopServerHost = "secureus40.sgcpanel.com";
        public const UInt16 port = 995;
        public const string username = "catch-all@utechusa.us";
        public const string password = "Tr@d3sh0w";
        public const string CRLF = "\r\n";

        Pop3Client client;

        public EmailService()
        {
            //client = new Pop3Client();
            // Connect to the server
            //client.Connect(PopServerHost, port, true);

            //// Authenticate ourselves towards the server
            //client.Authenticate(username, password);
        }

        public bool Send(EmailEntity emailEntity)
        {
            SmtpClient smtpServer;
            MailMessage mailMessage;
            if (string.IsNullOrWhiteSpace(emailEntity.ToUserEmail))
            {
                throw new ArgumentNullException("ToUserEmail");
            }

            if (string.IsNullOrWhiteSpace(emailEntity.EmailBodyText) && string.IsNullOrWhiteSpace(emailEntity.EmailBodyHtml))
            {
                //throw new ArgumentNullException("EmailBodyText & EmailBodyHtml");
            }
            string smtpAddress = PopServerHost; // "smtp.mail.yahoo.com";
            int portNumber = 587; // 465;
            bool enableSSL = true;

            //Create SMTP client, the configration is in the web.config file, system.net/mailSettings/smtp
            smtpServer = new SmtpClient(smtpAddress, portNumber);
            //Yahoo!	smtp.mail.yahoo.com	587	Yes
            //GMail	    smtp.gmail.com	    587	Yes
            //Hotmail	smtp.live.com	    587	Yes
            //smtpServer.Host = WebConfigurationManager.AppSettings["SmtpHost"].ToString();
            smtpServer.Credentials = new NetworkCredential(username, password);
            //smtpServer.UseDefaultCredentials = false;
            smtpServer.EnableSsl = enableSSL;
            smtpServer.DeliveryMethod = SmtpDeliveryMethod.Network;
            //smtp.Send(mail);

            //Generate Email Message based on passed in informatin
            mailMessage = new MailMessage();

            //If From Email is not , then setup the from email, else use the one from web.config system.net/mailSettings/smtp/from
            if (!string.IsNullOrWhiteSpace(emailEntity.FromUserEmail))
            {
                mailMessage.From = new MailAddress(emailEntity.FromUserEmail, emailEntity.FromUserDisplayName);
            }
            else
            {
                mailMessage.From = new MailAddress(username, "HYT-20");
            }
            //To email
            if (!string.IsNullOrWhiteSpace(emailEntity.ToUserEmail))
            {
                mailMessage.To.Add(new MailAddress(emailEntity.ToUserEmail, emailEntity.ToUserDisplayName));
            }

            //if we have more recepints, add it here
            //if (emailEntity.ToUsers != null && emailEntity.ToUsers.Count() > 0)
            //{
            //    foreach (var item in emailEntity.ToUsers)
            //    {
            //        mailMessage.To.Add(new MailAddress(item.Key, item.Value));
            //    }
            //}

            //Priority
            //mailMessage.Priority = (MailPriority)emailEntity.MailPriority;

            //Email Subject
            if (!string.IsNullOrWhiteSpace(emailEntity.EmailSubject))
            {
                mailMessage.Subject = emailEntity.EmailSubject;
            }

            //Email Body
            //Text
            mailMessage.Body = emailEntity.EmailBodyText;

            //if contains HTML body , will use html body
            if (!string.IsNullOrWhiteSpace(emailEntity.EmailBodyHtml))
            {
                mailMessage.Body = emailEntity.EmailBodyHtml;
                mailMessage.IsBodyHtml = true;
            }

            smtpServer.Send(mailMessage);
            mailMessage.Dispose();
            smtpServer.Dispose();

            return true;
        }

        public async Task<bool> SendMailAsync(EmailEntity emailEntity)
        {
            return await Task.Run(() => Send(emailEntity));
        }

        static System.IO.StreamWriter sw = null;
        static System.Net.Sockets.TcpClient tcpc = null;
        static SslStream _sslStream = default(SslStream);

        //public List<Message> FetchAllMessages(string hostname, int port, bool useSsl, string username, string password)
        public List<Message> FetchAllMessages()
        {
            // The client disconnects from the server when being disposed
            using (Pop3Client client = new Pop3Client())
            {
                // Connect to the server
                client.Connect(PopServerHost, port, true);

                // Authenticate ourselves towards the server
                client.Authenticate(username, password);

                // Get the number of messages in the inbox
                int messageCount = client.GetMessageCount();

                // We want to download all messages
                List<OpenPop.Mime.Message> allMessages = new List<OpenPop.Mime.Message>(messageCount);

                // Messages are numbered in the interval: [1, messageCount]
                // Ergo: message numbers are 1-based.
                // Most servers give the latest message the highest number
                for (int i = messageCount; i > 0; i--)
                {
                    allMessages.Add(client.GetMessage(i));
                }

                client.Disconnect();

                // Now return the fetched messages
                return allMessages;
            }
        }

        //public List<Message> FetchUnseenMessages(string hostname, int port, bool useSsl, string username, string password, List<string> seenUids)
        public List<Message> FetchUnseenMessages(List<string> seenUids)
        {
            // The client disconnects from the server when being disposed
            using (Pop3Client client = new Pop3Client())
            {
                // Connect to the server
                client.Connect(PopServerHost, port, true);

                // Authenticate ourselves towards the server
                client.Authenticate(username, password);

                // Fetch all the current uids seen
                List<string> uids = client.GetMessageUids();

                // Create a list we can return with all new messages
                List<Message> newMessages = new List<Message>();

                // All the new messages not seen by the POP3 client
                for (int i = 0; i < uids.Count; i++)
                {
                    string currentUidOnServer = uids[i];
                    if (!seenUids.Contains(currentUidOnServer))
                    {
                        // We have not seen this message before.
                        // Download it and add this new uid to seen uids

                        // the uids list is in messageNumber order - meaning that the first
                        // uid in the list has messageNumber of 1, and the second has 
                        // messageNumber 2. Therefore we can fetch the message using
                        // i + 1 since messageNumber should be in range [1, messageCount]
                        Message unseenMessage = client.GetMessage(i + 1);

                        // Add the message to the new messages
                        newMessages.Add(unseenMessage);

                        // Add the uid to the seen uids, as it has now been seen
                        seenUids.Add(currentUidOnServer);
                    }
                }

                // Return our new found messages
                return newMessages;
            }
        }
        public void FindPlainTextInMessage(Message message)
        {
            MessagePart plainText = message.FindFirstPlainTextVersion();
            if (plainText != null)
            {
                // Save the plain text to a file, database or anything you like
                plainText.Save(new FileInfo("plainText.txt"));
            }
        }

        /// <summary>
        /// Example showing:
        ///  - how to find a html version in a Message
        ///  - how to save MessageParts to file
        /// </summary>
        /// <param name="message">The message to examine for html</param>
        public void FindHtmlInMessage(Message message)
        {
            MessagePart html = message.FindFirstHtmlVersion();
            if (html != null)
            {
                // Save the plain text to a file, database or anything you like
                html.Save(new FileInfo("html.txt"));
            }
        }

        public void FindXmlInMessage(Message message)
        {
            MessagePart xml = message.FindFirstMessagePartWithMediaType("text/xml");
            if (xml != null)
            {
                // Get out the XML string from the email
                string xmlString = xml.GetBodyAsText();

                System.Xml.XmlDocument doc = new System.Xml.XmlDocument();

                // Load in the XML read from the email
                doc.LoadXml(xmlString);

                // Save the xml to the filesystem
                doc.Save("test.xml");
            }
        }

        public bool DeleteMessageByMessageId(string messageId)
        {
            // The client disconnects from the server when being disposed
            using (Pop3Client client = new Pop3Client())
            {
                // Connect to the server
                client.Connect(PopServerHost, port, true);

                // Authenticate ourselves towards the server
                client.Authenticate(username, password);// Get the number of messages on the POP3 server
                int messageCount = client.GetMessageCount();

                // Run trough each of these messages and download the headers
                for (int messageItem = messageCount; messageItem > 0; messageItem--)
                {
                    // If the Message ID of the current message is the same as the parameter given, delete that message
                    if (client.GetMessageHeaders(messageItem).MessageId == messageId)
                    {
                        // Delete
                        client.DeleteMessage(messageItem);
                        return true;
                    }
                }

                // We did not find any message with the given messageId, report this back
                return false;
            }
        }
    }

}
