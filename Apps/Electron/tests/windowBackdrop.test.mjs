import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'
import test from 'node:test'
import { URL } from 'node:url'
import ts from 'typescript'

const css = readFileSync(
  new URL('../src/renderer/src/theme/WindowBackdrop.css', import.meta.url),
  'utf8'
)
const backdropTs = readFileSync(
  new URL('../src/renderer/src/theme/windowBackdrop.ts', import.meta.url),
  'utf8'
)
const settingTs = readFileSync(
  new URL('../src/renderer/src/components/settings/WindowBackdropSetting.tsx', import.meta.url),
  'utf8'
)
const mainTs = readFileSync(new URL('../src/main/index.ts', import.meta.url), 'utf8')

async function importBackdrop(platform) {
  const materials = []
  const root = { dataset: {} }
  globalThis.window = {
    bridge: {
      platform,
      setBackgroundMaterial: async (material) => {
        materials.push(material)
      }
    }
  }
  globalThis.document = { documentElement: root }

  const stripped = backdropTs.replace(
    /import \{ settingsApi \} from '\.\.\/api\/settings'\r?\n/,
    'const settingsApi = { get: async () => ({ value: null }) }\n'
  )
  const output = ts.transpileModule(stripped, {
    compilerOptions: {
      module: ts.ModuleKind.ESNext,
      target: ts.ScriptTarget.ES2022
    }
  }).outputText
  const moduleUrl = `data:text/javascript;charset=utf-8,${encodeURIComponent(`${output}\n`)}`
  const mod = await import(`${moduleUrl}#${platform}-${Date.now()}-${Math.random()}`)
  return { mod, root, materials }
}

test('Linux mica/acrylic CSS stays opaque and paints a light mica stand-in', () => {
  assert.match(css, /:not\(\[data-platform='linux'\]\)/)
  assert.match(css, /--udt-mica-chrome:\s*color-mix/)
  assert.match(css, /--udt-mica-wallpaper-sample:\s*#9bb39a/)
  assert.match(css, /36%,\s*#f6f6f6/)
  assert.match(css, /#ffffff 55%/)
  assert.match(css, /#ffffff 78%/)
  assert.match(
    css,
    /\[data-theme='light'\]\[data-platform='linux'\]\[data-backdrop='mica'\]/
  )
  assert.match(css, /--udt-surface-navigation:\s*var\(--udt-mica-chrome\)/)
  assert.match(
    css,
    /\[data-platform='linux'\]\[data-backdrop='mica'\] \.udt-app-shell/
  )
  assert.match(css, /:not\(\[data-platform='linux'\]\) \.udt-nav/)
  assert.doesNotMatch(
    css,
    /\[data-platform='linux'\]\[data-backdrop='(?:mica|acrylic)'\] \.udt-nav/
  )
})

test('Linux settings still offer Windows mica as an opaque approximation', () => {
  assert.match(settingTs, /PLATFORM === 'darwin' \? \['macOS', 'Off'\] : \['Windows', 'macOS', 'Off'\]/)
  assert.doesNotMatch(settingTs, /PLATFORM === 'linux' \? \['Off'\]/)
})

test('Linux BrowserWindow uses theme-aware mica chrome, not a hardcoded #202020', () => {
  assert.match(mainTs, /LINUX_MICA_CHROME_LIGHT = '#d5ded5'/)
  assert.match(mainTs, /linuxWindowBackgroundColor\(\)/)
  assert.match(mainTs, /applyLinuxWindowBackgroundColor\(\)/)
  assert.doesNotMatch(
    mainTs,
    /process\.platform === 'linux'[\s\S]{0,400}backgroundColor: '#202020'/
  )
})

test('applyWindowBackdrop keeps mica on Linux and requests no native material', async () => {
  const { mod, root, materials } = await importBackdrop('linux')
  mod.applyWindowBackdrop('Windows')
  assert.equal(root.dataset.backdrop, 'mica')
  assert.deepEqual(materials, ['none'])

  mod.applyWindowBackdrop('macOS')
  assert.equal(root.dataset.backdrop, 'acrylic')
  assert.deepEqual(materials, ['none', 'none'])

  mod.applyWindowBackdrop('Off')
  assert.equal(root.dataset.backdrop, 'none')
  assert.deepEqual(materials, ['none', 'none', 'none'])
})

test('applyWindowBackdrop still applies native mica on Windows', async () => {
  const { mod, root, materials } = await importBackdrop('win32')
  mod.applyWindowBackdrop('Windows')
  assert.equal(root.dataset.backdrop, 'mica')
  assert.deepEqual(materials, ['mica'])
})
