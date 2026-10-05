param([string]$OutputDirectory='')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(-not $OutputDirectory){$OutputDirectory=Join-Path $root 'dist\Codex Startup Windows'}
$destination=[IO.Path]::GetFullPath($OutputDirectory)
foreach($directory in @('','windows','extension','assets')){New-Item -ItemType Directory -Force -Path (Join-Path $destination $directory) | Out-Null}
$files=@('index.html','style.css','animation.js','image-settings.js','assets\avatar.jpg','assets\artwork.jpg','assets\contours.js','extension\cdp.mjs','extension\payload.mjs','extension\renderer.mjs','extension\wallpaper.css','extension\windows.mjs','windows\launch.ps1','windows\verify-port.ps1','windows\Activation.cs')
foreach($file in $files){Copy-Item -LiteralPath (Join-Path $root $file) -Destination (Join-Path $destination $file) -Force}
$animatedFiles=@('assets\artwork.gif','assets\artwork-contours.js')
$animatedPair=@($animatedFiles | Where-Object {Test-Path -LiteralPath (Join-Path $root $_)}).Count -eq 2
foreach($file in $animatedFiles){
    if($animatedPair){Copy-Item -LiteralPath (Join-Path $root $file) -Destination (Join-Path $destination $file) -Force}
    elseif(Test-Path -LiteralPath (Join-Path $destination $file)){Remove-Item -LiteralPath (Join-Path $destination $file) -Force}
}
$compiler=Join-Path $env:SystemRoot 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(-not(Test-Path -LiteralPath $compiler)){throw '.NET Framework C# compiler not found'}
Copy-Item -LiteralPath (Join-Path $root 'assets\windows-codex.ico') -Destination (Join-Path $destination 'assets\windows-codex.ico') -Force
& $compiler /nologo /codepage:65001 /target:winexe /optimize+ /reference:System.Windows.Forms.dll ('/win32icon:'+(Join-Path $root 'assets\windows-codex.ico')) ('/out:'+(Join-Path $destination 'CodexStartup.exe')) (Join-Path $root 'windows\Launcher.cs')
if($LASTEXITCODE -ne 0){throw 'Windows launcher build failed'}
Copy-Item -LiteralPath (Join-Path $root 'docs\windows.md') -Destination (Join-Path $destination '使用说明.md') -Force
Copy-Item -LiteralPath (Join-Path $root 'docs\references.md') -Destination (Join-Path $destination 'references.md') -Force
Write-Output $destination
