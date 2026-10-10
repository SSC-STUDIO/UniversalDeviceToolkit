(() => {
  let nextId = 0;
  const pending = new Map();
  const listeners = new Map();
  const invoke = (method, parameters) => new Promise((resolve, reject) => {
    const id = ++nextId;
    pending.set(id, { resolve, reject });
    window.chrome.webview.postMessage({ id, method, parameters });
  });
  const subscribe = (name, callback) => {
    const callbacks = listeners.get(name) ?? new Set();
    callbacks.add(callback);
    listeners.set(name, callbacks);
    return () => callbacks.delete(callback);
  };
  window.chrome.webview.addEventListener('message', ({ data }) => {
    if (data.event) {
      for (const callback of listeners.get(data.event) ?? []) callback(data.payload);
      return;
    }
    const request = pending.get(data.id);
    if (!request) return;
    pending.delete(data.id);
    if (data.error) request.reject(new Error(data.error));
    else request.resolve(data.result);
  });
  window.installerApi = {
    isUninstaller: false,
    getInfo: () => invoke('info'),
    getTheme: () => invoke('theme'),
    chooseDirectory: () => invoke('chooseDirectory'),
    install: options => invoke('install', options),
    launch: executable => invoke('launch', executable),
    minimize: () => invoke('minimize'),
    close: () => invoke('close'),
    onProgress: callback => subscribe('progress', callback),
    onThemeChanged: callback => subscribe('theme', callback)
  };
})();
