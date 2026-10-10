# review-translations.ps1 - batch quality review of translated resx files
#
# Uses a large model (gemma-3-12b, 16K context) to verify each existing
# translation against the English source. Focuses on short strings (<= 40
# chars), which are the most error-prone for low-resource languages.
# Corrections are written back to the resx files; a report is written to
# reports/review-<stamp>.json.
#
# Usage:
#   .\review-translations.ps1                    # review all locales
#   .\review-translations.ps1 -Locales su,tg    # specific locales
#   .\review-translations.ps1 -Port 11436 -MaxLen 40 -ParallelJobs 4
#
# Requires: a llama-server with the review model on the given port
# (e.g. 12B, 2 slots x 16K ctx, ngl partial):
#   llama-server -m models\gemma-3-12b-it-Q4_K_M.gguf -c 32768 --parallel 2 ^
#     --flash-attn on -ngl 60 --cache-type-k q4_0 --cache-type-v q4_0 --port 11436
param(
    [string[]]$Locales,
    [int]$Port = 11436,
    [int]$MaxLen = 40,
    [int]$ParallelJobs = 4,
    [int]$BatchSize = 25,
    [string]$RepoRoot = ''
)

$ErrorActionPreference = 'Stop'
$scriptDir = $PSScriptRoot
if (-not $RepoRoot) { $RepoRoot = Split-Path (Split-Path $scriptDir -Parent) -Parent }
$reportDir = Join-Path $scriptDir 'reports'
New-Item -ItemType Directory -Path $reportDir -Force | Out-Null

# ---------------------------------------------------------------- helpers ---

function Get-PlaceholderSet {
    param([string]$S)
    $set = New-Object System.Collections.Generic.List[string]
    foreach ($m in [regex]::Matches($S, '\{\{-[A-Za-z0-9_]+\}\}|\{\{[A-Za-z0-9_]+,\s*[^}]+\}\}|\{\{[A-Za-z0-9_]+\}\}|\{[0-9]+:[^}]+\}|\{[0-9]+\}|\{[A-Za-z_][A-Za-z0-9_]*\}|%[sdifxun%]|\\[rn]|<[^>]+>|&(?:amp|lt|gt|quot|apos);|&#(?:[0-9]+|x[0-9A-Fa-f]+);')) {
        $set.Add($m.Value)
    }
    return ($set | Sort-Object) -join '|'
}

function Read-TextRows {
    param([string]$Path)
    $xml = New-Object System.Xml.XmlDocument
    $xml.Load($Path)
    $rows = New-Object System.Collections.Generic.List[object]
    foreach ($node in $xml.SelectNodes('//data')) {
        if ($node.HasAttribute('type') -or $node.HasAttribute('mimetype')) { continue }
        $name = $node.GetAttribute('name')
        $v = $node.SelectSingleNode('value').InnerText
        if ([string]::IsNullOrWhiteSpace($v)) { continue }
        $rows.Add(@{ Name = $name; Value = $v.Trim() })
    }
    return $rows
}

function Repair-Text {
    param([string]$S)
    if ([string]::IsNullOrWhiteSpace($S)) { return $S }
    $t = $S.Trim()
    $t = $t -replace '^```(?:\w+)?\s*', ''
    $t = $t -replace '\s*```\s*$', ''
    $t = $t -replace '[\u200B-\u200F\u2028-\u202F\u2060\uFEFF\u00AD]', ''
    $t = $t.Replace([char]0x00A0, ' ')
    $t = $t.Trim()
    if ($t.Length -ge 2) {
        $pairs = @(@('"','"'), @("'","'"), @([string][char]0x201C, [string][char]0x201D), @([string][char]0x300C, [string][char]0x300D))
        foreach ($p in $pairs) {
            if ($t.StartsWith($p[0]) -and $t.EndsWith($p[1])) {
                $inner = $t.Substring($p[0].Length, $t.Length - $p[0].Length - $p[1].Length).Trim()
                if ($inner) { $t = $inner }
            }
        }
    }
    return $t
}

# --------------------------------------------------------------- worker ----

