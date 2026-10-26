param([Parameter(Mandatory = $true)][string]$Executable)
$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFrom([IO.Path]::GetFullPath($Executable))
foreach ($type in $assembly.GetTypes()) {
    if ($type.FullName -match 'Tests|SelfTest|TestProgram') { throw "Test type present in release: $($type.FullName)" }
}
$bytes = [IO.File]::ReadAllBytes([IO.Path]::GetFullPath($Executable))
$binaryText = [Text.Encoding]::Unicode.GetString($bytes)
$shiftedText = [Text.Encoding]::Unicode.GetString($bytes, 1, $bytes.Length - 1)
foreach ($flag in @('--self-test', '--logic-test', '--render-test')) {
    if ($binaryText.Contains($flag) -or $shiftedText.Contains($flag)) { throw "Legacy test mode present: $flag" }
}
if ($assembly.EntryPoint.DeclaringType.FullName -ne 'LocalScreenshot.Program') { throw 'Incorrect release entry point.' }
Write-Output 'PASS: release contains no test types or legacy diagnostic flags; normal application entry point.'
