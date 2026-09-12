import { invokeObject } from './bridge'

export interface AiStatus {
  supported: boolean
  enabled: boolean
}

export const aiApi = {
  getStatus: (): Promise<AiStatus> => invokeObject<AiStatus>('ai.getStatus', {}),
  setEnabled: (enabled: boolean): Promise<{ ok: boolean }> =>
    invokeObject<{ ok: boolean }>('ai.setEnabled', { enabled })
}
