import { invokeObject } from './bridge'

export interface PowerApi {
  restart(): Promise<{ ok: boolean }>
}

export const powerApi: PowerApi = {
  async restart() {
    return invokeObject<{ ok: boolean }>('power.restart', {})
  }
}
