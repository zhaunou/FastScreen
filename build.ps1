$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /out:"$PSScriptRoot\LocalScreenshot.exe" "$PSScriptRoot\LocalScreenshot.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Output "Built: $PSScriptRoot\LocalScreenshot.exe"
