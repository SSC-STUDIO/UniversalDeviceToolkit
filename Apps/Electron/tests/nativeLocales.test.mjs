import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { execFileSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import vm from 'node:vm'
import test from 'node:test'
import ts from 'typescript'

const catalog = JSON.parse(readFileSync(new URL('../src/shared/i18n/catalog.json', import.meta.url), 'utf8'))
const source = readFileSync(new URL('../src/shared/i18n/nativeLabels.ts', import.meta.url), 'utf8')
const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText
const module = { exports: {} }
vm.runInNewContext(compiled, { module, exports: module.exports, require: () => catalog })
const { nativeLabels } = module.exports

test('native labels stay synchronized with all 25 renderer languages', () => {
  execFileSync(process.execPath, [fileURLToPath(new URL('../scripts/sync-native-locales.mjs', import.meta.url)), '--check'])
  assert.equal(Object.keys(catalog).length, 25)
  for (const [language, labels] of Object.entries(catalog)) {
    assert.equal(Object.keys(labels).length, 15, language)
    assert.ok(Object.values(labels).every(label => typeof label === 'string' && label.trim()), language)
  }
})

test('tray language selection handles regional tags, script aliases and unknown locales', () => {
  for (const [input, expected] of [
    ['en-US', 'en'], ['ZH_hant_HK', 'zh-Hant'], ['zh-TW', 'zh-Hant'], ['zh-SG', 'zh-CN'],
    ['ja-JP', 'ja'], ['nl-BE', 'nl-NL'], ['pt-PT', 'pt'], ['uz-Latn', 'uz-Latn-UZ'], ['unknown', 'en']
  ]) assert.equal(nativeLabels(input), catalog[expected], input)
})

test('non-English tray navigation and exit labels use translated values', () => {
  for (const language of ['zh-CN', 'zh-Hant', 'ja', 'de', 'fr', 'es', 'ru', 'ar']) {
    assert.notEqual(nativeLabels(language).close, catalog.en.close, language)
    assert.notEqual(nativeLabels(language).keyboard, catalog.en.keyboard, language)
  }
})
