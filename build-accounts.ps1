param([string]$JdkPath = 'C:/Program Files/Eclipse Adoptium/jdk-25.0.4.101-hotspot')
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
$javac = Join-Path $JdkPath 'bin/javac.exe'
$jar = Join-Path $JdkPath 'bin/jar.exe'
foreach ($tool in @($compiler, $javac, $jar)) { if (!(Test-Path -LiteralPath $tool)) { throw "Missing compiler: $tool" } }
$classes = Join-Path $root 'build/bridge-classes'
New-Item -ItemType Directory -Path $classes -Force | Out-Null
$helper = Join-Path $root 'build/compiler-helper'
New-Item -ItemType Directory -Path $helper -Force | Out-Null
& $javac -encoding UTF-8 -d $helper (Join-Path $root 'tools/CompileJava.java')
if ($LASTEXITCODE -ne 0) { throw 'Compiler helper build failed.' }
& (Join-Path $JdkPath 'bin/java.exe') -cp $helper CompileJava (Join-Path $root 'MICRO_NST.jar') $classes (Join-Path $root 'src/AccountBootstrap.java') (Join-Path $root 'src/GameAccountObserver.java') (Join-Path $root 'src/ItemStatistics.java') (Join-Path $root 'src/GameAutoController.java')
if ($LASTEXITCODE -ne 0) { throw 'Bridge compilation failed.' }
& $jar cf (Join-Path $root 'account-bridge.jar') -C $classes .
if ($LASTEXITCODE -ne 0) { throw 'Bridge packaging failed.' }
$managerBuild = Join-Path $root 'build/manager'
New-Item -ItemType Directory -Path $managerBuild -Force | Out-Null
$stagedManager = Join-Path $managerBuild 'QLTK_Accounts.exe'
& $compiler /nologo /target:winexe /platform:anycpu "/out:$stagedManager" /r:System.Core.dll /r:System.Xml.Linq.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll (Join-Path $root 'src/AccountCore.cs') (Join-Path $root 'src/CharacterSnapshot.cs') (Join-Path $root 'src/AccountHistory.cs') (Join-Path $root 'src/ProcessSleep.cs') (Join-Path $root 'src/AutoSettings.cs') (Join-Path $root 'src/AutoSettingsDialog.cs') (Join-Path $root 'src/AccountManager.cs')
if ($LASTEXITCODE -ne 0) { throw 'QLTK compilation failed.' }
# Stage first so a running tool never prevents compilation or loses its binary.
$target = Join-Path $root 'QLTK_Accounts.exe'
if (Test-Path -LiteralPath $target) {
    $previous = Join-Path $root ('QLTK_Accounts.previous-' + [Guid]::NewGuid().ToString('N').Substring(0, 8) + '.exe')
    Move-Item -LiteralPath $target -Destination $previous -ErrorAction Stop
}
Move-Item -LiteralPath $stagedManager -Destination $target -ErrorAction Stop
Write-Output 'Built QLTK_Accounts.exe and account-bridge.jar (previous executable preserved).'
