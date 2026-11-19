param([string]$Destination='')
$ErrorActionPreference='Stop'
$executable=Join-Path $PSScriptRoot '月咲 Shion.exe'
if(-not (Test-Path -LiteralPath $executable)){throw 'Run the copy of this script inside the 月咲 Shion portable folder.'}
if(-not $Destination){$Destination=[Environment]::GetFolderPath('Programs')}
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$shortcut=(New-Object -ComObject WScript.Shell).CreateShortcut((Join-Path $Destination '月咲 Shion.lnk'))
$shortcut.TargetPath=$executable
$shortcut.WorkingDirectory=$PSScriptRoot
$shortcut.Description='月咲 Shion'
$shortcut.IconLocation=$executable+",0"
$shortcut.Save()
Write-Output (Join-Path $Destination '月咲 Shion.lnk')
