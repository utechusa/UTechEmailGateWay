using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;
using System.Net.Sockets;
using System.Net.Security;
using System.Timers;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows.Forms.ComponentModel;
using System.Collections.ObjectModel;
using ADKCoreEngine_CLR;
using UTechEmailGateway.Models;
using UTechEmailGateway.Services.Interface;
using UTechEmailGateway.Services.Implement;
using System.IO;
using OpenPop.Mime;
using OpenPop.Pop3;
using OpenPop.Pop3.Exceptions;
using System.Text.RegularExpressions;
using System.Configuration;

namespace UTechEmailGateway
{
    public partial class MainWindow : Window
    {
        ADKFramework _adk;

        ObservableCollection<Channel> _channelCollection = new ObservableCollection<Channel>();
        Dictionary<uint, string> _msgRequesetIdMsgItemInfoDict = new Dictionary<uint, string>();
        IEmailService email_service = new EmailService();

        uint _dispatcherDeviceId = 0;
        public const string CRLF = "\r\n";

        List<SendLog> sendlog_items = new List<SendLog>();
        List<ReceiveLog> receivelog_items = new List<ReceiveLog>();
        Timer timer;
        List<string> seenUids = new List<string>();
        uint dispatchStationID = 0;
        List<Person> contactList = new List<Person>();

        string FromUserEmail = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            lvSendLog.Items.Clear();
            lbDispatchStationID.Content = "Not Connected";
            lbDispatchStationID.Foreground = System.Windows.Media.Brushes.Red;

            SetTimer();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            _adk = new ADKFramework();
            //ADK Initial
            _adk.Initialize();

            contactsEntities contacts = new contactsEntities();
            contactList = (from c in contacts.People
                               select c).ToList();

            //Register Text message service
            ServiceBase servicebase = _adk.GetService(ServiceType.TMP);
            if (servicebase != null)
            {
                //Application Text message event handle.When receive text message data, it will be invoked.
                servicebase.Register(EventHandler_TMP);
                servicebase.SetTimeOut(20000, TimeoutType.RECEIVE_STATUS_FROM_DISPATCHER_STATION);
                servicebase.SetTimeOut(20000, TimeoutType.RECEIVE_ACK_FROM_TARGET_RADIO);
            }

            //Register system service(online/offline)
            servicebase = _adk.GetService(ServiceType.SYSTEM);
            if (servicebase != null)
            {
                try
                {
                    servicebase.Register(EventHandler_SYS);
                }
                catch (Exception exception)
                {
                }
            }

            //Register system service(online/offline)
            servicebase = _adk.GetService(ServiceType.RCP);
            if (servicebase != null)
            {
                try
                {
                    servicebase.Register(EventHandler_RCP);
                }
                catch (Exception exception)
                {
                }
            }

        }

        private void EventHandler_RCP(EventBase e)
        {
            try
            {
                ServiceEvent serviceEvent = e as ServiceEvent;
                if (serviceEvent == null) return;
            }
            catch (Exception exception)
            {

            }
        }


