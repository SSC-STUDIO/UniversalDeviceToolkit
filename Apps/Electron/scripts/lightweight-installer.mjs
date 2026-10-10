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
ShowUninstDetails show
SetCompressor /SOLID lzma
!include "LogicLib.nsh"
!include "FileFunc.nsh"
!include "TextFunc.nsh"
${ownershipUninstallFunctions(files)}
Section
  \${GetParameters} $0
  ClearErrors
  \${GetOptions} $0 "/WRITEUNINSTALL" $1
  IfErrors registerInstall
  WriteUninstaller "$EXEDIR\\uninstall.exe"
  IfErrors uninstallFailed
  SetErrorLevel 0
  Quit
uninstallFailed:
  SetErrorLevel 1
  Quit
registerInstall:
  SetRegView 64
  SetShellVarContext all
  IfFileExists "$INSTDIR\\UniversalDeviceToolkit.exe" +3 0
  SetErrorLevel 1
  Quit
  CopyFiles /SILENT "$EXEDIR\\uninstall.exe" "$INSTDIR\\Uninstall.exe"
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
  Call un.DeleteOwnedFiles
  StrCmp $3 0 uninstallCleanup
  System::Call 'kernel32::FormatMessageW(i 0x1200,p 0,i r5,i 0,t .r6,i \${NSIS_MAX_STRLEN},p 0)'
  DetailPrint "$6"
  MessageBox MB_OK|MB_ICONSTOP "$6" /SD IDOK
  SetErrorLevel 1
  Quit
uninstallCleanup:
${directories.map(directory => `  StrCpy $1 "${escapeNsis(directory)}"
  Call un.BuildOwnedPath
  StrCmp $9 1 0 +2
  RMDir "$1"`).join('\n')}
  RMDir "$INSTDIR"
  Delete "$DESKTOP\\Universal Device Toolkit.lnk"
  Delete "$SMPROGRAMS\\Universal Device Toolkit\\Universal Device Toolkit.lnk"
  RMDir "$SMPROGRAMS\\Universal Device Toolkit"
  DeleteRegKey HKLM "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\UniversalDeviceToolkit"
SectionEnd
`, 'utf8')
  await run(compiler, ['/V2', script], { cwd: projectRoot })
  // Match electron-builder's uninstaller generation: this build-only mode
  // writes one output file without elevation or changes to system metadata.
  await run(join(setup, 'register.exe'), ['/WRITEUNINSTALL'], {
    cwd: setup, env: { __COMPAT_LAYER: 'RunAsInvoker' }
  })
}

function escapeNsis(value) { return value.replaceAll('$', '$$').replaceAll('"', '$\\"') }

// Match the installation's selected ownership list against this package's known paths.
export function ownershipUninstallFunctions(files) {
  const allowed = [...new Set([...files, 'installer-selection.ini', 'Uninstall.exe',
    'resources/install-files.json', 'resources/install-files.txt'].map(file => file.replaceAll('/', '\\')))]
  const recoveryHostPaths = allowed.filter(file => ['universaldevicetoolkit.host.exe',
    'resources\\host\\universaldevicetoolkit.host.exe'].includes(file.toLowerCase()))
  for (const file of allowed) {
    const hasControlCharacter = Array.from(file).some(character => {
      const code = character.charCodeAt(0)
      return code < 0x20 || code === 0x7f
    })
    if (file.length > 950 || hasControlCharacter || /["<>:|*?]/.test(file)
      || file.split('\\').some(part => !part || part === '.' || part === '..' || /[. ]$/.test(part))) {
      throw new Error('Invalid installation manifest entry: ' + file)
    }
  }
  return `Var udtMetadataBackup
Var udtMetadataRestoreFailed
Var udtProcessManifest
Var udtProcessSnapshot
Var udtProcessEntry
Var udtProcessTarget
Var udtProcessHandle
Var udtProcessId
Var udtRecoveryHostPath
Var udtSelectedShell
Function un.CheckOwnedPath
  StrCpy $9 0
  StrCpy $7 $1
unCheckParent:
  System::Call 'kernel32::GetFileAttributesW(w r7)i.r8'
  IntCmp $8 -1 unNextParent
  IntOp $8 $8 & 0x400
  IntCmp $8 0 unNextParent unUnsafePath unUnsafePath
unNextParent:
  \${GetParent} $7 $7
  StrCmp $7 "" unSafePath
  Goto unCheckParent
unSafePath:
  StrCpy $9 1
  Return
unUnsafePath:
  StrCpy $5 5
FunctionEnd
Function un.BuildOwnedPath
  StrCpy $9 0
  StrLen $7 $INSTDIR
  StrLen $8 $1
  IntOp $7 $7 + $8
  IntOp $7 $7 + 1
  IntCmp $7 \${NSIS_MAX_STRLEN} unPathTooLong unPathLengthSafe unPathTooLong
