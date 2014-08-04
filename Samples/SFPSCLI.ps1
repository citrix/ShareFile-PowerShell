

Function Set-FileTimeStamps
{
Param (
    [Parameter(mandatory=$true)]
    [string[]]$Path,
    [datetime]$DateCreated = (Get-Date),
    [datetime]$DateModified = (Get-Date),
    [datetime]$DateAccessed = (Get-Date))
    Get-ChildItem -Path $path | ForEach-Object {
        $_.CreationTime = $DateCreated
        $_.LastAccessTime = $DateAccessed
        $_.LastWriteTime = $DateModified
    }
}

Function SyncFilesFromCloud([string]$ShareFilePath, [string] $LocalPath, [string]$login, [boolean]$overwrite, [boolean]$recursive){

    $sfClient = Get-SfClient -Name $login
    if (!(Test-Path sfdrive:)){
        New-PSDrive -Name sfdrive -PSProvider ShareFile -Root "\" -Client $sfClient
    }

    #Check starting folder date in cloud to see if updated since last sync
    $sfItems = Get-ChildItem ("sfdrive:" + $ShareFilePath)

    if (!(Test-Path $LocalPath)){
        mkdir $LocalPath
    }

    Write-Host "Downloading files from ShareFile..."
    Write-Host "  from " $ShareFilePath " to " $LocalPath

    foreach ($sfItem in $sfItems){
        switch ($sfItem.GetType())
        {
            "ShareFile.Api.Models.Folder" {
                #here we check ProgenyEditDate and recurse if necessary
                if ($recursive){
                    if($sfItem.ProgenyEditDate -gt $LastSync){
                        $pathString = $LocalPath + $sfItem.Name + "/"
                        mkdir $pathString -ErrorAction SilentlyContinue
                        $SFPath = $ShareFilePath + $sfItem.Name + "/"
                        SyncFilesFromCloud $SFPath $pathString $login $overwrite $recursive

                    }
                }
            }
        
            "ShareFile.Api.Models.File" {
                #check if file already exists locally and needs to be updated
                $localItem = $LocalPath + $sfItem.Name
                $localFile = Get-ChildItem $localItem -ErrorAction SilentlyContinue
                
                #write debugging info
                Write-Host "Checking cloud file:  " $sfItem.Name
                if ($localFile.Length -eq 0) {Write-Host "  No local copy so downloading file..."}
                elseif ($localFile.LastWriteTime.ToUniversalTime() -lt $sfItem.CreationDate) {
                    Write-Host "  Local copy is older than cloud so downloading file..."
                    Write-Host "  Local (UTC): " $localFile.LastWriteTime.ToUniversalTime().ToString() " | Cloud (UTC): " $sfItem.CreationDate.ToString()
                }

                #check if there is no local file or cloud is newer
                if ($localFile.Length -eq 0 -or $localFile.LastWriteTime.ToUniversalTime() -lt $sfItem.CreationDate) {
                  
                    if ($overwrite){
                        Copy-SfItem ("sfdrive:" + $ShareFilePath + $sfItem.Name) $LocalPath -Force 1
                    } else {
                        Copy-SfItem ("sfdrive:" + $ShareFilePath + $sfItem.Name) $LocalPath
                    }
                    Set-FileTimeStamps -Path $localItem -DateCreated $sfItem.CreationDate -DateModified $sfItem.CreationDate
                }
                else {
                    Write-Host "  File up-to-date"
                }
            }
        }
    }

}

Function SyncFilesToCloud ( [string]$ShareFilePath,[string] $LocalPath, [string]$login) {

    $sfClient = Get-SfClient -Name $login

    #ensure the sfdrive exists
    if (!(Test-Path sfdrive:)){
        New-PSDrive -Name sfdrive -PSProvider ShareFile -Root "\" -Client $sfClient 
    }

    $drive = "sfdrive:" + $ShareFilePath
    #force the upload of the files to ShareFile
    Copy-SfItem $LocalPath $drive  -Force 1
    
}


