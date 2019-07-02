#Script to delete users and reassign items and groups


Function DeleteUsers{

#Optional define parameters on function
Param ( [Parameter(Mandatory, HelpMessage="Please define the UserType - client or employee")][string]$UserType,
        [Parameter(Mandatory, HelpMessage="Enter a UserID to reassign user data to [Format:GUID]")][string]$UserID_ItemReassign,
        [Parameter(Mandatory, HelpMessage="Enter a UserID to eassign user groups to [Format:GUID]")][string]$UserID_GroupReassign
       )

      $client = New-SfClient -Name "c:\tmp\sfclient.sfps"

      $sfUserObjects = Import-Csv ("C:\tmp\" + $UserType + ".csv")

      foreach($sfUser in $sfUserObjects)
      {
           Send-SfRequest -Client $client -Method Delete -Entity Users -Id $sfUser.UserId -Parameters @{"completely" = "true"; "itemsReassignTo" = "$UserID_ItemReassign"; "groupsReassignTo" = "$UserID_GroupReassign"}
      }

}

#New-SfClient -Name "c:\tmp\sfclient.sfps"
Add-PSSnapin ShareFile


#Alternatively Read-Host into variables
$vUserType = Read-Host -Prompt 'What user type will be deleted? (employee/client)'
$vUserID_ItemReassign = Read-Host -Prompt 'Enter a UserID to reassign user data to [Format:GUID]'
$vUserID_GroupReassign = Read-Host -Prompt 'Enter a UserID to eassign user groups to [Format:GUID]'

DeleteUsers -UserType $vUserType -UserID_ItemReassign $vUserID_ItemReassign -UserID_GroupReassign $vUserID_GroupReassign;