unPathTooLong:
  StrCpy $5 206
  Return
unPathLengthSafe:
  StrCpy $1 "$INSTDIR\\$1"
  Call un.CheckOwnedPath
FunctionEnd
Function un.DeleteOwnedFiles
  StrCpy $3 0
  StrCpy $2 0
  StrCpy $5 2
  StrCpy $udtMetadataBackup ""
  StrCpy $udtMetadataRestoreFailed 0
  StrCpy $udtRecoveryHostPath ""
  StrCpy $udtSelectedShell 0
  Call un.StopOwnedProcesses
  StrCmp $3 0 0 unDeleteComplete
  Call un.RestoreNetworkState
  StrCmp $3 0 0 unDeleteComplete
  StrCpy $1 "resources\\install-files.txt"
  Call un.BuildOwnedPath
  StrCmp $9 1 0 unManifestFailed
  ClearErrors
  FileOpen $0 "$1" r
  IfErrors unManifestFailed
unReadOwned:
  ClearErrors
  FileReadUTF16LE $0 $1
  IfErrors unReadComplete
  \${TrimNewLines} $1 $1
  StrCmp $1 "resources\\install-files.txt" unOwnManifest
  StrCmp $1 "resources\\install-files.json" unOwnJson
  StrCmp $1 "Uninstall.exe" unOwnUninstaller
  StrCmp $1 "installer-selection.ini" unOwnSelection
${allowed.filter(file => !['resources\\install-files.txt', 'resources\\install-files.json', 'Uninstall.exe', 'installer-selection.ini'].includes(file))
    .map(file => `  StrCmp $1 "${escapeNsis(file)}" unDeleteSelected`).join('\n')}
  Goto unReadOwned
unOwnManifest:
  IntOp $2 $2 | 1
  Goto unReadOwned
unOwnJson:
  IntOp $2 $2 | 2
  Goto unReadOwned
unOwnUninstaller:
  IntOp $2 $2 | 4
  Goto unReadOwned
unOwnSelection:
  IntOp $2 $2 | 8
  Goto unReadOwned
unDeleteSelected:
  Call un.DeleteOwnedPath
  Goto unReadOwned
unReadComplete:
  FileClose $0
  StrCmp $3 0 0 unDeleteComplete
  IntOp $4 $2 & 1
  StrCmp $4 1 0 unManifestFailed
  ClearErrors
  GetTempFileName $udtMetadataBackup
  IfErrors unManifestFailed
  Delete "$udtMetadataBackup"
  IfErrors unManifestFailed
  CreateDirectory "$udtMetadataBackup"
  IfErrors unManifestFailed
  IntOp $4 $2 & 2
  StrCmp $4 2 0 +3
  StrCpy $1 "resources\\install-files.json"
  Call un.BackupOwnedMetadata
  IntOp $4 $2 & 8
  StrCmp $4 8 0 +3
  StrCpy $1 "installer-selection.ini"
  Call un.BackupOwnedMetadata
  IntOp $4 $2 & 4
  StrCmp $4 4 0 +3
  StrCpy $1 "Uninstall.exe"
  Call un.BackupOwnedMetadata
  StrCpy $1 "resources\\install-files.txt"
  Call un.BackupOwnedMetadata
  StrCmp $3 0 0 unDeleteComplete
  IntOp $4 $2 & 2
  StrCmp $4 2 0 +3
  StrCpy $1 "resources\\install-files.json"
  Call un.DeleteOwnedPath
  IntOp $4 $2 & 8
  StrCmp $4 8 0 +3
  StrCpy $1 "installer-selection.ini"
  Call un.DeleteOwnedPath
  StrCmp $3 0 0 unRestoreMetadata
  IntOp $4 $2 & 4
  StrCmp $4 4 0 +3
  StrCpy $1 "Uninstall.exe"
  Call un.DeleteOwnedPath
  StrCmp $3 0 0 unRestoreMetadata
  StrCpy $1 "resources\\install-files.txt"
  Call un.DeleteOwnedPath
  StrCmp $3 0 unDeleteComplete
unRestoreMetadata:
  IntOp $4 $2 & 2
  StrCmp $4 2 0 +3
  StrCpy $1 "resources\\install-files.json"
  Call un.RestoreOwnedMetadata
  IntOp $4 $2 & 8
  StrCmp $4 8 0 +3
  StrCpy $1 "installer-selection.ini"
  Call un.RestoreOwnedMetadata
  IntOp $4 $2 & 4
  StrCmp $4 4 0 +3
  StrCpy $1 "Uninstall.exe"
  Call un.RestoreOwnedMetadata
  StrCpy $1 "resources\\install-files.txt"
  Call un.RestoreOwnedMetadata
  Goto unDeleteComplete
