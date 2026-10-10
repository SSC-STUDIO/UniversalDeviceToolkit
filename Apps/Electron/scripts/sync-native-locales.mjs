import { readFileSync, writeFileSync, mkdirSync } from 'node:fs'
import { dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import ts from 'typescript'

const rendererLocales = new URL('../src/renderer/src/shared/i18n/locales/', import.meta.url)
const output = new URL('../src/shared/i18n/catalog.json', import.meta.url)
const languages = ['en', 'zh-CN', 'zh-Hant', 'ja', 'de', 'fr', 'es', 'it', 'pt-BR', 'pt', 'ru', 'uk', 'pl', 'cs', 'sk', 'hu', 'ro', 'bg', 'tr', 'el', 'ar', 'lv', 'nl-NL', 'vi', 'uz-Latn-UZ']
const keys = {
  dashboard: 'nav.dashboard', keyboard: 'nav.keyboard', automation: 'nav.automation',
  macro: 'nav.macro', windowsOptimization: 'nav.windowsOptimization',
  open: 'wpf.open', close: 'wpf.exit', unnamed: 'wpf.unnamed',
  deactivateGpu: 'automation.deactivateGpu', powerMode: 'feature.powerMode',
  quiet: 'feature.powerModeOptions.quiet', balance: 'feature.powerModeOptions.balance',
  performance: 'feature.powerModeOptions.performance', extreme: 'feature.powerModeOptions.extreme',
  custom: 'feature.powerModeOptions.godMode'
}

function readLocale(language) {
  const file = language === 'en' ? 'en-US.ts' : `${language}.ts`
  const tree = ts.createSourceFile(file, readFileSync(new URL(file, rendererLocales), 'utf8'), ts.ScriptTarget.Latest, true)
  const entries = new Map()
  function flatten(object, prefix = '') {
    for (const property of object.properties) {
      if (!ts.isPropertyAssignment(property)) continue
      const key = prefix + property.name.text
      if (ts.isObjectLiteralExpression(property.initializer)) flatten(property.initializer, `${key}.`)
      else if (ts.isStringLiteralLike(property.initializer)) entries.set(key, property.initializer.text)
    }
  }
  function visit(node) {
    if (ts.isPropertyAssignment(node) && node.name.text === 'translation' && ts.isObjectLiteralExpression(node.initializer)) {
      flatten(node.initializer)
      return
    }
    ts.forEachChild(node, visit)
  }
  visit(tree)
  return Object.fromEntries(Object.entries(keys).map(([name, key]) => {
    const value = entries.get(key)
    if (!value) throw new Error(`${language} is missing native label ${key}`)
    return [name, value]
  }))
}

const serialized = `${JSON.stringify(Object.fromEntries(languages.map(language => [language, readLocale(language)])), null, 2)}\n`
if (process.argv.includes('--check')) {
  if (readFileSync(output, 'utf8') !== serialized) throw new Error('Run node scripts/sync-native-locales.mjs to synchronize native labels.')
} else {
  mkdirSync(dirname(fileURLToPath(output)), { recursive: true })
  writeFileSync(output, serialized, 'utf8')
}
console.log(`Native labels synchronized for ${languages.length} languages.`)
