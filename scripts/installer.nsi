Unicode true
ManifestDPIAware true
ManifestSupportedOS all
RequestExecutionLevel user
SetCompressor /SOLID lzma
SetCompressorDictSize 32
!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "WinVer.nsh"
!include "x64.nsh"
!include "FileFunc.nsh"
!ifndef VERSION
  !error "Build using scripts/build-installer.ps1"
!endif
!define APP_NAME "桌面小伙伴"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
Name "${APP_NAME} ${VERSION}"
Caption "${APP_NAME}安装向导"
OutFile "${OUTPUT_FILE}"
; Empty default preserves NSIS's /D= override; fill the per-user default in .onInit.
InstallDir ""
BrandingText "桌面小伙伴 · cdpandas.com"
VIProductVersion "${VERSION}.0"
VIAddVersionKey /LANG=2052 "ProductName" "${APP_NAME}"
VIAddVersionKey /LANG=2052 "FileDescription" "${APP_NAME}安装程序"
VIAddVersionKey /LANG=2052 "FileVersion" "${VERSION}.0"
VIAddVersionKey /LANG=2052 "ProductVersion" "${VERSION}"
VIAddVersionKey /LANG=2052 "LegalCopyright" "cdpandas.com"
Icon "..\src\app.ico"
UninstallIcon "..\src\app.ico"
ShowInstDetails show
ShowUninstDetails show
Var NeedsRestart
Var RuntimeRelease
Var RuntimeResult
Var RuntimeMessage
Var RuntimeLog
Var PowerShellExe
Var RunningMutex
Var ExitEvent
Var ExitWait

!define MUI_ICON "..\src\app.ico"
!define MUI_UNICON "..\src\app.ico"
!define MUI_ABORTWARNING
!define MUI_WELCOMEPAGE_TITLE "欢迎安装桌面小伙伴"
!define MUI_WELCOMEPAGE_TEXT "小布、阿金、糯米和洛洛，陪你待在桌面上。$\r$\n$\r$\n向导将安装程序并创建桌面和开始菜单快捷方式。$\r$\n$\r$\n支持 Windows 10 / 11。若缺少所需运行环境，向导会从微软下载并安装；这一步可能需要网络和管理员批准。"
!insertmacro MUI_PAGE_WELCOME
!define MUI_DIRECTORYPAGE_TEXT_TOP "选择程序的安装位置。覆盖安装会保留原有宠物选择与设置。"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_TITLE "桌面小伙伴已安装"
!define MUI_FINISHPAGE_TEXT "你可以从桌面或开始菜单打开桌面小伙伴。$\r$\n$\r$\n关闭控制窗口后，宠物仍会陪在桌面；完全退出请使用托盘菜单。"
!define MUI_FINISHPAGE_RUN
!define MUI_FINISHPAGE_RUN_TEXT "现在启动桌面小伙伴"
!define MUI_FINISHPAGE_RUN_FUNCTION LaunchDeskPets
!define MUI_FINISHPAGE_NOREBOOTSUPPORT
!define MUI_PAGE_CUSTOMFUNCTION_SHOW FinishShow
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH
!insertmacro MUI_LANGUAGE "SimpChinese"

!macro SetUserRegistryView
  ${If} ${RunningX64}
    SetRegView 64
  ${Else}
    SetRegView 32
  ${EndIf}
!macroend

Function .onInit
  SetShellVarContext current
  StrCpy $NeedsRestart 0
  ${IfNot} ${AtLeastWin10}
    MessageBox MB_OK|MB_ICONSTOP "桌面小伙伴需要 Windows 10 或更新版本。"
    SetErrorLevel 5100
    Abort
  ${EndIf}
  !insertmacro SetUserRegistryView
  ; NSIS removes /D= from $CMDLINE and has already put its value in $INSTDIR.
  ${If} $INSTDIR == ""
    ReadRegStr $INSTDIR HKCU "${UNINSTALL_KEY}" "InstallLocation"
    ${If} $INSTDIR == ""
      StrCpy $INSTDIR "$LOCALAPPDATA\Programs\DeskPets"
    ${EndIf}
  ${EndIf}
  StrCpy $PowerShellExe "$SYSDIR\WindowsPowerShell\v1.0\powershell.exe"
  ${If} ${RunningX64}
    StrCpy $PowerShellExe "$WINDIR\Sysnative\WindowsPowerShell\v1.0\powershell.exe"
  ${EndIf}
