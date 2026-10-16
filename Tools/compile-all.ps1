# compile-all.ps1 —— 用 csc 直接编译 5 个程序集，绕开 Unity 验证改动
#
# 【为什么放在 Tools/ 而不是 Temp/】
#   `Temp/` 是 Unity 自己的临时目录，会被 Unity 自动清理（我因此丢过两次脚本）。
#   Tools/ 在工程根目录，不受 Unity 管理。
#
# 用法：
#   $src = Get-Content -LiteralPath 'D:\ugame\engineer\Tools\compile-all.ps1' -Raw -Encoding UTF8
#   Invoke-Expression $src

$ErrorActionPreference = 'Continue'

$ROOT    = 'D:\ugame\engineer'
$SCRIPTS = Join-Path $ROOT 'Assets\Scripts'
$UNITY   = 'D:\Unity\2022.3.62f2c1\Editor\Data'
$MANAGED = Join-Path $UNITY 'Managed'
$UGUI    = Join-Path $MANAGED 'UnityEngine'
$OUT     = Join-Path $ROOT 'Tools\buildcheck'
New-Item -ItemType Directory -Force -Path $OUT | Out-Null

# --- csc.exe ---
$CSC = Join-Path $MANAGED 'Roslyn\csc.exe'
if (-not (Test-Path $CSC)) {
    $c = Get-ChildItem (Join-Path $UNITY '..') -Recurse -Filter 'csc.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($c) { $CSC = $c.FullName }
}
if (-not (Test-Path $CSC)) { Write-Output '★ 找不到 csc.exe'; return }
Write-Output "csc : $CSC"

# --- 参考程序集 ---
$refs = @()
$refs += Get-ChildItem $UGUI -Filter '*.dll' -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName }
$refs += Get-ChildItem $MANAGED -Filter '*.dll' -ErrorAction SilentlyContinue |
         Where-Object { $_.Name -in @('mscorlib.dll','System.dll','System.Core.dll','netstandard.dll') } |
         ForEach-Object { $_.FullName }
$bcl = Join-Path $UNITY 'NetStandard\ref\2.1.0'
if (Test-Path $bcl) {
    $refs += Get-ChildItem $bcl -Filter '*.dll' -ErrorAction SilentlyContinue | ForEach-Object { $_.FullName }
}
# TextMeshPro / glTFast 等包程序集（Unity 编译过的）
$refs += Get-ChildItem (Join-Path $ROOT 'Library\ScriptAssemblies') -Filter '*.dll' -ErrorAction SilentlyContinue |
         Where-Object { $_.Name -notlike 'Engineer*' } | ForEach-Object { $_.FullName }

Write-Output ("BCL 参考程序集   : " + ($refs | Where-Object { $_ -like '*NetStandard*' -or $_ -like '*mscorlib*' }).Count)
Write-Output ("Unity 参考程序集 : " + ($refs | Where-Object { $_ -like '*UnityEngine*' }).Count)
Write-Output ("借用包程序集     : " + ($refs | Where-Object { $_ -like '*ScriptAssemblies*' }).Count)

$refArgs = @()
foreach ($r in ($refs | Sort-Object -Unique)) { $refArgs += "/reference:`"$r`"" }
$defArgs = @('/define:UNITY_2022_3_OR_NEWER','/define:UNITY_EDITOR','/define:UNITY_STANDALONE_WIN','/define:ENABLE_TEXTMESHPRO')

function Build-Assembly {
    param([string]$Name, [string]$Dir, [string[]]$ExtraRefs)

    Write-Output ''
    Write-Output ("=== 编译 " + $Name + " ===")
    $files = @()
    if (Test-Path $Dir) { $files = Get-ChildItem $Dir -Recurse -Filter '*.cs' | ForEach-Object { $_.FullName } }
    if ($files.Count -eq 0) { Write-Output '  (没有源文件)'; return $false }
    Write-Output ("  源文件: " + $files.Count + " 个")

    $outDll = Join-Path $OUT ($Name + '.dll')
    $allRefs = $refArgs + ($ExtraRefs | ForEach-Object { "/reference:`"$_`"" })
    $cscArgs = @('/nologo','/target:library','/langversion:9.0','/nowarn:0169,0649,0414,0162,0108') +
               $defArgs + $allRefs + @("/out:`"$outDll`"") + $files

    $log = & $CSC @cscArgs 2>&1
    $errs = $log | Where-Object { $_ -match 'error CS' }
    if ($errs) {
        $errs | Select-Object -First 12 | ForEach-Object { Write-Output ('  ' + $_) }
        Write-Output ("  ★ 失败  (退出码 " + $LASTEXITCODE + ")")
        return $false
    }
    $sz = if (Test-Path $outDll) { [math]::Round((Get-Item $outDll).Length/1KB,1) } else { 0 }
    Write-Output ("  ★ 通过  ->  " + $Name + ".dll (" + $sz + " KB)")
    return $true
}

$core   = Join-Path $OUT 'Engineer.Core.dll'
$config = Join-Path $OUT 'Engineer.Config.dll'
$ui     = Join-Path $OUT 'Engineer.UI.dll'
$editor = Join-Path $OUT 'Engineer.Editor.dll'
$uiEd   = Join-Path $SCRIPTS 'UI\Editor'

$ok = [ordered]@{}
$ok['Engineer.Core']      = Build-Assembly -Name 'Engineer.Core'      -Dir (Join-Path $SCRIPTS 'Core')   -ExtraRefs @()
$ok['Engineer.Config']    = Build-Assembly -Name 'Engineer.Config'    -Dir (Join-Path $SCRIPTS 'Config') -ExtraRefs @($core)
$ok['Engineer.Editor']    = Build-Assembly -Name 'Engineer.Editor'    -Dir (Join-Path $SCRIPTS 'Editor') -ExtraRefs @($core, $config)
$ok['Engineer.UI']        = Build-Assembly -Name 'Engineer.UI'        -Dir (Join-Path $SCRIPTS 'UI')     -ExtraRefs @($core, $config)
$ok['Engineer.UI.Editor'] = Build-Assembly -Name 'Engineer.UI.Editor' -Dir $uiEd                        -ExtraRefs @($core, $config, $ui, $editor)

Write-Output ''
Write-Output ('=' * 64)
$fail = 0
foreach ($k in $ok.Keys) {
    $st = if ($ok[$k]) { '通过' } else { '★ 失败' }
    if (-not $ok[$k]) { $fail++ }
    Write-Output ("  {0,-22} {1}" -f $k, $st)
}
Write-Output ('=' * 64)
if ($fail -eq 0) { Write-Output '全部程序集编译通过。' } else { Write-Output ("有 " + $fail + " 个程序集编译失败。") }
