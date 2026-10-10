import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import vm from 'node:vm'
import test from 'node:test'
import { defaultFeatures, normalizeFeatures, OPTIONAL_FEATURES } from '../installer/features.mjs'
import { installerLocale, installerText, installerProgressText } from '../installer/i18n.mjs'

test('translated wizard catalogs include every English key and placeholder', () => {
  const source = readFileSync(new URL('../installer/i18n.mjs', import.meta.url), 'utf8')
  const catalogs = vm.runInNewContext(source.replace(/^export /gm, '') + '\n catalogs')
  const english = catalogs['en-US']
  const placeholders = value => [...value.matchAll(/\{\w+\}/g)].map(match => match[0]).sort()
  const ui = renderer()
  const selectable = Array.from(vm.runInContext('languageOptions.map(([id]) => installerLocale(id))', ui.context))
  assert.equal(selectable.length, 25)
  assert.deepEqual(Object.keys(catalogs).sort(), selectable.sort())
  for (const language of selectable) {
    assert.deepEqual(Object.keys(catalogs[language]).sort(), Object.keys(english).sort())
    for (const key of Object.keys(english)) {
      assert.ok(catalogs[language][key].trim(), `${language}.${key}`)
      assert.deepEqual(placeholders(catalogs[language][key]), placeholders(english[key]), `${language}.${key}`)
    }
  }
})

function renderer() {
  const root = { innerHTML: '', addEventListener() {} }
  const document = { querySelector: selector => selector === '#app' ? root : null, documentElement: {} }
  const source = readFileSync(new URL('../installer/renderer.mjs', import.meta.url), 'utf8').replace(/^import .*$/gm, '')
  const context = vm.createContext({
    defaultFeatures, normalizeFeatures, OPTIONAL_FEATURES, installerText, installerLocale, installerProgressText, document,
    window: { installerApi: { getInfo: () => new Promise(() => {}), onProgress() {}, onThemeChanged() {} } }
  })
  vm.runInContext(source, context)
  return { context, document, render(language, page) {
    context.language = language
    context.page = page
    vm.runInContext('state.language = language; state.page = page; render()', context)
    return root.innerHTML
  } }
}

test('wizard pages follow English and traditional Chinese without changing installation selection', () => {
  const ui = renderer()
  vm.runInContext("state.destination = 'C:/UDT'; state.deviceMode = 'basic'; state.features.macro = false", ui.context)
  const pages = ['welcome', 'device', 'features', 'install', 'complete', 'uninstall']
  const headings = ['Ready to install', 'Choose device support', 'Choose features', 'Installing', 'Installation complete', 'Uninstall Universal Device Toolkit']
  for (const [index, page] of pages.entries()) {
    const html = ui.render('en', page)
    assert.ok(html.includes(headings[index]), page)
    assert.doesNotMatch(html, /\p{Script=Han}/u, page)
  }
  assert.equal(ui.document.documentElement.lang, 'en-US')
  assert.match(ui.render('zh-Hant', 'welcome'), /準備安裝/)
  assert.match(ui.render('zh-Hant', 'complete'), /安裝完成/)
  assert.equal(ui.document.documentElement.lang, 'zh-Hant')
  assert.equal(vm.runInContext('state.destination', ui.context), 'C:/UDT')
  assert.equal(vm.runInContext('state.deviceMode', ui.context), 'basic')
  assert.equal(vm.runInContext('state.features.macro', ui.context), false)
})

test('language aliases and unsupported languages use a consistent wizard fallback', () => {
  assert.equal(installerLocale('ZH_hant_HK'), 'zh-Hant')
  assert.equal(installerLocale('zh-TW'), 'zh-Hant')
  assert.equal(installerLocale('zh-SG'), 'zh-CN')
  assert.equal(installerLocale('de-DE'), 'de')
  assert.equal(installerLocale('pt-PT'), 'pt')
  assert.equal(installerLocale('PT_br'), 'pt-BR')
  assert.equal(installerLocale('nl-BE'), 'nl-NL')
  assert.equal(installerLocale('uz'), 'uz-Latn-UZ')
  assert.equal(installerText('unknown', 'cancel'), 'Cancel')
  assert.equal(installerText('en', 'requiredSpaceValue', { size: '12 MB' }), 'Required space: 12 MB')
})

test('every selectable language translates wizard headings and restores reading direction', () => {
  const ui = renderer()
  const languages = Array.from(vm.runInContext('languageOptions.map(([id]) => id)', ui.context))
  for (const language of languages) {
    const html = ui.render(language, 'welcome')
    assert.ok(html.includes(installerText(language, 'welcomeTitle')), language)
    if (language !== 'en') assert.doesNotMatch(html, /Ready to install/, language)
    assert.equal(ui.document.documentElement.dir, language === 'ar' ? 'rtl' : 'ltr')
    assert.match(html, /id="destination"[^>]*dir="ltr"/)
  }
  vm.runInContext("state.destination = 'C:/UDT'; state.progress.file = 'host.dll'", ui.context)
  assert.match(ui.render('ar', 'install'), /<strong dir="ltr">C:\/UDT<\/strong>/)
  assert.match(ui.render('ar', 'install'), /<span dir="ltr">host.dll<\/span>/)
  ui.render('en', 'welcome')
  assert.equal(ui.document.documentElement.dir, 'ltr')
})

test('progress translates known phases while preserving diagnostic warnings and escaped paths', () => {
  assert.equal(installerProgressText('zh-Hant', { phase: 'copying' }), '正在複製應用程式檔案...')
  assert.equal(installerProgressText('en', { phase: 'downloading', message: '下载中' }), 'Downloading application files...')
  assert.equal(installerProgressText('en', { phase: 'warning', message: 'Disk is full' }), 'Disk is full')
  const ui = renderer()
  vm.runInContext("state.progress.message = '<img src=x>'; state.progress.file = '<script>bad</script>'", ui.context)
  const html = ui.render('en', 'install')
  assert.ok(html.includes('&lt;img src=x&gt;'))
  assert.ok(html.includes('&lt;script&gt;bad&lt;/script&gt;'))
})
