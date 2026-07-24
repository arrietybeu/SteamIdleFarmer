import type { AuthStatus } from "../types";
import { useI18n } from "../i18n";
import { LangSwitcher } from "./LangSwitcher";
import { IconLogout, IconTrophy } from "./ui";

interface Props {
  auth: AuthStatus;
  wsOnline: boolean;
  onLogout: () => void;
}

export function TopBar({ auth, wsOnline, onLogout }: Props) {
  const { t } = useI18n();
  const persona = auth.persona ?? t("topbar.defaultPersona");
  return (
    <header className="topbar">
      <div className="topbar__left">
        <LangSwitcher className="lang-switch--bar" />
        <div className="topbar__brand">
          <span className="brand-mark">
            <IconTrophy width={20} height={20} />
          </span>
          <div className="topbar__brand-text">
            <span className="topbar__title">{t("brand.name")}</span>
            <span className="topbar__subtitle">{t("brand.taglineBar")}</span>
          </div>
        </div>
      </div>

      <div className="topbar__right">
        <span
          className={`live-dot${wsOnline ? " live-dot--on" : ""}`}
          title={wsOnline ? t("topbar.wsOnlineTitle") : t("topbar.wsOfflineTitle")}
        >
          {wsOnline ? t("topbar.live") : t("topbar.offline")}
        </span>
        <div
          className="persona"
          title={
            auth.steamId ? t("topbar.steamIdTitle", { id: auth.steamId }) : undefined
          }
        >
          <span className="persona__avatar" aria-hidden="true">
            {persona.trim().charAt(0).toUpperCase() || "S"}
          </span>
          <span className="persona__name">{persona}</span>
        </div>
        <button className="btn btn--ghost btn--sm" onClick={onLogout}>
          <IconLogout width={16} height={16} />
          {t("topbar.logout")}
        </button>
      </div>
    </header>
  );
}
