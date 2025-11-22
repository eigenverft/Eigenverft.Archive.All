
function Save-DotNetInstallScript {
<#
.SYNOPSIS
  Download dotnet-install.ps1 into a folder.
.PARAMETER OutDir
  Target folder.
.EXAMPLE
  $installer = Save-DotNetInstallScript -OutDir $kit
#>
  param([Parameter(Mandatory)][string]$OutDir)
  try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 } catch {}
  $dst = Join-Path $OutDir 'dotnet-install.ps1'
  if (-not (Test-Path -LiteralPath $dst -PathType Leaf)) {
    Invoke-WebRequest -UseBasicParsing -MaximumRedirection 10 -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $dst
  }
  return $dst
}

function Get-DotNetSdkUrls {
<#
.SYNOPSIS
  Parse ALL payload URLs from dotnet-install.ps1 -DryRun output + the resolved Version.
#>
  [CmdletBinding()]
  param(
    [Parameter(Mandatory)][string]$Installer,
    [string]$Channel = 'LTS',
    [ValidateSet('x64','x86','arm64')][string]$Architecture = $(if([Environment]::Is64BitOperatingSystem){'x64'}else{'x86'})
  )

  $lines = & $Installer -Channel $Channel -Architecture $Architecture -DryRun 2>&1 3>&1 4>&1 5>&1 6>&1 | ForEach-Object { $_.ToString() }
  $text  = ($lines -join "`n")

  # Collect all "URL #n - <url>" entries
  $urls = [System.Collections.Generic.List[string]]::new()
  foreach ($m in [regex]::Matches($text, '(?m)^\s*dotnet-install:\s*URL\s*#\d+\s*-\s*(https?://\S+?\.zip)\s*$')) {
    $u = $m.Groups[1].Value
    if (-not $urls.Contains($u)) { $urls.Add($u) }
  }
  # Fallback: any https...zip anywhere
  if ($urls.Count -eq 0) {
    foreach ($m in [regex]::Matches($text, '(https?://\S+?\.zip)')) {
      $u = $m.Value
      if (-not $urls.Contains($u)) { $urls.Add($u) }
    }
  }
  if ($urls.Count -eq 0) { throw "Get-DotNetSdkUrls: No payload URLs found for channel '$Channel'." }

  # Version from "Repeatable invocation" or filename (supports legacy "dev" zips)
  $version = $null
  $mVer = [regex]::Match($text, '(?m)^\s*dotnet-install:\s*Repeatable invocation:.*?-Version\s+"([^"]+)"')
  if ($mVer.Success) { $version = $mVer.Groups[1].Value }
  if (-not $version) {
    foreach ($u in $urls) {
      $m1 = [regex]::Match($u, 'dotnet-sdk-([0-9\.]+)-win-(x64|x86|arm64)\.zip', 'IgnoreCase')
      if ($m1.Success) { $version = $m1.Groups[1].Value; break }
      $m2 = [regex]::Match($u, 'dotnet-dev-win-(x64|x86|arm64)\.([0-9\.]+)\.zip', 'IgnoreCase') # legacy
      if ($m2.Success) { $version = $m2.Groups[2].Value; break }
    }
  }
  if (-not $version) { throw 'Get-DotNetSdkUrls: Could not determine SDK version.' }

  [pscustomobject]@{ Urls = $urls; Version = $version }
}

# Kept for compatibility (first candidate)
function Get-DotNetSdkUrl {
  [CmdletBinding()]
  param(
    [Parameter(Mandatory)][string]$Installer,
    [string]$Channel = 'LTS',
    [ValidateSet('x64','x86','arm64')][string]$Architecture = $(if([Environment]::Is64BitOperatingSystem){'x64'}else{'x86'})
  )
  $r = Get-DotNetSdkUrls -Installer $Installer -Channel $Channel -Architecture $Architecture
  [pscustomobject]@{ Url = $r.Urls[0]; Version = $r.Version }
}

