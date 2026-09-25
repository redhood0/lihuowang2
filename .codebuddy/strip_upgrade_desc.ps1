param([switch]$Apply)
$path = "z:\projects\lihuowang2\lihuowang2\lihuowang2\localization\zhs\cards.json"
$enc = New-Object System.Text.UTF8Encoding($false)
$lines = [System.IO.File]::ReadAllLines($path, [System.Text.Encoding]::UTF8)
$rx = '^\s*"[^"]+\.upgradeDescription":'
$out = New-Object System.Collections.Generic.List[string]
$removed = New-Object System.Collections.Generic.List[string]
foreach ($l in $lines) {
  if ($l -match $rx) {
    $k = $l -replace '^\s*"', ''
    $k = $k -replace '":.*$', ''
    $removed.Add($k)
    continue
  }
  $out.Add($l)
}
for ($i = $out.Count - 1; $i -ge 0; $i--) {
  $t = $out[$i].Trim()
  if ($t -eq '' -or $t -eq '}') { continue }
  if ($t.EndsWith(',')) {
    $out[$i] = $t.TrimEnd(',')
    Write-Output ("trailing comma stripped on line " + ($i + 1))
  }
  break
}
Write-Output ("removed upgradeDescription keys: " + $removed.Count)
foreach ($k in $removed) { Write-Output ("  " + $k) }
if ($Apply) {
  [System.IO.File]::WriteAllLines($path, $out, $enc)
  Write-Output "APPLIED"
} else {
  Write-Output "DRY RUN (nothing written)"
}
