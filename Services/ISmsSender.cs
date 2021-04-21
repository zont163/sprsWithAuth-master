using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TryMailAndSMSMVC.Services
{
    public interface ISmsSender
    {
        Task SendSmsAsync_Twilio(string number, string message);
        Task SendSmsAsync_ASPSMS(string number, string message);
    }
}
