/**
 * Lightweight i18n — no external library.
 *
 * A React context exposes `t(key, params?)`, the active `lang`, and `setLang`.
 * Two dictionaries (`en`, `vi`) live in `src/locales/*`. English is the source
 * of truth for the key set and the default language; the choice is persisted in
 * localStorage under `xc_lang`. On first visit (no stored value) we default to
 * English — we deliberately do NOT auto-detect from the browser.
 */
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
} from "react";
import type { ReactNode } from "react";
import { en } from "./locales/en";
import { vi } from "./locales/vi";

export type Lang = "en" | "vi";
export type TranslationKey = keyof typeof en;
export type TParams = Record<string, string | number>;

const DICTS: Record<Lang, Record<TranslationKey, string>> = { en, vi };

const STORAGE_KEY = "xc_lang";
const DEFAULT_LANG: Lang = "en";

interface I18nContextValue {
  lang: Lang;
  setLang: (lang: Lang) => void;
  t: (key: TranslationKey, params?: TParams) => string;
}

const I18nContext = createContext<I18nContextValue | null>(null);

function readStored(): Lang {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === "en" || stored === "vi") return stored;
  } catch {
    /* localStorage may be unavailable — fall back to default */
  }
  return DEFAULT_LANG;
}

/** Replace `{name}` placeholders with values from `params`. */
function interpolate(template: string, params?: TParams): string {
  if (!params) return template;
  return template.replace(/\{(\w+)\}/g, (match, key: string) =>
    key in params ? String(params[key]) : match,
  );
}

/**
 * Module-level mirror of the active language so non-React modules (e.g. the
 * `api` fetch wrapper) can localize the messages they throw. Kept in sync by
 * the provider below.
 */
let activeLang: Lang = readStored();

/** Standalone translator for code outside the React tree. */
export function translate(key: TranslationKey, params?: TParams): string {
  const template = DICTS[activeLang][key] ?? en[key] ?? key;
  return interpolate(template, params);
}

export function I18nProvider({ children }: { children: ReactNode }) {
  const [lang, setLangState] = useState<Lang>(readStored);

  useEffect(() => {
    activeLang = lang;
    try {
      localStorage.setItem(STORAGE_KEY, lang);
    } catch {
      /* ignore — persistence is best-effort */
    }
    document.documentElement.lang = lang;
  }, [lang]);

  const setLang = useCallback((next: Lang) => setLangState(next), []);

  const t = useCallback(
    (key: TranslationKey, params?: TParams): string => {
      const template = DICTS[lang][key] ?? en[key] ?? key;
      return interpolate(template, params);
    },
    [lang],
  );

  const value = useMemo<I18nContextValue>(
    () => ({ lang, setLang, t }),
    [lang, setLang, t],
  );

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n(): I18nContextValue {
  const ctx = useContext(I18nContext);
  if (!ctx) throw new Error("useI18n must be used within <I18nProvider>");
  return ctx;
}
