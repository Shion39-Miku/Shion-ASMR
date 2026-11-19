param([string]$Output = '', [string]$Executable = '', [string]$CodecFixtures = '', [ValidateSet('current','previous','legacy')][string]$Suite='current')
$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
if(-not $Output){$Output=Join-Path $root '.validation\current'}
if(-not [IO.Path]::IsPathRooted($Output)){$Output=Join-Path $root $Output}
$Output=[IO.Path]::GetFullPath($Output)
if(-not $CodecFixtures){$CodecFixtures=Join-Path $root 'test-fixtures'}
$projectXml=[xml](Get-Content -LiteralPath (Join-Path $root 'src\Nocturne\Nocturne.csproj') -Raw -Encoding UTF8)
$version=[string]$projectXml.Project.PropertyGroup.Version
$portable=Join-Path $root ('发行\Tsukisaki-Shion-'+$version+'-win-x64\runtime\Shion.exe')
$exe=if($Executable){$Executable}elseif(Test-Path $portable){$portable}else{Join-Path $root 'src\Nocturne\bin\Release\net10.0-windows\win-x64\Nocturne.exe'}
if((Test-Path -LiteralPath $Output) -and @(Get-ChildItem -LiteralPath $Output -Force).Count){throw 'Use a fresh isolated output directory; archive reports before cleaning generated fixtures.'}
$env:NOCTURNE_CODEC_FIXTURES=$CodecFixtures
New-Item -ItemType Directory -Force $Output | Out-Null
$testArguments=@('--self-test',('"'+$Output+'"'))
if($Suite -eq 'current'){$testArguments+='--branding-release'}elseif($Suite -eq 'previous'){$testArguments+='--revision11-only'}else{Write-Warning 'Legacy assertions include superseded designs. Use current for the delivery checks.'}
$process=Start-Process -FilePath $exe -WorkingDirectory $root -ArgumentList $testArguments -WindowStyle Hidden -PassThru
$samples=[System.Collections.Generic.List[object]]::new()
while(-not $process.HasExited){
    $phase='initialization'
    try {
        $phaseFile=Join-Path $Output 'frame-phase.json'
        if(-not(Test-Path -LiteralPath $phaseFile)){$phaseFile=Join-Path $Output 'performance-phase.json'}
        if(Test-Path -LiteralPath $phaseFile){$item=Get-Content -LiteralPath $phaseFile -Raw -Encoding UTF8 | ConvertFrom-Json;$phase=if($item.scene){$item.scene}else{$item.phase}}
        $counter=Get-Counter '\GPU Engine(*)\Utilization Percentage' -SampleInterval 1 -MaxSamples 2 -ErrorAction Stop | Select-Object -Last 1
        $engines=@($counter.CounterSamples | Where-Object {$_.InstanceName -match ('^pid_'+$process.Id+'_')})
        $samples.Add([pscustomobject]@{time=[DateTimeOffset]::Now.ToString('o');phase=$phase;pid=$process.Id;engineSum=($engines | Measure-Object CookedValue -Sum).Sum;maxEngine=($engines | Measure-Object CookedValue -Maximum).Maximum;engineCount=$engines.Count})
    } catch {$samples.Add([pscustomobject]@{phase=$phase;error=$_.Exception.Message});Start-Sleep -Milliseconds 500}
    $process.Refresh()
}
$samples | ConvertTo-Json -Depth 6 | Set-Content -Encoding utf8 (Join-Path $Output 'gpu-samples.json')
Get-CimInstance Win32_OperatingSystem | Select-Object Caption,Version,TotalVisibleMemorySize | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $Output 'os.json')
Get-CimInstance Win32_Processor | Select-Object Name,NumberOfLogicalProcessors | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $Output 'cpu.json')
Get-CimInstance Win32_VideoController | Select-Object Name,DriverVersion,CurrentRefreshRate,CurrentHorizontalResolution,CurrentVerticalResolution | ConvertTo-Json | Set-Content -Encoding utf8 (Join-Path $Output 'gpu.json')
$report=Join-Path $Output 'verification.json'
$failed=if(Test-Path -LiteralPath $report){(Get-Content -LiteralPath $report -Raw -Encoding UTF8 | ConvertFrom-Json).failures}else{1}
Write-Output "Verification exited: $($process.ExitCode); failed checks: $failed; report: $Output"
if($failed -gt 0){exit 1}
exit $process.ExitCode
