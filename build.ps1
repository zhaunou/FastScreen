param([string]$OutputPath)
$ErrorActionPreference = 'Stop'
$version = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
if ($version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
    throw 'VERSION must contain MAJOR.MINOR.PATCH, for example 1.0.0.'
}
foreach ($part in $version.Split('.')) {
    if ([decimal]$part -gt 65534) { throw 'Each version component must be between 0 and 65534.' }
}
if (-not $OutputPath) { $OutputPath = Join-Path $PSScriptRoot "FastScreen-$version.exe" }
$generatedFolder = Join-Path $PSScriptRoot 'obj'
New-Item -ItemType Directory -Force -Path $generatedFolder | Out-Null
$versionSource = Join-Path $generatedFolder 'Version.g.cs'
@"
[assembly: System.Reflection.AssemblyVersion("$version.0")]
[assembly: System.Reflection.AssemblyFileVersion("$version.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("$version")]
"@ | Set-Content -LiteralPath $versionSource -Encoding UTF8
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' | ForEach-Object { $_.FullName }) + @($versionSource)
$sound = Join-Path $PSScriptRoot 'assets\screenshot.wav'
if (-not (Test-Path -LiteralPath $sound)) { throw 'Screenshot sound asset is missing.' }
& $compiler /nologo /target:winexe /optimize+ /platform:anycpu /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /resource:"$sound",FastScreen.Screenshot.wav /out:"$OutputPath" $sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Output "Built: $OutputPath"
Write-Output "Version: $version"
