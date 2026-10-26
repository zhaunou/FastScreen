$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$generatedFolder = Join-Path $project 'obj'
New-Item -ItemType Directory -Force -Path $generatedFolder | Out-Null
$testExecutable = Join-Path $generatedFolder 'SingleInstanceTests.exe'
& (Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe') /nologo /target:exe /out:"$testExecutable" (Join-Path $project 'SingleInstance.cs') (Join-Path $PSScriptRoot 'SingleInstanceTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& $testExecutable
if ($LASTEXITCODE -ne 0) { throw 'Single-instance tests failed.' }
