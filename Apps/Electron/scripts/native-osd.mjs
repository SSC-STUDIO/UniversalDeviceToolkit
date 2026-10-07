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
    const root = document.getElementById('udt-root');
    if (!root) return;
    // Measure the layout independently of the current native viewport so
    // horizontal layouts can grow and shrink without clipping or feedback.
    root.style.width = 'max-content';
    let lastWidth = -1;
    let lastHeight = -1;
    const reportSize = () => {
      const width = Math.ceil(root.offsetWidth);
      const height = Math.ceil(root.offsetHeight);
      if (width === lastWidth && height === lastHeight) return;
      lastWidth = width;
      lastHeight = height;
      transport.postMessage({ event: 'osd.size', width, height });
    };
    transport.addEventListener('message', ({ data }) => {
      if (!data || data.event !== 'osd.render') return;
      const store = view.configure(data.settings);
      globalThis.udtRender(view.render(store, data.snapshot, data.fps, data.options));
      reportSize();
    });
    new ResizeObserver(reportSize).observe(root);
  })();`.replace(/<\/script/gi, '<\\/script')
  const html = presentation.document(nonce).replace('</script>', adapter + '</script>')
  await writeFile(join(uiDirectory, 'osd.html'), html, 'utf8')
}
