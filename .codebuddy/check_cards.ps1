$p = "z:\projects\lihuowang2\lihuowang2\lihuowang2\localization\zhs\cards.json"
$b = "z:\projects\lihuowang2\.codebuddy\cards.json.bak"
$out = "z:\projects\lihuowang2\.codebuddy\check_cards.txt"
$lines = New-Object System.Collections.Generic.List[string]
$raw = [System.IO.File]::ReadAllText($p, [System.Text.Encoding]::UTF8)
$map = @{}
try {
  $j = $raw | ConvertFrom-Json
  foreach ($pr in $j.PSObject.Properties) { $map[$pr.Name] = $pr.Value }
  $lines.Add("JSON: OK, keys=" + (($j.PSObject.Properties | Measure-Object).Count))
} catch {
  $lines.Add("JSON PARSE ERROR: " + $_)
}
$tokens = [regex]::Matches($raw, '\{(\w+)[:\}]') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique
$lines.Add("VARS USED: " + ($tokens -join ', '))
foreach ($k in $map.Keys) {
  if ($k.EndsWith('.description')) {
    $sk = $k -replace '\.description$', '.smartDescription'
    if ($map.ContainsKey($sk) -and $map[$sk] -ne $map[$k]) { $lines.Add("DIVERGE desc/smart: " + $k) }
  }
}
$old = @{}
$oj = ([System.IO.File]::ReadAllText($b, [System.Text.Encoding]::UTF8) | ConvertFrom-Json)
foreach ($pr in $oj.PSObject.Properties) { $old[$pr.Name] = $pr.Value }
foreach ($k in $map.Keys) {
  if (-not $old.ContainsKey($k)) { $lines.Add("NEW KEY: " + $k); continue }
  if ($old[$k] -ne $map[$k]) {
    $lines.Add("CHANGED " + $k)
    $lines.Add("   old: " + $old[$k])
    $lines.Add("   new: " + $map[$k])
  }
}
foreach ($k in $old.Keys) { if (-not $map.ContainsKey($k)) { $lines.Add("DELETED KEY: " + $k) } }
[System.IO.File]::WriteAllLines($out, $lines, (New-Object System.Text.UTF8Encoding($false)))
Write-Output ("report written, lines=" + $lines.Count)