unManifestFailed:
  StrCpy $3 1
unDeleteComplete:
  StrCmp $udtMetadataBackup "" unMetadataComplete
  StrCmp $udtMetadataRestoreFailed 0 0 unRetainMetadataBackup
  Delete "$udtMetadataBackup\\install-files.json"
  Delete "$udtMetadataBackup\\installer-selection.ini"
  Delete "$udtMetadataBackup\\Uninstall.exe"
  Delete "$udtMetadataBackup\\install-files.txt"
  RMDir "$udtMetadataBackup"
  Goto unMetadataComplete
unRetainMetadataBackup:
  DetailPrint "$udtMetadataBackup"
unMetadataComplete:
FunctionEnd
Function un.StopOwnedProcesses
  StrCpy $1 "resources\\install-files.txt"
  Call un.BuildOwnedPath
  StrCmp $9 1 0 unProcessManifestFailed
  ClearErrors
  FileOpen $udtProcessManifest "$1" r
  IfErrors unProcessManifestFailed
unReadProcessSelections:
  ClearErrors
  FileReadUTF16LE $udtProcessManifest $1
  IfErrors unStopSelectedShell
  \${TrimNewLines} $1 $1
${recoveryHostPaths.map(file => `  StrCmp $1 "${escapeNsis(file)}" 0 +2\n  StrCpy $udtRecoveryHostPath $1`).join('\n')}
${allowed.some(file => file.toLowerCase() === 'universaldevicetoolkit.exe')
    ? '  StrCmp $1 "UniversalDeviceToolkit.exe" 0 +2\n  StrCpy $udtSelectedShell 1' : ''}
  Goto unReadProcessSelections
unStopSelectedShell:
  ClearErrors
  FileSeek $udtProcessManifest 0 SET
  IfErrors unStopProcessSeekFailed
  StrCmp $udtSelectedShell 1 0 unReadExecutable
  StrCpy $1 "UniversalDeviceToolkit.exe"
  Call un.StopOwnedExecutable
  StrCmp $3 0 0 unStopProcessesComplete
unReadExecutable:
  ClearErrors
  FileReadUTF16LE $udtProcessManifest $1
  IfErrors unStopProcessesComplete
  \${TrimNewLines} $1 $1
${allowed.filter(file => file.toLowerCase().endsWith('.exe') && file.toLowerCase() !== 'uninstall.exe')
    .map(file => `  StrCmp $1 "${escapeNsis(file)}" unStopSelectedProcess`).join('\n')}
  Goto unReadExecutable
unStopSelectedProcess:
  Call un.StopOwnedExecutable
  StrCmp $3 0 unReadExecutable
  Goto unStopProcessesComplete
unStopProcessSeekFailed:
  StrCpy $3 1
unStopProcessesComplete:
  FileClose $udtProcessManifest
  Return
unProcessManifestFailed:
  StrCpy $3 1
FunctionEnd
Function un.StopOwnedExecutable
  Call un.BuildOwnedPath
  StrCmp $9 1 0 unStopProcessPathFailed
  StrCpy $udtProcessTarget $1
  System::Call 'kernel32::CreateToolhelp32Snapshot(i 2,i 0)p.r8'
  StrCpy $udtProcessSnapshot $8
  StrCmp $udtProcessSnapshot -1 unStopProcessPathFailed
  System::Alloc 556
  Pop $udtProcessEntry
  StrCmp $udtProcessEntry 0 unStopProcessAllocationFailed
  System::Call '*$udtProcessEntry(i 556)'
  System::Call 'kernel32::Process32FirstW(p $udtProcessSnapshot,p $udtProcessEntry)i.r8'
  StrCmp $8 0 unStopProcessSnapshotComplete
unInspectProcess:
  System::Call '*$udtProcessEntry(i .,i .,i .r8)'
  StrCpy $udtProcessId $8
  System::Call 'kernel32::GetCurrentProcessId()i.r8'
  StrCmp $udtProcessId $8 unNextOwnedProcess
  System::Call 'kernel32::OpenProcess(i 0x101001,i 0,i $udtProcessId)p.r8'
  StrCpy $udtProcessHandle $8
  StrCmp $udtProcessHandle 0 unNextOwnedProcess
  System::Call 'kernel32::QueryFullProcessImageNameW(p $udtProcessHandle,i 0,w .r7,*i \${NSIS_MAX_STRLEN})i.r8'
  StrCmp $8 0 unReleaseProcessHandle
  StrCmp $7 $udtProcessTarget 0 unReleaseProcessHandle
  System::Call 'kernel32::TerminateProcess(p $udtProcessHandle,i 0)i.r8'
  StrCmp $8 0 unStopProcessTerminationFailed
  System::Call 'kernel32::WaitForSingleObject(p $udtProcessHandle,i 5000)i.r8'
  StrCmp $8 0 unReleaseProcessHandle
  StrCpy $3 1
  StrCpy $5 1460
  Goto unReleaseProcessHandle
