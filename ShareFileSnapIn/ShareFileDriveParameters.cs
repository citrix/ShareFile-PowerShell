using System;
using System.Management.Automation;

namespace ShareFile.Api.Powershell
{
    public class ShareFileDriveParameters
    {
        public ShareFileDriveParameters()
        {
            Client = null;
            Id = null;
        }

        [Parameter(Mandatory=true)]
        public PSShareFileClient Client { get; set; }

        [Parameter(Mandatory = false)]
        public string Id { get; set; }
    }
}
