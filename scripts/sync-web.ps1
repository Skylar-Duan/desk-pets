$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$qaRoot = Join-Path $projectRoot '_work\qa'
New-Item -ItemType Directory -Path $qaRoot -Force | Out-Null
Push-Location $projectRoot
try {
    & "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:exe "/out:$qaRoot\ExportWebAssets.exe" /r:System.Drawing.dll /r:System.Web.Extensions.dll scripts\ExportWebAssets.cs src\PetDef.cs
    if ($LASTEXITCODE -ne 0) { throw '网页素材导出器编译失败' }
    & (Join-Path $qaRoot 'ExportWebAssets.exe') (Join-Path $projectRoot 'pets') (Join-Path $projectRoot 'web\assets')
    if ($LASTEXITCODE -ne 0) { throw '网页素材导出失败' }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'backgrounds') -Destination (Join-Path $projectRoot 'web') -Recurse -Force
    $webRoot = Join-Path $projectRoot 'web'
    $pagePath = Join-Path $webRoot 'index.html'
    $page = [IO.File]::ReadAllText($pagePath)
    $currentBundles = @()
    foreach ($name in @('app.js','background.js','style.css')) {
        $sourcePath = Join-Path $webRoot $name
        $parts = $name.Split('.')
        $bundle = $parts[0] + '.' + (Get-FileHash -LiteralPath $sourcePath -Algorithm SHA256).Hash.Substring(0,12).ToLowerInvariant() + '.' + $parts[1]
        $currentBundles += $bundle
        Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $webRoot $bundle) -Force
        $pattern = $parts[0] + '(\.[0-9a-f]{12})?\.' + $parts[1]
        $page = [regex]::Replace($page, '(src|href)="' + $pattern + '"', '$1="' + $bundle + '"')
    }
    [IO.File]::WriteAllText($pagePath + '.tmp', $page, [Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath ($pagePath + '.tmp') -Destination $pagePath -Force
    Get-ChildItem -LiteralPath $webRoot -File | Where-Object { $_.Name -match '^(app|background|style)\.[0-9a-f]{12}\.(js|css)$' -and $_.Name -notin $currentBundles } | ForEach-Object { Remove-Item -LiteralPath $_.FullName }
} finally { Pop-Location }
