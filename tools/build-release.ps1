param([string]$OutputDirectory='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$project=Get-Content -LiteralPath (Join-Path $root 'project.json') -Raw | ConvertFrom-Json
if($project.version -ne '1.0.0'){throw 'Update InstallCore.Version and release metadata together before changing the version'}
if(-not $OutputDirectory){$OutputDirectory=Join-Path $root 'release'}
$destination=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$runtime=Join-Path $root '.build/release/runtime'
& (Join-Path $PSScriptRoot 'build-windows.ps1') -OutputDirectory $runtime
Add-Type -AssemblyName System.IO.Compression.FileSystem
$payload=Join-Path $root '.build/release/payload.zip'
if(Test-Path -LiteralPath $payload){Remove-Item -LiteralPath $payload -Force}
[IO.Compression.ZipFile]::CreateFromDirectory($runtime,$payload,[IO.Compression.CompressionLevel]::Optimal,$false)
$compiler=Join-Path $env:SystemRoot 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$installer=Join-Path $destination ('CodexStartupWindows-Setup-'+$project.version+'.exe')
& $compiler /nologo /codepage:65001 /target:winexe /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Web.Extensions.dll /reference:Microsoft.CSharp.dll ('/win32icon:'+(Join-Path $root 'assets/windows-codex.ico')) ('/resource:'+$payload+',payload.zip') ('/out:'+$installer) (Join-Path $root 'windows/InstallCore.cs') (Join-Path $root 'windows/InstallIntegration.cs') (Join-Path $root 'windows/Setup.cs')
if($LASTEXITCODE -ne 0){throw 'Setup build failed'}
$portable=Join-Path $destination ('CodexStartupWindows-Portable-'+$project.version+'.zip')
if(Test-Path -LiteralPath $portable){Remove-Item -LiteralPath $portable -Force}
[IO.Compression.ZipFile]::CreateFromDirectory($runtime,$portable,[IO.Compression.CompressionLevel]::Optimal,$true)
Copy-Item -LiteralPath (Join-Path $root 'docs/windows.md') -Destination (Join-Path $destination '使用说明.md') -Force
$hashes=@(Get-Item -LiteralPath $installer,$portable | ForEach-Object {[ordered]@{file=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash;bytes=$_.Length}})
[ordered]@{project=$project.name;version=$project.version;files=$hashes;installerSigned=$false} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $destination 'release-manifest.json') -Encoding UTF8
$hashes | ForEach-Object {$_.sha256+'  '+$_.file} | Set-Content -LiteralPath (Join-Path $destination 'SHA256SUMS.txt') -Encoding ASCII
Write-Output $installer
Write-Output $portable
