ShareFile-PowerShell
====================
The ShareFile PowerShell SDK is a PowerShell snap-in that provides support for saving a user login for use in scripts, provides access to the ShareFile API, and also a provider that can be used within PowerShell to map to a ShareFile account.

Download
----
Download release v1.0 here: https://github.com/citrix/ShareFile-PowerShell/releases/tag/v1.2


Install
----
    To install the snap-in, just download and extract the files in ShareFileSnapIn.zip from the link above and then follow these steps:
    * Right-click each .dll, select "Properties" and Unblock the DLL (or just run "gci | Unblock-File" in PowerShell)
    * Open a command prompt or PowerShell window as Administrator
    * Navigate to the directory where you copied the binaries
    * Run C:\Windows\Microsoft.NET\Framework\v4.0.30319\installutil -i .\ShareFileSnapIn.dll
    * (for x64) Run C:\Windows\Microsoft.NET\Framework64\v4.0.30319\installutil -i .\ShareFileSnapIn.dll

    Note: The PowerShell SDK is not signed, so you will need to modify the execution policy in PowerShell. The easiest way is to run "Set-ExecutionPolicy RemoteSigned", but check the documentation on script signing to understand what the options are. http://technet.microsoft.com/en-us/library/hh849812.aspx


References
----
For examples of usage, check out the PowerShell SDK Wiki here:
https://github.com/citrix/ShareFile-PowerShell/wiki
