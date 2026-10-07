import { join } from 'node:path'
import { installerText } from '../installer/i18n.mjs'

const escapeNsis = value => value.replaceAll('$', '$$').replaceAll('"', '$\\"').replaceAll('\n', '$\\r$\\n')
const languages = [
  ['en', 'English', 'ENGLISH'], ['zh-CN', 'SimpChinese', 'SIMPCHINESE'],
  ['zh-Hant', 'TradChinese', 'TRADCHINESE'], ['ja', 'Japanese', 'JAPANESE'],
  ['de', 'German', 'GERMAN'], ['fr', 'French', 'FRENCH'], ['es', 'Spanish', 'SPANISH'],
  ['it', 'Italian', 'ITALIAN'], ['pt-BR', 'PortugueseBR', 'PORTUGUESEBR'],
  ['pt', 'Portuguese', 'PORTUGUESE'], ['ru', 'Russian', 'RUSSIAN'],
  ['uk', 'Ukrainian', 'UKRAINIAN'], ['pl', 'Polish', 'POLISH'], ['cs', 'Czech', 'CZECH'],
  ['sk', 'Slovak', 'SLOVAK'], ['hu', 'Hungarian', 'HUNGARIAN'], ['ro', 'Romanian', 'ROMANIAN'],
  ['bg', 'Bulgarian', 'BULGARIAN'], ['tr', 'Turkish', 'TURKISH'], ['el', 'Greek', 'GREEK'],
  ['ar', 'Arabic', 'ARABIC'], ['lv', 'Latvian', 'LATVIAN'], ['nl-NL', 'Dutch', 'DUTCH'],
  ['vi', 'Vietnamese', 'VIETNAMESE'], ['uz-Latn-UZ', 'Uzbek', 'UZBEK']
]