function Save-Url {
<#
.SYNOPSIS
  Download a URL into a folder, returning the full path (PS5/PS7 compatible).
#>
  param([Parameter(Mandatory)][string]$Url,[Parameter(Mandatory)][string]$OutDir)
  try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12 } catch {}
  $name = Split-Path -Leaf $Url
  $dst  = Join-Path $OutDir $name
  try {
    Invoke-WebRequest -UseBasicParsing -MaximumRedirection 10 -Uri $Url -OutFile $dst -ErrorAction Stop
  } catch {
    throw "Save-Url: Failed to download '$Url' -> '$dst'. Error: $($_.Exception.Message)"
  }
  if (-not (Test-Path -LiteralPath $dst)) { throw "Save-Url: Download missing after success path: $dst" }
  return $dst
}

function Update-DotNetInventory {
<#
.SYNOPSIS
  Record + finalize phase: download SDK ZIPs (robustly), quick-verify ZIPs, and (re)build inventory.json.
.DESCRIPTION
  - Auto-downloads dotnet-install.ps1 into the bundle folder if missing.
  - Tries all candidate URLs from dotnet-install -DryRun; marks channel as Missing if none work.
  - After EACH successful download, rewrites inventory.json (includes relative paths only) to support offline replay.
  - Ensures InstallerFile='dotnet-install.ps1' exists in the folder (finalized bundle).
.PARAMETER InventoryPath
  Directory path (preferred) or full path to inventory.json.
.PARAMETER Channels
  Channels to record (e.g. '7.0','8.0','LTS','STS'); old channels may be Missing.
.PARAMETER Architecture
  Target architecture; defaults to OS bitness.
.PARAMETER ComputeHash
  Include SHA256 for each ZIP.
#>
  [CmdletBinding()]
  param(
    [Parameter(Mandatory)][string]$InventoryPath,
    [string[]]$Channels,
    [ValidateSet('x64','x86','arm64')][string]$Architecture = $(if([Environment]::Is64BitOperatingSystem){'x64'}else{'x86'}),
    [switch]$ComputeHash
  )

  # Resolve folder vs file
  $isJson = [IO.Path]::GetExtension($InventoryPath) -ieq '.json'
  $outDir = if ($isJson) { Split-Path -Parent (Resolve-Path -LiteralPath $InventoryPath -ErrorAction SilentlyContinue) } else { $InventoryPath }
  if (-not $outDir) { $outDir = $InventoryPath }
  if (-not (Test-Path -LiteralPath $outDir -PathType Container)) { New-Item -ItemType Directory -Force -Path $outDir | Out-Null }
  $invFile = if ($isJson) { $InventoryPath } else { Join-Path $outDir 'inventory.json' }

  # Ensure installer in bundle (integrated "finalize")
  $installer = Save-DotNetInstallScript -OutDir $outDir

  # Helper: zip quick readability check (no external deps)
  function _Test-ZipReadable([string]$Path) {
    try {
      try { Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue } catch {}
      $za = [System.IO.Compression.ZipFile]::OpenRead($Path)
      $null = $za.Entries.Count
      $za.Dispose()
      return $true
    } catch { return $false }
  }

  # Track channels that produced no file (Missing)
  $missing = @{}

  # Writer: rescan folder, merge Missing, write JSON (NO LocalPath/InstallDir)
  function _Write-Inventory([hashtable]$MissingMap) {
    $files = Get-ChildItem -LiteralPath $outDir -File -Filter '*.zip' | Sort-Object Name
    $entries = @()

    foreach ($f in $files) {
      $leaf = $f.Name
      $version = $null; $arch = $null

      # modern
      $m1 = [regex]::Match($leaf, 'dotnet-sdk-([0-9\.]+)-win-(x64|x86|arm64)\.zip', 'IgnoreCase')
      if ($m1.Success) { $version = $m1.Groups[1].Value; $arch = $m1.Groups[2].Value }
      # legacy 1.x
      $m2 = [regex]::Match($leaf, 'dotnet-dev-win-(x64|x86|arm64)\.([0-9\.]+)\.zip', 'IgnoreCase')
      if (($null -eq $version) -and $m2.Success) { $version = $m2.Groups[2].Value; $arch = $m2.Groups[1].Value }

      $channel = $null
      if ($version) { $p = $version.Split('.'); if ($p.Count -ge 2) { $channel = "$($p[0]).$($p[1])" } }

      $sha256 = $null
      if ($ComputeHash) { $sha256 = (Get-FileHash -LiteralPath (Join-Path $outDir $leaf) -Algorithm SHA256).Hash }

      $entries += [pscustomobject]@{
        FileName       = $leaf
        LocalPathRel   = $leaf
        Url            = $null
        Version        = $version
        Channel        = $channel
        Architecture   = if ($arch) { $arch } else { $Architecture }
        SizeBytes      = [int64]$f.Length
        SHA256         = $sha256
        DownloadedOn   = $null
        Status         = 'Present'     # conservative default
        Installed      = $false
        InstalledOn    = $null
        InstallResult  = $null
        InstallerFile  = 'dotnet-install.ps1'
      }
    }

    foreach ($k in $MissingMap.Keys) {
      $d = $MissingMap[$k]
      $entries += [pscustomobject]@{
        FileName       = $null
        LocalPathRel   = $null
        Url            = $null
        Version        = $null
        Channel        = $d.Channel
        Architecture   = $Architecture
        SizeBytes      = 0
        SHA256         = $null
        DownloadedOn   = $null
        Status         = 'Missing'
        Installed      = $false
        InstalledOn    = $null
        InstallResult  = "Missing: $($d.Reason)"
        InstallerFile  = 'dotnet-install.ps1'
      }
    }

    ($entries | ConvertTo-Json -Depth 6) | Set-Content -LiteralPath $invFile -Encoding UTF8
  }

  # Initial write (in case only Missing ends up recorded)
  _Write-Inventory -MissingMap $missing

  # Download phase (if channels given)
  if ($Channels -and $Channels.Count -gt 0) {
    foreach ($ch in $Channels) {
      $meta = $null
      try { $meta = Get-DotNetSdkUrls -Installer $installer -Channel $ch -Architecture $Architecture } catch { $meta = $null }
      if (-not $meta) {
        $missing["__channel__$ch"] = @{ Channel=$ch; Reason='No URLs from -DryRun' }
        _Write-Inventory -MissingMap $missing
        continue
      }

      $ok = $false; $lastErr = $null; $zip = $null; $pickedUrl = $null
      foreach ($u in $meta.Urls) {
        try {
          $zip = Save-Url -Url $u -OutDir $outDir
          if (-not (_Test-ZipReadable $zip)) {
            Remove-Item -LiteralPath $zip -Force -ErrorAction SilentlyContinue
            throw "ZIP verification failed (cannot open/read): $zip"
          }
          $pickedUrl = $u; $ok = $true; break
        } catch { $lastErr = $_.Exception.Message }
      }

      if ($ok) {
        # Rewrite inventory immediately after each success
        _Write-Inventory -MissingMap $missing

        # Annotate the just-downloaded file with richer fields
        $entries = Get-Content -LiteralPath $invFile -Raw | ConvertFrom-Json
        if ($entries -isnot [System.Collections.IEnumerable]) { $entries = @($entries) }
        $leaf = Split-Path -Leaf $zip
        foreach ($e in $entries) {
          if ($e.FileName -eq $leaf) {
            $e.Url          = $pickedUrl
            $e.Version      = if ($e.Version) { $e.Version } else { $meta.Version }
            $e.Channel      = $ch
            $e.DownloadedOn = [DateTimeOffset]::UtcNow.ToString("o")
            $e.Status       = 'Downloaded'
            $e.InstallerFile= 'dotnet-install.ps1'
            break
          }
        }
        ($entries | ConvertTo-Json -Depth 6) | Set-Content -LiteralPath $invFile -Encoding UTF8
      } else {
        $missing["__channel__$ch"] = @{ Channel=$ch; Reason=$lastErr }
        _Write-Inventory -MissingMap $missing
      }
    }
  }

  return $invFile
}

