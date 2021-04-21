using System;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using TryMailAndSMSMVC.Models.Account;
using TryMailAndSMSMVC.Services;
using System.IO;
using Newtonsoft.Json;
using Microsoft.Data.SqlClient;
using System.Data;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;

namespace TryMailAndSMSMVC.Controllers {
    public class AccountController : Controller {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly ISmsSender _smsSender;
        private readonly IConfiguration _config;
        private IMemoryCache _cache;

        public AccountController(
            IConfiguration config,
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            ISmsSender smsSender,
            IMemoryCache memoryCache) {
            _config = config;
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _smsSender = smsSender;
            _cache = memoryCache;
        }


        // GET: Account/Register
        public ActionResult Register() {
            return View();
        }

        // POST: Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterModel input) {
            if(ModelState.IsValid) {
                var user = new IdentityUser { UserName = input.Email, Email = input.Email, PhoneNumber = input.PhoneNumber };

                var result = await _userManager.CreateAsync(user, input.Password);
                if(result.Succeeded) {
                    _logger.LogInformation("User created a new account with password.");

                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                    var callbackUrl = Url.Action("ConfirmEmail", "Account", new { userId = user.Id, code = code }, Request.Scheme);
                    await _emailSender.SendEmailAsync(input.Email, "Confirm your email",
                        $"Интеллектуальная Арганизация Корпоративных Разработчиков (ИА КРАБ) предлагает вам подтвердить свой емейл, для этого нужно прогуляться по  <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>ссылочке</a>.");

                    if(_userManager.Options.SignIn.RequireConfirmedAccount) {
                        return RedirectToAction("RegisterConfirmation", new { email = input.Email });
                    }
                    else {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect("Index");
                    }
                }
                foreach(var error in result.Errors) {
                    ModelState.AddModelError(string.Empty, error.Description);
                }

            }

            // If we got this far, something failed, redisplay form
            return View();
        }

        // GET: Account/RegisterConfirmation
        public async Task<IActionResult> RegisterConfirmation(string email) {
            if(email == null) {
                return RedirectToPage("/Index");
            }

            var user = await _userManager.FindByEmailAsync(email);
            if(user == null) {
                return NotFound($"Unable to load user with email '{email}'.");
            }
            return View();
        }

        // GET: Account/Login
        public async Task<IActionResult> Login(string returnUrl = null) {
            LoginModel loginModel = new LoginModel();

            if(!string.IsNullOrEmpty(loginModel.ErrorMessage)) {
                ModelState.AddModelError(string.Empty, loginModel.ErrorMessage);
            }

            returnUrl = returnUrl ?? Url.Content("~/");

            // Clear the existing external cookie to ensure a clean login process
            //await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            loginModel.ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            loginModel.ReturnUrl = returnUrl;
            return View(loginModel);
        }



        // POST: Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginModel loginModel, string returnUrl = null) {
            returnUrl = returnUrl ?? Url.Content("~/");

