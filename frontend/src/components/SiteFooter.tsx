import { useI18n } from "../i18n";
import { IconGithub } from "./ui";

const AUTHOR_URL = "https://github.com/arrietybeu";
const REPO_URL = "https://github.com/arrietybeu/SteamIdleFarmer";

/** Author + source-repo footer, shown on the login screen and inside the app. */
export function SiteFooter({ className = "" }: { className?: string }) {
  const { t } = useI18n();
  return (
    <footer className={`site-foot${className ? ` ${className}` : ""}`}>
      <span className="site-foot__seg">
        {t("footer.madeBy")}{" "}
        <a
          className="site-foot__link"
          href={AUTHOR_URL}
          target="_blank"
          rel="noopener noreferrer"
        >
          <IconGithub width={14} height={14} />
          arrietybeu
        </a>
      </span>
      <span className="site-foot__dot" aria-hidden="true">
        ·
      </span>
      <span className="site-foot__seg">
        {t("footer.source")}{" "}
        <a
          className="site-foot__link"
          href={REPO_URL}
          target="_blank"
          rel="noopener noreferrer"
        >
          <IconGithub width={14} height={14} />
          SteamIdleFarmer
        </a>
      </span>
    </footer>
  );
}
