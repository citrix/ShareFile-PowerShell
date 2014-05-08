ShareFile-PowerShell
====================
The ShareFile PowerShell SDK is a PowerShell snap-in that provides support for saving a user login for use in scripts, provides access to the ShareFile API, and also a provider that can be used within PowerShell to map to a ShareFile account.

Download release v1.0 here: https://github.com/citrix/ShareFile-PowerShell/releases/tag/v1.0

INSTALLATION
To install the snap-in, just download the files in the "ShareFileSnapIn\bin\Release" folder and then follow these steps:
1. Right-click each .dll, select "Properties" and Unblock the DLL
2. Open a command prompt or PowerShell window as Administrator
3. Navigate to the directory where you copied the binaries
4. Run C:\Windows\Microsoft.NET\v4.0.30319\installutil -i .\ShareFileSnapIn.dll


USAGE-AUTHENTICATION
Once you have the snap-in registered, you can use in PowerShell as follows:

Load the snap-in:
    Import-Module ShareFileSnapIn.dll

Authenticate to a session (and save for future use):
    New-SfClient –Name mySubdomain

Note: The name provided here will be used to save the oAuth token for this connection. Subsequent scripts can access this user session without requiring an interactive user login or putting a password in your script. You should protect this token file as if it were credentials, though it can be revoked in the ShareFile application indepenent of password if there is ever a concern of inappropriate access.

Load a saved session:
    $sfClient = Get-SfClient –Name mySubdomain


USAGE-API
Once you have a session, you can make calls to the API as follows:
    Send-SFRequest –Client $sfClient –Method GET –Entity Users

Documentation for the ShareFile API entity model and more information is available here:
https://developer.sharefile.com


USAGE-PROVIDER
This snap-in also includes a provider that will allow you to access ShareFile content from within your scripts. This is useful for scripting out sync tasks that need to run unattended or to sync specific folders or in specific ways (like unidirectional sync.)

Create the provider:
    New-PSDrive -Name sf -PSProvider ShareFile -Root / -Client $sfClient

Access the provider:
    Set-Location sf:

Copy files to/from ShareFile:
    Copy-SFItem sf:/Folder/Folder C:\LocalFolder
    Copy-SFItem C:\LocalFolder\* sf:/Folder

Note: You can also use del (Remove-Item), cd (Set-Location), and dir (Get-ChildItem).
