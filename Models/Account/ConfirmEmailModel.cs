using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TryMailAndSMSMVC.Models.Account {
    [AllowAnonymous]
    public class ConfirmEmailModel {
        [TempData]
        public string StatusMessage { get; set; }
    }
}
