import assert from 'node:assert/strict'
import { readFileSync, readdirSync } from 'node:fs'
import { URL } from 'node:url'
import test from 'node:test'
import ts from 'typescript'

const i18nUrl = new URL('../src/renderer/src/i18n/index.ts', import.meta.url)
const antdLocaleUrl = new URL('../src/renderer/src/i18n/antdLocale.ts', import.meta.url)
const localizationApiUrl = new URL('../src/renderer/src/api/localization.ts', import.meta.url)
const dateFormatUrl = new URL('../src/renderer/src/utils/dateFormat.ts', import.meta.url)
const enUsUrl = new URL('../src/renderer/src/i18n/locales/en-US.ts', import.meta.url)
const zhCnUrl = new URL('../src/renderer/src/i18n/locales/zh-CN.ts', import.meta.url)
const electronViteUrl = new URL('../electron.vite.config.ts', import.meta.url)
const localesUrl = new URL('../src/renderer/src/i18n/locales/', import.meta.url)
const localeFiles = readdirSync(localesUrl).filter((file) => file.endsWith('.ts'))
const nonEnglishLocaleFiles = localeFiles.filter((file) => !['en-US.ts', 'dashboard-parity.ts'].includes(file))

function placeholders(value) {
  return [...value.matchAll(/\{\{[^{}]+\}\}|\{[^{}]+\}/g)].map((match) => match[0]).sort()
}

