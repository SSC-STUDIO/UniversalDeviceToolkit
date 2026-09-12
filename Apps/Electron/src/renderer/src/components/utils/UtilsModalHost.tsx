import ActionDetailsModalHost from './ActionDetailsModal'
import CrashReportNotificationModalHost from './CrashReportNotificationModal'
import StatusModalHost from './StatusModal'
import SymbolPickerModalHost from './SymbolPickerModal'
import UnsupportedDeviceModalHost from './UnsupportedDeviceModal'
import UpdateModalHost from './UpdateModal'

/**
 * Mounts the promise-driven modal hosts. Each host is invisible until its
 * `open*`/`show*` helper is called. Add once in the app shell (AppLayout) so
 * any page can open these modals.
 */
export default function UtilsModalHost(): React.JSX.Element {
  return (
    <>
      <ActionDetailsModalHost />
      <CrashReportNotificationModalHost />
      <StatusModalHost />
      <SymbolPickerModalHost />
      <UnsupportedDeviceModalHost />
      <UpdateModalHost />
    </>
  )
}
