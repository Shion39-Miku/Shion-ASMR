$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$root=Split-Path -Parent $PSScriptRoot
$projectXml=[xml](Get-Content -LiteralPath (Join-Path $root 'src\Nocturne\Nocturne.csproj') -Raw -Encoding UTF8)
$version=[string]$projectXml.Project.PropertyGroup.Version
$archive=Join-Path $root ('发行\Tsukisaki-Shion-'+$version+'-source.zip')
# Include source and public documentation, skipping old local resources and generated output.
$files=@(
    foreach ($name in @('README.md','AGENTS.md','LICENSE','THIRD-PARTY-NOTICES.md','Directory.Build.props','NuGet.Config','.gitignore','docs\usage.md')) {
        Get-Item -LiteralPath (Join-Path $root $name)
    }
    foreach ($name in @('src','scripts','docs\images','test-fixtures')) {
        Get-ChildItem -LiteralPath (Join-Path $root $name) -Recurse -File | Where-Object {
            $_.FullName -notmatch '\\(bin|obj|__pycache__)\\|\\Assets\\Startup\\' -and
            ($_.Extension -in '.cs','.csproj','.xaml','.manifest','.ps1','.py','.ttf','.png','.md','.txt','.flac','.ogg','.mp4','.wmv' -or $_.Name -eq 'packages.lock.json')
        }
    }
)
New-Item -ItemType Directory -Force (Split-Path -Parent $archive) | Out-Null
$stream=[IO.File]::Create($archive)
$zip=[IO.Compression.ZipArchive]::new($stream,[IO.Compression.ZipArchiveMode]::Create)
try{foreach($file in $files){$relative=$file.FullName.Substring($root.Length+1).Replace('\','/');[IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file.FullName,('Tsukisaki-Shion-'+$version+'-source/'+$relative),[IO.Compression.CompressionLevel]::Optimal) | Out-Null}}finally{$zip.Dispose();$stream.Dispose()}
Get-Item -LiteralPath $archive | Select-Object Name,Length