Function CleanFilesandFolders([string]$login, [string] $ShareFilePath, [string] $LocalPath, [string] $type, [string] $location){
    $sfClient = Get-SfClient -Name $login

    #for recursion we don't want to recreate the drive if it exists
    if (!(Test-Path checkcontent:)){
        New-PSDrive -Name checkcontent -PSProvider ShareFile -Root "\" -Client $sfClient
    }

    #Get the starting folder in cloud
    $sfItems = Get-ChildItem ("checkcontent:" + $ShareFilePath)

    #loop through all the items in the folder
    foreach ($sfItem in $sfItems){
        #when we clean the initial item will actually be the first folder when cleaning locally
        if (($LocalPath.EndsWith($sfItem.Name +"/"))-or($LocalPath.EndsWith($sfItem.Name +"\"))){
            Write-Host $LocalPath
            $SFPath = $ShareFilePath  + $sfItem.Name + "/"
            CleanFilesandFolders $login $SFPath $LocalPath $type $location
            $localItem = $LocalPath
            $localFolder = Get-Item $localItem -ErrorAction SilentlyContinue
            $localFolder.Delete()
        } else {   
            switch ($sfItem.GetType())
            {
                "ShareFile.Api.Models.Folder" {
                    #here we recurse through all the folders
                    $localItem = $LocalPath + $sfItem.Name + "/"
                    $localFolder = Get-Item $localItem -ErrorAction SilentlyContinue
                    $SFPath = $ShareFilePath  + $sfItem.Name + "/"
                    CleanFilesandFolders $login $SFPath $localItem $type $location
                    if (($type -eq "folders")-and($location -eq "cloud")){
                        #after we return from the recursion we delete the folder
                        Send-SfRequest -Client $sfClient -Entity Items -Method DELETE -Id $sfItem.Id
                    } elseif (($type -eq "folders") -and ($location -eq "local")){
                        $localFolder.Delete()
                    }           
                }
        
                "ShareFile.Api.Models.File" {

                    if ($type -eq "files"){
                        #check if file already exists locally and needs to be updated
                        $localItem = $LocalPath  + $sfItem.Name 
                        $localFile = Get-ChildItem $localItem -ErrorAction SilentlyContinue
                        #write debugging info
                        Write-Host "Checking cloud file:  " $sfItem.Name
                        #check if there is no local file or cloud is newer
                        if ($localFile.Length -eq 0 -and $localFile.LastWriteTime.ToUniversalTime() -gt $sfItem.CreationDate) {
                            #Safety check to make sure the file has been downloaded, if something doesn't match we leave it 
                        } elseif ($location -eq "cloud") {
                            #Cloud File matches local so it can be deleted
                            Send-SfRequest -Client $sfClient -Entity Items -Method DELETE -Id $sfItem.Id
                        } elseif ($location -eq "local") {
                            #Local File matches Cloud so it can be deleted
                            $localFile.Delete()
                        }
                    }
                }
            }
        }
    }

}

#This function takes a ShareFile folder Id and generates a path that can be used with the PowerShell commands
Function FindCloudFolderPathFromID([string]$FolderId, [string] $login){
    $sfClient = Get-SfClient -Name $login
    $folder = Send-SfRequest -Client $sfClient -Entity Items -Id $FolderId
    #this command returns a path of folder Ids which we need to parse to string names
    $folderPath = $folder.Path.ToString()
    #remove first / before split so it doesn't create empty string
    $fullPath = $folderPath.Substring(1).Split("/")
    $returnPath = "/"
    $count = 0
    #the returned path contains root and the account folder which we do not need
    for ($x=2;$x-lt$fullPath.Count; $x++ ){
        $item = $fullPath[$x]
        #we want to grab the name of the folder in the path
        $tmpItem = Send-SfRequest -Client $sfClient -Entity Items -Id $item
        $returnPath += $tmpItem.Name + "/"
       
        $count++
    }
    $returnPath += $folder.Name + "/"
    return $returnPath
}

Function SFPS {
     Param ( 
     [string]$ShareFileFolderID,
     [string]$LocalPath,
     [string]$sf,
     [string]$l,
     [switch]$deep,
     [switch]$recursive,
     [switch]$overwrite,
     [switch]$move,
     [switch]$keepfolders,
     [string]$login="C:\tmp\",
     [switch]$up,
     [switch]$down,
     [switch]$upload,
     [switch]$download,
     [switch]$strict
     )


    Add-PSSnapin ShareFile -ErrorAction SilentlyContinue

    #Check to make sure drives do not exist
    if (Test-Path sfdrive:){
        Remove-PSDrive -Name sfdrive
    }
    if (Test-Path checkcontent:){
        Remove-PSDrive -Name checkcontent
    }

    if ($login.ToString().EndsWith(".sfps")) {
        $localLogin = $login
    } elseif (($login.ToString().EndsWith("\"))-or($login.ToString().EndsWith("/"))){
        $localLogin = $login + "sflogin.sfps"  
    } elseif ($login.ToString().Contains("\")) {
        $localLogin = $login + "\sflogin.sfps"
    } elseif ($login.ToString().Contains("/")) {
        $localLogin = $login + "/sflogin.sfps"
    }

    #login check
    $loginFile = Get-ChildItem $localLogin -ErrorAction SilentlyContinue
    if ($loginFile.Length -eq 0){
        New-SfClient -Name $localLogin
    } 

    #do parameter null check and consolidate
    if(!$ShareFileFolderID){
        if($sf){$ShareFileFolderID = $sf}
    }

    #find full path for ShareFile folder based upon folder name
    $ShareFilePath = FindCloudFolderPathFromID $ShareFileFolderID $localLogin

    if(!$LocalPath){
        if($l){$LocalPath = $l}
    }
    #ensure local path is the format we need
    if (($LocalPath.ToString().Contains("/"))-and(!($LocalPath.ToString().EndsWith("/")))) {
        $LocalPath += "/"
    } elseif (($LocalPath.ToString().Contains("\"))-and(!($LocalPath.ToString().EndsWith("\")))) {
        $LocalPath += "\"
    }

    if(!$recursive){
        if($deep){$recursive = $true}
        else { $recursive = $false }
    } else {
        $recursive = $true
    }

    if ($overwrite){
        $overwrite = $true
    } else {
        $overwrite = $false
    }

    if(!$upload){
        if($up){$upload = $up}
    }

    if(!$download){
        if($down){$download=$down}
    }

    #strict - if strict is chosen we clean out the files and folders before sync so only exact files and directories will exist
    if ($strict){
        if($upload){
            CleanFilesandFolders $localLogin $ShareFilePath $LocalPath "files" "cloud"
            CleanFilesandFolders $localLogin $ShareFilePath $LocalPath "folders" "cloud"
        }
        if($download){
            CleanFilesandFolders $localLogin $ShareFilePath $LocalPath "files" "local"
            CleanFilesandFolders $localLogin $ShareFilePath $LocalPath "folders" "local"
        }
    }

    #sync up or down
    if ($upload){
        #upload is always recursive
        SyncFilesToCloud $ShareFilePath $LocalPath $localLogin
    }

    if ($download){
        SyncFilesFromCloud $ShareFilePath $LocalPath $localLogin $overwrite $recursive
    }


    #cleanup files or folders if those options are chosen

    if ($move){
        #if we uploaded with the move parameter we want to clean local files and possibly folders
        if($upload){
            CleanFilesandFolders $localLogin $ShareFilePath $LocalPath "files" "local"
            if (!$keepfolders){
                CleanFilesandFolders $localLogin $ShareFilePath $LocalPath "folders" "local"
            }
        }
        #if we downloaded with the move parameter we want to clean cloud files and possibly folders
        if($download){
            CleanFilesandFolders $localLogin $ShareFilePath $LocalPath "files" "cloud"
            if (!$keepfolders){
                CleanFilesandFolders $localLogin $ShareFilePath $LocalPath "folders" "cloud"
            }
        }
    }
}

