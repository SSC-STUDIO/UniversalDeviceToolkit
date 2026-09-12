; Universal Device Toolkit branded NSIS pages.
;
; The installer remains electron-builder + NSIS so it keeps the established
; per-machine install, shortcut, upgrade, signing, and uninstall behavior. The
; assisted wizard pages are deliberately drawn as a dark product surface so
; the setup experience matches the Electron product instead of exposing the
; default white Modern UI pages.

!include LogicLib.nsh
!include WinMessages.nsh
!include nsDialogs.nsh

!define UDT_SELECTION_FILE "$INSTDIR\installer-selection.ini"
!define UDT_BG "0x171717"
!define UDT_PANEL "0x0D0D0D"
!define UDT_CARD "0x242424"
!define UDT_BORDER "0x3A3A3A"
!define UDT_TEXT "0xF5F5F5"
!define UDT_MUTED "0xB6B6B6"
!define UDT_RED "0xF52632"

!ifndef BUILD_UNINSTALLER
Var udtSelectionInitialized
Var udtLanguage
Var udtDeviceMode
Var udtFeatOpt
Var udtFeatNet
Var udtFeatAuto
Var udtFeatMacro
Var udtFeatKbd
Var udtLanguageCombo
Var udtAutoRadio
Var udtBasicRadio
Var udtFeatOptCheck
Var udtFeatNetCheck
Var udtFeatAutoCheck
Var udtFeatMacroCheck
Var udtFeatKbdCheck
Var udtPathEdit
Var udtBrowseButton
Var udtRequiredLabel
Var udtAvailableLabel
Var udtFontTitle
Var udtFontBody
Var udtFontSmall

Function UdtLoadSelection
  ${If} $udtSelectionInitialized == "1"
    Return
  ${EndIf}

  ; Defaults also cover silent installs and upgrades made before this wizard
  ; existed. An existing valid selection is retained on an upgrade.
  StrCpy $udtLanguage "zh-CN"
  StrCpy $udtDeviceMode "auto"
  StrCpy $udtFeatOpt "1"
  StrCpy $udtFeatNet "1"
  StrCpy $udtFeatAuto "1"
  StrCpy $udtFeatMacro "1"
  StrCpy $udtFeatKbd "1"
  IfFileExists "${UDT_SELECTION_FILE}" 0 udtSelectionValidate
    ReadINIStr $udtLanguage "${UDT_SELECTION_FILE}" "installation" "language"
    ReadINIStr $udtDeviceMode "${UDT_SELECTION_FILE}" "installation" "deviceMode"
    ReadINIStr $0 "${UDT_SELECTION_FILE}" "installation" "windowsOptimization"
    StrCmp $0 "0" 0 +2
      StrCpy $udtFeatOpt "0"
    ReadINIStr $0 "${UDT_SELECTION_FILE}" "installation" "networkAcceleration"
    StrCmp $0 "0" 0 +2
      StrCpy $udtFeatNet "0"
    ReadINIStr $0 "${UDT_SELECTION_FILE}" "installation" "automation"
    StrCmp $0 "0" 0 +2
      StrCpy $udtFeatAuto "0"
    ReadINIStr $0 "${UDT_SELECTION_FILE}" "installation" "macro"
    StrCmp $0 "0" 0 +2
      StrCpy $udtFeatMacro "0"
    ReadINIStr $0 "${UDT_SELECTION_FILE}" "installation" "keyboard"
    StrCmp $0 "0" 0 +2
      StrCpy $udtFeatKbd "0"

  udtSelectionValidate:
  StrCmp $udtLanguage "en" udtLanguageValid
  StrCmp $udtLanguage "zh-CN" udtLanguageValid
  StrCmp $udtLanguage "zh-Hant" udtLanguageValid
  StrCmp $udtLanguage "ja" udtLanguageValid
  StrCmp $udtLanguage "de" udtLanguageValid
  StrCmp $udtLanguage "fr" udtLanguageValid
  StrCmp $udtLanguage "es" udtLanguageValid
  StrCmp $udtLanguage "it" udtLanguageValid
  StrCmp $udtLanguage "pt-BR" udtLanguageValid
  StrCmp $udtLanguage "pt" udtLanguageValid
  StrCmp $udtLanguage "ru" udtLanguageValid
  StrCmp $udtLanguage "uk" udtLanguageValid
  StrCmp $udtLanguage "pl" udtLanguageValid
  StrCmp $udtLanguage "cs" udtLanguageValid
  StrCmp $udtLanguage "sk" udtLanguageValid
  StrCmp $udtLanguage "hu" udtLanguageValid
  StrCmp $udtLanguage "ro" udtLanguageValid
  StrCmp $udtLanguage "bg" udtLanguageValid
  StrCmp $udtLanguage "tr" udtLanguageValid
  StrCmp $udtLanguage "el" udtLanguageValid
  StrCmp $udtLanguage "ar" udtLanguageValid
  StrCmp $udtLanguage "lv" udtLanguageValid
  StrCmp $udtLanguage "nl-NL" udtLanguageValid
  StrCmp $udtLanguage "vi" udtLanguageValid
  StrCmp $udtLanguage "uz-Latn-UZ" udtLanguageValid
  StrCpy $udtLanguage "zh-CN"
  udtLanguageValid:
  StrCmp $udtDeviceMode "basic" udtDeviceModeValid
  StrCpy $udtDeviceMode "auto"
  udtDeviceModeValid:
  ${If} $udtFeatOpt != "1"
    StrCpy $udtFeatNet "0"
  ${EndIf}
  StrCpy $udtSelectionInitialized "1"
