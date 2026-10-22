param(
    [Parameter(Mandatory = $true)][string]$PackageRoot,
    [string]$Version = '1.2.1',
    [string]$MakeNSIS
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$PackageRoot = (Resolve-Path -LiteralPath $PackageRoot).Path
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw '版本号格式必须为X.Y.Z' }
foreach ($required in @('DeskPets.exe', 'DeskPets.exe.config', '使用说明.txt', 'pets\nuomi\pet.json')) {
    if (-not (Test-Path -LiteralPath (Join-Path $PackageRoot $required))) { throw "安装载荷不完整：$required" }
}
if ((Get-Item -LiteralPath (Join-Path $PackageRoot 'DeskPets.exe')).VersionInfo.FileVersion -ne "$Version.0") {
    throw '安装载荷的程序版本与安装器版本不一致'
}
if (-not $MakeNSIS) {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA 'tauri\NSIS\makensis.exe'),
        (Join-Path $env:LOCALAPPDATA 'tauri\NSIS\Bin\makensis.exe'),
        (Join-Path $projectRoot '_work\installer-deps\nsis\makensis.exe')
    )
    $MakeNSIS = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $MakeNSIS) {
        $command = Get-Command makensis.exe -ErrorAction SilentlyContinue
        if ($command) { $MakeNSIS = $command.Source }
    }
}
if (-not $MakeNSIS -or -not (Test-Path -LiteralPath $MakeNSIS)) { throw '找不到NSIS Unicode编译器；请通过-MakeNSIS指定makensis.exe（可使用Tauri缓存）。' }
$MakeNSIS = (Resolve-Path -LiteralPath $MakeNSIS).Path
$buildDirectory = Join-Path $projectRoot ('_work\installer-build-' + [guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory -Path $buildDirectory -Force | Out-Null
function ConvertTo-NsisString([string]$Value) {
    if ($Value.IndexOfAny([char[]]@('"', "`r", "`n")) -ge 0) { throw '安装载荷路径包含不支持的字符' }
    return $Value.Replace('$', '$$')
}
# Exact file manifest: uninstall never recursively deletes a user-chosen directory.
$installLines = New-Object 'System.Collections.Generic.List[string]'
$uninstallLines = New-Object 'System.Collections.Generic.List[string]'
$directories = New-Object 'System.Collections.Generic.HashSet[string]'
$payloadBytes = 0L
foreach ($file in (Get-ChildItem -LiteralPath $PackageRoot -File -Recurse | Sort-Object FullName)) {
    $relative = $file.FullName.Substring($PackageRoot.Length).TrimStart('\')
    $relativeNsis = ConvertTo-NsisString $relative
    $relativeDirectory = Split-Path $relative -Parent
    $targetDirectory = if ($relativeDirectory) { '$INSTDIR\' + (ConvertTo-NsisString $relativeDirectory) } else { '$INSTDIR' }
    $installLines.Add('SetOutPath "' + $targetDirectory + '"')
    $installLines.Add('File "' + (ConvertTo-NsisString $file.FullName) + '"')
    $uninstallLines.Add('Delete "$INSTDIR\' + $relativeNsis + '"')
    while ($relativeDirectory) {
        [void]$directories.Add($relativeDirectory)
        $relativeDirectory = Split-Path $relativeDirectory -Parent
    }
    $payloadBytes += $file.Length
}
foreach ($directory in ($directories | Sort-Object Length -Descending)) { $uninstallLines.Add('RMDir "$INSTDIR\' + (ConvertTo-NsisString $directory) + '"') }
$utf8Bom = New-Object Text.UTF8Encoding $true
[IO.File]::WriteAllLines((Join-Path $buildDirectory 'install-files.nsh'), $installLines, $utf8Bom)
[IO.File]::WriteAllLines((Join-Path $buildDirectory 'uninstall-files.nsh'), $uninstallLines, $utf8Bom)
$installer = Join-Path $projectRoot "dist\桌面小伙伴-$Version-安装包.exe"
$arguments = @('/INPUTCHARSET', 'UTF8', '/V3', "/DVERSION=$Version", "/DPAYLOAD_INCLUDE=$buildDirectory", "/DOUTPUT_FILE=$installer", "/DESTIMATED_SIZE_KB=$([Math]::Ceiling($payloadBytes / 1KB))", (Join-Path $PSScriptRoot 'installer.nsi'))
& $MakeNSIS @arguments | Write-Host
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $installer)) { throw '安装器构建失败' }
if ((Get-Item -LiteralPath $installer).VersionInfo.FileVersion -ne "$Version.0") { throw '安装器文件版本核对失败' }
Write-Output $installer