function localeEntries(source, fileName, preferredVariableName) {
  const sourceFile = ts.createSourceFile(fileName, source, ts.ScriptTarget.Latest, true, ts.ScriptKind.TS)
  assert.equal(sourceFile.parseDiagnostics.length, 0, `${fileName} must be valid TypeScript`)

  let root
  if (preferredVariableName != null) {
    for (const statement of sourceFile.statements) {
      if (!ts.isVariableStatement(statement)) continue
      root = statement.declarationList.declarations.find(
        (declaration) =>
          ts.isIdentifier(declaration.name) &&
          declaration.name.text === preferredVariableName &&
          declaration.initializer != null &&
          ts.isObjectLiteralExpression(declaration.initializer)
      )?.initializer
      if (root != null) break
    }
  }

  if (root == null) {
    const exportAssignment = sourceFile.statements.find(ts.isExportAssignment)
    const expression = exportAssignment?.expression
    if (expression != null && ts.isObjectLiteralExpression(expression)) root = expression
    else if (expression != null && ts.isCallExpression(expression) && ts.isObjectLiteralExpression(expression.arguments[0])) {
      root = expression.arguments[0]
    }
  }
  assert.ok(root != null, `${fileName} must export a locale object`)

  const entries = new Map()
  const propertyName = (node) => node.text ?? node.getText(sourceFile)
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

test('locale chunks are loaded through Vite glob with a .ts extension', () => {
  const source = readFileSync(i18nUrl, 'utf8')
  assert.match(source, /import\.meta\.glob<LocaleBundle>/)
  assert.match(source, /'\.\/locales\/\*\.ts'/)
  assert.doesNotMatch(source, /import\(`\.\/locales\/\$\{lng\}`\)/)
  assert.match(source, /localeModulePath\(lng\)/)
})

test('zh-CN locale ships Simplified Chinese UI strings', () => {
  const source = readFileSync(zhCnUrl, 'utf8')
  assert.match(source, /name: '通用设备工具箱'/)
  assert.match(source, /settings: '设置'/)
})

test('locale sources contain no encoding pollution', () => {
  const confirmedJapaneseMojibake = /[\u9289\u9287\u9352\u7e3a\u83a0\u95c1\u55d8\u6d49\u93ba\u7a13\u934b\u5396\u95c6\u6e41\u6ec4\u6d93\u9418\u8235\u53a0\u66d8\u7477\u8bf2\u757e\u51a6\u5a13\u693c\u2541\u5030\u5113\u5aca\u93c5\u93c8\u6fb6\u59cf\u95ac\u70bd\u6769\u5157\u5114\u935a\u5d85]/u
  for (const file of localeFiles) {
    const source = readFileSync(new URL(file, localesUrl), 'utf8')
    assert.notEqual(source.charCodeAt(0), 0xfeff, `${file} must be UTF-8 without a BOM`)
    assert.doesNotMatch(source, /\u2026\?/u, `${file} contains a damaged multibyte sequence`)
    assert.doesNotMatch(source, /\uFFFD|[\uE000-\uF8FF]/u, `${file} contains replacement/private-use characters`)
    assert.doesNotMatch(source, /UDT(?:PH|SEG)|UTT?PH/u, `${file} contains a translation sentinel`)
    if (file === 'ja.ts') assert.doesNotMatch(source, confirmedJapaneseMojibake, `${file} contains Japanese mojibake`)
  }
})

test('every non-English locale covers English leaves and placeholders', () => {
  const english = localeEntries(readFileSync(enUsUrl, 'utf8'), 'en-US.ts', 'enUS')
  const englishKeys = [...english.keys()].sort()
  for (const file of nonEnglishLocaleFiles) {
    const source = readFileSync(new URL(file, localesUrl), 'utf8')
    assert.ok(source.includes("from './en-US'"), `${file} must use English fallback`)
    assert.doesNotMatch(source, /translation:\s*enUS\.translation/, `${file} must not replace its locale with English`)
    const entries = localeEntries(source, file)
    assert.deepEqual([...entries.keys()].sort(), englishKeys, `${file} key set differs from English`)
    for (const [key, englishValue] of english) {
      assert.deepEqual(
        placeholders(entries.get(key)),
        placeholders(englishValue),
        `${file} placeholders differ at ${key}`
      )
    }
  }
})

test('renderer build does not strip dynamic locale imports', () => {
  const source = readFileSync(electronViteUrl, 'utf8')
  assert.doesNotMatch(source, /dynamicImportVars:\s*false/)
})

test('Ant Design locale map covers every selectable language', () => {
  const languages = readFileSync(i18nUrl, 'utf8')
  const antd = readFileSync(antdLocaleUrl, 'utf8')
  const codes = [...languages.matchAll(/code: '([^']+)'/g)].map((match) => match[1])

  assert.equal(codes.length, 25)
  for (const code of codes) {
    const escaped = code.replaceAll('-', '\\-')
    assert.match(antd, new RegExp(`(?:['"]${escaped}['"]|${escaped}):`), `${code} needs an Ant Design locale mapping`)
  }
  assert.match(antd, /'uz-Latn-UZ': \(\) => import\('antd\/locale\/uz_UZ'\)/)
  assert.match(antd, /getAntDesignLocale\(language: string\): AntDesignLocale/)
  assert.match(antd, /loadAntDesignLocale\(language: string\): Promise<AntDesignLocale>/)
  assert.doesNotMatch(antd, /import arEG from 'antd\/locale\/ar_EG'/)
})

test('Host culture synchronization and UI date formatting use explicit contracts', () => {
  const localizationApi = readFileSync(localizationApiUrl, 'utf8')
  const dateFormat = readFileSync(dateFormatUrl, 'utf8')
  const handler = readFileSync(
    new URL('../../Host/Rpc/Handlers/LocalizationHandlers.cs', import.meta.url),
    'utf8'
  )
  const program = readFileSync(
    new URL('../../Host/Program.cs', import.meta.url),
    'utf8'
  )
  const optimization = readFileSync(
    new URL('../../Host/Rpc/Handlers/OptimizationHandlers.cs', import.meta.url),
    'utf8'
  )

  assert.match(localizationApi, /localization\.getCulture/)
  assert.match(localizationApi, /localization\.setCulture/)
  assert.match(localizationApi, /hostCultureForLanguage/)
  assert.match(handler, /localization\.getCulture/)
  assert.match(handler, /localization\.setCulture/)
  assert.match(handler, /TryGetCultureName/)
  assert.match(program, /LocalizationRuntime\.Initialize\(\)/)
  assert.match(optimization, /LocalizationCatalog\.GetString\(/)
  assert.match(dateFormat, /toLocaleDateString\(language\)/)
})