FunctionEnd

Function UdtHideNativeChrome
  ; The custom page owns the content header. Hide only the Modern UI header
  ; labels/branding; native window controls and the footer remain accessible.
  GetDlgItem $0 $HWNDPARENT 1034
  ShowWindow $0 ${SW_HIDE}
  GetDlgItem $0 $HWNDPARENT 1035
  ShowWindow $0 ${SW_HIDE}
  GetDlgItem $0 $HWNDPARENT 1036
  ShowWindow $0 ${SW_HIDE}
  GetDlgItem $0 $HWNDPARENT 1037
  ShowWindow $0 ${SW_HIDE}
  GetDlgItem $0 $HWNDPARENT 1038
  ShowWindow $0 ${SW_HIDE}
  GetDlgItem $0 $HWNDPARENT 1039
  ShowWindow $0 ${SW_HIDE}
  GetDlgItem $0 $HWNDPARENT 1028
  ShowWindow $0 ${SW_HIDE}
  GetDlgItem $0 $HWNDPARENT 1256
  ShowWindow $0 ${SW_HIDE}

  ; Keep the wizard background from flashing white around the custom dialog.
  SetCtlColors $HWNDPARENT "" "${UDT_BG}"
  GetDlgItem $0 $HWNDPARENT 1
  SetCtlColors $0 "${UDT_TEXT}" "${UDT_RED}"
  GetDlgItem $0 $HWNDPARENT 2
  SetCtlColors $0 "${UDT_TEXT}" "${UDT_CARD}"
  GetDlgItem $0 $HWNDPARENT 3
  SetCtlColors $0 "${UDT_TEXT}" "${UDT_CARD}"
FunctionEnd

Function UdtCreateFonts
  CreateFont $udtFontTitle "Segoe UI" 18 700
  CreateFont $udtFontBody "Segoe UI" 10 400
  CreateFont $udtFontSmall "Segoe UI" 8 400
FunctionEnd