            if(ModelState.IsValid) {
                // This doesn't count login failures towards account lockout
                // To enable password failures to trigger account lockout, set lockoutOnFailure: true
                //подготавливаем нужные данные, если авторизация по телефону


                var result = await _signInManager.PasswordSignInAsync(loginModel.Email, loginModel.Password, loginModel.RememberMe, lockoutOnFailure: false);
                if(result.Succeeded) {
                    _logger.LogInformation("User logged in.");
                    return LocalRedirect(returnUrl);
                }
                if(result.RequiresTwoFactor) {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = loginModel.RememberMe });
                }
                if(result.IsLockedOut) {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToPage("./Lockout");
                }
                else {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return View();
                }
            }

            // If we got this far, something failed, redisplay form
            return View();
        }

        public async Task<string> LoginByPhone(string eCode, string pNumber, bool rMe) {
            string result = CheckPhoneAuth(eCode,pNumber);
            if(result == null) {
                //достаем все данные юзера из базы по телефону
                string userId = await FindIdByPhone(pNumber);
                var user = await _userManager.FindByIdAsync(userId);
                await _signInManager.SignInAsync(user, rMe);
                _logger.LogInformation("User logged in by phone.");
                RedirectToPage("/Index");
                return result;
            }
            else { return result; }
        }

        public string CheckPhoneAuth(string enteredCode, string pNumber) {
            var sessionId = HttpContext.TraceIdentifier;
            string sentCode = "";
            string result = null;

            if(_cache.TryGetValue("SMS_" + pNumber, out sentCode)) {
                if(!sentCode.Equals(enteredCode)) {
                    result = "коды не совпадают";
                }
            }
            else {
                result = "в кеше нет такой сессии";
                _logger.LogInformation("в кеше нет такой сессии");
            }
            return result;
        }

        // GET: Account/ConfirmEmailModel
        public async Task<IActionResult> ConfirmEmail(string userId, string code, ConfirmEmailModel input) {
            if(userId == null || code == null) {
                return RedirectToPage("/Index");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if(user == null) {
                return NotFound($"Unable to load user with ID '{userId}'.");
            }

            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            var result = await _userManager.ConfirmEmailAsync(user, code);
            input.StatusMessage = result.Succeeded ? "Спасибо за подтверждение емейла в нашей вселенской организации" : "Что то пошло не так в подтверждении почты. Обратитесь к крабам.";
            return View(input);
        }

        public async Task<IActionResult> SendMessage(string ph) {
            string sentCode;
            string inputPhoneNumber = JsonConvert.DeserializeObject<string>(ph);
            var sessionId = HttpContext.TraceIdentifier;

            //finding fucking user
            string id = await FindIdByPhone(inputPhoneNumber);
            if(id == null)
                return NotFound($"такого номера в базе нет - '{inputPhoneNumber}'.");

            var user = await _userManager.FindByIdAsync(id);
            if(user == null)
                return View("Error - с таким ИД к нам в палату не поступали");

            //generate sms-code
            sentCode = await _userManager.GenerateChangePhoneNumberTokenAsync(user, inputPhoneNumber);

            //пишем ид сессии и код в кэш
            _cache.Set("SMS_"+ inputPhoneNumber,"1234"/* sentCode*/, new MemoryCacheEntryOptions {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
            });
            _logger.LogInformation("sentCode writen in the cache");

            //send sms
            await _smsSender.SendSmsAsync_ASPSMS(inputPhoneNumber, "Vash krabokod: " + sentCode);

            return new JsonResult("Сообщение отправлено вроде как");
        }

        public async Task<string> FindIdByPhone(string phoneNumber) {
            _logger.LogInformation("start sql proc.");
            string connString = _config.GetConnectionString("DefaultConnection");
            using SqlConnection sql_connect = new SqlConnection(connString);
            try {
                sql_connect.Open();


                SqlCommand cmd = new SqlCommand("dbo.usersIdByPhone_sel", sql_connect);
                // указываем, что команда представляет хранимую процедуру
                cmd.CommandType = System.Data.CommandType.StoredProcedure;

                SqlParameter param = new SqlParameter {
                    ParameterName = "@PhoneNumber",
                    Value = phoneNumber
                };
                cmd.Parameters.Add(param);
                SqlDataAdapter adapter = new SqlDataAdapter(cmd);

                DataSet dataset = new DataSet();
                adapter.Fill(dataset);

                DataTable dt = dataset.Tables[0];
                //if (dt.Rows.Count > 1)
                //выдать ошибку, что юзеров с таким номером телефона больше двух. Или сделать поле PhoneNumver уникальным

                return dt.Rows[0]["Id"].ToString();
            }
            catch(Exception e) {
                throw e;
                // return null;
            }
        }



        // POST: Account/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection) {
            try {
                // TODO: Add update logic here

                return RedirectToAction(nameof(Index));
            }
            catch {
                return View();
            }
        }

        // GET: Account/Delete/5
        public ActionResult Delete(int id) {
            return View();
        }

        // POST: Account/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection) {
            try {
                // TODO: Add delete logic here

                return RedirectToAction(nameof(Index));
            }
            catch {
                return View();
            }
        }
    }
}