$worker = {
    param($Args2)
    $pair = $Args2.pair
    $loc = $Args2.loc
    $port = $Args2.port
    $maxLen = $Args2.maxLen
    $batchSize = $Args2.batchSize
    $ErrorActionPreference = 'Stop'

    function Read-TextRows {
        param([string]$Path)
        $xml = New-Object System.Xml.XmlDocument
        $xml.Load($Path)
        $rows = New-Object System.Collections.Generic.List[object]
        foreach ($node in $xml.SelectNodes('//data')) {
            if ($node.HasAttribute('type') -or $node.HasAttribute('mimetype')) { continue }
            $name = $node.GetAttribute('name')
            $v = $node.SelectSingleNode('value').InnerText
            if ([string]::IsNullOrWhiteSpace($v)) { continue }
            $rows.Add(@{ Name = $name; Value = $v.Trim() })
        }
        return $rows
    }
    function Repair-Text {
        param([string]$S)
        if ([string]::IsNullOrWhiteSpace($S)) { return $S }
        $t = $S.Trim()
        $t = $t -replace '^```(?:\w+)?\s*', ''
        $t = $t -replace '\s*```\s*$', ''
        $t = $t -replace '[\u200B-\u200F\u2028-\u202F\u2060\uFEFF\u00AD]', ''
        $t = $t.Replace([char]0x00A0, ' ')
        $t = $t.Trim()
        if ($t.Length -ge 2) {
            $pairs = @(@('"','"'), @("'","'"), @([string][char]0x201C, [string][char]0x201D), @([string][char]0x300C, [string][char]0x300D))
            foreach ($p in $pairs) {
                if ($t.StartsWith($p[0]) -and $t.EndsWith($p[1])) {
                    $inner = $t.Substring($p[0].Length, $t.Length - $p[0].Length - $p[1].Length).Trim()
                    if ($inner) { $t = $inner }
                }
            }
        }
        return $t
    }
    function Invoke-Chat2 {
        param([int]$Port, [string]$System, [string]$User)
        $payload = @{ model = 'local'; messages = @(); temperature = 0; max_tokens = 2048 }
        if ($System) { $payload.messages += @{ role = 'system'; content = $System } }
        $payload.messages += @{ role = 'user'; content = $User }
        $body = $payload | ConvertTo-Json -Depth 6
        $bytes = [System.Text.Encoding]::UTF8.GetBytes($body)
        $r = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/v1/chat/completions" -Method Post `
            -ContentType 'application/json' -Body $bytes -TimeoutSec 600
        return [string]$r.choices[0].message.content
    }
    function Invoke-Chat3 {
        param([int]$Port, [string]$System, [string]$User)
        for ($a = 1; $a -le 3; $a++) {
            try { return (Invoke-Chat2 $Port $System $User) }
            catch {
                if ($a -eq 3) { throw }
                Start-Sleep -Seconds 3
            }
        }
    }
    function Get-PlaceholderSet2 { param([string]$S) (([regex]::Matches($S, '\{[0-9]+\}|\{[A-Za-z_][A-Za-z0-9_]*\}|%[sdifxun%]|\\[rn]|<[^>]+>') | ForEach-Object { $_.Value }) | Sort-Object -Unique) -join '|' }

    $srcRows = Read-TextRows $pair.Source
    $trgPath = $pair.TranslationPattern.Replace('%locale%', $loc.Code)
    if (-not (Test-Path -LiteralPath $trgPath)) { return $null }
    $trgRows = Read-TextRows $trgPath

    $srcByName = @{}
    foreach ($r in $srcRows) { $srcByName[$r.Name] = $r.Value }
    $trgByName = @{}
    foreach ($r in $trgRows) { $trgByName[$r.Name] = $r.Value }

    # collect short strings (the risky set), in source order
    $candidates = New-Object System.Collections.Generic.List[object]
    foreach ($r in $srcRows) {
        if ($r.Value.Length -gt $maxLen) { continue }
        if (-not $trgByName.ContainsKey($r.Name)) { continue }
        $t = $trgByName[$r.Name]
        if ($t -eq $r.Value) { continue }          # untranslated placeholder files skip
        if ((Get-PlaceholderSet2 $r.Value) -ne (Get-PlaceholderSet2 $t)) { continue }  # already flagged by pipeline
        $candidates.Add(@{ Name = $r.Name; Src = $r.Value; Trg = $t })
    }

    $stats = @{ checked = 0; corrected = 0; failed = 0; corrections = @() }
    if ($candidates.Count -eq 0) {
        Write-Host ("    [{0}] {1}: nothing to review" -f $loc.Code, (Split-Path $pair.Source -Parent | Split-Path -Leaf))
        return $null
    }
    Write-Host ("    [{0}] {1}: reviewing {2} strings" -f $loc.Code, (Split-Path $pair.Source -Parent | Split-Path -Leaf), $candidates.Count)

    $system = "You are a professional multilingual quality reviewer for a Windows laptop-utility app. For each numbered pair, judge whether the {LANG} translation is correct, natural and complete, and keeps every placeholder ({0}, %s, \n) exactly. Reply with exactly one line per pair: if correct output '[n] OK'; if wrong output '[n] CORRECTED_TEXT' (only the corrected {LANG} text, no explanation). No markdown, no commentary, no extra lines."
    $system = $system.Replace('{LANG}', $loc.Name)

    $corrections = @{}
    for ($i = 0; $i -lt $candidates.Count; $i += $batchSize) {
      try {
        $batch = @($candidates[$i..([math]::Min($i + $batchSize - 1, $candidates.Count - 1))])
        $lines = @()
        $j = 0
        foreach ($c in $batch) {
            $j++
            $lines += ("[{0}] EN: {1} / {2}: {3}" -f $j, $c.Src, $loc.Name, $c.Trg)
        }
        $user = "Review these {0} pairs:`n{1}" -f $loc.Name, ($lines -join "`n")
        $content = Invoke-Chat3 $port $system $user

        $parsed = @{}
        try {
            foreach ($m in [regex]::Matches($content, '\[(\d+)\]\s*(.*)')) {
                $idx = [int]$m.Groups[1].Value
                $val = (Repair-Text $m.Groups[2].Value)
                if ($idx -ge 1 -and $idx -le $batch.Count -and $val) { $parsed[$idx] = $val }
            }
        } catch { }
        foreach ($c in $batch) {
            $idx = $batch.IndexOf($c) + 1
            $stats.checked++
            if (-not $parsed.ContainsKey($idx)) { $stats.failed++; continue }
            $val = $parsed[$idx]
            if ($val -match '^(OK|ok|ok\.|correct|✓)$') { continue }
            # placeholder guard on the correction
            if ((Get-PlaceholderSet2 $c.Src) -ne (Get-PlaceholderSet2 $val)) { $stats.failed++; continue }
            $corrections[$c.Name] = $val
            $stats.corrected++
            $stats.corrections += @{ key = $c.Name; src = $c.Src; from = $c.Trg; to = $val }
        }
        Write-Host ("      review batch {0}/{1} done, corrected so far {2}" -f ([math]::Floor($i / $batchSize) + 1), [math]::Ceiling($candidates.Count / $batchSize), $stats.corrected)
      } catch {
        foreach ($c in $batch) { $stats.failed++ }
      }
    }

    if ($corrections.Count -eq 0) { return $stats }

    # write back corrections
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
    $settings.Indent = $true
    $writer = [System.Xml.XmlWriter]::Create($trgPath, $settings)
    $writer.WriteStartDocument()
    $writer.WriteStartElement('root')
    $tXml = New-Object System.Xml.XmlDocument
    $tXml.PreserveWhitespace = $false
    $tXml.Load($trgPath)
    foreach ($node in $tXml.SelectNodes('/root/resheader')) { $writer.WriteRaw($node.OuterXml) }
    foreach ($r in $trgRows) {
        $writer.WriteStartElement('data')
        $writer.WriteAttributeString('name', $r.Name)
        $writer.WriteStartElement('value')
        if ($corrections.ContainsKey($r.Name)) { $writer.WriteString($corrections[$r.Name]) }
        else { $writer.WriteString($r.Value) }
        $writer.WriteEndElement()
        $writer.WriteEndElement()
    }
    $writer.WriteEndElement()
    $writer.WriteEndDocument()
    $writer.Close()

    return $stats
}

