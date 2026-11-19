[CmdletBinding(SupportsShouldProcess=$true)]
param([Parameter(Mandatory=$true)][string]$Path)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)).TrimEnd('\')
$allowed=Join-Path $root '.validation'
$target=(Resolve-Path -LiteralPath $Path).Path.TrimEnd('\')
if(-not $target.StartsWith($allowed+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Only isolated child directories inside this workspace .validation can be removed.'}
if(-not ((Test-Path (Join-Path $target 'verification.json')) -or (Test-Path (Join-Path $target 'fatal-test-error.json')))){throw 'No verification marker: preserve this directory.'}
if((Get-Item -LiteralPath $target).Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'Linked directory: preserve it.'}
if(@(Get-ChildItem -LiteralPath $target -Recurse -Attributes ReparsePoint).Count){throw 'Linked content: preserve this directory.'}
$profile=Join-Path $env:LOCALAPPDATA 'Nocturne\settings.json'
if(Test-Path -LiteralPath $profile){
 $settings=Get-Content -LiteralPath $profile -Raw | ConvertFrom-Json
 $references=@($settings.Roots)+@($settings.FixedImage)+@($settings.WorkCovers.PSObject.Properties.Value)+@($settings.Covers.PSObject.Properties.Value)+@($settings.Tracks.PSObject.Properties.Name)+@($settings.Tracks.PSObject.Properties.Value|ForEach-Object {$_.Subtitle})
 foreach($reference in $references){if(-not $reference){continue};$absolute=[IO.Path]::GetFullPath($reference);if($absolute.Equals($target,[StringComparison]::OrdinalIgnoreCase)-or $absolute.StartsWith($target+'\',[StringComparison]::OrdinalIgnoreCase)-or $target.StartsWith($absolute.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Directory overlaps user media references: preserve it.'}}
}
foreach($process in (Get-CimInstance Win32_Process|Where-Object {$_.ExecutablePath})){if($process.ExecutablePath.StartsWith($target+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'A program is running from this directory.'}}
$bytes=(Get-ChildItem -LiteralPath $target -Recurse -File|Measure-Object Length -Sum).Sum
if($PSCmdlet.ShouldProcess($target,'Delete verified generated test fixtures')){Remove-Item -LiteralPath $target -Recurse;[pscustomobject]@{Removed=$target;LogicalBytes=$bytes}}
