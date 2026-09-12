import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import { URL } from 'node:url'
import test from 'node:test'
import ts from 'typescript'

const localesUrl = new URL('../src/renderer/src/i18n/locales/', import.meta.url)
const enUsUrl = new URL('../src/renderer/src/i18n/locales/en-US.ts', import.meta.url)
import { readdirSync } from 'node:fs'
const localeFiles = readdirSync(localesUrl).filter((file) => file.endsWith('.ts'))
const nonEnglishLocaleFiles = localeFiles.filter((file) => !['en-US.ts', 'dashboard-parity.ts'].includes(file))

/** Brand names, technical terms, and short tokens that are legitimately kept as-is. */
const ALLOWED_ENGLISH = new Set([
  'Universal Device Toolkit',
  'Legion Y9000P IRX9',
  'CPU', 'GPU', 'VRAM', 'SSD', 'RAM', 'OSD', 'HDR', 'RGB', 'UDT',
  'PCIe', 'NVMe', 'SATA', 'BIOS', 'UEFI', 'WMI', 'VBIOS',
  'Fn Lock', 'Fn', 'GSync', 'Over Drive', 'Hybrid-Auto',
  'DirectX', 'NVIDIA', 'AMD', 'Intel', 'Wi-Fi', 'Wi',
  'USB', 'HDMI', 'DisplayPort', 'Thunderbolt',
  'WeChat', 'QQ NT', 'Telegram', 'DingTalk',
  'pip', 'npm', 'Yarn', 'NuGet',
  'macOS', 'Linux', 'Windows',
  'NAT', 'DNS', 'DoH', 'IPv6',
  'Vantage', 'PC Support',
  'AppImage',
  'HWiNFO64', 'Nilesoft Shell',
  'PCH', 'FPS',
  'Ctrl', 'Shift', 'Alt',
  'Segoe UI Variable', 'Microsoft YaHei UI',
  'Fn Lock',
  '--'
])

/**
 * A string is considered "residual English" when it matches the English source
 * AND contains a meaningful English word (3+ lowercase letters) AND is not an
 * allowed brand/term. This avoids flagging short technical tokens.
 */
function isLikelyEnglishResidual(value, englishValue) {
  if (value !== englishValue) return false
  if (ALLOWED_ENGLISH.has(value)) return false
  if (value.length < 8) return false
  if (/^[A-Z0-9\s_.\-/()×%]+$/u.test(value)) return false
  if (/\{\{[^}]+\}\}|\{[^}]+\}/.test(value)) return false
  const words = value.split(/\s+/)
  const hasLongWord = words.some((w) => w.length >= 4 && /^[a-z]/.test(w))
  return hasLongWord
}

function localeEntries(source, fileName) {
  const sf = ts.createSourceFile(fileName, source, ts.ScriptTarget.Latest, true, ts.ScriptKind.TS)
  let root
  // Pattern 1: withEnglishFallback({ translation: { ... } })
  for (const st of sf.statements) {
    if (ts.isCallExpression(st) && st.expression && st.expression.expression && ts.isIdentifier(st.expression.expression) && st.expression.expression.text === 'withEnglishFallback' && ts.isObjectLiteralExpression(st.expression.arguments[0])) {
      root = st.expression.arguments[0]
    }
  }
  // Pattern 2: const enUS = { translation: { ... } }
  if (!root) {
    for (const st of sf.statements) {
      if (!ts.isVariableStatement(st)) continue
      for (const decl of st.declarationList.declarations) {
        if (ts.isIdentifier(decl.name) && decl.initializer && ts.isObjectLiteralExpression(decl.initializer)) {
          root = decl.initializer
        }
      }
    }
  }
  // Pattern 3: export default withEnglishFallback({ ... }) or export default { ... }
  if (!root) {
    const exportAssignment = sf.statements.find(ts.isExportAssignment)
    const expression = exportAssignment?.expression
    if (expression != null && ts.isCallExpression(expression) && ts.isObjectLiteralExpression(expression.arguments[0])) {
      root = expression.arguments[0]
    } else if (expression != null && ts.isObjectLiteralExpression(expression)) {
      root = expression
    }
  }
  assert.ok(root != null, `${fileName} must export a locale object`)

  const entries = new Map()
  const propertyName = (node) => node.text ?? node.getText(sf)
  const visit = (object, path) => {
    for (const property of object.properties) {
      if (!ts.isPropertyAssignment(property)) continue
      const nextPath = [...path, propertyName(property.name)]
      const value = property.initializer
      if (ts.isStringLiteral(value) || ts.isNoSubstitutionTemplateLiteral(value)) {
        entries.set(nextPath.join('.'), value.text)
      } else if (ts.isObjectLiteralExpression(value)) {
        visit(value, nextPath)
      }
    }
  }
  visit(root, [])
  return entries
}

test('every locale file imports withEnglishFallback', () => {
  for (const file of nonEnglishLocaleFiles) {
    const source = readFileSync(new URL(file, localesUrl), 'utf8')
    assert.ok(
      source.includes("from './en-US'"),
      `${file} must import withEnglishFallback from ./en-US`
    )
  }
})

test('no locale file replaces its translation root with English', () => {
  for (const file of nonEnglishLocaleFiles) {
    const source = readFileSync(new URL(file, localesUrl), 'utf8')
    assert.doesNotMatch(
      source,
      /translation:\s*enUS\.translation/,
      `${file} must not replace its locale with English`
    )
  }
})

test('locale files contain no BOM or encoding corruption', () => {
  for (const file of localeFiles) {
    const source = readFileSync(new URL(file, localesUrl), 'utf8')
    assert.notEqual(source.charCodeAt(0), 0xfeff, `${file} must be UTF-8 without a BOM`)
    assert.doesNotMatch(source, /\uFFFD|[\uE000-\uF8FF]/u, `${file} contains replacement/private-use characters`)
  }
})

test('English residual audit: report untranslated strings per locale', () => {
  const english = localeEntries(readFileSync(enUsUrl, 'utf8'), 'en-US.ts')
  const results = {}
  for (const file of nonEnglishLocaleFiles) {
    const source = readFileSync(new URL(file, localesUrl), 'utf8')
    const entries = localeEntries(source, file)
    const residual = []
    for (const [key, englishValue] of english) {
      const localeValue = entries.get(key)
      if (localeValue !== undefined && isLikelyEnglishResidual(localeValue, englishValue)) {
        residual.push(key)
      }
    }
    results[file] = residual.length
  }
  console.log('\nLocale English residual counts:')
  for (const [file, count] of Object.entries(results).sort((a, b) => b[1] - a[1])) {
    console.log(`  ${file}: ${count} untranslated strings`)
  }
  // This test currently reports residuals but does not fail — the withEnglishFallback
  // mechanism handles missing translations. A future improvement would be to set
  // thresholds per locale.
})
