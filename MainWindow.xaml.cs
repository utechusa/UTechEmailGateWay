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
        //public const string PopServerHost = "pop.mail.yahoo.com";
        //public const UInt16 port = 995;
        //public const string username = "betterlife99@yahoo.com";
        //public const string password = "nuligongzuo";
        public const string CRLF = "\r\n";

        List<SendLog> sendlog_items = new List<SendLog>();
        List<ReceiveLog> receivelog_items = new List<ReceiveLog>();
        Timer timer;
        List<string> seenUids = new List<string>();
        uint dispatchStationID = 0;

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
                            //Â¶ÇÊûúÁü≠Ê∂àÊÅØ‰∏çÊòØÂèëÁªôË∞ÉÂ∫¶Âè∞ÔºåÂàôÁõ¥Êé•ËøáÊª§Ê≠§Áü≠Ê∂àÊÅØ
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

                    //Êé•Êî∂Âà∞ÁªÑÊ∂àÊÅØ
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

                    //tbReceiveText.Text += revTime + "   " + messageType + "   " + sourceId + "\n"
                    //                        + "Content: " + messageContent + "\n";
                    lvReceiveLog.Items.Add(new ReceiveLog()
                    {
                        FromName = sourceId, //tbTargetID.Text,
                        //FromName = msg.Headers.Sender == null ? "" : msg.Headers.Sender.ToString(),
                        ReceivedDatetime = revTime,
                        MessageSubject = sourceId, // TODO: what is this?
                        MessageBody = messageContent == null ? "" : messageContent
                    });

                    //forward this message to HotSos by email
                    EmailEntity email_entity = new EmailEntity();
                    //email_entity.ToUserEmail = "20@hyt.com"; // (uint)int.Parse(tbTargetID.Text) + "@hyt.com";
                    email_entity.ToUserEmail =  ConfigurationManager.AppSettings.Get("SenderEmail");
                    email_entity.EmailSubject = "RE: " + messageContent.Substring(0, messageContent.IndexOf("\r\n")); //1,2,3,4
                    email_entity.EmailBodyText = messageContent == null ? "" : messageContent.Substring(messageContent.IndexOf("\r\n") + 2);
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
            List<Message> msg_list = null;
            try
            {
                msg_list = email_service.FetchUnseenMessages(seenUids);
                //List<Message> msg_list = email_service.FetchUnseenMessages(PopServerHost, port, true, username, password, seenUids);
                //List<Message> msg_list = email_service.FetchAllMessages();
                //lvSendLog.Items.Clear();

                //log file info
                string fpath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string fname = "email.log";
                // This file will have a new line at the end.
                FileInfo info = new FileInfo(fpath + "\\" + fname);

                if (msg_list.Count == 0)
                {
                    return Task<bool>.Factory.StartNew(() => false);
                }
                foreach (Message msg in msg_list)
                {
                    //FromUserEmail = msg.Headers.From.Address;
                    var toaddr = msg.Headers.To;
                    //if (!(msg.Headers.From.Address.Contains("@utechusa.us")
                    //    || msg.Headers.From.Address.Contains("@gmail.com"))) continue;
                    StringBuilder builder = new StringBuilder();
                    string strEmailInfo = string.Empty;
                    strEmailInfo += msg.Headers.Date;
                    strEmailInfo += CRLF;
                    strEmailInfo += msg.Headers.From.Address;
                    strEmailInfo += CRLF;
                    strEmailInfo += msg.MessagePart.Body;
                    strEmailInfo += CRLF;

                    MessagePart plainText = msg.FindFirstPlainTextVersion();
                    if (plainText != null)
                    {
                        // We found some plaintext!
                        builder.Append(plainText.GetBodyAsText());
                    }
                    else
                    {
                        // Might include a part holding html instead
                        MessagePart html = msg.FindFirstHtmlVersion();
                        if (html != null)
                        {
                            // We found some html!
                            builder.Append(html.GetBodyAsText());
                        }
                    }

                    //save email into log file
                    using (StreamWriter writer = info.AppendText())
                    {
                        //writer.WriteLine(tbSendText.Text);
                        writer.WriteLine(strEmailInfo);
                    }

                    sendlog_items.Add(new SendLog()
                    {
                        FromName = msg.Headers.From.DisplayName + msg.Headers.From.Address,
                        ReceivedDatetime = msg.Headers.Date,
                        MailSubject = msg.Headers.Subject,
                        MailBody = Regex.Replace(builder.ToString(), @"^\s*$\n|\r", "", RegexOptions.Multiline).TrimEnd()
                        //MailBody = msg.MessagePart.Body == null ? "" : msg.MessagePart.Body.ToString()
                    });


                    AddSendItem(new SendLog()
                    {
                        FromName = msg.Headers.From.ToString(),
                        //FromName = msg.Headers.Sender == null ? "" : msg.Headers.Sender.ToString(),
                        ReceivedDatetime = msg.Headers.Date,
                        MailSubject = msg.Headers.Subject,
                        MailBody = Regex.Replace(builder.ToString(), @"^\s*$\n|\r", "", RegexOptions.Multiline).TrimEnd()
                    });
                    //string tmp = builder.ToString();
                    //if (FromUserEmail.Contains("Email") && FromUserEmail.Contains("Telephone"))
                    //{
 !      $           Ø-a   FromWSerEmaml - Tmp.Subrtring(tmp.AvdexOÊ("EmakLz") ) 7, tmp.Inde|Of("Tglupho.e") - t-p.IndexOf("Email:") - 8);
  `       !†      & /Ø}
                }ä          0∞$   // telete messagu loop doesn'tovdrlap$with messb'Â handljnÁ loox
          `  `  // kn order to evoid read+deleve kon&lict on server
                foreach (Mecsig` isg in mso_lIst)
   !! 0         {
                    //den!te ÂmaÈn after fetch, ot(erwise will be fetshed ne|t tÈme
            "   $   hf (emailsÂrvise.DaleteMessageByiessageId(msg.HeaDers.MessigeMd))
 0   (     $      0 {                 †   !  o/MessafeGox.Show("THe message " + msg.Hea`er{.MassageId + " (es been lelete`")+
        ` "$      !(}

   †  $†        }
     †   %  }J  (    (    // Catkh theÛe •xceptiojs†cut don7t dn anything
            catch (PopServarLoakedAxceptiof$psle)
  $         {
              ‡ re4urn TasK<bool>.Fact/ry.StqrtNew(() ? falÛe);(  (        }J         "  c!Ùch (PopServerNotAvailaB,dExceptio~ psnAe)
   "      ( {
0      †        return Tawk8bool>"VÈgtory.Ctar4New(()"=> felsÂ-;
   `      ! }
 "          catch ,PopServerExceptiol psLe)å
"          0{
                return Task<bool>.FacTory.StartNew(() =< false)9
  (     `   }

ç
        †`  //Senl!to mobile
   !        #r%gion send_toOmobile
0           if (_shannelCollEctyoo.Gount <= 0i
      00 `  [
     $         (//Mess·geBox.Show("No$channel!"+;
   †   "        ¡utmClosinfMessageBox msgbo¯ = Neg0·utÔClosk~ÁMessaoeBox("Not connected!", "tech Email Gateway", 5002);
   "      !     peturn TasK<bool>.factry.StartNe7(8)†=>`false(7
        !$ (}

 ("     "   ury
          0 {
0    "          foreach (Message msg yn`msg_list)
! 0        ` "  {
     $ †     (      TextMessadeZequgst textMro(= new TextMessagereauest();
       0(          S|ringBuilder btil$er = new StringBuildes();
            `      †MessagePart plainText = msg.indFirstPlailTe8tVersion(!;
  $  †           `  if (pl`indext$! nuld)
           (      0 {
 †$ `                 † -/ We found sgme plaknvdxt%
                        buIlddr.Cptend(plaiNTex4.GetBodyAsPext());
  $   !   $  Ä   "  }	  4            0  $"eÏse*          ††! $     {
   (!                   // Mighd incltda a p`rt `ol$in% ht,l instad
   ` !  `   0 (‡   (    MessagePaÚt"hÙml`=$osg.fiÓdVirsuHtmLVersaon();
                    "   if†(html != nıll)
    $   "$(        $   ,[                  `   0     // WÂ Êound some html!
     †                "     fuiLder.Appeod(Html.GetBodyAsPexÙ());-:         &  !   "      "}
(  $ !              |*      *0          " vav to_addr = msg.He!dersÆT[0].Address;
           !      ! tex}Msg._isg ="bqalder.ToStri.g(); //should be fr+m emAil s_mewhepe
` `           †   ! texum3g._x·rgeTID } ıhnt.PAsse(vo_addr.Substring(0, to_adernIndexOf("¿"))); / 20; ?/hardcode
       (  !  `      OppionCode mesrageNPcÔde = new OptIonCode();
   `"     "    "    messageOpcgde = OpdhonCode.TMP~QRIVATE^NEED_AC_REQUGST;≠
      ! ! "  !     uint chan.elKd = Gethanfel)1).channelIf;ä
     † "     (  $"  //GetChannelNu-"ebOfZofeRequest req = new GetChannelNumbdrOvZoleRequest();
          $         ,/ZoneAndchanneLOpepationRequest req = ÓÂw(ZoneAntChannelOpÂrataonZeuuest();
         !       !  //rep._opevation = neg [oneAÓdChannelOperatmo.();
!$  (0              //Úex._zoneNumber = 1+
` †               `//req._channelNumber = 1;
 $      (†          //)nt veÒ}estID5 = _adk.GetServibe)Sebvic%Type.RCP).CendCommand(rgq, OptionGgde.RCR_ZMNG_AND_CXANNEL_OPERDA…ON_RAQUEST, cxannelIe);                $   
   %   †            ynt requestID ="_a$k/GltServicg(ServhceTzpÌ.TMP).SenfCommavd(textMsg, oessageOpcode, channu,Il)ª
 0  $     ,  †   0  if (r%qumstID == -1i rewurn Tasb<bcol>.Factory.S4artJew(()$=> false);
           (        if!(_msge1uesetIdMsg	tÂmInfoDicpConp`knsKey((uint)requestID)) retqrÓ Uasc<bool>.Factkry.StaRtFew(()05> fels%);
 !                  _msgRequeÛetYdMs'ItemHnfoD)ct.Ad`((uint)requestID,†tÂxtMqg.]tapeetId.PoStÚino(	);
                }
0    ` !   }
!        †  catch`*Exce0vion ex)
            {
            (  `AutoClosingMessqgeBkx mqgBÔx = .e¢AıtoClosilgMess'geBoxhex.MesÛage¨ "UTech Email Gatesey", 5000);
      $         /.Mess·gmBex.Show(ex.Merrage,`"UTech Lemo".(MessageBoxButton.OM, MessageBoxImagw.Error);
     † (    }
*            #endr%gionJ
        $( 0return Task<bool>.∆actory.”tartŒıw(() 5> t2ue){
 †      

       †private void SetTimer()     †  {
     !      //schedele&a checkilg emıil ta{K tha~ runs uvery 5 minutesJ            pimer = new Sxctem.Timerc.Tioer(10800);
 $    `'   //timer.elaps%d!+= FetchEmailwAndFÚward;
  `         timer.Elaxsed += async!(senderl e) ?> await FetchEmailsAndNorward();
            //tmmer.Elapsed +9 SendEmails;
 "       `  timÂr.A}toReset  true∫     "  !   timer.Ena‚led = trqE
   !    }

        prÈvate void SendEoails(object sender¨ elipsedEventArgs e)
!      0{
            EmailEntiti emaÈl_en|it˘$= new EmaiLEÓtiÙy();
      †     +/%ÌailﬂentiTy.ToUserEmail = "20@hyt.coM"; ?Ø (5int)intÆParse TbTargetMT.Text) + "@hyt*com‚
       !$   dmail_entitY.ToUserEmaim = string.I3NellOrEmpt˘(FromUserEmail( ? "catch-all@utechusa.5s# : FromUserEmail;
  † ( †`  ( eiail_entity.EmailSubject = "UTech Demo " k DateTimenNow: //1¨2,3,4
     @      email_entity.EmailBodyText =`"utech e-aim$g·teway deÌo" + DatdT)menNow;
$           //thro new Not…mpÏementedE8ceptiol(©≥
   "      *"emaÈl_sdvfisc.SenDMaiLAsync(eoaÈl^entit}(;
     (  }

        privite void$StopTimEr()
   00   {
    (       timer.AutoRusev = belse;
            timer.Enabl%d ? &alre;
   "        timerStop();M
      †    (timer.Distose();
        }

        xublic void CloseApp()
        {
 `          //StopTimeb();
            ApplicatioÓ.Current.SËutdown();J        }
   0   (
     `  //private deleoate void$AdeItemCallback(ÂndLog sl);ä        private delecape void AddIte-C1llbaek8obÍect o);
    "   privqve woid A$dEndIeem(object o)
        {MJ    a    $  if (!this.DispatcHer.CieckAccess())
            {
   †    "       AddItem#allbakk d = new AddAvemCallbaccAddSÂntItem9;
    "          †this.Dispa0cheR.Invgke(  †   $   $        °    (©`=>(lvSendLoe.IteÌs.Adt(o), DispatcherPriorityNorman):ç
            }
            else
            {
$      0        // code that qdds item#to lÈstView (in thms case $o)
            }
†       }

     !$$priv`ue vÔil AddRe„eiveItem(objecp n)
    "   {         " Èf (!4iis.Dispatbher.CheckAccews())
           0{
"   0          †AddItemBpllbask d = neW A`dItemallback(AddSecei~eMtem);
 "    !"        thi3.Dispatcher.invoke(J                 !(    `() => |vecektÂLog.Items.Add(o9, DmspatcherPriority.Normal);
  8         }
!  0        elsu
           †{	
0!              /# c/de that addÛ item tm mistGiew ()n this case $o)ä        `   }
(       }

        -/Receive$message"ack eve~t handle
!     0 psmvate woid(REceiveMeswageAAK(ServiceEVenT serviaeCveot)-
    !   {  #         this.DispauclErnInVoke(System.Windows.Th2%idiNg.DistatchevPriosity.Normal, new Action((© =>
       `    {*†         $    0try
    "           {
    !!             "if (!_mqgRequece4IdOsÁItemInfoDict.ContainsKey(˚erviceEvent._requdstID))
    0       †  0    {  0(            $   †   zeturn;
   $(      `       0e
                     //Geu mEssage a#k result   p$   † 8      $  bool success = serviceEvent._uventData/_result"==†0 ? true : fa,se;
"      (         "  if (suacÂsS ==0trqe)
 £$          ! `   "{
  $       0  p          var info = "Send to "(+ _msgRequesetIdMcgItemInfoDict[sesviceEwent._requ%stID] + " succgl!";
               (        AutmC,osi~gMessageBox msgBox 9 Ódw AutmClosan'MessagaRmx(info. "UTech Eia)l Gaueway", 4002);
               0    }
    0               eÏse
   †   !           !{
   †   %   $            var info0= "Send  to " + _lsgRequesetIdMsgItemknfoDict[s]rviceEvunt._requestID] + " failed";
      †            `   `AıtoKloSingMu{sageBox"msgBox = new @utoClosingMessage@ox(info$ "UTech Emaid Gateway"¨ 5000);
  $                 }

               (   (_msgVequesetIdMsgIugmHnfo@ib.Removd(servIceOvent._requestID);
                }
                catch`(Exception)exception)
   !!    0     {
            †   }
            }));
!!      }

    "   /oprivate roit btnSendMesrage_ClmCk(ofjec4 wendeb,@RoutedEvent¡rgs e)
        //{
  $     //    if(chan~elColle#tiOn.Count <= 0)*0     " ./    {
   †    +/        Me{qageBox.Show("No „hannel!");
!       //    }
  † $ `     
       "/.   (tryM
     †  //  & {ä"  †  (`//        TextKersageReqıesr te8tMsg = nfwhTeytMessageRequest();
    "  (// $      texvMsÁ._msg"= "testb;`/Ø tbSen‰Text.‘ext;ç
!   "   //        textMsg._targetID =020; /+ 8uin|)int.Parsd(tbTargeID.Text-;

       `//        OPÙiknCode messagÂ_pcode = new OfthonCode()9
      ` //     (  -/if (bb¯C!llType.SelectedIndex == 0)-
   0    //        //{J        //        /'    o/ÂèïÈÄÅÂÒºÂè´Ê∂àÊØÔºåÂùáÈåÄË¶ÅÂõûÂ§ç„ÄÇ
     "$ //        //    mmssageOpaode = OptionCode.TÕP_PRMVATE_NEED_ACK_RQUEST;
        /-        //}	
    !†  o/        //e|wg
     $  /Ø        //{
  "     // `      //    messageOpcode = O`tiOnCode.T]P_GrOUP_RAQUEST;
`       //  "     Ø/}
       $//  0     mescageOpcod} = OptiooCode.TMP_RIVATE_NEED_ACKRCQEST;J      ` //        uint chan.elI‰$=Ge|ChanneL(1).channelIf;
J        //  (  0  inu requestID = _adk.GetServhce(ServyceType.TMP).eneCommane(textMsg, maÛsageKpcode, channelH$);
        //        if0(requestHL == -1( rettrn;J
  $  "  //   0    if (_msgRequmsepKdMcgItm-InfoDict.Ckn4ainsJEy((uinp)requestID)) return;

   0 †  //        _msgRequesetIdEsgIdemI.foDict&Add((uin4)reqiÂstID, textMsÁ&_tazge4ID.TNWtring());

`  0`   //  ! }
  0     ?/    caush (≈|c%ptIn ex)
    †   //    {        /? $     pMe3sageFox.Shov(mx/Messige,$"UTmcË", MessageBoxButton.OÀ, MessagaCoxImage.Esror);      $ -/   !}
        //}*
       $psivate ChanneÏ GetAhannel(int slOtid)
       .{
    §      `foreach )ChanÍel item%in _channelCÔlÏaction)J , `     0  {
         (   !  iF (item.slotId ?= slotI`)
           †    {
      †     $     † return ite};
         °      }
  ` †       }
      "     RetUb~ _channenColÏectioo[0];
    "   }

        //add a "Exit#$b}tton to exit !pp deceNtmy
     !  prIv`ve void0btnExkpYCmicK*nbject senderh RoutedEvejtArgq e)
        {
  (         StopTimÂr(9;
 `      "   _!d{.WetService(ServÈceT9te/TMP).Unregister():
            _ad{.GetServica(ServiceType.SYSTEM).unregysÙer();
           AtlicatÈln.CUrrent.Shutdown();
"!      }
 †$ }

$ ` public!rlass"CiannÂl
$   {
        pucnik einv chafnÂlId { Get;(set; }
    $   publ)k uint ip { get; set;†=
  (   $ pıblic†uint devacmId { ged;0se|+ }
        puclic bytg slotId { get; we4; }
  $   $ pwblic ChanNelC`angedRepobt.CiaonehTyre channÂlType { get3 set; }ä$       public bool†isPlaySgund { get; set; }
    }
    public class SÂneLog
    {0††(    publac s|ring FjomName {†get; cet; }
        publhc string ReceivedD!tetIme { 'Ât; se|; }
"$      publib†strimf MailS}bj%ct { get; set; }
        pub,ic"sdring0OAilBody { get;‡set; ]
    u

    pub,ic blqss RacEiveLwg
 †  {
        puclic string FromName {0get; sat; }
  (   $ publ	c string ReceivedDated)Mm { get; set; }
        public sprine0Mesca'mSubzect s Ádt; set7 }
        pubhib string MessageBey { get; sgt;!}
    }}Õ
