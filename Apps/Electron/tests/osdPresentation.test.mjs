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

function createRendererDocument() {
  class Element {
    constructor(tagName) {
      this.tagName = tagName
      this.className = ''
      this.style = {}
      this.children = []
      this.textContent = ''
      this.classList = {
        toggle: (value, enabled) => {
          const names = new Set(this.className.split(' ').filter(Boolean))
          if (enabled) names.add(value)
          else names.delete(value)
          this.className = [...names].join(' ')
        }
      }
    }

    append(...children) { this.children.push(...children) }
    replaceChildren(fragment) { this.children = [...fragment.children] }
  }

  const root = new Element('div')
  root.offsetWidth = 320
  root.offsetHeight = 96
  const body = new Element('body')
  body.scrollWidth = 320
  body.scrollHeight = 96
  body.append(root)
  return {
    root,
    body,
    getElementById: id => id === 'udt-root' ? root : null,
    createElement: tagName => new Element(tagName),
    createDocumentFragment: () => new Element('fragment')
  }
}

function descendants(element) {
  return element.children.flatMap(child => [child, ...descendants(child)])
}

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

test('complete native OSD inline script accepts real render messages and draws every shared layout', async () => {
  const directory = await mkdtemp(join(tmpdir(), 'udt-osd-'))
  try {
    await prepareNativeOsd(directory, project)
    const html = await readFile(join(directory, 'osd.html'), 'utf8')
    const script = html.match(/<script nonce="[^"]+">([\s\S]*)<\/script>/)?.[1]
    assert.ok(script)
    const messages = []
    let receive
    const document = createRendererDocument()
    const context = {
      document,
      window: { chrome: { webview: {
        addEventListener: (_, callback) => { receive = callback },
        postMessage: message => messages.push(message)
      } } },
      ResizeObserver: class { constructor(callback) { this.callback = callback } observe() { this.callback() } }
    }
    // Execute the shipped script boundary and the actual DOM renderer. Slicing
    // out the adapter or replacing udtRender would miss a concatenation failure.
    vm.runInNewContext(script, context)
    assert.equal(typeof receive, 'function')
    assert.equal(typeof context.udtRender, 'function')
    assert.equal(document.root.children.length, 0)
    for (const [index, layout, opacityFactor] of [[0, 'panel', 1], [1, 'bar', 0.8], [2, 'mini', 0.75]]) {
      const locked = index === 1
      receive({ data: { event: 'osd.render', settings: {
        SelectedStyleIndex: index, Items: ['CpuTemperature', 'Fps'], IsLocked: locked, BackgroundOpacity: 0.4
      }, snapshot: null, fps: null, options } })
      assert.equal(document.root.className, 'osd-root osd-root--' + layout)
      assert.equal(document.body.className, locked ? '' : 'osd-body--draggable')
      assert.equal(document.root.style.backgroundColor, 'rgba(30,30,30,' + (0.4 * opacityFactor).toFixed(3) + ')')
      const values = descendants(document.root).filter(node =>
        ['osd-value', 'osd-bar-value', 'osd-mini-value'].includes(node.className))
      assert.equal(values.length, 2)
      assert.ok(values.every(node => node.textContent === '-'))
    }
    assert.equal(messages[0].width, 320)
    assert.equal(messages[0].height, 96)
    assert.match(html, /default-src 'none'/)
  } finally { await rm(directory, { recursive: true, force: true }) }
})

test('native OSD measures intrinsic layouts and does not repeat unchanged size messages', async () => {
  const directory = await mkdtemp(join(tmpdir(), 'udt-osd-size-'))
  try {
    await prepareNativeOsd(directory, project)
    const html = await readFile(join(directory, 'osd.html'), 'utf8')
    const script = html.match(/<script nonce="[^"]+">([\s\S]*)<\/script>/)?.[1]
    assert.ok(script)
    const messages = []
    let receive
    let resize
    let viewportWidth = 320
    let contentWidth = 0
    let contentHeight = 0
    const root = {
      style: {},
      get offsetWidth() { return this.style.width === 'max-content' ? contentWidth : viewportWidth },
      get offsetHeight() { return contentHeight }
    }
    const context = {
      document: { getElementById: () => root },
      window: { chrome: { webview: {
        addEventListener: (_, callback) => { receive = callback },
        postMessage: message => messages.push(message)
      } } },
      ResizeObserver: class {
        constructor(callback) { resize = callback }
        observe(element) { assert.equal(element, root) }
      },
      udtRender: model => {
        contentWidth = model.layout === 'bar' ? 920 : model.layout === 'mini' ? 480 : 250
        contentHeight = model.layout === 'panel' ? 420 : 32
      }
    }
    vm.runInNewContext(script.slice(script.indexOf('(function () {')), context)
    const render = index => receive({ data: {
      event: 'osd.render', settings: { SelectedStyleIndex: index }, snapshot: null, fps: null, options
    } })
    render(1)
    assert.equal(messages.at(-1).width, 920)
    viewportWidth = 920
    resize()
    render(1)
    assert.equal(messages.length, 1)
    render(2)
    assert.equal(messages.at(-1).width, 480)
    viewportWidth = 480
    resize()
    assert.equal(messages.length, 2)
    render(0)
    assert.equal(messages.at(-1).width, 250)
    assert.equal(messages.at(-1).height, 420)
    resize()
    assert.equal(messages.length, 3)
  } finally { await rm(directory, { recursive: true, force: true }) }
})