Function UdtWelcomePage
  Call UdtLoadSelection
  Call UdtHideNativeChrome
  Call UdtCreateFonts

  nsDialogs::Create 1018
  Pop $0
  ${If} $0 == error
    Abort
  ${EndIf}
  ; nsDialogs normally reserves the former Modern UI header area. Stretch the
  ; custom surface back to the whole client so the brand panel starts directly
  ; below the title bar like the reference design.
  System::Call 'user32::SetWindowPos(i $0, i 0, i 0, i 0, i 750, i 500, i 0)'
  SetCtlColors $0 "" "${UDT_BG}"

  ; Left brand panel, matching the reference composition.
  ${NSD_CreateLabel} 0u 0u 145u 250u ""
  Pop $1
  SetCtlColors $1 "" "${UDT_PANEL}"
  ${NSD_CreateLabel} 18u 28u 110u 28u "U"
  Pop $1
  SetCtlColors $1 "${UDT_TEXT}" "transparent"
  SendMessage $1 ${WM_SETFONT} $udtFontTitle 0
  ${NSD_CreateLabel} 16u 75u 120u 25u "Universal Device"
  Pop $1
  SetCtlColors $1 "${UDT_TEXT}" "transparent"
  SendMessage $1 ${WM_SETFONT} $udtFontBody 0
  ${NSD_CreateLabel} 16u 97u 120u 25u "Toolkit"
  Pop $1
  SetCtlColors $1 "${UDT_TEXT}" "transparent"
  SendMessage $1 ${WM_SETFONT} $udtFontBody 0
  ${NSD_CreateLabel} 16u 130u 120u 18u "6.1.1"
  Pop $1
  SetCtlColors $1 "${UDT_MUTED}" "transparent"
  SendMessage $1 ${WM_SETFONT} $udtFontSmall 0
  ${NSD_CreateLabel} 16u 202u 118u 22u "WINDOWS x64"
  Pop $1
  SetCtlColors $1 "${UDT_MUTED}" "transparent"
  SendMessage $1 ${WM_SETFONT} $udtFontSmall 0
  ${NSD_CreateLabel} 16u 225u 118u 20u "●  LOCAL INSTALL"
  Pop $1
  SetCtlColors $1 "0x46D369" "transparent"
  SendMessage $1 ${WM_SETFONT} $udtFontSmall 0

  ; Main setup card.
  ${NSD_CreateLabel} 155u 0u 185u 250u ""
  Pop $1
  SetCtlColors $1 "" "${UDT_BG}"
  ${NSD_CreateLabel} 170u 22u 155u 28u "准备安装"
  Pop $1
  SetCtlColors $1 "${UDT_TEXT}" "transparent"
  SendMessage $1 ${WM_SETFONT} $udtFontTitle 0
  ${NSD_CreateLabel} 170u 52u 155u 20u "选择安装位置，然后开始安装。"
  Pop $1
  SetCtlColors $1 "${UDT_MUTED}" "transparent"
  ${NSD_CreateLabel} 170u 83u 155u 18u "安装位置"
  Pop $1
  SetCtlColors $1 "${UDT_TEXT}" "transparent"
  SendMessage $1 ${WM_SETFONT} $udtFontBody 0

  ${NSD_CreateDirRequest} 170u 105u 130u 24u "$INSTDIR"
  Pop $udtPathEdit
  SetCtlColors $udtPathEdit "${UDT_TEXT}" "${UDT_CARD}"
  ${NSD_CreateBrowseButton} 304u 105u 35u 24u "浏览"
  Pop $udtBrowseButton
  SetCtlColors $udtBrowseButton "${UDT_TEXT}" "${UDT_CARD}"
  ${NSD_OnClick} $udtBrowseButton UdtBrowseClicked

  ${NSD_CreateLabel} 170u 145u 155u 58u ""
  Pop $1
  SetCtlColors $1 "" "${UDT_CARD}"
  ${NSD_CreateLabel} 180u 154u 62u 16u "需要空间"
  Pop $1
  SetCtlColors $1 "${UDT_MUTED}" "transparent"
  ${NSD_CreateLabel} 180u 172u 62u 22u "763 MB"
  Pop $udtRequiredLabel
  SetCtlColors $udtRequiredLabel "${UDT_TEXT}" "transparent"
  SendMessage $udtRequiredLabel ${WM_SETFONT} $udtFontBody 0
  ${NSD_CreateLabel} 252u 154u 70u 16u "可用空间"
  Pop $1
  SetCtlColors $1 "${UDT_MUTED}" "transparent"
  ${NSD_CreateLabel} 252u 172u 70u 22u "检测中..."
  Pop $udtAvailableLabel
  SetCtlColors $udtAvailableLabel "${UDT_TEXT}" "transparent"
  SendMessage $udtAvailableLabel ${WM_SETFONT} $udtFontBody 0

  nsDialogs::Show
FunctionEnd

Function UdtBrowseClicked
  Pop $0
  ${NSD_GetText} $udtPathEdit $1
  nsDialogs::SelectFolderDialog "选择安装位置" $1
  Pop $1
  ${If} $1 != error
    SendMessage $udtPathEdit ${WM_SETTEXT} 0 STR:$1
    StrCpy $INSTDIR $1
  ${EndIf}
FunctionEnd