# --------------------------------------------------------------- dispatch ----

$pairs = @(
    @{ Source = Join-Path $RepoRoot 'Libraries\Device\Resources\Resource.resx'; TranslationPattern = Join-Path $RepoRoot 'Libraries\Device\Resources\Resource.%locale%.resx' },
    @{ Source = Join-Path $RepoRoot 'Libraries\Automation\Resources\Resource.resx'; TranslationPattern = Join-Path $RepoRoot 'Libraries\Automation\Resources\Resource.%locale%.resx' },
    @{ Source = Join-Path $RepoRoot 'Libraries\Macro\Resources\Resource.resx'; TranslationPattern = Join-Path $RepoRoot 'Libraries\Macro\Resources\Resource.%locale%.resx' },
    @{ Source = Join-Path $RepoRoot 'Apps\CLI\Resources\CLI.Resources.resx'; TranslationPattern = Join-Path $RepoRoot 'Apps\CLI\Resources\CLI.Resources.%locale%.resx' }
)

$localeFile = Join-Path $scriptDir 'locales.txt'
$localeLines = Get-Content -LiteralPath $localeFile -Encoding UTF8 | Where-Object { $_ -and -not $_.TrimStart().StartsWith('#') }
$allLocales = New-Object System.Collections.Generic.List[object]
foreach ($line in $localeLines) {
    $parts = $line -split '\s+'
    if ($parts.Count -lt 2) { continue }
    $allLocales.Add(@{ Code = $parts[0]; Engine = $parts[1] })
}
if ($Locales -and $Locales.Count -gt 0) {
    $wanted = @($Locales -join ',' -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
    $filtered = $allLocales | Where-Object { $wanted -contains $_.Code }
    $allLocales = New-Object System.Collections.Generic.List[object]
    foreach ($f in $filtered) { $allLocales.Add($f) }
}

Write-Host "Review: $($pairs.Count) modules x $($allLocales.Count) locales (port $Port, maxLen $MaxLen, batch $BatchSize)"
$workerBlock = [scriptblock]::Create($worker)

$tasks = New-Object System.Collections.Generic.List[object]
foreach ($pair in $pairs) {
    if (-not (Test-Path -LiteralPath $pair.Source)) { continue }
    foreach ($loc in $allLocales) {
        $tasks.Add(@{ pair = $pair; loc = $loc })
    }
}

$active = New-Object System.Collections.Generic.List[object]
$i = 0
$allStats = @()
while ($i -lt $tasks.Count -or $active.Count -gt 0) {
    while ($active.Count -lt $ParallelJobs -and $i -lt $tasks.Count) {
        $t = $tasks[$i]
        $jobArgs = @{ pair = $t.pair; loc = $t.loc; port = $Port; maxLen = $MaxLen; batchSize = $BatchSize }
        $active.Add(@{ Job = Start-Job -ScriptBlock $workerBlock -ArgumentList $jobArgs; Locale = $t.loc.Code })
        $i++
    }
    $finished = @($active | Where-Object { $_.Job.State -ne 'Running' })
    foreach ($f in $finished) {
        $result = $null
        try {
            $result = Receive-Job -Job $f.Job -Wait -AutoRemoveJob
        } catch {
            Write-Host ("    WARN job failed: " + $_.Exception.Message)
            $null = Remove-Job -Job $f.Job -Force -ErrorAction SilentlyContinue
        }
        if ($result) {
            $allStats += $result
            Write-Host ("    DONE [{0}] checked={1} corrected={2} failed={3}" -f $result.Locale, $result.checked, $result.corrected, $result.failed)
            foreach ($c in $result.corrections) {
                Write-Host ("         fix [{0}]: '{1}' -> '{2}'" -f $c.key, $c.from, $c.to)
            }
        }
        $active.Remove($f) | Out-Null
    }
    if ($active.Count -eq $ParallelJobs) { Start-Sleep -Seconds 2 }
}

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$allStats | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $reportDir ("review-$stamp.json")) -Encoding UTF8
$checked = ($allStats | Measure-Object -Property checked -Sum).Sum
$corrected = ($allStats | Measure-Object -Property corrected -Sum).Sum
Write-Host "Review finished: checked=$checked corrected=$corrected. Report: reports/review-$stamp.json"
Write-Host "  node Tools/CheckSourceUnicode/check-unicode.mjs"

