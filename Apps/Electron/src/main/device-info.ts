/**
 * Device info projection for the `device.info` bridge method.
 * Mirrors the original client's MachineCompatibility.MachineInformation shape;
 * data comes from the Host `system.info` RPC and is sanitized field by field
 * so a partial or malformed payload degrades to the fallback instead of
 * leaking `unknown` values into the renderer.
 */
import { hostClient } from './host-client'

export interface DeviceInfo {
  vendor: string
  model: string
  machineType: string
  serialNumber: string
  biosVersion: string
  processor?: DeviceInfoProcessor | null
  videoController?: DeviceInfoVideoController | null
  memory?: DeviceInfoMemory | null
  warranty?: DeviceInfoWarranty | null
}

export interface DeviceInfoProcessor {
  name?: string | null
  numberOfCores?: number | null
  numberOfLogicalProcessors?: number | null
  maxClockSpeedMHz?: number | null
}

export interface DeviceInfoVideoController {
  name?: string | null
  adapterCompatibility?: string | null
  adapterRamBytes?: number | null
}

export interface DeviceInfoMemory {
  totalCapacityBytes?: number | null
  moduleCount?: number | null
  configuredClockSpeedMHz?: number | null
  speedMHz?: number | null
}

export interface DeviceInfoWarranty {
  startDate?: string | null
  endDate?: string | null
  link?: string | null
}

interface DeviceInfoHardware {
  processor?: DeviceInfoProcessor | null
  videoController?: DeviceInfoVideoController | null
  memory?: DeviceInfoMemory | null
}

const FALLBACK_DEVICE_INFO: DeviceInfo = {
  vendor: '',
  model: 'Universal Device Toolkit',
  machineType: '',
  serialNumber: '',
  biosVersion: ''
}

function sanitizeProcessor(value: unknown): DeviceInfoProcessor | null {
  if (!value || typeof value !== 'object') return null
  const source = value as Record<string, unknown>
  const processor: DeviceInfoProcessor = {}
  if (typeof source.name === 'string' && source.name.length > 0) processor.name = source.name
  if (typeof source.numberOfCores === 'number') processor.numberOfCores = source.numberOfCores
  if (typeof source.numberOfLogicalProcessors === 'number') {
    processor.numberOfLogicalProcessors = source.numberOfLogicalProcessors
  }
  if (typeof source.maxClockSpeedMHz === 'number') processor.maxClockSpeedMHz = source.maxClockSpeedMHz
  return processor.name ? processor : null
}

function sanitizeVideoController(value: unknown): DeviceInfoVideoController | null {
  if (!value || typeof value !== 'object') return null
  const source = value as Record<string, unknown>
  const videoController: DeviceInfoVideoController = {}
  if (typeof source.name === 'string' && source.name.length > 0) videoController.name = source.name
  if (typeof source.adapterCompatibility === 'string') {
    videoController.adapterCompatibility = source.adapterCompatibility
  }
  if (typeof source.adapterRamBytes === 'number') videoController.adapterRamBytes = source.adapterRamBytes
  return videoController.name ? videoController : null
}

function sanitizeMemory(value: unknown): DeviceInfoMemory | null {
  if (!value || typeof value !== 'object') return null
  const source = value as Record<string, unknown>
  const memory: DeviceInfoMemory = {}
  if (typeof source.totalCapacityBytes === 'number') memory.totalCapacityBytes = source.totalCapacityBytes
  if (typeof source.moduleCount === 'number') memory.moduleCount = source.moduleCount
  if (typeof source.configuredClockSpeedMHz === 'number') {
    memory.configuredClockSpeedMHz = source.configuredClockSpeedMHz
  }
  if (typeof source.speedMHz === 'number') memory.speedMHz = source.speedMHz
  return memory.totalCapacityBytes || memory.moduleCount || memory.configuredClockSpeedMHz || memory.speedMHz
    ? memory
    : null
}

function sanitizeWarranty(value: unknown): DeviceInfoWarranty | null {
  if (!value || typeof value !== 'object') return null
  const source = value as Record<string, unknown>
  const warranty: DeviceInfoWarranty = {}
  if (typeof source.startDate === 'string') warranty.startDate = source.startDate
  if (typeof source.endDate === 'string') warranty.endDate = source.endDate
  if (typeof source.link === 'string') warranty.link = source.link
  return warranty.startDate || warranty.endDate || warranty.link ? warranty : null
}

/** Best-effort device info via the host's system.info; never throws. */
export async function getDeviceInfo(): Promise<DeviceInfo> {
  try {
    const result = (await hostClient.invoke('system.info', {})) as
      | (Partial<DeviceInfo> & { hardware?: DeviceInfoHardware | null })
      | null
      | undefined
    if (!result || typeof result !== 'object') return { ...FALLBACK_DEVICE_INFO }
    const hardware = result.hardware && typeof result.hardware === 'object' ? result.hardware : null
    return {
      vendor: typeof result.vendor === 'string' ? result.vendor : '',
      model:
        typeof result.model === 'string' && result.model.length > 0
          ? result.model
          : FALLBACK_DEVICE_INFO.model,
      machineType: typeof result.machineType === 'string' ? result.machineType : '',
      serialNumber: typeof result.serialNumber === 'string' ? result.serialNumber : '',
      biosVersion: typeof result.biosVersion === 'string' ? result.biosVersion : '',
      processor: sanitizeProcessor(hardware?.processor),
      videoController: sanitizeVideoController(hardware?.videoController),
      memory: sanitizeMemory(hardware?.memory),
      warranty: sanitizeWarranty(result.warranty)
    }
  } catch (error) {
    console.error('[main] failed to load device info:', error)
    return { ...FALLBACK_DEVICE_INFO }
  }
}
