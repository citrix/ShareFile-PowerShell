ShareFile-PowerShell
====================
The ShareFile PowerShell SDK is a PowerShell snap-in that provides support for saving a user login for use in scripts, provides access to the ShareFile API, and also a provider that can be used within PowerShell to map to a ShareFile account.

Download
----
Download the latest release here: https://github.com/citrix/ShareFile-PowerShell/releases

Requirements
----
The PowerShell SDK requires PowerShell version 4.0 which you can learn more about here
http://social.technet.microsoft.com/wiki/contents/articles/21016.how-to-install-windows-powershell-4-0.aspx

As well as .Net version 4.x
http://msdn.microsoft.com/en-us/library/5a4x27ek(v=vs.110).aspx

Install
----
    To install the snap-in, just download and extract the files in ShareFileSnapIn.zip from the link above 
    and then follow these steps:
    * Right-click each .dll, select "Properties" and Unblock the DLL (or just run "gci | Unblock-File" in PowerShell)
    * Open a command prompt or PowerShell window as Administrator
    * Navigate to the directory where you copied the binaries
    * Run C:\Windows\Microsoft.NET\Framework\v4.0.30319\installutil -i .\ShareFileSnapIn.dll
    * (for x64) Run C:\Windows\Microsoft.NET\Framework64\v4.0.30319\installutil -i .\ShareFileSnapIn.dll
    * Note: To uninstall, just run the above commands with -u instead of -i

    Note: The PowerShell SDK is not signed, so you will need to modify the execution policy in PowerShell. 
    The easiest way is to run "Set-ExecutionPolicy RemoteSigned", but check the documentation on script 
    signing to understand what the options are. http://technet.microsoft.com/en-us/library/hh849812.aspx


License
----
All code is licensed under the [MIT
License](https://github.com/citrix/ShareFile-PowerShell/blob/master/ShareFileSnapIn/LICENSE.txt).


References
----
For examples of usage, check out the PowerShell SDK Wiki here:
https://github.com/citrix/ShareFile-PowerShell/wiki

For some blogs on using the PoswerShell SDK take a look here:
http://blogs.citrix.com/tag/powershell-sdk/
