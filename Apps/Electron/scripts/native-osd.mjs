import { randomBytes } from 'node:crypto'
import { readFile, writeFile } from 'node:fs/promises'
import { join } from 'node:path'
import vm from 'node:vm'
import ts from 'typescript'

export async function prepareNativeOsd(uiDirectory, project) {
  const source = await readFile(join(project, 'src/shared/osd-presentation.ts'), 'utf8')
  const compiled = ts.transpileModule(source, {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 }
  }).outputText
  const module = { exports: {} }
  vm.runInNewContext(compiled, { exports: module.exports, module })
  const nonce = randomBytes(16).toString('base64')
  const presentation = module.exports.createOsdPresentation()
  const adapter = `(function () {
    const exports = {};
    ${compiled}
    const view = exports.createOsdPresentation();
    const transport = window.chrome.webview;
    transport.addEventListener('message', ({ data }) => {
      if (!data || data.event !== 'osd.render') return;
      const store = view.configure(data.settings);
      globalThis.udtRender(view.render(store, data.snapshot, data.fps, data.options));
    });
    new ResizeObserver(() => {
      const root = document.getElementById('udt-root');
      if (root) transport.postMessage({ event: 'osd.size', width: Math.ceil(root.offsetWidth), height: Math.ceil(root.offsetHeight) });
    }).observe(document.getElementById('udt-root'));
  })();`.replace(/<\/script/gi, '<\\/script')
  const html = presentation.document(nonce).replace('</script>', adapter + '</script>')
  await writeFile(join(uiDirectory, 'osd.html'), html, 'utf8')
}
