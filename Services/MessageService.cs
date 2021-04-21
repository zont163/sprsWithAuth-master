using MimeKit;
using MailKit.Net.Smtp;
using System.Threading.Tasks;
using MailKit.Security;
using Microsoft.Extensions.Options;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;
using Microsoft.AspNetCore.Identity.UI.Services;

namespace TryMailAndSMSMVC.Services
{
    public class MessageService : IEmailSender, ISmsSender
    {
        public MessageService(IOptions<SMSoptions> optionsAccessor)
        {
            SMSoptions = optionsAccessor.Value;
        }
        public SMSoptions SMSoptions { get; }  // set only via Secret Manager
        public SMSoptions_Twilio SMSoptionsTwilio { get; }  // set only via Secret Manager

        public async Task SendEmailAsync(string email, string subject, string message)
        {
            var emailMessage = new MimeMessage();

            emailMessage.From.Add(new MailboxAddress("Интеллектуальная Арганизация Корпоративных Разработчиков", "info@kpn-samara.ru"));
            emailMessage.To.Add(new MailboxAddress("KPN", email));
            emailMessage.Subject = subject;
            emailMessage.Body = new TextPart(MimeKit.Text.TextFormat.Html)
            {
                Text = message
            };

            using (var client = new SmtpClient())
            {
                await client.ConnectAsync("smtp.kpn-samara.ru", 25, SecureSocketOptions.None);
                await client.AuthenticateAsync("info@kpn-samara.ru", "Tavria385");
                await client.SendAsync(emailMessage);

                await client.DisconnectAsync(true);
            }
        }

        public Task SendSmsAsync_Twilio(string number, string message)
        {
            // Plug in your SMS service here to send a text message.
            // Your Account SID from twilio.com/console
            var accountSid = SMSoptionsTwilio.SMSAccountIdentification;
            // Your Auth Token from twilio.com/console
            var authToken = SMSoptionsTwilio.SMSAccountPassword;

            TwilioClient.Init(accountSid, authToken);

            return MessageResource.CreateAsync(
              to: new PhoneNumber(number),
              from: new PhoneNumber(SMSoptionsTwilio.SMSAccountFrom),
              body: message);
        }

        public async Task SendSmsAsync_ASPSMS(string number, string message)
        {
            var userkey = SMSoptions.ASPSMSUserkey;
            var password = SMSoptions.ASPSMSPassword;

            ASPSMS.SMS tASPSMS = new ASPSMS.SMS(userkey, password);
            tASPSMS.AddRecipient(number);
            tASPSMS.Originator = "KPN_test";
            tASPSMS.MessageData = message;
            await tASPSMS.SendTextSMS();

            // //нужно передать инфо о статусе отправки
            //if (tASPSMS.ErrorCode == 1)
            //    ViewBag.Status = "Message successfully Sent";
            //else
            //    ViewBag.Status = "Error: " + tASPSMS.ErrorCode + " " + tASPSMS.ErrorCodeDescription;
                
        }
    }


}