unStopProcessTerminationFailed:
  System::Call 'kernel32::GetExitCodeProcess(p $udtProcessHandle,*i .r8)i'
  StrCmp $8 259 0 unReleaseProcessHandle
  StrCpy $3 1
  StrCpy $5 5
unReleaseProcessHandle:
  System::Call 'kernel32::CloseHandle(p $udtProcessHandle)'
  StrCmp $3 0 unNextOwnedProcess unStopProcessSnapshotComplete
unNextOwnedProcess:
  System::Call 'kernel32::Process32NextW(p $udtProcessSnapshot,p $udtProcessEntry)i.r8'
  StrCmp $8 0 unStopProcessSnapshotComplete unInspectProcess
unStopProcessSnapshotComplete:
  System::Free $udtProcessEntry
  System::Call 'kernel32::CloseHandle(p $udtProcessSnapshot)'
  Return
unStopProcessAllocationFailed:
  System::Call 'kernel32::CloseHandle(p $udtProcessSnapshot)'
unStopProcessPathFailed:
  StrCpy $3 1
  StrCpy $5 5
FunctionEnd
Function un.RestoreNetworkState
  StrCmp $udtRecoveryHostPath "" unRecoveryComplete
  StrCpy $1 $udtRecoveryHostPath
  Call un.BuildOwnedPath
  StrCmp $9 1 0 unRecoveryFailed
  ClearErrors
  nsExec::ExecToLog '"$1" --restore-network-state'
  Pop $8
  StrCmp $8 "0" unRecoveryComplete
unRecoveryFailed:
  StrCpy $3 1
  StrCpy $5 31
unRecoveryComplete:
FunctionEnd
Function un.DeleteOwnedPath
  Call un.BuildOwnedPath
  StrCmp $9 1 0 unPathFailed
  StrCpy $5 5
  ClearErrors
  Delete "$1"
  IfErrors unPathFailed unPathComplete
unPathFailed:
  StrCpy $3 1
unPathComplete:
FunctionEnd
Function un.BackupOwnedMetadata
  Call un.BuildOwnedPath
  StrCmp $9 1 0 unBackupFailed
  IfFileExists "$1" 0 unBackupComplete
  \${GetFileName} $1 $6
  ClearErrors
  CopyFiles /SILENT "$1" "$udtMetadataBackup\\$6"
  IfErrors unBackupFailed unBackupComplete
unBackupFailed:
  StrCpy $3 1
  StrCpy $5 5
unBackupComplete:
FunctionEnd
Function un.RestoreOwnedMetadata
  Call un.BuildOwnedPath
  StrCmp $9 1 0 unRestoreFailed
  IfFileExists "$1" unRestoreComplete
  \${GetFileName} $1 $6
  IfFileExists "$udtMetadataBackup\\$6" 0 unRestoreComplete
  ClearErrors
  CopyFiles /SILENT "$udtMetadataBackup\\$6" "$1"
  IfErrors unRestoreFailed unRestoreComplete
unRestoreFailed:
  StrCpy $3 1
  StrCpy $5 5
  StrCpy $udtMetadataRestoreFailed 1
unRestoreComplete:
FunctionEnd
`
}

export function bootstrapScript(payload, output, icon) {
  return `Unicode true
Name "Universal Device Toolkit (WebView2)"
OutFile "${escapeNsis(output)}"
Icon "${escapeNsis(icon)}"
InstallDir "$PROGRAMFILES64\\Universal Device Toolkit"
InstallDirRegKey HKLM "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\UniversalDeviceToolkit" "InstallLocation"
RequestExecutionLevel user
SetCompressor /SOLID lzma
!include "FileFunc.nsh"
Function .onInit
  SetRegView 64
  System::Call 'kernel32::GetCommandLineW()w.r1'
  ClearErrors
  \${GetOptions} $1 "/D=" $2
  IfErrors existingDestination destinationReady
existingDestination:
  ReadRegStr $0 HKLM "Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\UniversalDeviceToolkit" "InstallLocation"
  StrCmp $0 "" +2 0
  StrCpy $INSTDIR $0
destinationReady:
FunctionEnd
Section
  InitPluginsDir
  SetOutPath "$PLUGINSDIR\\payload"
  File /r "${escapeNsis(payload)}\\*"
  \${GetParameters} $1
  ClearErrors
  \${GetOptions} $1 "/CHECKUI" $2
  IfErrors normalSetup
  ExecWait '"$PLUGINSDIR\\payload\\UniversalDeviceToolkit.exe" --setup --preview --diagnose-setup' $0
  Goto done
normalSetup:
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
