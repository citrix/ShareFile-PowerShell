using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;
using ShareFile.Api;
using ShareFile.Api.Models;
using System.IO;
using ShareFile.Api.Client.Requests;
using System.Windows.Forms;
using ShareFile.Api.Powershell.Browser;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security;
using System.Runtime.InteropServices;
using System.Resources;
using Microsoft.Win32;

namespace ShareFile.Api.Powershell
{
    [Cmdlet(VerbsCommon.Get, Noun)]
    public class GetSfClient : PSCmdlet
    {
        private const string Noun = "SfClient";

        [Parameter(Position=0)]
        public string Name { get; set; }

        protected override void ProcessRecord()
        {
            if (Name.IndexOf('.') < 0) Name += ".sfps";
            var psc = new PSShareFileClient(Name);
            psc.Load();
            psc.Client.Sessions.Get().Execute();
            WriteObject(psc);
        }
    }
}
