using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MikuSB.Configuration;
using MikuSB.Database.Account;
using MikuSB.SdkServer.Models;
using MikuSB.Util;
using System.Text;
using System.Text.Json;

namespace MikuSB.SdkServer.Handlers;

[ApiController]
public class RouteController : ControllerBase
{
    public static ConfigContainer Config = ConfigManager.Config;

    public static object BuildServerEntry(string type)
    {
        var name = Config.GameServer.GameServerName;
        var host = Config.GameServer.PublicAddress;
        var port = Config.GameServer.Port;
        return new
        {
            type,
            name,
            title = name,
            addr = host,
            host,
            ip = host,
            port,
            id = 1,
            server_id = 1,
            status = 1,
            state = 1,
            is_open = 1,
            open = 1,
            recommend = 1
        };
    }

    public static object[] BuildServerQueryList()
        => [BuildServerEntry("ASIA"), BuildServerEntry("1")];

    public static object BuildServerList(string version = "")
    {
        var servers = BuildServerQueryList();
        return new
        {
            code = 0,
            ret = 0,
            msg = "ok",
            message = "ok",
            version,
            server_time = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            is_open = true,
            isOpen = true,
            open = true,
            servers,
            server_list = servers,
            list = servers,
            data = new
            {
                is_open = true,
                isOpen = true,
                open = true,
                servers,
                server_list = servers,
                list = servers,
                host = Config.GameServer.PublicAddress,
                ip = Config.GameServer.PublicAddress,
                port = Config.GameServer.Port
            },
            game_server = new
            {
                host = Config.GameServer.PublicAddress,
                ip = Config.GameServer.PublicAddress,
                port = Config.GameServer.Port
            },
            http_server = new
            {
                host = Config.HttpServer.PublicAddress,
                port = Config.HttpServer.Port
            }
        };
    }

