import { useI18n } from "../i18n";
import type { Lang } from "../i18n";

const LANGS: { code: Lang; label: string }[] = [
  { code: "en", label: "EN" },
  { code: "vi", label: "VI" },
];

/** Compact EN/VI pill switcher — gold accent on the active language. */
export function LangSwitcher({ className = "" }: { className?: string }) {
  const { lang, setLang, t } = useI18n();
  return (
    <div
      className={`lang-switch${className ? ` ${className}` : ""}`}
      role="group"
      aria-label={t("lang.switcher")}
    >
      {LANGS.map((l) => {
        const active = lang === l.code;
        return (
          <button
            key={l.code}
            type="button"
            className={`lang-switch__btn${active ? " lang-switch__btn--active" : ""}`}
            aria-pressed={active}
            onClick={() => setLang(l.code)}
          >
            {l.label}
          </button>
        );
      })}
    </div>
  );
}
