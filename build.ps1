param([string]$OutputPath = (Join-Path $PSScriptRoot 'LocalScreenshot.exe'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object { $_.FullName }
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /out:"$OutputPath" $sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Output "Built: $OutputPath"
