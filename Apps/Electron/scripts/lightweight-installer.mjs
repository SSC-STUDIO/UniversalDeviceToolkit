import { cp, mkdir, readdir, writeFile } from 'node:fs/promises'
import { dirname, join, relative } from 'node:path'

// Keep the original installer pages shared; only their native bridge changes.
export async function prepareSetup(payload, projectRoot, version, compiler, run) {
  const files = (await readdir(payload, { recursive: true, withFileTypes: true }))
    .filter(entry => entry.isFile()).map(entry => relative(payload, join(entry.parentPath, entry.name)))
  const setup = join(payload, 'resources/setup')
  await mkdir(setup, { recursive: true })
  for (const file of ['index.html', 'styles.css', 'renderer.mjs', 'features.mjs', 'i18n.mjs']) {
    await cp(join(projectRoot, 'installer', file), join(setup, file))
  }
  await cp(join(projectRoot, 'resources/icon.png'), join(setup, 'icon.png'))
  await writeFile(join(setup, 'files.json'), JSON.stringify(files), 'utf8')
  const directories = [...new Set(files.flatMap(file => {
    const parents = []
    for (let directory = dirname(file); directory !== '.'; directory = dirname(directory)) parents.push(directory)
    return parents
  }))].sort((left, right) => right.length - left.length)
  const script = join(dirname(payload), 'register.nsi')
  await writeFile(script, `Unicode true
Name "Universal Device Toolkit"
OutFile "${escapeNsis(join(setup, 'register.exe'))}"
RequestExecutionLevel admin
SilentInstall silent
SetCompressor /SOLID lzma
!include "LogicLib.nsh"
Section
  SetRegView 64
  SetShellVarContext all
  IfFileExists "$INSTDIR\\UniversalDeviceToolkit.exe" +3 0
  SetErrorLevel 1
  Quit
  WriteUninstaller "$INSTDIR\\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\\Universal Device Toolkit"
  CreateShortCut "$SMPROGRAMS\\Universal Device Toolkit\\Universal Device Toolkit.lnk" "$INSTDIR\\UniversalDeviceToolkit.exe" "" "$INSTDIR\\UniversalDeviceToolkit.exe"
  CreateShortCut "$DESKTOP\\Universal Device Toolkit.lnk" "$INSTDIR\\UniversalDeviceToolkit.exe" "" "$INSTDIR\\UniversalDeviceToolkit.exe"
  WriteRegStr HKLM "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\UniversalDeviceToolkit" "DisplayName" "Universal Device Toolkit"
  WriteRegStr HKLM "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\UniversalDeviceToolkit" "DisplayVersion" "${escapeNsis(version)}"
  WriteRegStr HKLM "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\UniversalDeviceToolkit" "InstallLocation" "$INSTDIR"
  WriteRegStr HKLM "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\UniversalDeviceToolkit" "DisplayIcon" "$INSTDIR\\UniversalDeviceToolkit.exe"
  WriteRegStr HKLM "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\UniversalDeviceToolkit" "UninstallString" '$\\"$INSTDIR\\Uninstall.exe$\\"'
  \${If} \${Errors}
    SetErrorLevel 1
  \${EndIf}
SectionEnd
Section "Uninstall"
  SetRegView 64
  SetShellVarContext all
${files.map(file => `  Delete "$INSTDIR\\${escapeNsis(file)}"`).join('\n')}
  Delete "$INSTDIR\\installer-selection.ini"
  Delete "$INSTDIR\\Uninstall.exe"
${directories.map(directory => `  RMDir "$INSTDIR\\${escapeNsis(directory)}"`).join('\n')}
  RMDir "$INSTDIR"
  Delete "$DESKTOP\\Universal Device Toolkit.lnk"
  Delete "$SMPROGRAMS\\Universal Device Toolkit\\Universal Device Toolkit.lnk"
  RMDir "$SMPROGRAMS\\Universal Device Toolkit"
  DeleteRegKey HKLM "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\UniversalDeviceToolkit"
SectionEnd
`, 'utf8')
  await run(compiler, ['/V2', script], { cwd: projectRoot })
}

function escapeNsis(value) { return value.replaceAll('$', '$$').replaceAll('"', '$\\"') }

export function bootstrapScript(payload, output, icon) {
  return `Unicode true
Name "Universal Device Toolkit (WebView2)"
OutFile "${escapeNsis(output)}"
Icon "${escapeNsis(icon)}"
InstallDir "$PROGRAMFILES64\\Universal Device Toolkit"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
!define WEBVIEW2_GUID "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}"
Function .onInit
  ReadRegStr $0 HKLM "SOFTWARE\\WOW6432Node\\Microsoft\\EdgeUpdate\\Clients\\\${WEBVIEW2_GUID}" "pv"
  StrCmp $0 "" 0 runtimeReady
  ReadRegStr $0 HKCU "SOFTWARE\\Microsoft\\EdgeUpdate\\Clients\\\${WEBVIEW2_GUID}" "pv"
  StrCmp $0 "" 0 runtimeReady
  IfSilent +2 0
  MessageBox MB_ICONSTOP "Microsoft Edge WebView2 Runtime is required. Use the offline compatibility installer if it is not installed."
  SetErrorLevel 1
  Quit
runtimeReady:
FunctionEnd
Section
  InitPluginsDir
  SetOutPath "$PLUGINSDIR\\payload"
  File /r "${escapeNsis(payload)}\\*"
  IfSilent silent interactive
interactive:
  ExecWait '"$PLUGINSDIR\\payload\\UniversalDeviceToolkit.exe" --setup' $0
  Goto done
silent:
  ExecWait '"$PLUGINSDIR\\payload\\UniversalDeviceToolkit.exe" --setup --silent --destination "$INSTDIR"' $0
done:
  SetErrorLevel $0
SectionEnd
`
}