    private static string? ExtractUid(string? authInfo)
    {
        if (string.IsNullOrWhiteSpace(authInfo))
            return null;

        try
        {
            var normalized = Uri.UnescapeDataString(authInfo).Trim();
            var padding = normalized.Length % 4;
            if (padding > 0)
                normalized = normalized.PadRight(normalized.Length + (4 - padding), '=');

            var json = Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("uid", out var uid) ? uid.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    [HttpGet("/getGameConfig")]
    [HttpPost("/getGameConfig")]
    public IActionResult GetGameConfig()
    {
        object rsp = new
        {
            ret = 0,
            code = "0",
            msg = "success",
            data = new
            {
                agreementUpdateTime = "1728552600000",
                appDownLoadUrl = "",
                appName = "尘白禁区",
                enableReportDataToDouyin = false,
                loginType = new[] { "channel" },
                openActivationCode = false,
                qqGroup = (string?)null
            }
        };

        return Ok(rsp);
    }

    [HttpGet("/seasun/config")]
    [HttpPost("/seasun/config")]
    public IActionResult GetSeasunConfig()
    {
        object rsp = new
        {
            code = 0,
            data = new
            {
                platformPrivacyAgreement = "https://www.amazingseasun.com/privacy.html?lang=zh-Hant&gamecode=200001086",
                payType = new[] { "mycard" },
                loginType = new[] { "mail", "google", "twitter", "guest", "steam" },
                closeGeetest = true,
                userAgreement = "https://www.amazingseasun.com/user.html?lang=zh-Hant&gamecode=111111680",
                privacyAgreement = "https://www.amazingseasun.com/privacy.html?lang=zh-Hant&gamecode=111111680",
                initPrivacyUpdateTime = 0,
                platformUserAgreement = "https://www.amazingseasun.com/user.html?lang=zh-Hant&gamecode=200001086",
                accountPublicKey = "",
                payChannel = (string[]?)null,
                registerPrivacyUrl = "https://xgsdk.xoyo.games:13443/seasun/privacy-agreement/200001086/register/privacy.html?language=zh-Hant",
                loginPrivacyUrl = "https://xgsdk.xoyo.games:13443/seasun/privacy-agreement/111111680/login/privacy.html?language=zh-Hant"
            },
            msg = "操作成功"
        };

        return Ok(rsp);
    }

    private static AccountData? ResolveAccountByUid(string? uid)
    {
        if (int.TryParse(uid, out var parsedUid))
            return AccountData.GetAccountByUid(parsedUid);

        return null;
    }

    private static AccountData? ResolveAccountForSdkLogin(string? email, string? uid, string? token)
    {
        if (!string.IsNullOrWhiteSpace(token))
        {
            var accountByComboToken = AccountData.GetAccountByComboToken(token);
            if (accountByComboToken != null)
                return accountByComboToken;

            var accountByDispatchToken = AccountData.GetAccountByDispatchToken(token);
            if (accountByDispatchToken != null)
                return accountByDispatchToken;
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var accountByEmail = AccountData.GetAccountByEmail(email);
            if (accountByEmail != null)
                return accountByEmail;
        }

        return ResolveAccountByUid(uid);
    }

    private async Task<string?> GetJsonBodyValue(string propertyName)
    {
        if (!Request.HasJsonContentType())
            return null;

        Request.EnableBuffering();
        Request.Body.Position = 0;

        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        Request.Body.Position = 0;

        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            return document.RootElement.TryGetProperty(propertyName, out var value)
                ? value.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private IActionResult BuildLoginFailedResponse(string message)
    {
        object rsp = new
        {
            code = 1001,
            data = (object?)null,
            msg = message
        };

        return Ok(rsp);
    }

    private IActionResult BuildNotFoundResponse(string message)
    {
        object rsp = new
        {
            code = 1001,
            data = (object?)null,
            msg = message
        };

        return Ok(rsp);
    }

    [HttpGet("/seasun/loginByToken")]
    [HttpPost("/seasun/loginByToken")]
    public async Task<IActionResult> LoginByToken(
        [FromQuery] string? uid,
        [FromQuery] string? token,
        [FromForm] string? form_uid,
        [FromForm] string? form_token
    )
    {
        var finalUid = uid ?? form_uid ?? await GetJsonBodyValue("uid");
        var finalToken = token ?? form_token ?? await GetJsonBodyValue("token");
        var account = ResolveAccountForSdkLogin(null, finalUid, finalToken);
        if (account == null)
        {
            var fallbackName = !string.IsNullOrEmpty(finalUid) ? $"guest_{finalUid}" : "guest";
            account = AccountData.GetAccountByUserName(fallbackName);
            if (account == null && ConfigManager.Config.ServerOption.AutoCreateUser)
            {
                AccountData.CreateAccount(fallbackName, 0, "123456");
                account = AccountData.GetAccountByUserName(fallbackName);
            }
        }

        if (account == null)
            return BuildLoginFailedResponse("Account not found.");

        var responseUid = account.Uid.ToString();
        var responseToken = account.EnsureComboToken();

        object rsp = new
        {
            code = 0,
            data = new
            {
                associatedAccounts = Array.Empty<string>(),
                isFirstLogin = false,
                isNeedKoreaSciAuth = false,
                ksOpenId = $"ks_{responseUid}",
                nickname = account.Username,
                passportId = responseUid,
                playerFillAgeUrl = "",
                status = 0,
                thirdPartyUid = "",
                token = responseToken,
                type = "guest",
                uid = account.Uid
            },
            msg = "操作成功"
        };

        return Ok(rsp);
    }

    [HttpGet("/seasun/login")]
    [HttpPost("/seasun/login")]
    public async Task<IActionResult> Login(
        [FromQuery] string? uid,
        [FromQuery] string? token,
        [FromQuery] string? email,
        [FromQuery] string? account,
        [FromForm] string? form_uid,
        [FromForm] string? form_token,
        [FromForm] string? form_email,
        [FromForm] string? form_account
    )
    {
        var finalEmail = email ?? form_email ?? account ?? form_account ?? await GetJsonBodyValue("email") ?? await GetJsonBodyValue("account");
        if (!string.IsNullOrWhiteSpace(finalEmail))
        {
            var normalizedEmail = finalEmail.Trim();
            var accountData = AccountData.GetAccountByEmail(normalizedEmail) ?? AccountData.GetAccountByUserName(normalizedEmail);
            if (accountData == null)
            {
                if (!ConfigManager.Config.ServerOption.AutoCreateUser) return BuildLoginFailedResponse("Account not found.");
                AccountData.CreateAccount(normalizedEmail, 0, "123456");
                accountData = AccountData.GetAccountByEmail(normalizedEmail) ?? AccountData.GetAccountByUserName(normalizedEmail);
            }

            if (accountData != null)
            {
                var finalUidValue = accountData.Uid.ToString();
                var finalTokenValue = accountData.EnsureComboToken();

                object emailLoginRsp = new
                {
                    code = 0,
                    data = new
                    {
                        associatedAccounts = Array.Empty<string>(),
                        isFirstLogin = false,
                        isNeedKoreaSciAuth = false,
                        ksOpenId = $"ks_{finalUidValue}",
                        nickname = accountData.Username,
                        passportId = finalUidValue,
                        playerFillAgeUrl = "",
                        status = 0,
                        thirdPartyUid = "",
                        token = finalTokenValue,
                        type = "guest",
                        uid = accountData.Uid
                    },
                    msg = "操作成功"
                };

                return Ok(emailLoginRsp);
            }
        }

        var finalUid = uid ?? form_uid ?? await GetJsonBodyValue("uid");
        var finalToken = token ?? form_token ?? await GetJsonBodyValue("token");
        var resolvedAccount = ResolveAccountForSdkLogin(finalEmail, finalUid, finalToken);
        if (resolvedAccount == null)
        {
            var fallbackName = !string.IsNullOrEmpty(finalUid) ? $"guest_{finalUid}" : "guest";
            resolvedAccount = AccountData.GetAccountByUserName(fallbackName);
            if (resolvedAccount == null && ConfigManager.Config.ServerOption.AutoCreateUser)
            {
                AccountData.CreateAccount(fallbackName, 0, "123456");
                resolvedAccount = AccountData.GetAccountByUserName(fallbackName);
            }
        }

        if (resolvedAccount == null)
            return BuildLoginFailedResponse("Account not found.");

        var responseUid = resolvedAccount.Uid.ToString();
        var responseToken = resolvedAccount.EnsureComboToken();

        object rsp = new
        {
            code = 0,
            data = new
            {
                associatedAccounts = Array.Empty<string>(),
                isFirstLogin = false,
                isNeedKoreaSciAuth = false,
                ksOpenId = $"ks_{responseUid}",
                nickname = resolvedAccount.Username,
                passportId = responseUid,
                playerFillAgeUrl = "",
                status = 0,
                thirdPartyUid = "",
                token = responseToken,
                type = "guest",
                uid = resolvedAccount.Uid
            },
            msg = "操作成功"
        };

        return Ok(rsp);
    }

    [HttpGet("/seasun/getAccountInfoForGame")]
    [HttpPost("/seasun/getAccountInfoForGame")]
    public IActionResult GetAccountInfoForGame(
        [FromQuery] string? uid,
        [FromForm] string? form_uid
    )
    {
        var account = ResolveAccountByUid(uid ?? form_uid);
        if (account == null)
            return BuildNotFoundResponse("Account not found.");

        var uidString = account.Uid.ToString();

        object rsp = new
        {
            code = 0,
            data = new
            {
                bindAccountTypes = new[] { "google" },
                isNeedKoreaSciAuth = false,
                nickname = account.Username,
                passportId = uidString,
                playerFillAgeUrl = "",
                status = 0,
                thirdPartyUid = "",
                uid = account.Uid
            },
            msg = "操作成功"
        };

        return Ok(rsp);
    }

    [HttpPost("/bisdk/batchpush")]
    public IActionResult GetBatchPush()
    {
        object rsp = new
        {
            code = 0,
            data = (object?)null,
            msg = "操作成功"
        };

        return Ok(rsp);
    }

    [HttpGet("/query")]
    [HttpPost("/query")]
    public IActionResult GetQuery([FromQuery] string? version, [FromQuery] string? platform)
    {
        try
        {
            var list = BuildServerQueryList();
            var json = JsonSerializer.Serialize(list);
            Logger.GetByClassName().Info($"GET /query version={version} platform={platform} body={json}");
            return Content(json, "application/json");
        }
        catch (Exception ex)
        {
            Logger.GetByClassName().Error($"GET /query failed: {ex}");
            return Content("[]", "application/json");
        }
    }

    [HttpGet("/query_version={version}")]
    public IActionResult GetQueryVersionV1(string version)
    {
        return Ok(BuildServerList(version));
    }

    [HttpGet("/query_version")]
    [HttpPost("/query_version")]
    public IActionResult GetQueryVersionV2([FromQuery] string? version)
    {
        return Ok(BuildServerList(version ?? ""));
    }

    [HttpGet("/api/serverlist")]
    [HttpPost("/api/serverlist")]
    public IActionResult GetServerList()
    {
        return Ok(BuildServerList());
    }

    [HttpGet("/account/query-uid/{appId}")]
    [HttpPost("/account/query-uid/{appId}")]
    public async Task<IActionResult> QueryUid(
        string appId,
        [FromQuery] string? authInfo,
        [FromQuery] string? uid,
        [FromForm] string? form_authInfo,
        [FromForm] string? form_uid
    )
    {
        var finalAuthInfo = authInfo ?? form_authInfo ?? await GetJsonBodyValue("authInfo");
        var finalUid = uid ?? form_uid ?? ExtractUid(finalAuthInfo) ?? await GetJsonBodyValue("uid");
        var account = ResolveAccountByUid(finalUid);
        var resUid = account != null ? account.Uid.ToString() : (!string.IsNullOrWhiteSpace(finalUid) ? finalUid : "10001");

        object rsp = new
        {
            code = "0",
            msg = "success",
            data = new
            {
                uid = $"jinshan__{resUid}",
                historyPlatform = new[] { "PC" }
            }
        };

        return Ok(rsp);
    }

    [HttpGet("/v6/config/{appId}")]
    [HttpPost("/v6/config/{appId}")]
    public IActionResult GetV6Config(string appId)
    {
        var cfgObj = new
        {
            loginType = new[] { "channel" },
            canTouristPay = false,
            certification = "2"
        };
        object rsp = new
        {
            code = 1,
            msg = "操作成功",
            responseSuccess = true,
            data = cfgObj,
            config = cfgObj
        };
        return Ok(rsp);
    }

    [HttpGet("/v6/loginCaptcha/{appId}")]
    [HttpPost("/v6/loginCaptcha/{appId}")]
    public IActionResult LoginCaptchaV6(string appId)
    {
        object rsp = new
        {
            code = 1,
            msg = "操作成功",
            responseSuccess = true,
            fuzzyMobile = "199****1164",
            fuzzyMobileList = Array.Empty<string>(),
            needCaptcha = false
        };
        return Ok(rsp);
    }

    [HttpGet("/v6/sendSms/{appId}")]
    [HttpPost("/v6/sendSms/{appId}")]
    [HttpGet("/v6/sendCode/{appId}")]
    [HttpPost("/v6/sendCode/{appId}")]
    [HttpGet("/v6/smsCode/{appId}")]
    [HttpPost("/v6/smsCode/{appId}")]
    [HttpGet("/v6/sendSmsCode/{appId}")]
    [HttpPost("/v6/sendSmsCode/{appId}")]
    [HttpGet("/v6/mobileCode/{appId}")]
    [HttpPost("/v6/mobileCode/{appId}")]
    [HttpGet("/v6/sendMobileCode/{appId}")]
    [HttpPost("/v6/sendMobileCode/{appId}")]
    public IActionResult SendMobileCodeV6(string appId)
    {
        return Ok(new { code = 1, msg = "验证码已发送", responseSuccess = true, leftSeconds = 60 });
    }

    [HttpGet("/v6/loginBySms/{appId}")]
    [HttpPost("/v6/loginBySms/{appId}")]
    public async Task<IActionResult> LoginBySmsV6(
        string appId,
        [FromQuery] string? mobile,
        [FromQuery] string? phone,
        [FromQuery] string? account,
        [FromQuery] string? code,
        [FromQuery] string? smsCode,
        [FromForm] string? form_mobile,
        [FromForm] string? form_phone,
        [FromForm] string? form_account
    )
    {
        var inputAccount = mobile 
            ?? form_mobile
            ?? await GetJsonBodyValue("mobile") 
            ?? phone 
            ?? form_phone
            ?? await GetJsonBodyValue("phone") 
            ?? account 
            ?? form_account
            ?? await GetJsonBodyValue("account") 
            ?? "13800000000";

        var accountData = AccountData.GetAccountByUserName(inputAccount);
        if (accountData == null)
        {
            if (ConfigManager.Config.ServerOption.AutoCreateUser)
            {
                AccountData.CreateAccount(inputAccount, 0, "123456");
                accountData = AccountData.GetAccountByUserName(inputAccount);
            }
        }

        if (accountData == null)
        {
            return Ok(new { code = 0, msg = "账号创建失败", responseSuccess = false });
        }

        var uidStr = accountData.Uid.ToString();
        var tokenStr = accountData.EnsureComboToken();

        object rsp = new
        {
            code = 1,
            msg = "操作成功",
            expId = accountData.Uid,
            passportId = uidStr,
            uid = uidStr,
            token = tokenStr,
            hasBindedPhone = true,
            hasBindedEmail = true,
            accountType = 0,
            needVerifyIdCard = false,
            forcedVerifyIdCard = false,
            canTouristPay = false,
            certification = "2",
            fuzzyMobile = inputAccount.Length >= 7 ? $"{inputAccount[..3]}****{inputAccount[^4..]}" : "199****1164",
            needUpPsw = false,
            upPswMsg = (string?)null,
            faceDetectUrl = (string?)null,
            leftSeconds = "-1",
            idNumber = "410101199001011234",
            fuzzyPassportId = uidStr,
            newAccount = false,
            responseSuccess = true
        };

        return Ok(rsp);
    }

    [HttpGet("/v6/verifyMobileCode/{appId}")]
    [HttpPost("/v6/verifyMobileCode/{appId}")]
    public IActionResult VerifyMobileCodeV6(string appId)
    {
        return Ok(new { code = 1, msg = "操作成功", responseSuccess = true });
    }

    [HttpGet("/v6/verifyIdCard/{appId}")]
    [HttpPost("/v6/verifyIdCard/{appId}")]
    [HttpGet("/v6/idCard/{appId}")]
    [HttpPost("/v6/idCard/{appId}")]
    public IActionResult VerifyIdCardV6(string appId)
    {
        return Ok(new { code = 1, msg = "操作成功", certification = "2", responseSuccess = true });
    }

    [HttpGet("/privacy/gttest/gttest.html")]
    public IActionResult PrivacyGeetestHtml()
    {
        var html = @"<!DOCTYPE html><html><head><meta charset='utf-8'></head><body>
<script>
    try {
        var res = {
            lot_number: 'local',
            captcha_output: 'local',
            pass_token: 'local',
            gen_time: '1787069167',
            captcha_id: '720b159c165092060616c174e37e0c4e'
        };
        if (window.cefQuery) {
            window.cefQuery({
                request: JSON.stringify({ key: 'xg_browser_geetest_result', code: '0', data: { result_str: JSON.stringify(res) } }),
                onSuccess: function() {},
                onFailure: function() {}
            });
        }
        window.close();
    } catch(e) {}
</script>
</body></html>";
        return Content(html, "text/html", Encoding.UTF8);
    }

    [HttpGet("/v6/login/{appId}")]
    [HttpPost("/v6/login/{appId}")]
    public async Task<IActionResult> LoginV6(
        string appId,
        [FromQuery] string? uid,
        [FromQuery] string? token,
        [FromQuery] string? account,
        [FromQuery] string? password,
        [FromQuery] string? username,
        [FromQuery] string? mobile,
        [FromQuery] string? phone,
        [FromForm] string? form_uid,
        [FromForm] string? form_token,
        [FromForm] string? form_account,
        [FromForm] string? form_password,
        [FromForm] string? form_username,
        [FromForm] string? form_mobile,
        [FromForm] string? form_phone
    )
    {
        var inputAccount = account 
            ?? form_account 
            ?? await GetJsonBodyValue("account")
            ?? username
            ?? form_username
            ?? await GetJsonBodyValue("username")
            ?? mobile
            ?? form_mobile
            ?? await GetJsonBodyValue("mobile")
            ?? phone
            ?? form_phone
            ?? await GetJsonBodyValue("phone")
            ?? uid 
            ?? form_uid 
            ?? await GetJsonBodyValue("uid") 
            ?? "player";

        var inputPassword = password ?? form_password ?? await GetJsonBodyValue("password") ?? "123456";

        var accountData = AccountData.GetAccountByUserName(inputAccount);
        if (accountData == null)
        {
            if (ConfigManager.Config.ServerOption.AutoCreateUser)
            {
                AccountData.CreateAccount(inputAccount, 0, inputPassword);
                accountData = AccountData.GetAccountByUserName(inputAccount);
            }
        }

        if (accountData == null)
        {
            return Ok(new { code = 0, msg = "账号创建失败", responseSuccess = false });
        }

        var uidStr = accountData.Uid.ToString();
        var tokenStr = accountData.EnsureComboToken();

        object rsp = new
        {
            code = 1,
            msg = "操作成功",
            expId = accountData.Uid,
            passportId = uidStr,
            uid = uidStr,
            token = tokenStr,
            hasBindedPhone = true,
            hasBindedEmail = true,
            accountType = 0,
            needVerifyIdCard = false,
            forcedVerifyIdCard = false,
            canTouristPay = false,
            certification = "2",
            fuzzyMobile = inputAccount.Length >= 7 ? $"{inputAccount[..3]}****{inputAccount[^4..]}" : "199****1164",
            needUpPsw = false,
            upPswMsg = (string?)null,
            faceDetectUrl = (string?)null,
            leftSeconds = "-1",
            idNumber = "410101199001011234",
            fuzzyPassportId = uidStr,
            newAccount = false,
            responseSuccess = true
        };

        return Ok(rsp);
    }

    [HttpGet("/v6/loginByToken/{appId}")]
    [HttpPost("/v6/loginByToken/{appId}")]
    [HttpGet("/v6/quickLogin/{appId}")]
    [HttpPost("/v6/quickLogin/{appId}")]
    [HttpGet("/v6/autoLogin/{appId}")]
    [HttpPost("/v6/autoLogin/{appId}")]
    public async Task<IActionResult> LoginByTokenV6(
        string appId,
        [FromQuery] string? uid,
        [FromQuery] string? token,
        [FromForm] string? form_uid,
        [FromForm] string? form_token
    )
    {
        var finalUid = uid ?? form_uid ?? await GetJsonBodyValue("uid");
        var finalToken = token ?? form_token ?? await GetJsonBodyValue("token");
        var account = ResolveAccountForSdkLogin(null, finalUid, finalToken);
        if (account == null && !string.IsNullOrEmpty(finalUid))
        {
            account = ResolveAccountByUid(finalUid);
        }

        if (account == null)
        {
            var fallbackName = !string.IsNullOrEmpty(finalUid) ? $"player_{finalUid}" : "player";
            account = AccountData.GetAccountByUserName(fallbackName);
            if (account == null && ConfigManager.Config.ServerOption.AutoCreateUser)
            {
                AccountData.CreateAccount(fallbackName, 0, "123456");
                account = AccountData.GetAccountByUserName(fallbackName);
            }
        }

        if (account == null)
        {
            return Ok(new { code = 718, msg = "请重新登录", responseSuccess = false });
        }

        var uidStr = account.Uid.ToString();
        var tokenStr = account.EnsureComboToken();

        object rsp = new
        {
            code = 1,
            msg = "操作成功",
            expId = account.Uid,
            passportId = uidStr,
            uid = uidStr,
            token = tokenStr,
            hasBindedPhone = true,
            hasBindedEmail = true,
            accountType = 0,
            needVerifyIdCard = false,
            forcedVerifyIdCard = false,
            canTouristPay = false,
            certification = "2",
            fuzzyMobile = "199****1164",
            needUpPsw = false,
            upPswMsg = (string?)null,
            faceDetectUrl = (string?)null,
            leftSeconds = "-1",
            idNumber = "410101199001011234",
            fuzzyPassportId = uidStr,
            newAccount = false,
            responseSuccess = true
        };

        return Ok(rsp);
    }

    [HttpGet("/account/qrcode/gen")]
    [HttpPost("/account/qrcode/gen")]
    [HttpGet("/v6/account/qrcode/gen")]
    [HttpPost("/v6/account/qrcode/gen")]
    [HttpGet("/qrcode/gen")]
    [HttpPost("/qrcode/gen")]
    public IActionResult GetQrCodeGen([FromQuery] string? appId)
    {
        var qrId = Guid.NewGuid().ToString("N");
        object rsp = new
        {
            code = "0",
            msg = "success",
            data = new
            {
                qrcode = qrId,
                type = "login",
                expireTime = 600,
                downloadUrl = ""
            }
        };
        return Ok(rsp);
    }

    [HttpGet("/account/qrcode/refresh")]
    [HttpPost("/account/qrcode/refresh")]
    [HttpGet("/v6/account/qrcode/refresh")]
    [HttpPost("/v6/account/qrcode/refresh")]
    [HttpGet("/qrcode/refresh")]
    [HttpPost("/qrcode/refresh")]
    public IActionResult GetQrCodeRefresh([FromQuery] string? appId)
    {
        var qrId = Guid.NewGuid().ToString("N");
        object rsp = new
        {
            code = "0",
            msg = "success",
            data = new
            {
                qrcode = qrId,
                type = "login",
                expireTime = 600,
                downloadUrl = ""
            }
        };
        return Ok(rsp);
    }

    [HttpGet("/account/qrcode/status")]
    [HttpPost("/account/qrcode/status")]
    [HttpGet("/v6/account/qrcode/status")]
    [HttpPost("/v6/account/qrcode/status")]
    [HttpGet("/qrcode/status")]
    [HttpPost("/qrcode/status")]
    [HttpGet("/pay/qrcode/status")]
    [HttpPost("/pay/qrcode/status")]
    public IActionResult GetQrCodeStatus([FromQuery] string? qrcode, [FromQuery] string? appId)
    {
        var accountData = AccountData.GetAccountByUserName("player") ?? AccountData.GetAccountByUid(10001);
        if (accountData == null && ConfigManager.Config.ServerOption.AutoCreateUser)
        {
            AccountData.CreateAccount("player", 0, "123456");
            accountData = AccountData.GetAccountByUserName("player");
        }

        var tokenStr = accountData?.EnsureComboToken() ?? "local_offline_token";

        object rsp = new
        {
            code = "0",
            msg = "success",
            data = new
            {
                status = 2,
                statusMsg = "成功",
                qrToken = tokenStr,
                token = tokenStr,
                uid = accountData?.Uid ?? 10001,
                passportId = accountData?.Uid.ToString() ?? "10001",
                leftSeconds = (int?)null,
                antiAddictionExpiredTime = (string?)null
            }
        };
        return Ok(rsp);
    }

    [HttpGet("/account/qrcode/loginByToken")]
    [HttpPost("/account/qrcode/loginByToken")]
    [HttpGet("/v6/account/qrcode/loginByToken")]
    [HttpPost("/v6/account/qrcode/loginByToken")]
    [HttpGet("/qrcode/loginByToken")]
    [HttpPost("/qrcode/loginByToken")]
    public async Task<IActionResult> LoginByQrToken(
        [FromQuery] string? appId,
        [FromQuery] string? qrToken,
        [FromQuery] string? token,
        [FromForm] string? form_qrToken,
        [FromForm] string? form_token
    )
    {
        var finalToken = qrToken ?? form_qrToken ?? token ?? form_token ?? await GetJsonBodyValue("qrToken") ?? await GetJsonBodyValue("token");
        var accountData = AccountData.GetAccountByComboToken(finalToken ?? "")
            ?? AccountData.GetAccountByUserName("player")
            ?? AccountData.GetAccountByUid(10001);

        if (accountData == null && ConfigManager.Config.ServerOption.AutoCreateUser)
        {
            AccountData.CreateAccount("player", 0, "123456");
            accountData = AccountData.GetAccountByUserName("player");
        }

        if (accountData == null)
            return BuildLoginFailedResponse("Account not found.");

        var uidStr = accountData.Uid.ToString();
        var tokenStr = accountData.EnsureComboToken();

        object rsp = new
        {
            code = 1,
            msg = "操作成功",
            expId = accountData.Uid,
            passportId = uidStr,
            uid = uidStr,
            token = tokenStr,
            hasBindedPhone = true,
            hasBindedEmail = true,
            accountType = 0,
            needVerifyIdCard = false,
            forcedVerifyIdCard = false,
            canTouristPay = false,
            certification = "2",
            fuzzyMobile = "199****1164",
            needUpPsw = false,
            upPswMsg = (string?)null,
            faceDetectUrl = (string?)null,
            leftSeconds = "-1",
            idNumber = "410101199001011234",
            fuzzyPassportId = uidStr,
            newAccount = false,
            responseSuccess = true
        };
        return Ok(rsp);
    }

    [HttpGet("/qrcode/invalid")]
    [HttpPost("/qrcode/invalid")]
    [HttpGet("/account/qrcode/invalid")]
    [HttpPost("/account/qrcode/invalid")]
    public IActionResult GetQrCodeInvalid()
    {
        return Ok(new { code = "0", msg = "success", data = (object?)null });
    }

    [HttpGet("/v6/gameLoginLogout/{appId}")]
    [HttpPost("/v6/gameLoginLogout/{appId}")]
    public IActionResult GameLoginLogoutV6(string appId)
    {
        return Ok(new { code = 1, msg = "操作成功", responseSuccess = true });
    }

    [HttpGet("/data/report")]
    [HttpPost("/data/report")]
    [HttpGet("/data/report/v3")]
    [HttpPost("/data/report/v3")]
    [HttpGet("/data/report/{version}")]
    [HttpPost("/data/report/{version}")]
    public IActionResult DataReport()
    {
        return Ok(new { code = "0", msg = "success" });
    }

    [HttpGet("/health")]
    public IActionResult HealthCheck()
    {
        object rsp = new
        {
            status = "ok",
            service = Config.GameServer.GameServerName
        };

        return Ok(rsp);
    }

    [HttpPost("/api/auth/guest")]
    public IActionResult AuthGuest([FromQuery] string? Token)
    {
        object rsp = new
        {
            Provider = "Guest",
            Token = Token,
            Account = "Account",
            Pid = "123813131321312"
        };

        return Ok(rsp);
    }
}
