using ShareFile.Api.Client;
using System.Management.Automation;

namespace ShareFile.Api.Powershell
{
    public class ShareFileDriveInfo : PSDriveInfo
    {
        public ShareFileClient Client { get; private set; }

        public string RootId { get; set; }

        public ShareFileDriveInfo(PSDriveInfo driveInfo, ShareFileDriveParameters driveParams)
            : base( driveInfo )
        {
            Client = driveParams.Client.Client;
            RootId = driveParams.Id;
        }      
    }
}
