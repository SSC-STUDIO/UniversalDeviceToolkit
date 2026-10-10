import ActionDetailsModalHost from '../../shared/ui/dialogs/ActionDetailsModal'
import CrashReportNotificationModalHost from '../../shared/ui/dialogs/CrashReportNotificationModal'
import StatusModalHost from '../../features/dashboard/components/StatusModal'
import SymbolPickerModalHost from '../../shared/ui/dialogs/SymbolPickerModal'
import UnsupportedDeviceModalHost from '../startup/UnsupportedDeviceModal'
import UpdateModalHost from '../../features/settings/components/UpdateModal'

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
