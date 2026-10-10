import catalog from './catalog.json'

export type TrayStrings = typeof catalog.en
const labels: Readonly<Record<string, TrayStrings>> = catalog
const languageKeys = Object.keys(labels)

export function nativeLabels(language: string): TrayStrings {
  const tag = language.replaceAll('_', '-').toLowerCase()
  const exact = languageKeys.find(key => key.toLowerCase() === tag)
  if (exact) return labels[exact]
  if (/^zh-(hant|tw|hk|mo)(-|$)/.test(tag)) return catalog['zh-Hant']
  const base = tag.split('-')[0]
  if (base === 'zh') return catalog['zh-CN']
  if (base && labels[base]) return labels[base]
  const matching = languageKeys.find(key => key.toLowerCase().split('-')[0] === base)
  return matching ? labels[matching] : catalog.en
}
