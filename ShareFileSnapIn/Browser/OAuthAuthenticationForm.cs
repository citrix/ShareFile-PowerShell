using Newtonsoft.Json;
using ShareFile.Api.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShareFile.Api.Powershell.Browser
{
    public partial class OAuthAuthenticationForm : Form
    {
        public delegate bool UrlEventCallback(Uri uri);
        private Dictionary<string, UrlEventCallback> _urlEventHandlers = new Dictionary<string,UrlEventCallback>();

        public delegate bool DocumentEventCallback(Uri uri, Stream document);
        private List<DocumentEventCallback> _documentEventHandlers = new List<DocumentEventCallback>();

        public OAuthAuthenticationForm()
        {
            InitializeComponent();
            browser.AllowNavigation = true;
            browser.ScriptErrorsSuppressed = false;
            browser.ScrollBarsEnabled = true;
            browser.TabIndex = 1;
        }

        public void AddUrlEventHandler(string uri, UrlEventCallback handler)
        {
            _urlEventHandlers.Add(uri, handler);
        }

        public void AddDocumentEventHander(DocumentEventCallback handler)
        {
            _documentEventHandlers.Add(handler);
        }

        public void SetDocument(Uri uri, StreamReader sr)
        {
            string s = sr.ReadToEnd();
            browser.Navigate("about:blank");
            browser.Document.OpenNew(false);
            browser.Document.Write(s);
            browser.Refresh();
            browser.Url = uri;
        }

        public void Navigate(Uri uri)
        {
            browser.Navigate(uri);
            ShowDialog();
        }

        private void browser_Navigated(object sender, WebBrowserNavigatedEventArgs e)
        {
            foreach (var uri in _urlEventHandlers.Keys)
            {
                if (e.Url.ToString().StartsWith(uri))
                {
                    if (_urlEventHandlers[uri].Invoke(e.Url))
                    {
                        this.Close();
                    }
                }
            }
        }
    }
}
