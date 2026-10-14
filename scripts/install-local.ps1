param(
    [string]$Package,
    [string]$Destination,
    [switch]$InUserSession
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $Package) {
    $packageReport = Join-Path $projectRoot '_work\package-result.json'
    if (-not (Test-Path -LiteralPath $packageReport)) { throw '请先运行 scripts/package.ps1' }
    $Package = (Get-Content -LiteralPath $packageReport -Raw | ConvertFrom-Json).packageRoot
}
if (-not $Destination) {
    $Destination = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'Programs\DeskPets'
}
$Package = (Resolve-Path -LiteralPath $Package).Path
$Destination = [IO.Path]::GetFullPath($Destination)
$reportPath = Join-Path $projectRoot '_work\install-result.json'
if (-not $InUserSession) {
    # 与02项目安装约定一致：通过WMI在桌面应用隔离区外写快捷方式并启动。
    $launcher = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`" -Package `"$Package`" -Destination `"$Destination`" -InUserSession"
    $startup = New-CimInstance -ClassName Win32_ProcessStartup -ClientOnly -Property @{ShowWindow=[uint16]0}
    $started = Invoke-CimMethod -ClassName Win32_Process -MethodName Create -Arguments @{CommandLine = $launcher; ProcessStartupInformation=$startup}
    if ($started.ReturnValue -ne 0) { throw "安装启动失败：$($started.ReturnValue)" }
    Write-Output "安装进程：$($started.ProcessId)；结果：$reportPath"
    exit 0
}
try {
    $sourceExe = Join-Path $Package 'DeskPets.exe'
    if (-not (Test-Path -LiteralPath $sourceExe)) { throw '交付文件夹没有 DeskPets.exe' }
    if ($Package.TrimEnd('\') -eq $Destination.TrimEnd('\')) { throw '安装位置不能等于交付文件夹' }
    $existing = @(Get-CimInstance Win32_Process -Filter "Name='DeskPets.exe'")
    if ($existing.Count -gt 0) {
        $runningPath = $existing[0].ExecutablePath
        if (-not $runningPath) { throw '无法确认正在运行的桌宠位置，停止覆盖' }
        Start-Process -FilePath $runningPath -ArgumentList '--exit' -WindowStyle Hidden
        for ($i = 0; $i -lt 40 -and (Get-Process -Name DeskPets -ErrorAction SilentlyContinue); $i++) {
            Start-Sleep -Milliseconds 500
        }
        if (Get-Process -Name DeskPets -ErrorAction SilentlyContinue) { throw '桌宠未正常退出，停止覆盖；请从托盘退出后重试' }
    }
    New-Item -ItemType Directory -Path $Destination -Force | Out-Null
    foreach ($file in Get-ChildItem -LiteralPath $Package -File -Recurse) {
        $relative = $file.FullName.Substring($Package.Length).TrimStart('\')
        $target = Join-Path $Destination $relative
        New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target -Force
        if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
            throw "安装文件核对失败：$relative"
        }
    }
    $installedExe = Join-Path $Destination 'DeskPets.exe'
    $desktopLink = Join-Path ([Environment]::GetFolderPath('Desktop')) '桌面小伙伴.lnk'
    $startLink = Join-Path ([Environment]::GetFolderPath('Programs')) '桌面小伙伴.lnk'
    $shortcutShell = New-Object -ComObject WScript.Shell
    foreach ($link in @($desktopLink, $startLink)) {
        $shortcut = $shortcutShell.CreateShortcut($link)
        $shortcut.TargetPath = $installedExe
        $shortcut.WorkingDirectory = $Destination
        $shortcut.IconLocation = "$installedExe,0"
        $shortcut.Description = '小布、阿金陪伴桌面；查看我的小伙伴'
        $shortcut.Save()
    }
    Start-Process -FilePath $installedExe
    Start-Sleep -Seconds 2
    $process = @(Get-CimInstance Win32_Process -Filter "Name='DeskPets.exe'" | Where-Object { $_.ExecutablePath -eq $installedExe })
    if ($process.Count -ne 1) { throw '安装后的桌宠没有正常启动' }
    $result = [ordered]@{ok=$true; installedExe=$installedExe; version=(Get-Item -LiteralPath $installedExe).VersionInfo.FileVersion; desktopShortcut=$desktopLink; startMenuShortcut=$startLink; processId=$process[0].ProcessId}
} catch {
    $result = [ordered]@{ok=$false; error=$_.Exception.Message; destination=$Destination}
}
New-Item -ItemType Directory -Path (Split-Path $reportPath -Parent) -Force | Out-Null
$tempReport = "$reportPath.tmp"
$result | ConvertTo-Json | Set-Content -LiteralPath $tempReport -Encoding UTF8
Get-Content -LiteralPath $tempReport -Raw | ConvertFrom-Json | Out-Null
Move-Item -LiteralPath $tempReport -Destination $reportPath -Force
if (-not $result.ok) { exit 1 }
