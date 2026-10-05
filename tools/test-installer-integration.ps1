param([Parameter(Mandatory=$true)][string]$Installer)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$directory=Join-Path $root '.build/installer-tests'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$compiler=Join-Path $env:SystemRoot 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /codepage:65001 /debug+ /target:exe /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll /reference:System.Web.Extensions.dll /reference:Microsoft.CSharp.dll ('/out:'+(Join-Path $directory 'InstallerIntegrationTests.exe')) (Join-Path $root 'windows/InstallCore.cs') (Join-Path $root 'windows/InstallIntegration.cs') (Join-Path $root 'windows/ShellShortcut.cs') (Join-Path $PSScriptRoot 'InstallerIntegrationTests.cs')
if($LASTEXITCODE -ne 0){throw 'Installer integration test compilation failed'}
& (Join-Path $directory 'InstallerIntegrationTests.exe') ([IO.Path]::GetFullPath($Installer))
if($LASTEXITCODE -ne 0){throw 'Installer integration tests failed'}
