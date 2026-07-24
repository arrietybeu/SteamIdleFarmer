/** Number formatting, locale-aware (Vietnamese uses a comma decimal). */
import type { Lang } from "./i18n";

const FMT: Record<Lang, Intl.NumberFormat> = {
  en: new Intl.NumberFormat("en-US", {
    minimumFractionDigits: 0,
    maximumFractionDigits: 1,
  }),
  vi: new Intl.NumberFormat("vi-VN", {
    minimumFractionDigits: 0,
    maximumFractionDigits: 1,
  }),
};

/** 12.34 -> "12.3" (en) / "12,3" (vi) */
export function formatNumber(value: number, lang: Lang): string {
  return FMT[lang].format(value);
}

/** 12.34 -> "12.3 h" / "12,3 giờ" (unit label comes from i18n). */
export function formatHours(value: number, lang: Lang, unit: string): string {
  return `${FMT[lang].format(value)} ${unit}`;
}

/**
 * 3725 -> "1h 02:05" ; 125 -> "02:05".
 * `hourMark` is the localized short hours marker ("h" / "g").
 */
export function formatCountdown(totalSec: number, hourMark: string): string {
  const sec = Math.max(0, Math.floor(totalSec));
  const h = Math.floor(sec / 3600);
  const m = Math.floor((sec % 3600) / 60);
  const s = sec % 60;
  const mm = String(m).padStart(2, "0");
  const ss = String(s).padStart(2, "0");
  return h > 0 ? `${h}${hourMark} ${mm}:${ss}` : `${mm}:${ss}`;
}