Function UdtWelcomeLeave
  ${NSD_GetText} $udtPathEdit $INSTDIR
  ${If} $INSTDIR == ""
    StrCpy $INSTDIR "$PROGRAMFILES64\Universal Device Toolkit"
  ${EndIf}
FunctionEnd

Function UdtLanguagePage
  Call UdtLoadSelection
  Call UdtHideNativeChrome
  nsDialogs::Create 1018
  Pop $0
  ${If} $0 == error
    Abort
  ${EndIf}
  System::Call 'user32::SetWindowPos(i $0, i 0, i 0, i 0, i 750, i 500, i 0)'
  SetCtlColors $0 "" "${UDT_BG}"
  ${NSD_CreateLabel} 22u 22u 290u 28u "语言选择"
  Pop $1
  SetCtlColors $1 "${UDT_TEXT}" "transparent"
  SendMessage $1 ${WM_SETFONT} $udtFontTitle 0
  ${NSD_CreateLabel} 22u 55u 290u 20u "Select the application language / 选择应用语言"
  Pop $1
  SetCtlColors $1 "${UDT_MUTED}" "transparent"
  ${NSD_CreateLabel} 22u 92u 290u 18u "语言 / Language"
  Pop $1
  SetCtlColors $1 "${UDT_TEXT}" "transparent"
  ${NSD_CreateDropList} 22u 116u 290u 130u ""
  Pop $udtLanguageCombo
  SetCtlColors $udtLanguageCombo "${UDT_TEXT}" "${UDT_CARD}"
  ${NSD_CB_AddString} $udtLanguageCombo "English (en)"
  ${NSD_CB_AddString} $udtLanguageCombo "简体中文 (zh-CN)"
  ${NSD_CB_AddString} $udtLanguageCombo "繁體中文 (zh-Hant)"
  ${NSD_CB_AddString} $udtLanguageCombo "日本語 (ja)"
  ${NSD_CB_AddString} $udtLanguageCombo "Deutsch (de)"
  ${NSD_CB_AddString} $udtLanguageCombo "Français (fr)"
  ${NSD_CB_AddString} $udtLanguageCombo "Español (es)"
  ${NSD_CB_AddString} $udtLanguageCombo "Italiano (it)"
  ${NSD_CB_AddString} $udtLanguageCombo "Português (Brasil) (pt-BR)"
  ${NSD_CB_AddString} $udtLanguageCombo "Português (pt)"
  ${NSD_CB_AddString} $udtLanguageCombo "Русский (ru)"
  ${NSD_CB_AddString} $udtLanguageCombo "Українська (uk)"
  ${NSD_CB_AddString} $udtLanguageCombo "Polski (pl)"
  ${NSD_CB_AddString} $udtLanguageCombo "Čeština (cs)"
  ${NSD_CB_AddString} $udtLanguageCombo "Slovenčina (sk)"
  ${NSD_CB_AddString} $udtLanguageCombo "Magyar (hu)"
  ${NSD_CB_AddString} $udtLanguageCombo "Română (ro)"
  ${NSD_CB_AddString} $udtLanguageCombo "Български (bg)"
  ${NSD_CB_AddString} $udtLanguageCombo "Türkçe (tr)"
  ${NSD_CB_AddString} $udtLanguageCombo "Ελληνικά (el)"
  ${NSD_CB_AddString} $udtLanguageCombo "العربية (ar)"
  ${NSD_CB_AddString} $udtLanguageCombo "Latviešu (lv)"
  ${NSD_CB_AddString} $udtLanguageCombo "Nederlands (nl-NL)"
  ${NSD_CB_AddString} $udtLanguageCombo "Tiếng Việt (vi)"
  ${NSD_CB_AddString} $udtLanguageCombo "O'zbek (uz-Latn-UZ)"
  ${If} $udtLanguage == "en"
    ${NSD_CB_SelectString} $udtLanguageCombo "English (en)"
  ${ElseIf} $udtLanguage == "zh-Hant"
    ${NSD_CB_SelectString} $udtLanguageCombo "繁體中文 (zh-Hant)"
  ${ElseIf} $udtLanguage == "ja"
    ${NSD_CB_SelectString} $udtLanguageCombo "日本語 (ja)"
  ${ElseIf} $udtLanguage == "de"
    ${NSD_CB_SelectString} $udtLanguageCombo "Deutsch (de)"
  ${ElseIf} $udtLanguage == "fr"
    ${NSD_CB_SelectString} $udtLanguageCombo "Français (fr)"
  ${ElseIf} $udtLanguage == "es"
    ${NSD_CB_SelectString} $udtLanguageCombo "Español (es)"
  ${ElseIf} $udtLanguage == "it"
    ${NSD_CB_SelectString} $udtLanguageCombo "Italiano (it)"
  ${ElseIf} $udtLanguage == "pt-BR"
    ${NSD_CB_SelectString} $udtLanguageCombo "Português (Brasil) (pt-BR)"
  ${ElseIf} $udtLanguage == "pt"
    ${NSD_CB_SelectString} $udtLanguageCombo "Português (pt)"
  ${ElseIf} $udtLanguage == "ru"
    ${NSD_CB_SelectString} $udtLanguageCombo "Русский (ru)"
  ${ElseIf} $udtLanguage == "uk"
    ${NSD_CB_SelectString} $udtLanguageCombo "Українська (uk)"
  ${ElseIf} $udtLanguage == "pl"
    ${NSD_CB_SelectString} $udtLanguageCombo "Polski (pl)"
  ${ElseIf} $udtLanguage == "cs"
    ${NSD_CB_SelectString} $udtLanguageCombo "Čeština (cs)"
  ${ElseIf} $udtLanguage == "sk"
    ${NSD_CB_SelectString} $udtLanguageCombo "Slovenčina (sk)"
  ${ElseIf} $udtLanguage == "hu"
    ${NSD_CB_SelectString} $udtLanguageCombo "Magyar (hu)"
  ${ElseIf} $udtLanguage == "ro"
    ${NSD_CB_SelectString} $udtLanguageCombo "Română (ro)"
  ${ElseIf} $udtLanguage == "bg"
    ${NSD_CB_SelectString} $udtLanguageCombo "Български (bg)"
  ${ElseIf} $udtLanguage == "tr"
    ${NSD_CB_SelectString} $udtLanguageCombo "Türkçe (tr)"
  ${ElseIf} $udtLanguage == "el"
    ${NSD_CB_SelectString} $udtLanguageCombo "Ελληνικά (el)"
  ${ElseIf} $udtLanguage == "ar"
    ${NSD_CB_SelectString} $udtLanguageCombo "العربية (ar)"
  ${ElseIf} $udtLanguage == "lv"
    ${NSD_CB_SelectString} $udtLanguageCombo "Latviešu (lv)"
  ${ElseIf} $udtLanguage == "nl-NL"
    ${NSD_CB_SelectString} $udtLanguageCombo "Nederlands (nl-NL)"
  ${ElseIf} $udtLanguage == "vi"
    ${NSD_CB_SelectString} $udtLanguageCombo "Tiếng Việt (vi)"
  ${ElseIf} $udtLanguage == "uz-Latn-UZ"
    ${NSD_CB_SelectString} $udtLanguageCombo "O'zbek (uz-Latn-UZ)"
  ${Else}
    ${NSD_CB_SelectString} $udtLanguageCombo "简体中文 (zh-CN)"
  ${EndIf}
  nsDialogs::Show
