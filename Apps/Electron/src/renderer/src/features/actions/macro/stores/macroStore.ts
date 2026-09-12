import { macroApi } from '../api/macro'
import { createMacroStore } from './macroStoreCore'

export type { MacroStore } from './macroStoreCore'

export const useMacroStore = createMacroStore(macroApi)
