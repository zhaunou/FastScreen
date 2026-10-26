param([string]$OutputPath, [switch]$BuildTests)
$ErrorActionPreference = 'Stop'
$version = (Get-Content -LiteralPath (Join-Path $PSScriptRoot 'VERSION') -Raw).Trim()
if ($version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
    throw 'VERSION must contain MAJOR.MINOR.PATCH, for example 1.0.0.'
}
foreach ($part in $version.Split('.')) {
    if ([decimal]$part -gt 65534) { throw 'Each version component must be between 0 and 65534.' }
}
if (-not $OutputPath) {
    $OutputPath = if ($BuildTests) { Join-Path $PSScriptRoot 'obj\FastScreen.Tests.exe' } else { Join-Path $PSScriptRoot "FastScreen-$version.exe" }
}
$generatedFolder = Join-Path $PSScriptRoot 'obj'
New-Item -ItemType Directory -Force -Path $generatedFolder | Out-Null
$versionSource = Join-Path $generatedFolder 'Version.g.cs'
@"
[assembly: System.Reflection.AssemblyVersion("$version.0")]
[assembly: System.Reflection.AssemblyFileVersion("$version.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("$version")]
"@ | Set-Content -LiteralPath $versionSource -Encoding UTF8
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$productionFiles = @('AppInfo.cs', 'Design.cs', 'LocalScreenshot.cs', 'MainWindow.cs', 'Settings.cs', 'SettingsStorage.cs', 'SystemPrograms.cs', 'SingleInstance.cs', 'ScreenshotSound.cs', 'SelectionCursor.cs', 'LocalFolderGuard.cs')
$sources = @($productionFiles | ForEach-Object { Join-Path $PSScriptRoot $_ }) + @($versionSource)
$targetOptions = @('/target:winexe')
if ($BuildTests) {
    $testFiles = @('TestProgram.cs', 'BehaviorTests.cs', 'SettingsStorageTests.cs', 'SelfTest.cs')
    $sources += @($testFiles | ForEach-Object { Join-Path (Join-Path $PSScriptRoot 'tests') $_ })
    $targetOptions = @('/target:exe', '/main:LocalScreenshot.TestProgram')
}
$sound = Join-Path $PSScriptRoot 'assets\screenshot.wav'
if (-not (Test-Path -LiteralPath $sound)) { throw 'Screenshot sound asset is missing.' }
& $compiler /nologo $targetOptions /optimize+ /platform:anycpu /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /resource:"$sound",FastScreen.Screenshot.wav /out:"$OutputPath" $sources
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
Write-Output "Built: $OutputPath"
Write-Output "Version: $version"
