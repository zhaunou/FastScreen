param([string]$OutputPath)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$arguments = @{ BuildTests = $true }
if ($OutputPath) { $arguments.OutputPath = $OutputPath }
& (Join-Path $project 'build.ps1') @arguments
