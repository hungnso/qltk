$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$core = Join-Path $root 'src/AccountCore.cs'
if (!(Test-Path -LiteralPath $core)) { throw 'FAIL: QLTK account reader and launch planner are not implemented.' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
$output = Join-Path $PSScriptRoot 'AccountTests.exe'
& $compiler /nologo /target:exe "/out:$output" /r:System.Core.dll /r:System.Xml.Linq.dll $core (Join-Path $root 'src/CharacterSnapshot.cs') (Join-Path $PSScriptRoot 'AccountTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'Account tests failed.' }
$uiOutput = Join-Path $PSScriptRoot 'UiTests.exe'
& $compiler /nologo /target:exe "/out:$(Join-Path $PSScriptRoot 'FakeJava.exe')" /r:System.Core.dll (Join-Path $PSScriptRoot 'FakeJava.cs')
if ($LASTEXITCODE -ne 0) { throw 'UI process fixture compilation failed.' }
& $compiler /nologo /target:exe "/out:$uiOutput" "/r:$(Join-Path $root 'QLTK_Accounts.exe')" /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'UiTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'UI tests compilation failed.' }
Copy-Item -LiteralPath (Join-Path $root 'QLTK_Accounts.exe') -Destination (Join-Path $PSScriptRoot 'QLTK_Accounts.exe') -Force
& $uiOutput $root
if ($LASTEXITCODE -ne 0) { throw 'UI tests failed.' }
$javac = 'C:/Program Files/Eclipse Adoptium/jdk-25.0.4.101-hotspot/bin/javac.exe'
$testClasses = Join-Path $root 'build/test-classes'
New-Item -ItemType Directory -Path $testClasses -Force | Out-Null
$compileClasspath = (Join-Path $root 'account-bridge.jar') + ';' + (Join-Path $root 'MICRO_NST.jar')
& 'C:/Program Files/Eclipse Adoptium/jdk-25.0.4.101-hotspot/bin/java.exe' -cp (Join-Path $root 'build/compiler-helper') CompileJava $compileClasspath $testClasses (Join-Path $PSScriptRoot 'BridgeTests.java') (Join-Path $PSScriptRoot 'HeadlessSmoke.java')
if ($LASTEXITCODE -ne 0) { throw 'Bridge tests compilation failed.' }
$java = Join-Path $root 'jre/bin/java.exe'
$gamePath = ([xml](Get-Content -LiteralPath (Join-Path $root 'settings.xml') -Raw)).Settings.GamePath
& $java -cp ($testClasses + ';' + (Join-Path $root 'account-bridge.jar') + ';' + (Join-Path $root 'MICRO_NST.jar')) BridgeTests $gamePath
if ($LASTEXITCODE -ne 0) { throw 'Bridge tests failed.' }
$fixtureClasses = Join-Path $root 'build/fixture-classes'
New-Item -ItemType Directory -Path $fixtureClasses -Force | Out-Null
$fixtureSources = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'fixture') -Filter '*.java' | ForEach-Object FullName)
& 'C:/Program Files/Eclipse Adoptium/jdk-25.0.4.101-hotspot/bin/java.exe' -cp (Join-Path $root 'build/compiler-helper') CompileJava (Join-Path $root 'MICRO_NST.jar') $fixtureClasses @fixtureSources
if ($LASTEXITCODE -ne 0) { throw 'Fixture compilation failed.' }
$fixtureJar = Join-Path $root 'build/fixture.jar'
& 'C:/Program Files/Eclipse Adoptium/jdk-25.0.4.101-hotspot/bin/jar.exe' cfm $fixtureJar (Join-Path $PSScriptRoot 'fixture/MANIFEST.MF') -C $fixtureClasses .
if ($LASTEXITCODE -ne 0) { throw 'Fixture packaging failed.' }
& $java -cp ($testClasses + ';' + $compileClasspath) HeadlessSmoke $fixtureJar (Join-Path $root 'build/smoke-data')
if ($LASTEXITCODE -ne 0) { throw 'Offline emulator integration failed.' }