FunctionEnd

Function UdtLanguageLeave
  ${NSD_GetText} $udtLanguageCombo $0
  ${If} $0 == "English (en)"
    StrCpy $udtLanguage "en"
  ${ElseIf} $0 == "繁體中文 (zh-Hant)"
    StrCpy $udtLanguage "zh-Hant"
  ${ElseIf} $0 == "日本語 (ja)"
    StrCpy $udtLanguage "ja"
  ${ElseIf} $0 == "Deutsch (de)"
    StrCpy $udtLanguage "de"
  ${ElseIf} $0 == "Français (fr)"
    StrCpy $udtLanguage "fr"
  ${ElseIf} $0 == "Español (es)"
    StrCpy $udtLanguage "es"
  ${ElseIf} $0 == "Italiano (it)"
    StrCpy $udtLanguage "it"
  ${ElseIf} $0 == "Português (Brasil) (pt-BR)"
    StrCpy $udtLanguage "pt-BR"
  ${ElseIf} $0 == "Português (pt)"
    StrCpy $udtLanguage "pt"
  ${ElseIf} $0 == "Русский (ru)"
    StrCpy $udtLanguage "ru"
  ${ElseIf} $0 == "Українська (uk)"
    StrCpy $udtLanguage "uk"
  ${ElseIf} $0 == "Polski (pl)"
    StrCpy $udtLanguage "pl"
  ${ElseIf} $0 == "Čeština (cs)"
    StrCpy $udtLanguage "cs"
  ${ElseIf} $0 == "Slovenčina (sk)"
    StrCpy $udtLanguage "sk"
  ${ElseIf} $0 == "Magyar (hu)"
    StrCpy $udtLanguage "hu"
  ${ElseIf} $0 == "Română (ro)"
    StrCpy $udtLanguage "ro"
  ${ElseIf} $0 == "Български (bg)"
    StrCpy $udtLanguage "bg"
  ${ElseIf} $0 == "Türkçe (tr)"
    StrCpy $udtLanguage "tr"
  ${ElseIf} $0 == "Ελληνικά (el)"
    StrCpy $udtLanguage "el"
  ${ElseIf} $0 == "العربية (ar)"
    StrCpy $udtLanguage "ar"
  ${ElseIf} $0 == "Latviešu (lv)"
    StrCpy $udtLanguage "lv"
  ${ElseIf} $0 == "Nederlands (nl-NL)"
    StrCpy $udtLanguage "nl-NL"
  ${ElseIf} $0 == "Tiếng Việt (vi)"
    StrCpy $udtLanguage "vi"
  ${ElseIf} $0 == "O'zbek (uz-Latn-UZ)"
    StrCpy $udtLanguage "uz-Latn-UZ"
  ${Else}
    StrCpy $udtLanguage "zh-CN"
  ${EndIf}
