#Script to delete users found by the Disabled Users script


Function DeleteUsers{
Param ([Parameter(HelpMessage="Hi Keith")] [string]$UserType="employee")
      $client = New-SfClient -Name "c:\tmp\sfclient.sfps"

      $sfUserObjects = Import-Csv ("C:\tmp\" + $UserType + ".csv")

      foreach($sfUser in $sfUserObjects){
           Send-SfRequest -Client $client -Method Delete -Entity Users -Id $sfUser.UserId -Parameters @{"completely" = "true"}
      }

}

#New-SfClient -Name "c:\tmp\sfclient.sfps"
Add-PSSnapin ShareFile
DeleteUsers -UserType "employee";
DeleteUsers -UserType "client";