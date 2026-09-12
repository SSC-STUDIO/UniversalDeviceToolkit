import { BridgeInvokeError, invokeObject } from './bridge'

/**
 * Software disabler API — mirror of the host `software.*` handlers
 * (SoftwareDisablerHandlers → Lib SoftwareDisabler VantageDisabler /
 * LegionZoneDisabler / FnKeysDisabler).
 */

export type SoftwareDisablerApp = 'vantage' | 'legionZone' | 'fnKeys'

/** Mirror of Lib SoftwareStatus. */
export type SoftwareStatus = 'Enabled' | 'Disabled' | 'NotFound'

export interface SoftwareStatusResult {
  status: SoftwareStatus
  /** Whether the machine is a supported Legion machine (card visibility parity). */
  isLegionMachine: boolean
}

function isSoftwareStatus(value: unknown): value is SoftwareStatus {
  return value === 'Enabled' || value === 'Disabled' || value === 'NotFound'
}

export const softwareApi = {
  async getStatus(app: SoftwareDisablerApp): Promise<SoftwareStatusResult> {
    const result = await invokeObject<Partial<SoftwareStatusResult>>('software.getStatus', { app })
    if (!isSoftwareStatus(result.status) || typeof result.isLegionMachine !== 'boolean') {
      throw new BridgeInvokeError('software.getStatus returned an invalid payload')
    }
    return { status: result.status, isLegionMachine: result.isLegionMachine }
  },

  async setEnabled(
    app: SoftwareDisablerApp,
    enabled: boolean
  ): Promise<{ ok: boolean; status: SoftwareStatus }> {
    const result = await invokeObject<{ ok?: boolean; status?: SoftwareStatus }>(
      'software.setEnabled',
      { app, enabled }
    )
    if (typeof result.ok !== 'boolean' || !isSoftwareStatus(result.status)) {
      throw new BridgeInvokeError('software.setEnabled returned an invalid payload')
    }
    return { ok: result.ok, status: result.status }
  }
}