FunctionEnd

Function .onVerifyInstDir
  ${GetRoot} "$INSTDIR" $0
  ${If} $INSTDIR == "$0\"
  ${OrIf} $INSTDIR == "$0"
  ${OrIf} $INSTDIR == "$PROFILE"
  ${OrIf} $INSTDIR == "$DESKTOP"
  ${OrIf} $INSTDIR == "$WINDIR"
  ${OrIf} $INSTDIR == "$SYSDIR"
  ${OrIf} $INSTDIR == "$LOCALAPPDATA"
  ${OrIf} $INSTDIR == "$PROGRAMFILES"
    Abort
  ${EndIf}
  StrLen $0 "$WINDIR\"
  StrCpy $1 $INSTDIR $0
  ${If} $1 == "$WINDIR\"
    Abort
  ${EndIf}
  StrLen $0 "$SYSDIR\"
  StrCpy $1 $INSTDIR $0
  ${If} $1 == "$SYSDIR\"
    Abort
  ${EndIf}
  IfFileExists "$INSTDIR\DeskPets.exe" 0 directoryOkay
  ; csc's neutral-language ProductName resource identifies existing official versions.
  System::Call 'version::GetFileVersionInfoSizeW(w "$INSTDIR\DeskPets.exe", *i .r0) i.r1'
  ${If} $1 == 0
    Goto directoryReject
  ${EndIf}
  System::Alloc $1
  Pop $2
  System::Call 'version::GetFileVersionInfoW(w "$INSTDIR\DeskPets.exe", i 0, i r1, p r2) i.r0'
  StrCpy $5 ""
  ${If} $0 != 0
    System::Call 'version::VerQueryValueW(p r2, w "\StringFileInfo\000004b0\ProductName", *p .r3, *i .r4) i.r0'
    ${If} $0 != 0
      System::Call '*$3(&w${NSIS_MAX_STRLEN} .r5)'
    ${EndIf}
  ${EndIf}
  System::Free $2
  ${If} $5 != "${APP_NAME}"
    Goto directoryReject
  ${EndIf}
  IfFileExists "$INSTDIR\pets\nuomi\pet.json" directoryOkay directoryReject
  directoryReject:
    Abort
  directoryOkay:
FunctionEnd

Function ReadFrameworkRelease
  StrCpy $RuntimeRelease 0
  SetRegView 32
  StrCpy $0 0
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  ${If} $0 > $RuntimeRelease
    StrCpy $RuntimeRelease $0
  ${EndIf}
  ${If} ${RunningX64}
    SetRegView 64
    StrCpy $0 0
    ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
    ${If} $0 > $RuntimeRelease
      StrCpy $RuntimeRelease $0
    ${EndIf}
  ${EndIf}
  !insertmacro SetUserRegistryView
FunctionEnd

