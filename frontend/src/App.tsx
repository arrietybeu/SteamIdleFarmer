import { useCallback, useEffect, useRef, useState } from "react";
import type { AuthStatus, Game, Job, WsEvent } from "./types";
import { api } from "./api";
import { connectWs } from "./ws";
import { useToast } from "./toast";
import { useI18n } from "./i18n";
import { AuthGate } from "./components/AuthGate";
import { TopBar } from "./components/TopBar";
import { GameSidebar } from "./components/GameSidebar";
import { IdleTab } from "./components/IdleTab";
import { ManualTab } from "./components/ManualTab";
import { SiteFooter } from "./components/SiteFooter";
import { IconTrophy, Spinner } from "./components/ui";

const MAX_SELECT = 32;
const POLL_MS = 5000;

type Tab = "idle" | "manual";

export default function App() {
  const { push } = useToast();
  const { t } = useI18n();

  const [auth, setAuth] = useState<AuthStatus | null>(null); // null = đang kiểm tra
  const [challengeUrl, setChallengeUrl] = useState<string | null>(null);
  const [startingQr, setStartingQr] = useState(false);
  const [wsOnline, setWsOnline] = useState(false);

  const [games, setGames] = useState<Game[] | null>(null);
  const [jobs, setJobs] = useState<Job[]>([]);
  const [tab, setTab] = useState<Tab>("idle");
  const [selectedIds, setSelectedIds] = useState<Set<number>>(new Set());
  const [manualGame, setManualGame] = useState<Game | null>(null);

  const loggedIn = auth?.state === "logged_in";

  /* ------------------------------------------------ trạng thái ban đầu -- */

  useEffect(() => {
    api
      .authStatus()
      .then(setAuth)
      .catch((err: unknown) => {
        setAuth({
          state: "error",
          error: err instanceof Error ? err.message : t("error.serverConnect"),
        });
      });
  }, [t]);

  /* -------------------------------------------------- nạp dữ liệu app -- */

  const refreshJobs = useCallback(async () => {
    try {
      setJobs(await api.jobs());
    } catch {
      /* WS/poll sẽ thử lại — không làm phiền người dùng */
    }
  }, []);

  useEffect(() => {
    if (loggedIn) {
      setChallengeUrl(null);
      api
        .games()
        .then(setGames)
        .catch((err: unknown) => {
          setGames([]);
          push({
            kind: "error",
            title: t("toast.gamesLoadError"),
            detail: err instanceof Error ? err.message : undefined,
          });
        });
      void refreshJobs();
    } else {
      setGames(null);
      setJobs([]);
      setSelectedIds(new Set());
      setManualGame(null);
    }
  }, [loggedIn, refreshJobs, push, t]);

  /* -------------------------------------------------------- WebSocket -- */

  const gamesRef = useRef<Game[] | null>(null);
  gamesRef.current = games;

  const handleWsEvent = useCallback(
    (event: WsEvent) => {
      switch (event.type) {
        case "auth":
          setAuth(event.status);
          break;
        case "challenge":
          setChallengeUrl(event.challengeUrl);
          break;
        case "jobs":
          setJobs(event.jobs);
          break;
        case "achievement": {
          const game = gamesRef.current?.find((g) => g.appId === event.appId);
          push({
            kind: "gold",
            title: t("toast.unlocked", { name: event.name }),
            detail: game?.name,
          });
          break;
        }
      }
    },
    [push, t],
  );

  const wsHandlerRef = useRef(handleWsEvent);
  wsHandlerRef.current = handleWsEvent;

  useEffect(() => {
    const dispose = connectWs(
      (event) => wsHandlerRef.current(event),
      setWsOnline,
    );
    return dispose;
  }, []);

  /* ------------------------------------------- polling dự phòng (5 s) -- */

  const loggedInRef = useRef(loggedIn);
  loggedInRef.current = loggedIn;

  useEffect(() => {
    const timer = setInterval(() => {
      api.authStatus().then(setAuth).catch(() => {});
      if (loggedInRef.current) void refreshJobs();
    }, POLL_MS);
    return () => clearInterval(timer);
  }, [refreshJobs]);

  /* ----------------------------------------------------------- actions -- */

  async function startQr() {
    setStartingQr(true);
    try {
      const res = await api.qrStart();
      setChallengeUrl(res.challengeUrl);
      setAuth((prev) =>
        prev && prev.state !== "logged_in"
          ? { ...prev, state: "awaiting_qr", error: undefined }
          : prev,
      );
    } catch (err) {
      push({
        kind: "error",
        title: t("toast.qrCreateError"),
        detail: err instanceof Error ? err.message : undefined,
      });
    } finally {
      setStartingQr(false);
    }
  }

  async function logout() {
    try {
      await api.logout();
    } catch {
      /* phiên cũng sẽ được coi là đã thoát ở phía client */
    }
    setAuth({ state: "disconnected" });
    setChallengeUrl(null);
    push({ kind: "info", title: t("toast.loggedOut") });
  }

  function toggleIdleGame(game: Game) {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(game.appId)) {
        next.delete(game.appId);
      } else if (next.size >= MAX_SELECT) {
        push({
          kind: "error",
          title: t("toast.maxSelectTitle", { max: MAX_SELECT }),
          detail: t("toast.maxSelectDetail"),
        });
        return prev;
      } else {
        next.add(game.appId);
      }
      return next;
    });
  }

  function pickManualGame(game: Game) {
    setManualGame(game);
  }

  /* ------------------------------------------------------------ render -- */

  if (auth === null) {
    return (
      <div className="boot">
        <span className="brand-mark brand-mark--lg">
          <IconTrophy width={26} height={26} />
        </span>
        <Spinner size={22} />
        <p>{t("boot.checking")}</p>
      </div>
    );
  }

  if (!loggedIn) {
    return (
      <AuthGate
        auth={auth}
        challengeUrl={challengeUrl}
        starting={startingQr}
        onStart={() => void startQr()}
      />
    );
  }

  return (
    <div className="shell">
      <TopBar auth={auth} wsOnline={wsOnline} onLogout={() => void logout()} />

      <div className="shell__body">
        <GameSidebar
          games={games}
          mode={tab}
          selectedIds={selectedIds}
          manualGameId={manualGame?.appId ?? null}
          maxSelect={MAX_SELECT}
          onToggleIdle={toggleIdleGame}
          onPickManual={pickManualGame}
        />

        <main className="main">
          <nav className="tabs" role="tablist" aria-label={t("tabs.aria")}>
            <button
              role="tab"
              aria-selected={tab === "idle"}
              className={`tabs__tab${tab === "idle" ? " tabs__tab--active" : ""}`}
              onClick={() => setTab("idle")}
            >
              {t("tabs.idle")}
              {jobs.length > 0 && <span className="tabs__pill">{jobs.length}</span>}
            </button>
            <button
              role="tab"
              aria-selected={tab === "manual"}
              className={`tabs__tab${tab === "manual" ? " tabs__tab--active" : ""}`}
              onClick={() => setTab("manual")}
            >
              {t("tabs.manual")}
            </button>
          </nav>

          {tab === "idle" ? (
            <IdleTab
              games={games}
              selectedIds={selectedIds}
              maxSelect={MAX_SELECT}
              jobs={jobs}
              onClearSelection={() => setSelectedIds(new Set())}
              onJobsChanged={setJobs}
              refreshJobs={refreshJobs}
            />
          ) : (
            <ManualTab game={manualGame} />
          )}
        </main>
      </div>

      <SiteFooter className="site-foot--app" />
    </div>
  );
}
