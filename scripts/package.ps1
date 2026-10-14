param([string]$Version = '1.1.0')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$buildId = [guid]::NewGuid().ToString('N').Substring(0, 8)
$packageRoot = Join-Path $projectRoot "dist\桌面小伙伴-$Version-$buildId"
$exe = Join-Path $projectRoot 'DeskPets.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw '请先运行 build.bat' }
$actualVersion = (Get-Item -LiteralPath $exe).VersionInfo.FileVersion
if ($actualVersion -ne "$Version.0") { throw "版本不符：$actualVersion" }
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'pets\nuomi\pet.json'))) { throw '糯米桌面皮肤尚未准备好' }
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
Copy-Item -LiteralPath $exe -Destination (Join-Path $packageRoot 'DeskPets.exe') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot '使用说明.txt') -Destination $packageRoot -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'pets') -Destination $packageRoot -Recurse -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'backgrounds') -Destination $packageRoot -Recurse -Force
$zip = Join-Path $projectRoot "dist\桌面小伙伴-$Version.zip"
Compress-Archive -LiteralPath $packageRoot -DestinationPath $zip -Force
$report = Join-Path $projectRoot '_work\package-result.json'
$result = @{version=$Version; packageRoot=$packageRoot; zip=$zip; createdAt=(Get-Date).ToString('o')}
$result | ConvertTo-Json | Set-Content -LiteralPath "$report.tmp" -Encoding UTF8
Get-Content -LiteralPath "$report.tmp" -Raw | ConvertFrom-Json | Out-Null
Move-Item -LiteralPath "$report.tmp" -Destination $report -Force
Write-Output "交付文件夹：$packageRoot"
Write-Output "压缩包：$zip"