; Presence check only: a present runtime may still need Windows system repair.
Function EnsureFramework
  Call ReadFrameworkRelease
  ${If} $RuntimeRelease >= 528040
    DetailPrint "已检测到 .NET Framework 4.8 或更新版本。"
    Return
  ${EndIf}
  IfSilent runtimeSilent runtimeAsk
  runtimeAsk:
    MessageBox MB_OKCANCEL|MB_ICONINFORMATION "需要先安装微软 .NET Framework 4.8 运行环境（约116MB）。$\r$\n$\r$\n点击“确定”后向导会下载、验证微软文件并安装；Windows可能请求管理员批准。" IDOK runtimeProceed
    SetErrorLevel 1602
    Abort "你取消了运行环境安装，桌面小伙伴尚未安装。"
  runtimeSilent:
    ; Silent setup never opens an unexpected UAC prompt or installs system software.
    SetErrorLevel 5100
    Abort "缺少.NET Framework 4.8；请不带/S参数运行安装向导。"
  runtimeProceed:
    InitPluginsDir
    SetOutPath "$PLUGINSDIR"
    File /oname=install-runtime.ps1 "install-runtime.ps1"
    StrCpy $RuntimeResult "$PLUGINSDIR\runtime-result.ini"
    DetailPrint "准备微软运行环境；下载和安装可能需要几分钟。"
    nsExec::ExecToLog '"$PowerShellExe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$PLUGINSDIR\install-runtime.ps1" -ResultPath "$RuntimeResult" -DownloadDirectory "$PLUGINSDIR"'
    Pop $0
    ReadINIStr $RuntimeMessage "$RuntimeResult" "runtime" "message"
    ReadINIStr $RuntimeLog "$RuntimeResult" "runtime" "log"
    ${If} $0 == 3010
      StrCpy $NeedsRestart 1
      SetRebootFlag true
      DetailPrint "$RuntimeMessage"
      Return
    ${ElseIf} $0 == 0
      Call ReadFrameworkRelease
      ${If} $RuntimeRelease >= 528040
        DetailPrint "微软运行环境已安装。"
        Return
      ${EndIf}
    ${EndIf}
    ${If} $RuntimeMessage == ""
      StrCpy $RuntimeMessage "无法运行环境准备程序。请检查Windows系统组件后重试（退出码：$0）。"
    ${EndIf}
    MessageBox MB_OK|MB_ICONSTOP "$RuntimeMessage$\r$\n$\r$\n日志（若已生成）：$RuntimeLog"
    SetErrorLevel 1
    Abort "运行环境未准备完成；没有安装桌面小伙伴。"
FunctionEnd

; Signal normal Exit, wait at most 20 seconds. No taskkill/TerminateProcess path.
!macro DefineStopFunction PREFIX
Function ${PREFIX}StopDeskPets
  System::Call 'kernel32::OpenMutexW(i 0x00100001, i 0, w "DeskPets_SingleInstance") p.r0 ?e'
  Pop $1
  StrCpy $RunningMutex $0
  ${If} $RunningMutex == 0
    ${If} $1 == 2
      Return
    ${EndIf}
    MessageBox MB_OK|MB_ICONEXCLAMATION "无法确认正在运行的桌面小伙伴状态。请正常退出桌宠后重试。"
    SetErrorLevel 2
    Abort
  ${EndIf}
  System::Call 'kernel32::OpenEventW(i 0x0002, i 0, w "DeskPets_Exit") p.r0'
  StrCpy $ExitEvent $0
  ${If} $ExitEvent == 0
    System::Call 'kernel32::CloseHandle(p $RunningMutex)'
    MessageBox MB_OK|MB_ICONEXCLAMATION "无法通知正在运行的桌面小伙伴退出。请从右下角托盘正常退出，再重新运行向导。"
    SetErrorLevel 2
    Abort
  ${EndIf}
  DetailPrint "正在等待桌面小伙伴正常退出（最多20秒）……"
  System::Call 'kernel32::SetEvent(p $ExitEvent)'
  System::Call 'kernel32::CloseHandle(p $ExitEvent)'
  System::Call 'kernel32::WaitForSingleObject(p $RunningMutex, i 20000) i.r0'
  StrCpy $ExitWait $0
  ${If} $ExitWait == 0
  ${OrIf} $ExitWait == 128
    System::Call 'kernel32::ReleaseMutex(p $RunningMutex)'
    System::Call 'kernel32::CloseHandle(p $RunningMutex)'
    Return
  ${EndIf}
  System::Call 'kernel32::CloseHandle(p $RunningMutex)'
  MessageBox MB_OK|MB_ICONEXCLAMATION "桌面小伙伴在20秒内没有退出，向导已停止。请从托盘正常退出后重试；你的设置会保留。"
  SetErrorLevel 2
  Abort
