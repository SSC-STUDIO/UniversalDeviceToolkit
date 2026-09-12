import { invoke } from '../../../../shared/bridge/bridge'
import { createMacroApi } from './macroClient'

export type {
  MacroApi,
  MacroDirection,
  MacroEvent,
  MacroRecordingMode,
  MacroSlot,
  MacroSource,
  MacroState,
  SaveMacroSequenceParams
} from './macroClient'

export const macroApi = createMacroApi(invoke)