FunctionEnd

Function UdtDevicePage
  Call UdtLoadSelection
  Call UdtHideNativeChrome
  nsDialogs::Create 1018
  Pop $0
  ${If} $0 == error
    Abort
  ${EndIf}
  System::Call 'user32::SetWindowPos(i $0, i 0, i 0, i 0, i 750, i 500, i 0)'
  SetCtlColors $0 "" "${UDT_BG}"
  ${NSD_CreateLabel} 22u 22u 290u 28u "设备选择"
  Pop $1
  SetCtlColors $1 "${UDT_TEXT}" "transparent"
  SendMessage $1 ${WM_SETFONT} $udtFontTitle 0
  ${NSD_CreateLabel} 22u 55u 290u 35u "Choose the hardware mode / 选择硬件模式"
  Pop $1
  SetCtlColors $1 "${UDT_MUTED}" "transparent"
  ${NSD_CreateRadioButton} 22u 102u 290u 30u "自动识别支持的硬件（推荐）"
  Pop $udtAutoRadio
  SetCtlColors $udtAutoRadio "${UDT_TEXT}" "transparent"
  ${NSD_CreateRadioButton} 22u 140u 290u 35u "基础模式 — 仅插件与系统优化"
  Pop $udtBasicRadio
  SetCtlColors $udtBasicRadio "${UDT_TEXT}" "transparent"
  ${If} $udtDeviceMode == "basic"
    ${NSD_SetState} $udtBasicRadio ${BST_CHECKED}
  ${Else}
    ${NSD_SetState} $udtAutoRadio ${BST_CHECKED}
  ${EndIf}
  ${NSD_CreateLabel} 22u 192u 290u 35u "ⓘ  不会修改设备设置，安装后可在应用中更改。"
  Pop $1
  SetCtlColors $1 "${UDT_MUTED}" "transparent"
  nsDialogs::Show
FunctionEnd

Function UdtDeviceLeave
  ${NSD_GetState} $udtAutoRadio $0
  ${If} $0 == ${BST_CHECKED}
    StrCpy $udtDeviceMode "auto"
  ${Else}
    StrCpy $udtDeviceMode "basic"
  ${EndIf}
FunctionEnd

Function UdtApplyFeatureCheck
  ${NSD_GetState} $udtFeatOptCheck $0
  ${If} $0 == ${BST_CHECKED}
    EnableWindow $udtFeatNetCheck 1
  ${Else}
    ${NSD_SetState} $udtFeatNetCheck ${BST_UNCHECKED}
    EnableWindow $udtFeatNetCheck 0
  ${EndIf}
FunctionEnd

Function UdtOptClicked
  Pop $0
  Call UdtApplyFeatureCheck
FunctionEnd

