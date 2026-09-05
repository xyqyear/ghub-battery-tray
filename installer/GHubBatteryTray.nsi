Unicode True

!include "MUI2.nsh"
!include "LogicLib.nsh"
!include "WinVer.nsh"
!include "x64.nsh"

!ifndef APP_VERSION
  !define APP_VERSION "0.2.0"
!endif
!ifndef NUMERIC_VERSION
  !define NUMERIC_VERSION "0.2.0.0"
!endif
!ifndef SOURCE_EXE
  !define SOURCE_EXE "..\artifacts\publish\win-x64\GHubBatteryTray.exe"
!endif
!ifndef TARGET_ARCH
  !define TARGET_ARCH "x64"
!endif
!ifndef OUTPUT_FILE
  !define OUTPUT_FILE "..\artifacts\installer\GHubBatteryTray-${APP_VERSION}-win-x64-setup.exe"
!endif

!define APP_NAME "G HUB Battery Tray"
!define APP_EXE "GHubBatteryTray.exe"
!define APP_REGISTRY_KEY "Software\GHubBatteryTray"
!define UNINSTALL_REGISTRY_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\GHubBatteryTray"

Name "${APP_NAME} ${APP_VERSION}"
OutFile "${OUTPUT_FILE}"
InstallDir "$LOCALAPPDATA\Programs\GHubBatteryTray"
InstallDirRegKey HKCU "${APP_REGISTRY_KEY}" "InstallDir"
RequestExecutionLevel user
SetCompressor /SOLID lzma
ManifestSupportedOS Win10
BrandingText "G HUB Battery Tray"
ShowInstDetails show
ShowUninstDetails show

VIProductVersion "${NUMERIC_VERSION}"
VIAddVersionKey /LANG=1033 "ProductName" "${APP_NAME}"
VIAddVersionKey /LANG=1033 "ProductVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "FileDescription" "${APP_NAME} installer"
VIAddVersionKey /LANG=1033 "FileVersion" "${APP_VERSION}"
VIAddVersionKey /LANG=1033 "CompanyName" "xyqyear"
VIAddVersionKey /LANG=1033 "LegalCopyright" "Copyright (c) xyqyear"

!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Launch ${APP_NAME}"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "English"

Function CheckArchitecture
  !if "${TARGET_ARCH}" == "x64"
    ${IfNot} ${IsNativeAMD64}
      MessageBox MB_OK|MB_ICONSTOP "This installer requires x64 Windows."
      Abort
    ${EndIf}
  !else if "${TARGET_ARCH}" == "arm64"
    ${IfNot} ${IsNativeARM64}
      MessageBox MB_OK|MB_ICONSTOP "This installer requires ARM64 Windows."
      Abort
    ${EndIf}
  !else if "${TARGET_ARCH}" == "x86"
    ${IfNot} ${IsNativeIA32}
      MessageBox MB_OK|MB_ICONSTOP "This installer requires x86 Windows."
      Abort
    ${EndIf}
  !else
    !error "Unsupported TARGET_ARCH: ${TARGET_ARCH}"
  !endif
FunctionEnd

Function CheckApplicationStopped
  retry:
    System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "Local\GHubBatteryTray.Instance") p.r0'
    ${If} $0 != 0
      System::Call 'kernel32::CloseHandle(p r0)'
      MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION "Close ${APP_NAME}, then select Retry." /SD IDCANCEL IDRETRY retry
      Abort
    ${EndIf}
FunctionEnd

Function .onInit
  ${IfNot} ${AtLeastWin10}
    MessageBox MB_OK|MB_ICONSTOP "${APP_NAME} requires Windows 10 or later."
    Abort
  ${EndIf}
  Call CheckArchitecture
  Call CheckApplicationStopped
FunctionEnd

Section "Install"
  SetShellVarContext current
  SetOutPath "$INSTDIR"
  File "/oname=${APP_EXE}" "${SOURCE_EXE}"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortcut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}"

  WriteRegStr HKCU "${APP_REGISTRY_KEY}" "InstallDir" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTALL_REGISTRY_KEY}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKCU "${UNINSTALL_REGISTRY_KEY}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "${UNINSTALL_REGISTRY_KEY}" "DisplayIcon" "$INSTDIR\${APP_EXE}"
  WriteRegStr HKCU "${UNINSTALL_REGISTRY_KEY}" "Publisher" "xyqyear"
  WriteRegStr HKCU "${UNINSTALL_REGISTRY_KEY}" "URLInfoAbout" "https://github.com/xyqyear/ghub-battery-tray"
  WriteRegStr HKCU "${UNINSTALL_REGISTRY_KEY}" "UninstallString" "$\"$INSTDIR\Uninstall.exe$\""
  WriteRegStr HKCU "${UNINSTALL_REGISTRY_KEY}" "QuietUninstallString" "$\"$INSTDIR\Uninstall.exe$\" /S"
  WriteRegDWORD HKCU "${UNINSTALL_REGISTRY_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTALL_REGISTRY_KEY}" "NoRepair" 1
SectionEnd

Function un.onInit
  Call un.CheckApplicationStopped
FunctionEnd

Function un.CheckApplicationStopped
  retry:
    System::Call 'kernel32::OpenMutexW(i 0x100000, i 0, w "Local\GHubBatteryTray.Instance") p.r0'
    ${If} $0 != 0
      System::Call 'kernel32::CloseHandle(p r0)'
      MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION "Close ${APP_NAME}, then select Retry." /SD IDCANCEL IDRETRY retry
      Abort
    ${EndIf}
FunctionEnd

Section "Uninstall"
  SetShellVarContext current
  Delete "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk"
  RMDir "$SMPROGRAMS\${APP_NAME}"

  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "GHubBatteryTray"
  DeleteRegKey HKCU "${UNINSTALL_REGISTRY_KEY}"
  DeleteRegValue HKCU "${APP_REGISTRY_KEY}" "InstallDir"
  DeleteRegKey /ifempty HKCU "${APP_REGISTRY_KEY}"

  Delete "$INSTDIR\${APP_EXE}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
SectionEnd
