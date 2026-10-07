import assert from 'node:assert/strict'
import { mkdtemp, readFile, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import test from 'node:test'
import vm from 'node:vm'
import ts from 'typescript'
import { prepareNativeOsd } from '../scripts/native-osd.mjs'

const project = dirname(dirname(fileURLToPath(import.meta.url)))
const source = await readFile(join(project, 'src/shared/osd-presentation.ts'), 'utf8')
const compiled = ts.transpileModule(source, {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 }
}).outputText
const module = { exports: {} }
vm.runInNewContext(compiled, { exports: module.exports, module })
const options = { showCpuAverageFrequency: false, displayMemoryInGigabytes: false, temperatureUnit: 'C' }

test('shared OSD preserves layouts and produces safe missing-data values', () => {
  const view = module.exports.createOsdPresentation()
  for (const [index, layout] of [[0, 'panel'], [1, 'bar'], [2, 'mini']]) {
    const store = view.configure({ SelectedStyleIndex: index, Items: ['CpuTemperature', 'Fps'], IsLocked: true })
    const model = view.render(store, null, null, options)
    assert.equal(model.layout, layout)
    assert.equal(model.appearance.isLocked, true)
    assert.ok(model.groups.every(group => group.items.every(item => !item.text.includes('NaN'))))
  }
})

test('native OSD adapter renders through the shared document and reports dimensions', async () => {
  const directory = await mkdtemp(join(tmpdir(), 'udt-osd-'))
  try {
    await prepareNativeOsd(directory, project)
    const html = await readFile(join(directory, 'osd.html'), 'utf8')
    const script = html.match(/<script nonce="[^"]+">([\s\S]*)<\/script>/)?.[1]
    assert.ok(script)
    const messages = []
    let receive
    let rendered
    const context = {
      document: { getElementById: () => ({ offsetWidth: 320, offsetHeight: 96 }) },
      window: { chrome: { webview: {
        addEventListener: (_, callback) => { receive = callback },
        postMessage: message => messages.push(message)
      } } },
      ResizeObserver: class { constructor(callback) { this.callback = callback } observe() { this.callback() } }
    }
    // Exercise the transport adapter with the actual shared model generator.
    const adapter = script.slice(script.indexOf('(function () {'))
    context.udtRender = model => { rendered = model }
    vm.runInNewContext(adapter, context)
    receive({ data: { event: 'osd.render', settings: { SelectedStyleIndex: 1 }, snapshot: null, fps: null, options } })
    assert.equal(rendered.layout, 'bar')
    assert.equal(messages[0].width, 320)
    assert.equal(messages[0].height, 96)
    assert.match(html, /default-src 'none'/)
  } finally { await rm(directory, { recursive: true, force: true }) }
})
