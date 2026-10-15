$b = [IO.File]::ReadAllBytes("$PWD/Assets/Cabazon W01 Regular.ttf")
function U16([int]$o) { return ($b[$o] -shl 8) -bor $b[$o+1] }
function U32([int]$o) { return ([uint32]$b[$o] -shl 24) -bor ([uint32]$b[$o+1] -shl 16) -bor ([uint32]$b[$o+2] -shl 8) -bor [uint32]$b[$o+3] }

$numTables = U16 4
$nameOff = -1
for ($i = 0; $i -lt $numTables; $i++) {
  $rec = 12 + $i * 16
  $tag = [Text.Encoding]::ASCII.GetString($b, $rec, 4)
  if ($tag -eq 'name') { $nameOff = [int](U32 ($rec + 8)) }
  if ($i -lt 4) { "table[$i] tag='$tag' off=" + (U32 ($rec + 8)) }
}
if ($nameOff -lt 0) { 'no name table'; exit }
$count = (U16 ($nameOff + 2))
$strOff = $nameOff + (U16 ($nameOff + 4))
"records: $count"
for ($i = 0; $i -lt $count; $i++) {
  $r = $nameOff + 6 + $i * 12
  $plat = U16 $r; $enc = U16 ($r + 2); $lid = U16 ($r + 4); $nid = U16 ($r + 6)
  $len = U16 ($r + 8); $off = U16 ($r + 10)
  if ($nid -in 1,2,4,6,16,17) {
    $so = $strOff + $off
    if ($plat -eq 3) {
      $s = [Text.Encoding]::Unicode.GetString($b, $so, $len)
    } else {
      $s = [Text.Encoding]::ASCII.GetString($b, $so, $len)
    }
    "id=$nid plat=$plat lang=$lid -> '$s'"
  }
}