function Install-DotNetFromInventory {
<#
.SYNOPSIS
  Replay phase: install SDKs listed in inventory.json via local dotnet-install.ps1 using local ZIPs.
.DESCRIPTION
  Accepts inventory directory or the .json file path. Prefers the local installer in that folder.
  Uses only LocalPathRel (relative to the inventory folder); no machine-specific paths are stored.
.PARAMETER InventoryPath
  Directory path containing inventory.json or a full inventory.json path.
.PARAMETER InstallDir
  Target install dir. Default: $env:LOCALAPPDATA\Microsoft\dotnet (not stored in JSON).
.PARAMETER OnlyNotInstalled
  Process entries where Installed=$false only.
.PARAMETER Channel
  Optional filter by channel (exact).
.PARAMETER Version
  Optional filter by version (exact).
.PARAMETER Architecture
  Optional filter by architecture.
#>
  [CmdletBinding(SupportsShouldProcess=$true)]
  param(
    [Parameter(Mandatory)][string]$InventoryPath,
    [string]$InstallDir = "$env:LOCALAPPDATA\Microsoft\dotnet",
    [switch]$OnlyNotInstalled,
    [string]$Channel,
    [string]$Version,
    [ValidateSet('x64','x86','arm64')][string]$Architecture
  )

  $isJson = [IO.Path]::GetExtension($InventoryPath) -ieq '.json'
  $invFile = if ($isJson) { $InventoryPath } else { Join-Path $InventoryPath 'inventory.json' }
  if (-not (Test-Path -LiteralPath $invFile -PathType Leaf)) { throw "Inventory not found: $invFile" }
  $base = Split-Path -Parent (Resolve-Path -LiteralPath $invFile).ProviderPath

  $entries = Get-Content -LiteralPath $invFile -Raw | ConvertFrom-Json
  if ($entries -isnot [System.Collections.IEnumerable]) { $entries = @($entries) }

  $filtered = $entries | Where-Object {
    $ok = $_.FileName -and $_.Status -ne 'Missing'
    if ($OnlyNotInstalled) { $ok = $ok -and (-not $_.Installed) }
    if ($Channel)          { $ok = $ok -and ($_.Channel -eq $Channel) }
    if ($Version)          { $ok = $ok -and ($_.Version -eq $Version) }
    if ($Architecture)     { $ok = $ok -and ($_.Architecture -eq $Architecture) }
    $ok
  }

  foreach ($e in $filtered) {
    $zip = if ($e.LocalPathRel) { Join-Path $base $e.LocalPathRel } else { $null }
    if (-not $zip -or -not (Test-Path -LiteralPath $zip -PathType Leaf)) { $e.InstallResult = "Missing ZIP: $zip"; continue }

    $chan = if ($e.Channel) { $e.Channel } elseif ($e.Version) {
      $p=$e.Version.Split('.'); if ($p.Count -ge 2) {"$($p[0]).$($p[1])"} else {'LTS'}
    } else { 'LTS' }

    $arch = if ($e.Architecture) { $e.Architecture } else { if([Environment]::Is64BitOperatingSystem){'x64'}else{'x86'} }

    $inst = @(
      (Join-Path $base ($e.InstallerFile ? $e.InstallerFile : 'dotnet-install.ps1')),
      (Join-Path $base 'dotnet-install.ps1'),
      '.\dotnet-install.ps1'
    ) | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if (-not $inst) { throw "dotnet-install.ps1 not found near inventory: $base" }

    if ($PSCmdlet.ShouldProcess("$zip","Install to $InstallDir via channel '$chan'")) {
      try {
        Install-DotNetSdkFromZip -Installer $inst -Channel $chan -ZipPath $zip -InstallDir $InstallDir -Architecture $arch
        $e.Installed     = $true
        $e.InstalledOn   = [DateTimeOffset]::UtcNow.ToString("o")
        $e.InstallResult = "OK"
      } catch {
        $e.Installed     = $false
        $e.InstallResult = "Error: $($_.Exception.Message)"
      }
      ($entries | ConvertTo-Json -Depth 6) | Set-Content -LiteralPath $invFile -Encoding UTF8
    }
  }

  return $filtered
}



$someDireCanbeGeneratedOrFixed = ".\PackagesX"
$channels  = @('1.1','2.2','3.1','5.0','6.0','7.0','8.0','9.0','10.0')

Update-DotNetInventory -InventoryPath $someDireCanbeGeneratedOrFixed -Channels $channels -ComputeHash
Install-DotNetFromInventory -InventoryPath $someDireCanbeGeneratedOrFixed
