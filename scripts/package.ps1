param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$version = (Get-Content -LiteralPath (Join-Path $project 'VERSION') -Raw).Trim()
if ($version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') { throw 'Invalid release version.' }
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $project 'dist' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$archiveName = "FastScreen-$version-windows.zip"
$archive = Join-Path $OutputDirectory $archiveName
if (Test-Path -LiteralPath $archive) { throw "Package already exists: $archive. Use a fresh output directory." }
$stage = Join-Path $project ('obj\package-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $stage | Out-Null
$executable = Join-Path $stage 'FastScreen.exe'
& (Join-Path $project 'build.ps1') -OutputPath $executable
# A separate process releases the assembly file after the metadata check.
& (Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe') -NoProfile -ExecutionPolicy Bypass -File (Join-Path $project 'tests\check-release.ps1') -Executable $executable
if ($LASTEXITCODE -ne 0) { throw 'Release boundary check failed.' }
$metadata = [Diagnostics.FileVersionInfo]::GetVersionInfo($executable)
if ($metadata.ProductVersion -ne $version) { throw 'EXE version differs from VERSION.' }
foreach ($name in @('LICENSE', 'SECURITY.md', 'CHANGELOG.md')) { Copy-Item -LiteralPath (Join-Path $project $name) -Destination (Join-Path $stage $name) }
$instructions = @"
FastScreen $version
Copyright (c) 2026 zhaunou

Запуск
1. Полностью распакуйте архив в удобную локальную папку.
2. Если FastScreen уже работает, завершите его через меню трея -> Выйти.
3. Запустите FastScreen.exe. Установка и права администратора не требуются.

Требования: Windows 10/11 x64, .NET Framework 4.8.
Python и инструменты разработки для запуска не нужны.

Весь экран или выделенная область: кнопка Создать скриншот.
По умолчанию Ctrl+Shift+F — весь экран, Ctrl+Shift+A — область.
Сочетания можно изменить в разделе Горячие клавиши.
Esc отменяет выделение. Скриншоты сохраняются в PNG.
Папка по умолчанию: %LOCALAPPDATA%\LocalScreenshot\Screenshots.
Крестик скрывает окно в трей; двойной щелчок по значку возвращает его.
Полный выход: меню значка в трее -> Выйти.

Лицензия: FastScreen Non-Commercial License 1.0 (см. LICENSE).
Разрешено личное некоммерческое использование. Коммерческое использование,
продажа копий и платный доступ без письменного разрешения запрещены.

Программа сама не загружает снимки в интернет. Сторонняя синхронизация
обычной локальной папки остаётся вне контроля FastScreen.
Ссылки и перенаправления в папке сохранения не поддерживаются.
EXE не подписан цифровой подписью. Контрольные суммы: SHA256SUMS.txt.

Исходники и выпуски: https://github.com/zhaunou/FastScreen
Сообщения об уязвимостях: см. SECURITY.md.
"@
[IO.File]::WriteAllText((Join-Path $stage 'README.txt'), $instructions, (New-Object Text.UTF8Encoding($true)))
$payload = @('FastScreen.exe', 'LICENSE', 'README.txt', 'SECURITY.md', 'CHANGELOG.md')
$innerSums = @($payload | ForEach-Object { (Get-FileHash -LiteralPath (Join-Path $stage $_) -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + $_ })
[IO.File]::WriteAllLines((Join-Path $stage 'SHA256SUMS.txt'), $innerSums, (New-Object Text.UTF8Encoding($false)))
$payload += 'SHA256SUMS.txt'
Compress-Archive -LiteralPath @($payload | ForEach-Object { Join-Path $stage $_ }) -DestinationPath $archive -CompressionLevel Optimal
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$zip = [IO.Compression.ZipFile]::OpenRead($archive)
try {
    if ($zip.Entries.Count -ne $payload.Count) { throw 'Unexpected files in ZIP.' }
    foreach ($name in $payload) {
        $entry = $zip.GetEntry($name)
        if ($null -eq $entry) { throw "Missing ZIP entry: $name" }
        $stream = $entry.Open()
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $actual = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
        finally { $sha.Dispose(); $stream.Dispose() }
        if ($actual -ne (Get-FileHash -LiteralPath (Join-Path $stage $name) -Algorithm SHA256).Hash) { throw "ZIP checksum mismatch: $name" }
    }
} finally { $zip.Dispose() }
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'SHA256SUMS.txt'), "$hash  $archiveName`n", (New-Object Text.UTF8Encoding($false)))
$notes = @"
FastScreen $version — снимки всего экрана или выбранной области для Windows.

### Установка
Скачайте **$archiveName**, распакуйте и запустите **FastScreen.exe**. Архивы Source code содержат исходники, а не готовую программу.

### В выпуске
- Локальное сохранение PNG, выделение области и синее перекрестие.
- Настраиваемые глобальные горячие клавиши и работа из трея.
- Мягкий колокольчик после успешного сохранения.
- Один экземпляр программы, проверка настроек и защита папки сохранения.

Требования: Windows 10/11 x64, .NET Framework 4.8. Python и установка не нужны.
Лицензия: FastScreen Non-Commercial License 1.0. Коммерческое использование и продажа без отдельного разрешения запрещены.

SHA-256 архива: **$hash**. Отдельный файл **SHA256SUMS.txt** содержит эту же сумму. Внутри ZIP есть контрольные суммы всех файлов. EXE не подписан цифровой подписью.
"@
[IO.File]::WriteAllText((Join-Path $OutputDirectory 'RELEASE_NOTES.md'), $notes, (New-Object Text.UTF8Encoding($false)))
Write-Output "Verified package: $archive"
Write-Output "SHA256: $hash"