        //Application system event handle.When receive system event(online/offline...), it will be invoked.
        void EventHandler_SYS(EventBase e)
        {
            try
            {
                ServiceEvent serviceEvent = e as ServiceEvent;
                if (serviceEvent == null) return;

                ChannelChangedReport ccs = serviceEvent._eventData as ChannelChangedReport;
                if (ccs == null) return;

                //channel offline notification
                if (ccs._isDelete)
                {
                    for (int i = 0; i < _channelCollection.Count; i++)
                    {
                        if (ccs._channelID != _channelCollection[i].channelId) continue;

                        this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                        {
                            _channelCollection.RemoveAt(i);
                            //lbDispatchStationID.Content = "";
                            lbDispatchStationID.Content = "Not Connected";
                            lbDispatchStationID.Foreground = System.Windows.Media.Brushes.Red;
                        }));
                    }
                }
                //channel online notification
                else
                {
                    Channel channel = new Channel();
                    channel.channelId = ccs._channelID;
                    channel.deviceId = ccs._deviceID;
                    channel.ip = ccs._channelIP;
                    channel.slotId = ccs._soltID;
                    channel.isPlaySound = false;
                    channel.channelType = (ChannelChangedReport.ChannelType)ccs._channelType;

                    _dispatcherDeviceId = channel.deviceId;
                    _channelCollection.Add(channel);

                    if (_channelCollection.Count == 1 &&
                        _channelCollection[0].channelType == ChannelChangedReport.ChannelType.REPEATER_CHANNEL)
                    {
                        return;
                    }

                    this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
                    {
                        //lbDispatchStationID.Content = channel.deviceId;
                        lbDispatchStationID.Content = "Connected";
                        lbDispatchStationID.Foreground = System.Windows.Media.Brushes.Green;
                        dispatchStationID = channel.deviceId;
                    }));

                    _adk.SetHeardBeatInterval(channel.channelId, 0);
                }
            }
            catch (Exception exception)
            {

            }
        }


        //Application text message event handle.When receive text message event(message/message adk...), it will be invoked.
        void EventHandler_TMP(EventBase e)
        {
            try
            {
                ServiceEvent serviceEvent = e as ServiceEvent;
                if (serviceEvent == null)
                    return;

                switch (serviceEvent._opcode)
                {
                    case OptionCode.TMP_GROUP_REQUEST:
                    case OptionCode.TMP_GROUP_NO_ACK_NEED:
                    case OptionCode.TMP_PRIVATE_NEED_ACK_REQUEST:
                    case OptionCode.TMP_PRIVATE_NO_NEED_ACK_REQUEST:
                    case OptionCode.TMP_PRIVATE_SHORT_DATA_NO_NEED_ACK_REQUEST:
                    case OptionCode.TMP_PRIVATE_SHORT_DATA_NEED_ACK_REQUEST:
                    case OptionCode.TMP_GROUP_SHORT_DATA_REQUEST:
                    case OptionCode.TMP_GROUP_SHORT_DATA_NO_ACK_NEED:
                        //Receive message event handle
                        ReceiveMessageHandler(serviceEvent);

                        break;
                    case OptionCode.TMP_GROUP_ANSWER:
                    case OptionCode.TMP_PRIVATE_ANSWER:
                        //Receive message ack event handle
                        ReceiveMessageACK(serviceEvent);

                        break;
                    default:
                        break;
                }
            }
            catch (Exception exception)
            {
            }
        }

        //Receive message event handle
        private void ReceiveMessageHandler(ServiceEvent serviceEvent)
        {
            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
            {
                try
                {
                    switch (serviceEvent._opcode)
                    {
                        case OptionCode.TMP_PRIVATE_NEED_ACK_REQUEST:
                        case OptionCode.TMP_PRIVATE_NO_NEED_ACK_REQUEST:
                        case OptionCode.TMP_PRIVATE_SHORT_DATA_NO_NEED_ACK_REQUEST:
                        case OptionCode.TMP_PRIVATE_SHORT_DATA_NEED_ACK_REQUEST:
                            //如果短消息不是发给调度台，则直接过滤此短消息
                            //if ((((int)serviceEvent._localIP) & 0x00FFFFFF).ToString() != lbDispatchStationID.Content.ToString())
                            if ((((uint)serviceEvent._localIP) & 0x00FFFFFF) != dispatchStationID)
                            {
                                return;
                            }
                            break;
                    }

                    TextMessageReceivedData receiveData = serviceEvent._eventData as TextMessageReceivedData;
                    if (receiveData == null) return;

                    string revTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    string messageContent = receiveData._content.ToString();
                    string sourceId;
                    string messageType = string.Empty;

                    //接收到组消息
                    if (serviceEvent._opcode == OptionCode.TMP_GROUP_NO_ACK_NEED || serviceEvent._opcode == OptionCode.TMP_GROUP_REQUEST)
                    {
                        sourceId = (((int)serviceEvent._localIP) & 0x00FFFFFF).ToString();
                        messageType = "Group";
                    }
                    else
                    {
                        sourceId = (((int)serviceEvent._remoteIP) & 0x00FFFFFF).ToString();
                        messageType = "Private";
                    }

                    Person p = contactList.Find(x => x.TargetID.TrimEnd().Equals(sourceId.ToString()));
                    if (p == null) return;

                    //tbReceiveText.Text += revTime + "   " + messageType + "   " + sourceId + "\n"
                    //                        + "Content: " + messageContent + "\n";
                    lvReceiveLog.Items.Add(new ReceiveLog()
                    {
                        FromName = sourceId, //tbTargetID.Text,
                        //FromName = msg.Headers.Sender == null ? "" : msg.Headers.Sender.ToString(),
                        ReceivedDatetime = revTime,
                        MessageSubject = "RE: " + messageContent.Substring(0, messageContent.IndexOf(CRLF)).TrimEnd(),
                        MessageBody = messageContent == null ? "" : messageContent.Substring(messageContent.IndexOf(CRLF) + 2),
                        Status = "Success"
                    });

                    //forward this message to HotSos by email
                    EmailEntity email_entity = new EmailEntity();
                    email_entity.FromUserEmail = p.Email;
                    email_entity.FromUserDisplayName = p.FirstName + " " + p.LastName;
                    email_entity.ToUserEmail =  ConfigurationManager.AppSettings.Get("SenderEmail");
                    email_entity.EmailSubject = "RE: " + messageContent.Substring(0, messageContent.IndexOf(CRLF)).TrimEnd() + CRLF; //1,2,3,4
                    email_entity.EmailBodyText = messageContent == null ? "" : messageContent.Substring(messageContent.IndexOf(CRLF) + 2);
                    //email_entity.EmailBodyHtml = "<html>test</html>";
                    //email_service.Send(email_entity);
                    email_service.SendMailAsync(email_entity);


                    //if (string.IsNullOrEmpty(FromUserEmail))
                    //{
                    //    email_entity.ToUserEmail = FromUserEmail;
                    //    email_service.SendMailAsync(email_entity);
                    //}
                }
                catch (Exception exception)
                {
                }
            }));
        }

        private Task<bool> FetchEmailsAndForward()
        {
            //send to mobile
            #region send_to_mobile
            if (_channelCollection.Count <= 0)
            {
                //MessageBox.Show("No channel!");
                AutoClosingMessageBox msgBox = new AutoClosingMessageBox("Retrying ...", "Not connected!", 2000);
                return Task<bool>.Factory.StartNew(() => false);
            } 
            
            List<Message> msg_list = null;
            try
            {
                msg_list = email_service.FetchUnseenMessages(seenUids);
                if (msg_list.Count == 0)
                {
                    return Task<bool>.Factory.StartNew(() => false);
                }
            }
            // Catch these exceptions but don't do anything
            catch (PopServerLockedException psle)
            {
                return Task<bool>.Factory.StartNew(() => false);
            }
            catch (PopServerNotAvailableException psnae)
            {
                return Task<bool>.Factory.StartNew(() => false);
            }
            catch (PopServerException psle)
            {
                return Task<bool>.Factory.StartNew(() => false);
            }

            try
            {
                //log file info
                //string fpath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string fpath = Directory.GetCurrentDirectory();
                string fname = "email.log";
                // This file will have a new line at the end.
                FileInfo info = new FileInfo(fpath + "\\" + fname);

                foreach (Message msg in msg_list)
                {
                    var to_addr = msg.Headers.To[0].Address;
                    Person p = contactList.Find(x => x.Email.TrimEnd().Equals(to_addr));
                    if (p == null)
                    {
                        if (email_service.DeleteMessageByMessageId(msg.Headers.MessageId))
                        {
                            //MessageBox.Show("The message " + msg.Headers.MessageId + " has been deleted");
                            WriteMessageLog(info, msg);
                        }
                        continue;
                    }

                    TextMessageRequest textMsg = new TextMessageRequest();
                    StringBuilder builder = new StringBuilder();
                    builder.Append(msg.Headers.Subject);

                    textMsg._msg = builder.ToString().TrimEnd() + CRLF;
                    textMsg._targetID = uint.Parse(p.TargetID);
                    OptionCode messageOpcode = new OptionCode();
                    messageOpcode = OptionCode.TMP_PRIVATE_NEED_ACK_REQUEST;
                    uint channelId = GetChannel(1).channelId;

                    int requestID = _adk.GetService(ServiceType.TMP).SendCommand(textMsg, messageOpcode, channelId);
                    if (requestID == -1) continue; // return Task<bool>.Factory.StartNew(() => false);
                    if (_msgRequesetIdMsgItemInfoDict.ContainsKey((uint)requestID)) return Task<bool>.Factory.StartNew(() => false);
                    _msgRequesetIdMsgItemInfoDict.Add((uint)requestID, textMsg._targetID.ToString());

                    
                    sendlog_items.Add(new SendLog()
                    {
                        ID = requestID,
                        EmailID = msg.Headers.MessageId,
                        FromName = (string.IsNullOrEmpty(msg.Headers.From.DisplayName) ? "" : "<" + msg.Headers.From.DisplayName + ">") + msg.Headers.From.Address,
                        ReceivedDatetime = DateTime.Now.ToString(), //msg.Headers.Date,
                        MailSubject = msg.Headers.Subject,
                        MailBody = Regex.Replace(builder.ToString(), @"^\s*$\n|\r", "", RegexOptions.Multiline).TrimEnd()
                    });
                    WriteMessageLog(info, msg);
                    //string strEmailInfo = string.Empty;
                    //strEmailInfo += "Date: " + DateTime.Now; // msg.Headers.Date;
                    //strEmailInfo += CRLF;
                    //string display_name = msg.Headers.From.DisplayName == null ? "" : "<" + msg.Headers.From.DisplayName + ">";
                    //strEmailInfo += "From: " + display_name + msg.Headers.From.Address;
                    //strEmailInfo += CRLF;
                    //display_name = msg.Headers.To.FirstOrDefault().DisplayName == null ? "" : "<" + msg.Headers.To.FirstOrDefault().DisplayName + ">";
                    //strEmailInfo += "To: " + display_name + msg.Headers.To.FirstOrDefault().Address;
                    //strEmailInfo += CRLF;
                    //strEmailInfo += "Subject: " + msg.Headers.Subject;
                    //strEmailInfo += CRLF;
                    //string mail_body = msg.FindFirstPlainTextVersion() != null ? msg.FindFirstPlainTextVersion().GetBodyAsText() : msg.FindFirstHtmlVersion().GetBodyAsText();
                    //strEmailInfo += "Body: " + mail_body;
                    //strEmailInfo += CRLF;

                    ////save email into log file
                    //using (StreamWriter writer = info.AppendText())
                    //{
                    //    //writer.WriteLine(tbSendText.Text);
                    //    writer.WriteLine(strEmailInfo);
                    //}
                }
            }
            catch (Exception ex)
            {
                AutoClosingMessageBox msgBox = new AutoClosingMessageBox(ex.Message, "UTech Email Gateway", 5000);
                //MessageBox.Show(ex.Message, "UTech Demo", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            #endregion

            return Task<bool>.Factory.StartNew(() => true);
        }
        SendLog SendItem = new SendLog();
        private void WriteMessageLog(FileInfo info, Message msg)
        {
            string strEmailInfo = string.Empty;
            strEmailInfo += "Date: " + DateTime.Now; // msg.Headers.Date;
            strEmailInfo += CRLF;
            string display_name = string.IsNullOrEmpty(msg.Headers.From.DisplayName) ? "" : "<" + msg.Headers.From.DisplayName + ">";
            strEmailInfo += "From: " + display_name + msg.Headers.From.Address;
            strEmailInfo += CRLF;
            display_name = string.IsNullOrEmpty(msg.Headers.To.FirstOrDefault().DisplayName) ? "" : "<" + msg.Headers.To.FirstOrDefault().DisplayName + ">";
            strEmailInfo += "To: " + display_name + msg.Headers.To.FirstOrDefault().Address;
            strEmailInfo += CRLF;
            strEmailInfo += "Subject: " + msg.Headers.Subject;
            strEmailInfo += CRLF;
            string mail_body = msg.FindFirstPlainTextVersion() != null ? msg.FindFirstPlainTextVersion().GetBodyAsText() : msg.FindFirstHtmlVersion().GetBodyAsText();
            strEmailInfo += "Body: " + mail_body;
            strEmailInfo += CRLF;

            //save email into log file
            using (StreamWriter writer = info.AppendText())
            {
                //writer.WriteLine(tbSendText.Text);
                writer.WriteLine(strEmailInfo);
            }
        }

        private void SetTimer()
        {
            //schedule a checking email task than runs every 5 minutes
            timer = new System.Timers.Timer(int.Parse(ConfigurationManager.AppSettings.Get("CheckInterval")));
            //timer.Elapsed += FetchEmailsAndForward;
            timer.Elapsed += async (sender, e) => await FetchEmailsAndForward();
            //timer.Elapsed += SendEmails;
            timer.AutoReset = true;
            timer.Enabled = true;
        }

        private void SendEmails(object sender, ElapsedEventArgs e)
        {
            EmailEntity email_entity = new EmailEntity();
            //email_entity.ToUserEmail = "20@hyt.com"; // (uint)int.Parse(tbTargetID.Text) + "@hyt.com";
            email_entity.ToUserEmail = string.IsNullOrEmpty(FromUserEmail) ? "catch-all@utechusa.us" : FromUserEmail;
            email_entity.EmailSubject = "UTech Demo " + DateTime.Now; //1,2,3,4
            email_entity.EmailBodyText = "utech email gateway demo" + DateTime.Now;
            //throw new NotImplementedException();
            email_service.SendMailAsync(email_entity);
        }

        private void StopTimer()
        {
            timer.AutoReset = false;
            timer.Enabled = false;
            timer.Stop();
            timer.Dispose();
        }

        public void CloseApp()
        {
            //StopTimer();
            Application.Current.Shutdown();
        }
        
        //private delegate void AddItemCallback(SendLog sl);
        private delegate void AddItemCallback(object o);
        private void AddSendItem(object o)
        {
            if (!this.Dispatcher.CheckAccess())
            {
                AddItemCallback d = new AddItemCallback(AddSendItem);
                this.Dispatcher.Invoke(
                        () => lvSendLog.Items.Add(o), DispatcherPriority.Normal);
            }
            else
            {
                // code that adds item to listView (in this case $o)
            }
        }

        private void AddReceiveItem(object o)
        {
            if (!this.Dispatcher.CheckAccess())
            {
                AddItemCallback d = new AddItemCallback(AddReceiveItem);
                this.Dispatcher.Invoke(
                        () => lvReceiveLog.Items.Add(o), DispatcherPriority.Normal);
            }
            else
            {
                // code that adds item to listView (in this case $o)
            }
        }

        //Receive message ack event handle
        private void ReceiveMessageACK(ServiceEvent serviceEvent)
        {
            this.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Normal, new Action(() =>
            {
                try
                {
                    if (!_msgRequesetIdMsgItemInfoDict.ContainsKey(serviceEvent._requestID))
                    {
                        return;
                    }

                    SendItem = sendlog_items.Find(item => item.ID == serviceEvent._requestID);
                    //Get message ack result
                    bool success = serviceEvent._eventData._result == 0 ? true : false;
                    if (success == true)
                    {
                        var info = "Sent to " + _msgRequesetIdMsgItemInfoDict[serviceEvent._requestID] + " successfully!";
                        SendItem.Status = "Successful";
                        lvSendLog.Items.Add(SendItem);
                        sendlog_items.Remove(SendItem);
                        //AutoClosingMessageBox msgBox = new AutoClosingMessageBox(info, "UTech Email Gateway", 5000);
                        if (email_service.DeleteMessageByMessageId(SendItem.EmailID))
                        {
                            //MessageBox.Show("The message " + msg.Headers.MessageId + " has been deleted");
                        }
                    }
                    else
                    {
                        var info = "Send  to " + _msgRequesetIdMsgItemInfoDict[serviceEvent._requestID] + " failed!";
                        SendItem.Status = "Failed";
                        lvSendLog.Items.Add(SendItem);
                        sendlog_items.Remove(SendItem);
                        //AutoClosingMessageBox msgBox = new AutoClosingMessageBox(info, "UTech Email Gateway", 5000);
                    }

                    _msgRequesetIdMsgItemInfoDict.Remove(serviceEvent._requestID);
                }
                catch (Exception exception)
                {
                }
            }));
        }

        private Channel GetChannel(int slotId)
        {
            foreach (Channel item in _channelCollection)
            {
                if (item.slotId == slotId)
                {
                    return item;
                }
            }
            return _channelCollection[0];
        }

        //add a "Exit" button to exit app decently
        private void btnExit_Click(object sender, RoutedEventArgs e)
        {
            StopTimer();
            _adk.GetService(ServiceType.TMP).Unregister();
            _adk.GetService(ServiceType.SYSTEM).Unregister();
            Application.Current.Shutdown();
        }

    }

    public class Channel
    {
        public uint channelId { get; set; }
        public uint ip { get; set; }
        public uint deviceId { get; set; }
        public byte slotId { get; set; }
        public ChannelChangedReport.ChannelType channelType { get; set; }
        public bool isPlaySound { get; set; }
    }
    public class SendLog
    {
        public int ID { get; set; }
        public string FromName { get; set; }
        public string ReceivedDatetime { get; set; }
        public string MailSubject { get; set; }
        public string MailBody { get; set; }
        public string Status { get; set; }
        public string EmailID { get; set; }
    }

    public class ReceiveLog
    {
        public string FromName { get; set; }
        public string ReceivedDatetime { get; set; }
        public string MessageSubject { get; set; }
        public string MessageBody { get; set; }
        public string Status { get; set; }
    }
}
