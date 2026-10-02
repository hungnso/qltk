@echo off
set "QLTKDIR=%~dp0"
where powershell >nul 2>&1
if errorlevel 1 (
  echo May khong co PowerShell. Vui long cap nhat Windows roi chay lai.
  pause
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -Command "$m='#PS'+'START#'; $c=[IO.File]::ReadAllText('%~f0',[Text.Encoding]::UTF8); iex $c.Substring($c.IndexOf($m)+$m.Length)"
exit /b 0
#PSSTART#
# ===== PHAN POWERSHELL (nhung trong setup.bat) - hien popup tieng Viet co dau =====
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$AppDir = $env:QLTKDIR
if (-not $AppDir) { $AppDir = (Get-Location).Path }

function Test-Java {
    # CHI check JRE trong folder tool (jre\bin\javaw.exe). KHONG check Java cai he thong
    # (JAVA_HOME/PATH/Program Files) - vi tool CHI dung JRE bundle nay, Java he thong tool
    # khong quan duoc (launcher de JVM process con -> sai PID). Co roi -> bao, chua co -> cai.
    return (Test-Path (Join-Path $AppDir 'jre\bin\javaw.exe'))
}

$script:Form  = $null
$script:Label = $null
$script:Job   = $null

function Build-Form([string]$text) {
    $script:Form = New-Object Windows.Forms.Form
    $script:Form.Text = 'QLTK NST'
    $script:Form.Size = New-Object Drawing.Size(470, 200)
    $script:Form.StartPosition = 'CenterScreen'
    $script:Form.FormBorderStyle = 'FixedDialog'
    $script:Form.MaximizeBox = $false
    $script:Form.MinimizeBox = $false
    $script:Form.TopMost = $true
    $script:Label = New-Object Windows.Forms.Label
    $script:Label.Dock = 'Fill'
    $script:Label.TextAlign = 'MiddleCenter'
    $script:Label.Font = New-Object Drawing.Font('Segoe UI', 12)
    $script:Label.Text = $text
    $script:Form.Controls.Add($script:Label)
}

function Start-AutoClose([int]$sec) {
    $script:CloseTimer = New-Object Windows.Forms.Timer
    $script:CloseTimer.Interval = $sec * 1000
    $script:CloseTimer.Add_Tick({ $script:CloseTimer.Stop(); $script:Form.Close() })
    $script:CloseTimer.Start()
}

if (Test-Java) {
    Build-Form "Đã cài đủ thư viện (Java) rồi.`n`nHãy mở QLTK_NST.exe để sử dụng.`n`n(Cửa sổ tự đóng sau 5 giây)"
    Start-AutoClose 5
    [void]$script:Form.ShowDialog()
    return
}

# Nguon tai Java 8: web rieng (khach VN tai on dinh, khong bi GitHub chan/timeout).
$jreUrl = 'https://nsotien.com/uploads/setup/qltk_jre8.zip'
$jreZip = Join-Path $env:TEMP 'qltk_jre8.zip'
$jreTmp = Join-Path $env:TEMP 'qltk_jre8_out'

Build-Form "Đang tải và cài Java 8...`n`nVui lòng chờ, đừng tắt cửa sổ này."

$script:Job = Start-Job -ScriptBlock {
    param($url, $zip, $tmp, $appdir)

    # Xoa file an toan: xoa khong duoc (dang bi khoa) thi doi ten lanh sang ben, KHONG nem loi do.
    function Remove-Safe([string]$path) {
        if (-not (Test-Path $path)) { return }
        try { Remove-Item $path -Recurse -Force -ErrorAction Stop }
        catch {
            try { Rename-Item $path ($path + '.old_' + [Guid]::NewGuid().ToString('N').Substring(0,6)) -Force -ErrorAction Stop } catch { }
        }
    }

    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]'Tls12,Tls11,Tls'

        # Tai zip, retry 3 lan (mang chap chon -> thu lai thay vi fail ngay).
        Remove-Safe $zip
        $downloaded = $false
        $lastErr = ''
        for ($try = 1; $try -le 3; $try++) {
            try {
                Remove-Safe $zip
                (New-Object Net.WebClient).DownloadFile($url, $zip)
                if ((Test-Path $zip) -and ((Get-Item $zip).Length -gt 1MB)) { $downloaded = $true; break }
                $lastErr = 'File tai ve rong/hong.'
            } catch { $lastErr = $_.Exception.Message }
        }
        if (-not $downloaded) { return "ERR:Khong tai duoc Java. ($lastErr)" }

        # Giai nen
        Remove-Safe $tmp
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [IO.Compression.ZipFile]::ExtractToDirectory($zip, $tmp)

        # Tim thu muc chua bin\javaw.exe: nhan ca zip co folder boc (jre\bin\..)
        # lan zip nen thang noi dung (bin\.. ngay goc).
        $src = $null
        if (Test-Path (Join-Path $tmp 'bin\javaw.exe')) {
            $src = Get-Item $tmp
        } else {
            $src = Get-ChildItem $tmp -Directory -Recurse |
                   Where-Object { Test-Path (Join-Path $_.FullName 'bin\javaw.exe') } |
                   Select-Object -First 1
        }
        if (-not $src) { return 'ERR:Khong tim thay javaw trong goi tai ve.' }

        $dest = Join-Path $appdir 'jre'
        Remove-Safe $dest
        Move-Item $src.FullName $dest

        Remove-Safe $zip
        Remove-Safe $tmp
        if (Test-Path (Join-Path $appdir 'jre\bin\javaw.exe')) { return 'OK' }
        return 'ERR:Cai khong thanh cong.'
    } catch { return "ERR:$($_.Exception.Message)" }
} -ArgumentList $jreUrl, $jreZip, $jreTmp, $AppDir

$script:PollTimer = New-Object Windows.Forms.Timer
$script:PollTimer.Interval = 800
$script:PollTimer.Add_Tick({
    if ($script:Job.State -ne 'Running') {
        $script:PollTimer.Stop()
        $res = Receive-Job $script:Job
        Remove-Job $script:Job -Force -ErrorAction SilentlyContinue
        if ($res -eq 'OK') {
            $script:Label.Text = "Đã cài xong Java 8!`n`nHãy mở QLTK_NST.exe để sử dụng.`n`n(Cửa sổ tự đóng sau 5 giây)"
        } else {
            $msg = ($res -replace '^ERR:', '')
            $script:Label.Text = "Cài Java thất bại.`nKiểm tra mạng/tường lửa rồi chạy lại setup.`n`n$msg`n`n(Cửa sổ tự đóng sau 5 giây)"
        }
        Start-AutoClose 5
    }
})
$script:PollTimer.Start()
[void]$script:Form.ShowDialog()