Function UdtFeaturesPage
  Call UdtLoadSelection
  Call UdtHideNativeChrome
  Call UdtCreateFonts
  nsDialogs::Create 1018
  Pop $0
  ${If} $0 == error
    Abort
  ${EndIf}
  System::Call 'user32::SetWindowPos(i $0, i 0, i 0, i 0, i 750, i 500, i 0)'
  SetCtlColors $0 "" "${UDT_BG}"
  ${NSD_CreateLabel} 22u 12u 300u 24u "选择功能 / Choose features"
  Pop $1
  SetCtlColors $1 "${UDT_TEXT}" "transparent"
  SendMessage $1 ${WM_SETFONT} $udtFontTitle 0
  ${NSD_CreateLabel} 22u 38u 300u 18u "Required components stay installed. Optional modules can be omitted."
  Pop $1
  SetCtlColors $1 "${UDT_MUTED}" "transparent"

  ${NSD_CreateLabel} 22u 62u 145u 14u "Required / 必选"
  Pop $1
  SetCtlColors $1 "${UDT_TEXT}" "transparent"
  ${NSD_CreateCheckbox} 22u 80u 145u 14u "Host runtime"
  Pop $1
  ${NSD_SetState} $1 ${BST_CHECKED}
  EnableWindow $1 0
  SetCtlColors $1 "${UDT_TEXT}" "transparent"
  ${NSD_CreateCheckbox} 22u 96u 145u 14u "Desktop shell"
  Pop $1
  ${NSD_SetState} $1 ${BST_CHECKED}
  EnableWindow $1 0
  SetCtlColors $1 "${UDT_TEXT}" "transparent"
  ${NSD_CreateCheckbox} 22u 112u 145u 14u "Console / 控制台"
  Pop $1
  ${NSD_SetState} $1 ${BST_CHECKED}
  EnableWindow $1 0
  SetCtlColors $1 "${UDT_TEXT}" "transparent"
  ${NSD_CreateCheckbox} 22u 128u 145u 14u "Settings / 设置"
  Pop $1
  ${NSD_SetState} $1 ${BST_CHECKED}
  EnableWindow $1 0
  SetCtlColors $1 "${UDT_TEXT}" "transparent"
  ${NSD_CreateCheckbox} 22u 144u 145u 14u "About / 关于"
  Pop $1
  ${NSD_SetState} $1 ${BST_CHECKED}
  EnableWindow $1 0
  SetCtlColors $1 "${UDT_TEXT}" "transparent"

  ${NSD_CreateLabel} 175u 62u 155u 14u "Optional / 可选"
  Pop $1
  SetCtlColors $1 "${UDT_TEXT}" "transparent"
  ${NSD_CreateCheckbox} 175u 80u 155u 14u "System optimization"
  Pop $udtFeatOptCheck
  SetCtlColors $udtFeatOptCheck "${UDT_TEXT}" "transparent"
  ${NSD_CreateCheckbox} 175u 96u 155u 14u "Network acceleration"
  Pop $udtFeatNetCheck
  SetCtlColors $udtFeatNetCheck "${UDT_TEXT}" "transparent"
  ${NSD_CreateCheckbox} 175u 112u 155u 14u "Automation"
  Pop $udtFeatAutoCheck
  SetCtlColors $udtFeatAutoCheck "${UDT_TEXT}" "transparent"
  ${NSD_CreateCheckbox} 175u 128u 155u 14u "Custom macro"
  Pop $udtFeatMacroCheck
  SetCtlColors $udtFeatMacroCheck "${UDT_TEXT}" "transparent"
  ${NSD_CreateCheckbox} 175u 144u 155u 14u "Keyboard"
  Pop $udtFeatKbdCheck
  SetCtlColors $udtFeatKbdCheck "${UDT_TEXT}" "transparent"

  ${If} $udtFeatOpt == "1"
    ${NSD_SetState} $udtFeatOptCheck ${BST_CHECKED}
  ${EndIf}
  ${If} $udtFeatNet == "1"
    ${NSD_SetState} $udtFeatNetCheck ${BST_CHECKED}
  ${EndIf}
  ${If} $udtFeatAuto == "1"
    ${NSD_SetState} $udtFeatAutoCheck ${BST_CHECKED}
  ${EndIf}
  ${If} $udtFeatMacro == "1"
    ${NSD_SetState} $udtFeatMacroCheck ${BST_CHECKED}
  ${EndIf}
  ${If} $udtFeatKbd == "1"
    ${NSD_SetState} $udtFeatKbdCheck ${BST_CHECKED}
  ${EndIf}
  ${NSD_OnClick} $udtFeatOptCheck UdtOptClicked
  Call UdtApplyFeatureCheck
  nsDialogs::Show
