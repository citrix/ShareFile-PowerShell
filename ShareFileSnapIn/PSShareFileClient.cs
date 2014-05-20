using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ShareFile.Api.Client;
using ShareFile.Api.Client.Events;
using ShareFile.Api.Models;
using ShareFile.Api.Powershell.Browser;
using ShareFile.Api.Powershell.Properties;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShareFile.Api.Powershell
{
    /// <summary>
    /// Class the encapsulates a ShareFile Client SDK and an OAuth authentication context.
    /// It extends the SDK Client to support persistency of authentication context (OAuth, or
    /// saved passwords); handling of OAuth expiration; and support for multiple authentication
    /// domains for Connectors.
    /// </summary>
    public class PSShareFileClient
    {
        public string Path { get; set; }

        public ShareFileClient Client { get; set; }

        public AuthenticationDomain PrimaryDomain { get; set; }

        public Dictionary<string, AuthenticationDomain> Domains { get; set; }

        public PSShareFileClient(string path, AuthenticationDomain domain = null)
        {
            Path = path;
            Domains = new Dictionary<string, AuthenticationDomain>();
            if (domain != null)
            {
                Domains.Add(domain.Uri, domain);
                PrimaryDomain = domain;
            }
        }

        public Session GetSession()
        {
            if (PrimaryDomain != null)
            {
                // Add username/password credentials if domain is ShareFile; Account is known; and credentials were provided
                if (PrimaryDomain.IsShareFileUri
                    && PrimaryDomain.Credential != null 
                    && PrimaryDomain.Account != null 
                    && !PrimaryDomain.Account.Equals("secure") 
                    && !PrimaryDomain.Account.Equals("g"))
                {
                    AuthenticateUsernamePassword(PrimaryDomain.Account, PrimaryDomain.Domain, PrimaryDomain.Credential.UserName, PrimaryDomain.Credential.Password);
                }
                // Handle all other auth scenarios
                if (Client == null) Client = CreateClient(PrimaryDomain);
                return Client.Sessions.Get().Execute();
            }
            return null;
        }

        public AuthenticationDomain AuthenticateUsernamePassword(string domain)
        {
            var dialog = new BasicAuthDialog(domain);
            var output = dialog.ShowDialog();
            if (output == System.Windows.Forms.DialogResult.OK)
            {
                var authDomain = new AuthenticationDomain() { Uri = domain };
                if (authDomain.Provider.Equals(Resources.ShareFileProvider) && authDomain.IsShareFileUri)
                {
                    authDomain = AuthenticateUsernamePassword(authDomain.Account, authDomain.Domain, dialog.Username, dialog.Password);
                }
                else
                {
                    authDomain.Credential = new NetworkCredential(dialog.Username, dialog.Password);
                    if (Client == null) Client = CreateClient(authDomain);
                    Client.AddCredentials(new Uri(authDomain.Uri), "basic", authDomain.Credential);
                }
                if (Domains.ContainsKey(authDomain.Uri)) Domains.Remove(authDomain.Uri);
                Domains.Add(authDomain.Uri, authDomain);
                Save();
                return authDomain;
            }
            return null;
        }

        public AuthenticationDomain AuthenticateUsernamePassword(string account, string domain, string username, SecureString securePassword)
        {
            string password = null;
            IntPtr bstr = Marshal.SecureStringToBSTR(securePassword);
            try
            {
                password = Marshal.PtrToStringBSTR(bstr);
            }
            finally
            {
                Marshal.FreeBSTR(bstr);
            }
            return AuthenticateUsernamePassword(account, domain, username, password);
        }

        public AuthenticationDomain AuthenticateUsernamePassword(string account, string domain, string username, string password)
        {
            AuthenticationDomain authDomain = null;
            var accountAndDomain = string.IsNullOrEmpty(account) ? domain : string.Format("{0}.{1}", account, domain);
            var uri = string.Format("https://{0}/oauth/token", accountAndDomain);
            var request = HttpWebRequest.CreateHttp(uri);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            using (var body = new StreamWriter(request.GetRequestStream()))
            {

                body.Write(
                    Uri.EscapeUriString(string.Format("grant_type=password&client_id={0}&client_secret={1}&username={2}&password={3}",
                    Resources.ClientId,
                    Resources.ClientSecret,
                    username,
                    password)));
            }
            var response = (HttpWebResponse)request.GetResponse();
            using (var body = new StreamReader(response.GetResponseStream()))
            {
                var jobj = JsonConvert.DeserializeObject<JObject>(body.ReadToEnd());
                authDomain = new AuthenticationDomain();
                authDomain.OAuthToken = jobj["access_token"] != null ? jobj["access_token"].ToString() : null;
                uri = string.Format("https://{0}/{1}/{2}", accountAndDomain, Resources.ShareFileProvider, Resources.DefaultApiVersion);
                authDomain.Uri = uri;
                if (Domains.ContainsKey(authDomain.Uri)) Domains.Remove(authDomain.Uri);
                Domains.Add(authDomain.Uri, authDomain);
                if (Client == null) Client = CreateClient(authDomain);
                var session = Client.Sessions.Get().Execute();
                authDomain.AuthID = session.Id;
                this.Save();
            }
            return authDomain;
        }

        public AuthenticationDomain AuthenticateOAuth(AuthenticationDomain domain)
        {
            var webDialogThread = new WebDialogThread(this, domain);
            var t = new Thread(webDialogThread.Run);
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            var ResultDomain = webDialogThread.Result;
            if (ResultDomain != null && ResultDomain.OAuthToken != null)
            {
                if (Domains.ContainsKey(ResultDomain.Uri)) Domains.Remove(ResultDomain.Uri);
                Domains.Add(ResultDomain.Uri, ResultDomain);
                PrimaryDomain = ResultDomain;
                Client.AddOAuthCredentials(new Uri(ResultDomain.Uri), ResultDomain.OAuthToken);
                Client.BaseUri = new Uri(ResultDomain.Uri);
                this.Save();
            }
            else throw new Exception("Invalid Authentication");
            return ResultDomain;
        }

        private EventHandlerResponse OnDomainChange(HttpWebRequest request, Redirection redirection)
        {
            foreach (var id in Domains.Keys)
            {
                if (redirection.Uri.ToString().EndsWith(Domains[id].Domain))
                {
                    // we know about this domain; validate the login
                    // not implemented yet... 
                    // return;
                    return null;
                }
            }
            return new EventHandlerResponse() { Action = EventHandlerResponseAction.Redirect, Redirection = redirection };
        }

        private EventHandlerResponse OnException(HttpResponseMessage response, int retryCount)
        {
            if (retryCount > int.Parse(Resources.MaxExceptionRetry))
            {
                return new EventHandlerResponse() { Action = EventHandlerResponseAction.Throw };
            }
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Check cached credentials for this API domain
                foreach (var id in Domains.Keys)
                {
                    if (response.RequestMessage.RequestUri.Host.EndsWith(Domains[id].Domain))
                    {
                        if (!string.IsNullOrEmpty(Domains[id].OAuthToken) && retryCount == 0)
                        {
                            Client.AddOAuthCredentials(new Uri(Domains[id].Uri), Domains[id].OAuthToken);
                            return new EventHandlerResponse() { Action = EventHandlerResponseAction.Retry };
                        }
                        else if (!string.IsNullOrEmpty(Domains[id].OAuthRefreshToken) && retryCount == 1)
                        {
                            var token = GetTokenResponse(
                                string.Format("https://{0}.{1}/oauth/token", Domains[id].Account, Domains[id].Domain),
                                string.Format("grant_type=refresh_token&redirect_uri={0}&refresh_token={1}&client_id={2}&client_secret={3}",
                                    Resources.RedirectURL, Domains[id].OAuthRefreshToken, Resources.ClientId, Resources.ClientSecret));
                            Domains[id].OAuthToken = token.AccessToken;
                            Domains[id].OAuthRefreshToken = token.RefreshToken;
                            Client.AddOAuthCredentials(new Uri(Domains[id].Uri), token.AccessToken);
                            Save();
                            return new EventHandlerResponse() { Action = EventHandlerResponseAction.Retry };
                        }
                        else if (Domains[id].Credential != null && retryCount <= 1)
                        {
                            if (Domains[id].Provider.Equals(Resources.ShareFileProvider) && Domains[id].IsShareFileUri)
                            {
                                Domains[id] = AuthenticateUsernamePassword(Domains[id].Account, Domains[id].Domain, Domains[id].Credential.UserName, Domains[id].Credential.Password);
                            }
                            else
                            {
                                Client.AddCredentials(new Uri(Domains[id].Uri), "basic", Domains[id].Credential);
                            }
                            return new EventHandlerResponse() { Action = EventHandlerResponseAction.Retry };
                        }
                    }
                }
                // No cached, or bad cached credentials for this domain request
                // Check the headers for authentication challenges
                var authDomain = new AuthenticationDomain() { Uri = response.RequestMessage.RequestUri.ToString() };
                IEnumerable<string> values = null;
                if (response.Headers.TryGetValues("WWW-Authenticate", out values))
                {
                    foreach (var authMethodString in values)
                    {
                        var authMethod = authMethodString.Split(' ')[0].Trim();
                        if (authMethod.Equals("Bearer"))
                        {
                            authDomain = this.AuthenticateOAuth(authDomain);
                            if (authDomain != null) return new EventHandlerResponse() { Action = authDomain != null ? EventHandlerResponseAction.Retry : EventHandlerResponseAction.Throw };
                        }
                        else if (retryCount == 0 && (authMethod.Equals("NTLM") || authMethod.Equals("Kerberos")))
                        {
                            // if retryCount > 0, then Network Credentials failed at least once; 
                            // causes fallback to username/password 
                            Client.AddCredentials(new Uri(authDomain.Uri), authMethod, CredentialCache.DefaultNetworkCredentials);
                            return new EventHandlerResponse() { Action = EventHandlerResponseAction.Retry };
                        }
                        else if (authMethod.Equals("Basic") || authMethod.Equals("NTLM"))
                        {
                            authDomain = this.AuthenticateUsernamePassword(authDomain.Uri);
                            return new EventHandlerResponse() { Action = authDomain != null ? EventHandlerResponseAction.Retry : EventHandlerResponseAction.Throw };
                        }
                    }
                }
            }
            return new EventHandlerResponse() { Action = EventHandlerResponseAction.Throw };
        }

        public void Load()
        {
            using (var reader = new StreamReader(new FileStream(Path, FileMode.Open)))
            {
                AuthenticationDomain domain = null;
                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (line != null)
                    {
                        line = line.Trim();
                        if (line.StartsWith("["))
                        {
                            if (domain != null)
                            {
                                Domains.Add(domain.Id, domain);
                            }
                            domain = new AuthenticationDomain();
                            domain.Id = line.Substring(1, line.IndexOf(']') - 1);
                        }
                        else if (line.StartsWith("Provider")) domain.Provider = line.Split('=')[1].Trim();
                        else if (line.StartsWith("IsDefault")) domain.IsDefault = bool.Parse(line.Split('=')[1].Trim());
                        else if (line.StartsWith("Uri")) domain.Uri = line.Split('=')[1].Trim();
                        else if (line.StartsWith("AccessToken")) domain.OAuthToken = line.Split('=')[1].Trim();
                        else if (line.StartsWith("RefreshToken")) domain.OAuthRefreshToken = line.Split('=')[1].Trim();
                        else if (line.StartsWith("Account")) domain.Account = line.Split('=')[1].Trim();
                        else if (line.StartsWith("Domain")) domain.Domain = line.Split('=')[1].Trim();
                        else if (line.StartsWith("ApiVersion")) domain.ApiVersion = line.Split('=')[1].Trim();
                    }
                }
                if (domain != null)
                {
                    Domains.Add(domain.Id, domain);
                }
            }
            foreach (var id in Domains.Keys)
            {
                if (Domains[id].IsDefault)
                {
                    Client = CreateClient(Domains[id]);
                    break;
                }
            }
        }

        public void Save()
        {
            if (Path != null)
            {
                if (Path.IndexOf('.') < 0) Path += ".sfps";
                using (var writer = new StreamWriter(new FileStream(Path, FileMode.Create)))
                {
                    foreach (var id in Domains.Keys)
                    {
                        if (Domains[id].Domain != null && !Domains[id].Domain.ToLower().Equals("secure"))
                        {
                            writer.WriteLine(string.Format("[{0}]", id));
                            writer.WriteLine("Provider=" + Domains[id].Provider);
                            writer.WriteLine("IsDefault=" + (Client.BaseUri.ToString().ToLower() == Domains[id].Uri.ToLower()));
                            writer.WriteLine("Version=" + Resources.Version);
                            writer.WriteLine("Uri=" + Domains[id].Uri ?? "");
                            writer.WriteLine("AccessToken=" + Domains[id].OAuthToken ?? "");
                            writer.WriteLine("RefreshToken=" + Domains[id].OAuthRefreshToken ?? "");
                            writer.WriteLine("Account=" + Domains[id].Account ?? "");
                            writer.WriteLine("Domain=" + Domains[id].Domain ?? "");
                            writer.WriteLine("ApiVersion=" + Domains[id].ApiVersion ?? "");
                        }
                    }
                }
            }
        }

        private ShareFileClient CreateClient(AuthenticationDomain domain)
        {
            var client = new ShareFileClient(domain.Uri);
            if (domain.OAuthToken != null)
            {
                client.AddOAuthCredentials(new Uri(domain.Uri), domain.OAuthToken);
            }
            client.AddExceptionHandler(OnException);
            return client;
        }

        private OAuthToken GetTokenResponse(string uri, string body)
        {
            var request = HttpWebRequest.CreateHttp(uri);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            using (var writer = new StreamWriter(request.GetRequestStream()))
            {
                writer.Write(body);
            }
            var response = (HttpWebResponse)request.GetResponse();
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                return JsonConvert.DeserializeObject<OAuthToken>(reader.ReadToEnd());
            }
        }

        private class WebDialogThread
        {
            public AuthenticationDomain Result
            {
                get
                {
                    _waitHandle.WaitOne();
                    return _result;
                }
            }
            private AuthenticationDomain _result = null;
            private AutoResetEvent _waitHandle = new AutoResetEvent(false);

            private PSShareFileClient _psClient;

            private AuthenticationDomain _requestDomain;

            public WebDialogThread(PSShareFileClient psClient, AuthenticationDomain domain)
            {
                _psClient = psClient;
                _requestDomain = domain;
            }

            public void Run()
            {
                AuthenticationDomain authDomain = null;
                var browser = new OAuthAuthenticationForm();
                browser.AddUrlEventHandler(Resources.RedirectURL, uri =>
                {
                    try
                    {
                        // return is <redirect_url>/oauth/authorize#access_token=...&subdomain=...&apicp=...&appcp=...
                        var query = new Dictionary<string, string>();
                        OAuthToken token = null;
                        if (!string.IsNullOrEmpty(uri.Query))
                        {
                            foreach (var kvp in uri.Query.Substring(1).Split('&'))
                            {
                                var kvpSplit = kvp.Split('=');
                                if (kvpSplit.Length == 2) query.Add(kvpSplit[0], kvpSplit[1]);
                            }
                            var subdomain = query["subdomain"];
                            var apiCP = query["apicp"];
                            var appCP = query["appcp"];

                            token = _psClient.GetTokenResponse(
                                string.Format("https://{0}.{1}/oauth/token", subdomain, appCP),
                                string.Format("grant_type=authorization_code&code={0}&client_id={1}&client_secret={2}&requirev3=true", query["code"],
                                    Resources.ClientId, Resources.ClientSecret));

                            authDomain = new AuthenticationDomain();
                            authDomain.OAuthToken = token.AccessToken;
                            authDomain.OAuthRefreshToken = token.RefreshToken;
                            authDomain.Account = token.Subdomain;
                            authDomain.Provider = Resources.ShareFileProvider;
                            authDomain.Domain = token.ApiCP;
                            authDomain.ApiVersion = Resources.DefaultApiVersion;
                            return true;
                        }
                        else return false;
                    }
                    catch (Exception)
                    {
                        return true;
                    }
                });
                browser.Navigate(new Uri(string.Format("https://{0}.{1}/oauth/authorize?response_type=code&client_id={2}&redirect_uri={3}&autoredirect=true",
                    _requestDomain.Account != null ? _requestDomain.Account : "secure",
                    _requestDomain.Domain,
                    Resources.ClientId,
                    Uri.EscapeUriString(Resources.RedirectURL))));
                _result = authDomain;
                _waitHandle.Set();
            }
        }
    }


}
