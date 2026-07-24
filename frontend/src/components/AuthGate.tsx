import { useEffect, useState } from "react";
import { toDataURL } from "qrcode";
import type { AuthStatus } from "../types";
import { useI18n } from "../i18n";
import { LangSwitcher } from "./LangSwitcher";
import { SiteFooter } from "./SiteFooter";
import { IconGithub, IconQr, IconTrophy, Spinner } from "./ui";

const REPO_URL = "https://github.com/arrietybeu/SteamIdleFarmer";

interface Props {
  auth: AuthStatus;
  challengeUrl: string | null;
  starting: boolean;
  onStart: () => void;
}

/**
 * Màn hình đăng nhập: card kính giữa màn hình, mã QR đóng khung
 * kiểu "ống ngắm" máy quét, aurora vàng xoay chậm phía sau.
 */
export function AuthGate({ auth, challengeUrl, starting, onStart }: Props) {
  const { t } = useI18n();
  const [qrData, setQrData] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    if (!challengeUrl) {
      setQrData(null);
      return;
    }
    toDataURL(challengeUrl, {
      width: 480,
      margin: 1,
      errorCorrectionLevel: "M",
      color: { dark: "#101623", light: "#ffffff" },
    })
      .then((url) => {
        if (!cancelled) setQrData(url);
      })
      .catch(() => {
        if (!cancelled) setQrData(null);
      });
    return () => {
      cancelled = true;
    };
  }, [challengeUrl]);

  const loggingIn = auth.state === "logging_in";
  const hasQr = qrData !== null;

  return (
    <div className="auth">
      <div className="auth__aurora" aria-hidden="true" />
      <main className="auth__card glass">
        <LangSwitcher className="auth__lang" />

        <div className="auth__brand">
          <span className="brand-mark">
            <IconTrophy width={22} height={22} />
          </span>
          <div>
            <h1 className="auth__title">{t("brand.name")}</h1>
            <p className="auth__sub">{t("brand.taglineAuth")}</p>
          </div>
        </div>

        {!hasQr && !loggingIn && (
          <div className="auth__intro">
            <p>{t("auth.intro")}</p>
            <button
              className="btn btn--gold btn--lg"
              onClick={onStart}
              disabled={starting}
            >
              {starting ? <Spinner /> : <IconQr width={18} height={18} />}
              {t("auth.loginButton")}
            </button>
            {auth.state === "error" && (
              <p className="auth__error" role="alert">
                {auth.error ?? t("auth.errorDefault")}
              </p>
            )}
          </div>
        )}

        {(hasQr || loggingIn) && (
          <div className="auth__qr-zone">
            <div className={`qr-frame${loggingIn ? " qr-frame--busy" : ""}`}>
              <span className="qr-frame__corner qr-frame__corner--tl" />
              <span className="qr-frame__corner qr-frame__corner--tr" />
              <span className="qr-frame__corner qr-frame__corner--bl" />
              <span className="qr-frame__corner qr-frame__corner--br" />
              {hasQr ? (
                <img className="qr-frame__img" src={qrData} alt={t("auth.qrAlt")} />
              ) : (
                <div className="qr-frame__placeholder">
                  <Spinner size={28} />
                </div>
              )}
              {loggingIn && (
                <div className="qr-frame__overlay">
                  <Spinner size={26} />
                  <span>{t("auth.loggingIn")}</span>
                </div>
              )}
            </div>
            <p className="auth__hint">
              {t("auth.qrScanBefore")}
              <strong>{t("auth.qrScanApp")}</strong>
              {t("auth.qrScanAfter")}
            </p>
            <p className="auth__hint auth__hint--dim">{t("auth.qrRefreshHint")}</p>
            {auth.state === "error" && (
              <p className="auth__error" role="alert">
                {auth.error ?? t("auth.errorDefaultShort")}
              </p>
            )}
            <button
              className="btn btn--ghost btn--sm"
              onClick={onStart}
              disabled={starting || loggingIn}
            >
              {t("auth.newCode")}
            </button>
          </div>
        )}
      </main>

      <div className="auth-plinth">
        <div className="auth-plinth__ornament" aria-hidden="true">
          <span className="auth-plinth__orn-line" />
          <span className="auth-plinth__orn-gem" />
          <span className="auth-plinth__orn-line" />
        </div>

        <p className="auth-plinth__eyebrow">{t("auth.legendEyebrow")}</p>

        <p className="auth-plinth__legend">{t("auth.foot")}</p>

        <div className="auth-plinth__tail">
          <span className="auth-plinth__rule" aria-hidden="true" />

          <SiteFooter className="site-foot--auth" />

          <details className="auth__policy">
            <summary>{t("policy.summary")}</summary>
            <div className="auth__policy-body">
              <p className="auth__policy-lead">{t("policy.lead")}</p>

              <p className="auth__policy-open">
                {t("policy.openSourceLead")}{" "}
                <a
                  className="auth__policy-link"
                  href={REPO_URL}
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  <IconGithub width={13} height={13} />
                  {t("policy.openSourceLink")}
                </a>
              </p>

              <h4>{t("policy.h.collect")}</h4>
              <ul>
                <li dangerouslySetInnerHTML={{ __html: t("policy.collect1") }} />
                <li dangerouslySetInnerHTML={{ __html: t("policy.collect2") }} />
                <li dangerouslySetInnerHTML={{ __html: t("policy.collect3") }} />
                <li dangerouslySetInnerHTML={{ __html: t("policy.collect4") }} />
              </ul>

              <h4>{t("policy.h.safety")}</h4>
              <ul>
                <li dangerouslySetInnerHTML={{ __html: t("policy.safety1") }} />
                <li dangerouslySetInnerHTML={{ __html: t("policy.safety2") }} />
                <li dangerouslySetInnerHTML={{ __html: t("policy.safety3") }} />
                <li dangerouslySetInnerHTML={{ __html: t("policy.safety4") }} />
              </ul>

              <h4>{t("policy.h.honest")}</h4>
              <ul>
                <li dangerouslySetInnerHTML={{ __html: t("policy.honest1") }} />
                <li dangerouslySetInnerHTML={{ __html: t("policy.honest2") }} />
              </ul>
            </div>
          </details>
        </div>
      </div>
    </div>
  );
}
