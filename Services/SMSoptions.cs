using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TryMailAndSMSMVC.Services
{
    public class SMSoptions
    {
        public string ASPSMSUserkey { get; set; }
        public string ASPSMSPassword { get; set; }
    }
    public class SMSoptions_Twilio
    {
        public string SMSAccountIdentification { get; set; }
        public string SMSAccountPassword { get; set; }
        public string SMSAccountFrom { get; set; }
    }
}