export function compatibilityScript(payload, output, resources) {
  const selection = ['language', 'deviceMode', 'windowsOptimization', 'networkAcceleration', 'automation', 'macro', 'keyboard']
  const featureKeys = selection.slice(2)
  const labels = ['deviceTitle', 'deviceSubtitle', 'automatic', 'basic', 'featuresTitle', 'featuresSubtitle', ...featureKeys]
  return `Unicode true
Name "Universal Device Toolkit (Electron Compatibility)"
OutFile "${escapeNsis(output)}"
Icon "${escapeNsis(join(resources, 'icon.ico'))}"
InstallDir "$PROGRAMFILES64\\Universal Device Toolkit"
InstallDirRegKey HKLM "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\UniversalDeviceToolkit" "InstallLocation"
RequestExecutionLevel admin
ShowInstDetails show
SetCompressor /SOLID lzma
!include "MUI2.nsh"
!include "nsDialogs.nsh"
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!define MUI_ABORTWARNING
Var udtLanguage
Var udtDeviceMode
Var udtAutoControl
Var udtBasicControl
${featureKeys.map(key => `Var udt${key}\nVar udt${key}Control`).join('\n')}
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
Page custom UdtDevicePage UdtDeviceLeave
Page custom UdtFeaturesPage UdtFeaturesLeave
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\\UniversalDeviceToolkit.exe"
!insertmacro MUI_PAGE_FINISH
${languages.map(([, name]) => `!insertmacro MUI_LANGUAGE "${name}"`).join('\n')}
${labels.flatMap(key => languages.map(([locale, , constant]) => `LangString udtText${key} \${LANG_${constant}} "${escapeNsis(installerText(locale, key))}"`)).join('\n')}

Function .onInit
  SetRegView 64
  System::Call 'kernel32::GetCommandLineW()w.r1'
  ClearErrors
  \${GetOptions} $1 "/D=" $2
  IfErrors existingDestination destinationReady
existingDestination:
  ReadRegStr $0 HKLM "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\UniversalDeviceToolkit" "InstallLocation"
  StrCmp $0 "" +2
  StrCpy $INSTDIR $0
destinationReady:
  !insertmacro MUI_LANGDLL_DISPLAY
  StrCpy $udtLanguage "en"
${languages.map(([locale, , constant]) => `  StrCmp $LANGUAGE \${LANG_${constant}} 0 +2\n  StrCpy $udtLanguage "${locale}"`).join('\n')}
  StrCpy $udtDeviceMode "auto"
${featureKeys.map(key => `  StrCpy $udt${key} "1"`).join('\n')}
  IfFileExists "$INSTDIR\\installer-selection.ini" 0 selectionDone
  ReadINIStr $0 "$INSTDIR\\installer-selection.ini" "installation" "deviceMode"
  StrCmp $0 "basic" 0 +2
  StrCpy $udtDeviceMode "basic"
${featureKeys.map(key => `  ReadINIStr $0 "$INSTDIR\\installer-selection.ini" "installation" "${key}"\n  StrCmp $0 "0" 0 +2\n  StrCpy $udt${key} "0"`).join('\n')}
selectionDone:
FunctionEnd

Function UdtDevicePage
  !insertmacro MUI_HEADER_TEXT "$(udtTextdeviceTitle)" "$(udtTextdeviceSubtitle)"
  nsDialogs::Create 1018
  Pop $0
  StrCmp $0 error 0 +2
  Abort
  \${NSD_CreateRadioButton} 0 20u 100% 24u "$(udtTextautomatic)"
  Pop $udtAutoControl
  \${NSD_CreateRadioButton} 0 54u 100% 24u "$(udtTextbasic)"
  Pop $udtBasicControl
  \${If} $udtDeviceMode == "basic"
    \${NSD_Check} $udtBasicControl
  \${Else}
    \${NSD_Check} $udtAutoControl
  \${EndIf}
  nsDialogs::Show
FunctionEnd
Function UdtDeviceLeave
  \${NSD_GetState} $udtBasicControl $0
  StrCpy $udtDeviceMode "auto"
  StrCmp $0 \${BST_CHECKED} 0 +2
  StrCpy $udtDeviceMode "basic"
FunctionEnd
Function UdtFeaturesPage
  !insertmacro MUI_HEADER_TEXT "$(udtTextfeaturesTitle)" "$(udtTextfeaturesSubtitle)"
  nsDialogs::Create 1018
  Pop $0
  StrCmp $0 error 0 +2
  Abort
${featureKeys.map((key, index) => `  \${NSD_CreateCheckbox} 0 ${index * 26}u 100% 22u "$(udtText${key})"\n  Pop $udt${key}Control\n  \${If} $udt${key} == "1"\n    \${NSD_Check} $udt${key}Control\n  \${EndIf}`).join('\n')}
  nsDialogs::Show
FunctionEnd
Function UdtFeaturesLeave
${featureKeys.map(key => `  \${NSD_GetState} $udt${key}Control $udt${key}`).join('\n')}
  \${If} $udtwindowsOptimization != "1"
    StrCpy $udtnetworkAcceleration "0"
  \${EndIf}
FunctionEnd
Section
  InitPluginsDir
  SetOutPath "$PLUGINSDIR\\payload"
  File /r "${escapeNsis(payload)}\\*"
${selection.map(key => `  WriteINIStr "$PLUGINSDIR\\selection.ini" "installation" "${key}" "$udt${key === 'language' ? 'Language' : key === 'deviceMode' ? 'DeviceMode' : key}"`).join('\n')}
  nsExec::ExecToLog '"$PLUGINSDIR\\payload\\resources\\host\\UniversalDeviceToolkit.InstallHelper.exe" --install-payload --source "$PLUGINSDIR\\payload" --destination "$INSTDIR" --selection "$PLUGINSDIR\\selection.ini"'
  Pop $0
  StrCmp $0 "error" invocationFailed
  StrCmp $0 "timeout" invocationFailed
  StrCmp $0 0 complete
  Goto installFailed
invocationFailed:
  StrCpy $0 1
installFailed:
  SetErrorLevel $0
  IfSilent +2
  MessageBox MB_ICONSTOP "$(MUI_TEXT_ABORT_TITLE) ($0)"
  Abort
complete:
  SetErrorLevel 0
SectionEnd
`
}
