import enUS from 'antd/locale/en_US'

type AntDesignLocale = typeof enUS

const ANT_DESIGN_LOCALE_LOADERS: Record<string, () => Promise<{ default: AntDesignLocale }>> = {
  ar: () => import('antd/locale/ar_EG'),
  bg: () => import('antd/locale/bg_BG'),
  cs: () => import('antd/locale/cs_CZ'),
  de: () => import('antd/locale/de_DE'),
  el: () => import('antd/locale/el_GR'),
  en: () => Promise.resolve({ default: enUS }),
  es: () => import('antd/locale/es_ES'),
  fr: () => import('antd/locale/fr_FR'),
  hu: () => import('antd/locale/hu_HU'),
  it: () => import('antd/locale/it_IT'),
  ja: () => import('antd/locale/ja_JP'),
  lv: () => import('antd/locale/lv_LV'),
  'nl-NL': () => import('antd/locale/nl_NL'),
  pl: () => import('antd/locale/pl_PL'),
  pt: () => import('antd/locale/pt_PT'),
  'pt-BR': () => import('antd/locale/pt_BR'),
  ro: () => import('antd/locale/ro_RO'),
  ru: () => import('antd/locale/ru_RU'),
  sk: () => import('antd/locale/sk_SK'),
  tr: () => import('antd/locale/tr_TR'),
  uk: () => import('antd/locale/uk_UA'),
  vi: () => import('antd/locale/vi_VN'),
  'uz-Latn-UZ': () => import('antd/locale/uz_UZ'),
  'zh-CN': () => import('antd/locale/zh_CN'),
  'zh-Hant': () => import('antd/locale/zh_TW')
}

const localeCache = new Map<string, AntDesignLocale>([['en', enUS]])

export function getAntDesignLocale(language: string): AntDesignLocale {
  return localeCache.get(language) ?? enUS
}

export async function loadAntDesignLocale(language: string): Promise<AntDesignLocale> {
  const cached = localeCache.get(language)
  if (cached != null) return cached
  const loader = ANT_DESIGN_LOCALE_LOADERS[language]
  if (loader == null) return enUS
  const module = await loader()
  localeCache.set(language, module.default)
  return module.default
}
