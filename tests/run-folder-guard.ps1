$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$generatedFolder = Join-Path $project 'obj'
New-Item -ItemType Directory -Force -Path $generatedFolder | Out-Null
$testExecutable = Join-Path $generatedFolder 'FolderGuardTests.exe'
& (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe') /nologo /target:exe /out:"$testExecutable" (Join-Path $project 'LocalFolderGuard.cs') (Join-Path $project 'SettingsStorage.cs') (Join-Path $PSScriptRoot 'FolderGuardTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
$fixture = Join-Path $generatedFolder ('folder-guard-' + [guid]::NewGuid().ToString('N'))
$real = Join-Path $fixture 'real'
$alias = Join-Path $fixture 'alias'
New-Item -ItemType Directory -Path $real -Force | Out-Null
try {
    New-Item -ItemType Junction -Path $alias -Target $real | Out-Null
    & $testExecutable $fixture
    if ($LASTEXITCODE -ne 0) { throw 'Folder guard tests failed.' }
} finally {
    # Remove the junction itself first; never recurse through a test link.
    $aliasItem = Get-Item -LiteralPath $alias -Force -ErrorAction SilentlyContinue
    if ($null -ne $aliasItem) {
        if (-not ($aliasItem.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Unexpected non-link fixture.' }
        [IO.Directory]::Delete($alias) # Non-recursive: removes the junction, never its target.
    }
    $resolvedFixture = [IO.Path]::GetFullPath($fixture)
    $expectedParent = [IO.Path]::GetFullPath($generatedFolder).TrimEnd('\') + '\'
    if (-not $resolvedFixture.StartsWith($expectedParent, [StringComparison]::OrdinalIgnoreCase) -or ((Get-Item -LiteralPath $resolvedFixture).Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Unsafe fixture cleanup path.' }
    Remove-Item -LiteralPath $resolvedFixture -Recurse -Force
}
