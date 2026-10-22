param([string]$Version = '1.2.1', [string]$MakeNSIS)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$buildId = [guid]::NewGuid().ToString('N').Substring(0, 8)
$packageRoot = Join-Path $projectRoot "dist\桌面小伙伴-$Version-$buildId"
$exe = Join-Path $projectRoot 'DeskPets.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw '请先运行 build.bat' }
if (-not (Test-Path -LiteralPath "$exe.config")) { throw '程序运行环境声明缺失，请先运行 build.bat' }
$actualVersion = (Get-Item -LiteralPath $exe).VersionInfo.FileVersion
if ($actualVersion -ne "$Version.0") { throw "版本不符：$actualVersion" }
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'pets\nuomi\pet.json'))) { throw '糯米桌面皮肤尚未准备好' }
$qaRoot = Join-Path $projectRoot '_work\qa'
New-Item -ItemType Directory -Path $qaRoot -Force | Out-Null
Push-Location $projectRoot
try {
    & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe "/out:$qaRoot\UpdateChecks.exe" /r:System.Web.Extensions.dll scripts\UpdateChecks.cs src\UpdateChecker.cs src\AssemblyInfo.cs
    if ($LASTEXITCODE -ne 0) { throw '更新检查验收编译失败' }
    & (Join-Path $qaRoot 'UpdateChecks.exe')
    if ($LASTEXITCODE -ne 0) { throw '更新检查验收失败，停止打包' }
    & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe "/out:$qaRoot\SettingsChecks.exe" /r:System.Web.Extensions.dll scripts\SettingsChecks.cs src\UserSettings.cs src\ScaleAdjustment.cs
    if ($LASTEXITCODE -ne 0) { throw '设置与联动验收编译失败' }
    & (Join-Path $qaRoot 'SettingsChecks.exe') $qaRoot
    if ($LASTEXITCODE -ne 0) { throw '设置迁移或联动检查失败，停止打包' }
    & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe "/out:$qaRoot\AssetChecks.exe" /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll scripts\AssetChecks.cs src\PetDef.cs src\PetWindow.cs src\NativeMethods.cs src\Backdrop.cs src\UserSettings.cs src\PetBrain.cs
    if ($LASTEXITCODE -ne 0) { throw '发布验收检查编译失败' }
    & (Join-Path $qaRoot 'AssetChecks.exe') $projectRoot (Join-Path $projectRoot '_work\nuomi-desktop-qa')
    if ($LASTEXITCODE -ne 0) { throw '宠物体型、状态或贴地检查失败，停止打包' }
} finally { Pop-Location }
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
Copy-Item -LiteralPath $exe -Destination (Join-Path $packageRoot 'DeskPets.exe') -Force
Copy-Item -LiteralPath "$exe.config" -Destination (Join-Path $packageRoot 'DeskPets.exe.config') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot '使用说明.txt') -Destination $packageRoot -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'pets') -Destination $packageRoot -Recurse -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'backgrounds') -Destination $packageRoot -Recurse -Force
$zip = Join-Path $projectRoot "dist\桌面小伙伴-$Version.zip"
Compress-Archive -LiteralPath $packageRoot -DestinationPath $zip -Force
$installer = & (Join-Path $PSScriptRoot 'build-installer.ps1') -PackageRoot $packageRoot -Version $Version -MakeNSIS $MakeNSIS
if (-not (Test-Path -LiteralPath $installer)) { throw '安装器未生成，停止记录发布结果' }
$report = Join-Path $projectRoot '_work\package-result.json'
$result = @{version=$Version; packageRoot=$packageRoot; zip=$zip; installer=$installer; createdAt=(Get-Date).ToString('o')}
$result | ConvertTo-Json | Set-Content -LiteralPath "$report.tmp" -Encoding UTF8
Get-Content -LiteralPath "$report.tmp" -Raw | ConvertFrom-Json | Out-Null
Move-Item -LiteralPath "$report.tmp" -Destination $report -Force
Write-Output "交付文件夹：$packageRoot"
Write-Output "压缩包：$zip"
Write-Output "安装包：$installer"