FunctionEnd

Function UdtFeaturesLeave
  ${NSD_GetState} $udtFeatOptCheck $0
  ${If} $0 == ${BST_CHECKED}
    StrCpy $udtFeatOpt "1"
  ${Else}
    StrCpy $udtFeatOpt "0"
  ${EndIf}
  ${NSD_GetState} $udtFeatNetCheck $0
  ${If} $0 == ${BST_CHECKED}
    StrCpy $udtFeatNet "1"
  ${Else}
    StrCpy $udtFeatNet "0"
  ${EndIf}
  ${NSD_GetState} $udtFeatAutoCheck $0
  ${If} $0 == ${BST_CHECKED}
    StrCpy $udtFeatAuto "1"
  ${Else}
    StrCpy $udtFeatAuto "0"
  ${EndIf}
  ${NSD_GetState} $udtFeatMacroCheck $0
  ${If} $0 == ${BST_CHECKED}
    StrCpy $udtFeatMacro "1"
  ${Else}
    StrCpy $udtFeatMacro "0"
  ${EndIf}
  ${NSD_GetState} $udtFeatKbdCheck $0
  ${If} $0 == ${BST_CHECKED}
    StrCpy $udtFeatKbd "1"
  ${Else}
    StrCpy $udtFeatKbd "0"
  ${EndIf}
  ${If} $udtFeatOpt != "1"
    StrCpy $udtFeatNet "0"
  ${EndIf}
FunctionEnd

; Define the first page in place of the default white welcome page. The path
; selector lives inside the branded page, therefore the stock directory page
; is disabled in electron-builder.yml.
!macro customWelcomePage
  Page custom UdtWelcomePage UdtWelcomeLeave
!macroend

; Insert language and device pages before the file-copy progress page.
!macro customPageAfterChangeDir
  Page custom UdtLanguagePage UdtLanguageLeave
  Page custom UdtDevicePage UdtDeviceLeave
  Page custom UdtFeaturesPage UdtFeaturesLeave
!macroend
!endif

!macro customInstall
  Call UdtLoadSelection
  WriteINIStr "${UDT_SELECTION_FILE}" "installation" "language" "$udtLanguage"
  WriteINIStr "${UDT_SELECTION_FILE}" "installation" "deviceMode" "$udtDeviceMode"
  WriteINIStr "${UDT_SELECTION_FILE}" "installation" "windowsOptimization" "$udtFeatOpt"
  WriteINIStr "${UDT_SELECTION_FILE}" "installation" "networkAcceleration" "$udtFeatNet"
  WriteINIStr "${UDT_SELECTION_FILE}" "installation" "automation" "$udtFeatAuto"
  WriteINIStr "${UDT_SELECTION_FILE}" "installation" "macro" "$udtFeatMacro"
  WriteINIStr "${UDT_SELECTION_FILE}" "installation" "keyboard" "$udtFeatKbd"
  ${If} $udtFeatNet != "1"
    Delete "$INSTDIR\resources\host\UniversalDeviceToolkit.NetworkProxy.exe"
    Delete "$INSTDIR\resources\host\UniversalDeviceToolkit.NetworkProxy.dll"
    Delete "$INSTDIR\resources\host\UniversalDeviceToolkit.NetworkProxy.runtimeconfig.json"
    Delete "$INSTDIR\resources\host\UniversalDeviceToolkit.NetworkProxy.deps.json"
    Delete "$INSTDIR\resources\host\UniversalDeviceToolkit.NetworkProxy.pdb"
  ${EndIf}
!macroend

!macro customUnInstall
  ; Unregister Nilesoft Shell before deleting files so its file locks are released.
  StrCpy $R0 "$INSTDIR\Shell.exe"
  IfFileExists $R0 0 uninstallShellDone
    nsExec::Exec '"$R0" -unregister -treat -restart -silent'
    Sleep 2000
    Sleep 5000
  uninstallShellDone:
  Delete "$INSTDIR\installer-selection.ini"
!macroend
