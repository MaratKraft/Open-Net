param([switch]$WorkingTree)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    if ($WorkingTree) {
        $files = @(Get-ChildItem -LiteralPath $root -Recurse -File -Force | Where-Object { $_.FullName -notmatch '[\\/](bin|obj|publish|artifacts|tools|\.git)[\\/]' } | ForEach-Object { [IO.Path]::GetRelativePath($root,$_.FullName).Replace('\','/') })
    } else {
        $files = @(& git -c core.quotePath=false ls-files)
        if ($LASTEXITCODE -ne 0 -or !$files.Count) { throw 'Cannot list tracked files' }
    }
    $failures = [Collections.Generic.List[string]]::new()
    foreach ($file in $files) {
        if ($file -match '(?i)(^|/)(Data|Modules|Logs|Cache|Downloads|Backups|test-results|TestFixtures|verification|bin|obj|publish|tools)/|\.(exe|dll|sys|pdb|zip|log|pem|key|pfx|p12)$|(^|/)(\.env(?:\..*)?|settings\.json|config\.json)$') { $failures.Add("Forbidden file: $file") }
        if ($file -match '\.(png|ico)$') { continue }
        $content = [IO.File]::ReadAllText((Join-Path $root $file))
        $patterns = @('[A-Za-z]:[\\/]Users[\\/][^\\/\s]+','gh[pousr]_[A-Za-z0-9]{30,}','github_pat_[A-Za-z0-9_]{30,}','-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----','(?i)secret\s*[:=]\s*["'']?[0-9a-f]{32,}')
        foreach ($pattern in $patterns) { if ($content -match $pattern) { $failures.Add("Potential sensitive data in $file (value withheld)"); break } }
    }
    if ($failures.Count) { throw ($failures -join "`n") }
    Write-Output "Public-file guard passed: $($files.Count) files. Pattern checks are not a complete security audit."
} finally { Pop-Location }
