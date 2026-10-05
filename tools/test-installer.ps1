param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$directory=Join-Path $root '.build/installer-tests'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$compiler=Join-Path $env:SystemRoot 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /target:exe /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Web.Extensions.dll ('/out:'+(Join-Path $directory 'InstallerTests.exe')) (Join-Path $root 'windows/InstallCore.cs') (Join-Path $PSScriptRoot 'InstallerTests.cs')
if($LASTEXITCODE -ne 0){throw 'Installer test compilation failed'}
& (Join-Path $directory 'InstallerTests.exe')
if($LASTEXITCODE -ne 0){throw 'Installer tests failed'}
