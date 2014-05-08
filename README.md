ShareFile-PowerShell
====================
The ShareFile PowerShell SDK is a PowerShell snap-in that provides support for saving a user login for use in scripts, provides access to the ShareFile API, and also a provider that can be used within PowerShell to map to a ShareFile account.

Download release v1.0 here: https://github.com/citrix/ShareFile-PowerShell/releases/tag/v1.0

Installation
-----------
To install the snap-in, just download the files in the "ShareFileSnapIn\bin\Release" folder and then follow these steps:
* Right-click each .dll, select "Properties" and Unblock the DLL
* Open a command prompt or PowerShell window as Administrator
* Navigate to the directory where you copied the binaries
* Run C:\Windows\Microsoft.NET\v4.0.30319\installutil -i .\ShareFileSnapIn.dll


For examples of usage, check out the PowerShell SDK Wiki here:
https://github.com/citrix/ShareFile-PowerShell/wiki
