param(
    [Parameter(Mandatory = $true)][string]$ResultPath,
    [Parameter(Mandatory = $true)][string]$DownloadDirectory,
    [switch]$VerifyOnly
)
$ErrorActionPreference = 'Stop'
# Official .NET Framework 4.8 offline Runtime, downloaded/hash/signer checked 2026-10-22.
# https://dotnet.microsoft.com/download/dotnet-framework/thank-you/net48-offline-installer
$runtimeUrl = 'https://download.microsoft.com/download/f/3/a/f3a6af84-da23-40a5-8d1c-49cc10c8e76f/NDP48-x86-x64-AllOS-ENU.exe'
$runtimeSha256 = '0A3A390C47E639D0F7FC65B21195FEE6B7F65B066F80F70C60FAB191D14B7E40'
$runtimeFile = Join-Path $DownloadDirectory 'NDP48-x86-x64-AllOS-ENU.exe'
$logDirectory = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'DeskPets\Logs'
$runtimeLog = Join-Path $logDirectory ('dotnet48-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.txt')

function Complete-Runtime([int]$Code, [string]$Message) {
    $safeMessage = $Message.Replace("`r", ' ').Replace("`n", ' ')
    $result = "[runtime]`r`nmessage=$safeMessage`r`nlog=$runtimeLog`r`n"
    [IO.File]::WriteAllText($ResultPath, $result, [Text.Encoding]::Unicode)
    exit $Code
}
function Get-FrameworkRelease {
    $highest = 0
    foreach ($view in @([Microsoft.Win32.RegistryView]::Registry32, [Microsoft.Win32.RegistryView]::Registry64)) {
        $base = $null
        $key = $null
        try {
            $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $view)
            $key = $base.OpenSubKey('SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full')
            if ($key) { $highest = [Math]::Max($highest, [int]$key.GetValue('Release', 0)) }
        } finally {
            if ($key) { $key.Dispose() }
            if ($base) { $base.Dispose() }
        }
    }
    return $highest
}
function Assert-OfficialRuntime([string]$File) {
    if ((Get-FileHash -LiteralPath $File -Algorithm SHA256).Hash -ne $runtimeSha256) {
        throw '运行环境下载文件校验失败；未运行该文件。请重新运行安装程序。'
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $File
    if ($signature.Status -ne 'Valid' -or -not $signature.SignerCertificate -or
        $signature.SignerCertificate.Subject -notmatch '(^|,\s*)O=Microsoft Corporation(,|$)') {
        throw '无法确认运行环境文件的有效微软签名；未运行该文件。请检查系统时间和网络后重试。'
    }
}
try {
    New-Item -ItemType Directory -Path $DownloadDirectory -Force | Out-Null
    if ($VerifyOnly) {
        Assert-OfficialRuntime $runtimeFile
        Complete-Runtime 0 '微软运行环境文件的SHA256和数字签名校验通过；未安装。'
    }
    if ((Get-FrameworkRelease) -ge 528040) { Complete-Runtime 0 '已安装.NET Framework 4.8或更新版本。' }
    Write-Output '正在从微软下载.NET Framework 4.8运行环境（约116MB），请稍候。'
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    if (Test-Path -LiteralPath $runtimeFile) { Remove-Item -LiteralPath $runtimeFile -Force }
    $request = [Net.HttpWebRequest]::Create($runtimeUrl)
    $request.Timeout = 30000
    $request.ReadWriteTimeout = 30000
    $request.AllowAutoRedirect = $true
    $response = $null
    $source = $null
    $target = $null
    try {
        $response = $request.GetResponse()
        if ($response.ResponseUri.Scheme -ne 'https' -or $response.ResponseUri.Host -ne 'download.microsoft.com') {
            throw '运行环境下载未到达微软官方下载服务器。'
        }
        $source = $response.GetResponseStream()
        $target = [IO.File]::Create($runtimeFile)
        $buffer = New-Object byte[] 131072
        $downloaded = 0L
        $reported = 0L
        while (($count = $source.Read($buffer, 0, $buffer.Length)) -gt 0) {
            $target.Write($buffer, 0, $count)
            $downloaded += $count
            if ($downloaded -gt 150MB) { throw '运行环境下载大小异常，已停止。' }
            if ($downloaded - $reported -ge 10MB) {
                Write-Output ('运行环境已下载 {0:N0} MB。' -f ($downloaded / 1MB))
                $reported = $downloaded
            }
        }
    } finally {
        if ($target) { $target.Dispose() }
        if ($source) { $source.Dispose() }
        if ($response) { $response.Dispose() }
    }
    Write-Output '正在核对下载文件与微软数字签名。'
    Assert-OfficialRuntime $runtimeFile
    New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null
    Write-Output '正在安装微软运行环境；Windows将请求管理员批准。'
    try {
        $runtimeProcess = Start-Process -FilePath $runtimeFile -Verb RunAs -ArgumentList "/passive /norestart /log `"$runtimeLog`"" -PassThru -Wait
    } catch [System.ComponentModel.Win32Exception] {
        if ($_.Exception.NativeErrorCode -eq 1223) { Complete-Runtime 1602 '你取消了运行环境的管理员批准；桌面小伙伴尚未安装。' }
        throw
    }
    switch ($runtimeProcess.ExitCode) {
        0 {
            if ((Get-FrameworkRelease) -lt 528040) { Complete-Runtime 1 '微软安装程序已结束，但仍未检测到所需运行环境。请查看安装日志后重试。' }
            Complete-Runtime 0 '微软运行环境安装完成。'
        }
        3010 { Complete-Runtime 3010 '微软运行环境安装完成，需要重启电脑；本次不会启动桌面小伙伴。' }
        1641 { Complete-Runtime 3010 '微软运行环境安装完成，需要重启电脑；本次不会启动桌面小伙伴。' }
        1602 { Complete-Runtime 1602 '你取消了微软运行环境安装；桌面小伙伴尚未安装。' }
        5100 { Complete-Runtime 5100 '微软运行环境不支持此Windows版本。请更新Windows后重新安装。' }
        default { Complete-Runtime 1 ('微软运行环境安装失败，退出码：' + $runtimeProcess.ExitCode + '。请查看安装日志后重试。') }
    }
} catch { Complete-Runtime 1 ('运行环境准备失败：' + $_.Exception.Message) }