FunctionEnd
!macroend
!insertmacro DefineStopFunction ""
!insertmacro DefineStopFunction "un."

Section "桌面小伙伴" MainSection
  ; Apply the same path boundary in silent installations as in the directory page.
  Call .onVerifyInstDir
  Call EnsureFramework
  Call StopDeskPets
  SetOverwrite on
  ClearErrors
  !include "${PAYLOAD_INCLUDE}\install-files.nsh"
  IfErrors installFailed
  SetOutPath "$INSTDIR"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateShortCut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\DeskPets.exe" "" "$INSTDIR\DeskPets.exe" 0
  CreateShortCut "$SMPROGRAMS\${APP_NAME}.lnk" "$INSTDIR\DeskPets.exe" "" "$INSTDIR\DeskPets.exe" 0
  IfErrors installFailed
  !insertmacro SetUserRegistryView
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "cdpandas.com"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\DeskPets.exe"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegStr HKCU "${UNINSTALL_KEY}" "QuietUninstallString" '$\"$INSTDIR\Uninstall.exe$\" /S'
  WriteRegStr HKCU "${UNINSTALL_KEY}" "URLInfoAbout" "https://cdpandas.com/utilities/desk-pets/"
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "EstimatedSize" ${ESTIMATED_SIZE_KB}
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
  IfErrors installFailed
  ${If} $NeedsRestart == 1
    SetErrorLevel 3010
  ${Else}
    SetErrorLevel 0
  ${EndIf}
  Goto installDone
  installFailed:
    MessageBox MB_OK|MB_ICONSTOP "安装未完成。请确认目标文件夹可以写入、桌面小伙伴已正常退出，再运行安装向导。"
    SetErrorLevel 1
    Abort
  installDone:
SectionEnd

Function FinishShow
  ${If} $NeedsRestart == 1
    ShowWindow $mui.FinishPage.Run ${SW_HIDE}
    SendMessage $mui.FinishPage.Run ${BM_SETCHECK} ${BST_UNCHECKED} 0
    SendMessage $mui.FinishPage.Text ${WM_SETTEXT} 0 "STR:程序已安装，微软运行环境需要重启电脑才能使用。$\r$\n$\r$\n请先保存工作并自行重启，随后从桌面快捷方式打开桌面小伙伴。本次不会启动，也不会自动重启。"
  ${EndIf}
FunctionEnd
Function LaunchDeskPets
  ${If} $NeedsRestart != 1
    SetOutPath "$INSTDIR"
    Exec '"$INSTDIR\DeskPets.exe"'
  ${EndIf}
FunctionEnd
Function un.onInit
  SetShellVarContext current
  !insertmacro SetUserRegistryView
FunctionEnd
Section "Uninstall"
  Call un.StopDeskPets
  ReadRegStr $0 HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "DeskPets"
  ${If} $0 == '$\"$INSTDIR\DeskPets.exe$\" --background'
    DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "DeskPets"
  ${EndIf}
  ReadRegStr $0 HKCU "${UNINSTALL_KEY}" "InstallLocation"
  ${If} $0 == $INSTDIR
    Delete "$DESKTOP\${APP_NAME}.lnk"
    Delete "$SMPROGRAMS\${APP_NAME}.lnk"
    DeleteRegKey HKCU "${UNINSTALL_KEY}"
  ${EndIf}
  ; Exact build files only; nonrecursive RMDir removes empty folders only.
  !include "${PAYLOAD_INCLUDE}\uninstall-files.nsh"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  ; %LOCALAPPDATA%\DeskPets settings and diagnostic logs are preserved.
SectionEnd
