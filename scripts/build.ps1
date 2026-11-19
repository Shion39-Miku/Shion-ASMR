param([switch]$NoRestore, [switch]$NoArchive)
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
$localSdk=Join-Path $root '.tools\dotnet\dotnet.exe'
$sdk=if(Test-Path $localSdk){$localSdk}else{'dotnet'}
$env:DOTNET_CLI_HOME=Join-Path $root '.tools\cli'
$env:DOTNET_NOLOGO='1'
$project=Join-Path $root 'src\Nocturne\Nocturne.csproj'
$projectXml=[xml](Get-Content -LiteralPath $project -Raw -Encoding UTF8)
$version=[string]$projectXml.Project.PropertyGroup.Version
if(-not $NoRestore){& $sdk restore $project -r win-x64 --locked-mode --configfile (Join-Path $root 'NuGet.Config');if($LASTEXITCODE){throw 'Restore failed'}}
$distribution=Join-Path $root ('发行\Tsukisaki-Shion-'+$version+'-win-x64')
$output=Join-Path $distribution 'runtime'
New-Item -ItemType Directory -Force $output | Out-Null
& $sdk publish $project --no-restore -c Release -r win-x64 --self-contained true -o $output -p:DebugType=None -p:DebugSymbols=false
if($LASTEXITCODE){throw 'Publish failed'}
# Keep the assembly identity and embedded font URI; only the public apphost is named Shion.
$legacyHost=Join-Path $output 'Nocturne.exe'
if(Test-Path -LiteralPath $legacyHost){Move-Item -LiteralPath $legacyHost -Destination (Join-Path $output 'Shion.exe') -Force}
Copy-Item -LiteralPath (Join-Path $root 'scripts\create-shortcut.ps1') -Destination $distribution
# Native dependency packages can copy their own debug symbols even when application symbols are disabled.
foreach($symbol in (Get-ChildItem -LiteralPath $output -File -Filter '*.pdb')){Remove-Item -LiteralPath $symbol.FullName}
# The only user guide is placed at the distribution root.
Copy-Item -LiteralPath (Join-Path $root 'LICENSE') -Destination $output
Copy-Item -LiteralPath (Join-Path $root 'THIRD-PARTY-NOTICES.md') -Destination $output
# Test reports and development files are distributed separately from the runnable folder.
$licenses=Join-Path $output 'licenses';New-Item -ItemType Directory -Force $licenses | Out-Null
Get-ChildItem -LiteralPath (Join-Path $root '.packages') -Directory | ForEach-Object {
    $packageName=$_.Name
    Get-ChildItem -LiteralPath $_.FullName -Recurse -File | Where-Object {$_.Name -match '^(LICENSE|COPYING|THIRD-PARTY-NOTICES)(\.|$)' -and $_.Length -lt 2000000} | ForEach-Object {
        $destination=Join-Path $licenses ($packageName+'-'+$_.Directory.Name+'-'+$_.Name)
        Copy-Item -LiteralPath $_.FullName -Destination $destination
    }
}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$versionSource=Join-Path $root '.tools\Shion.Launcher.Version.cs'
[IO.File]::WriteAllText($versionSource,('[assembly:System.Reflection.AssemblyVersion("'+$version+'.0")][assembly:System.Reflection.AssemblyFileVersion("'+$version+'.0")]'))
$icon=Join-Path (Split-Path -Parent $project) 'obj\shion.ico'
& $compiler /nologo /target:winexe /codepage:65001 /reference:System.Windows.Forms.dll ('/win32icon:'+$icon) ('/out:'+(Join-Path $distribution '月咲 Shion.exe')) (Join-Path $root 'scripts\Launcher.cs') $versionSource
if($LASTEXITCODE){throw 'Launcher build failed'}
@'
# 月咲 Shion

完整解压后，运行同目录的 `月咲 Shion.exe`。请保留 `runtime` 目录。
可用 Windows PowerShell 执行同目录的 `create-shortcut.ps1`，为当前用户创建开始菜单快捷方式。

[使用教程](https://github.com/Shion39-Miku/Shion-ASMR/blob/main/docs/usage.md)

[版本下载](https://github.com/Shion39-Miku/Shion-ASMR/releases)
'@ | Set-Content -LiteralPath (Join-Path $distribution '使用说明.md') -Encoding UTF8
$archive=Join-Path $root ('发行\Tsukisaki-Shion-'+$version+'-win-x64.zip')
if(-not $NoArchive){
    Compress-Archive -Path $distribution -DestinationPath $archive -Force
    Get-FileHash -LiteralPath $archive -Algorithm SHA256 | Format-List
}
