/**
 * Temperature unit preference for the sensor dashboard.
 *
 * The value is persisted both to the backend 'application' scope (kept for the
 * host-side consumers such as the OSD / status tray) and to localStorage
 * 'udt-temperature-unit' so the renderer can read it synchronously. Sensor
 * sections should use getTemperatureUnit() below to format values.
 */
export type TemperatureUnit = 'C' | 'F'

export const TEMPERATURE_UNIT_STORAGE_KEY = 'udt-temperature-unit'

/** Returns the current temperature unit ('C' or 'F'). */
export function getTemperatureUnit(): TemperatureUnit {
  try {
    return localStorage.getItem(TEMPERATURE_UNIT_STORAGE_KEY) === 'F' ? 'F' : 'C'
  } catch {
    return 'C'
  }
}
