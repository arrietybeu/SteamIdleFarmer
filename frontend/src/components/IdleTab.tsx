import { useState } from "react";
import type { Game, Job } from "../types";
import { api } from "../api";
import { useToast } from "../toast";
import { useI18n } from "../i18n";
import { JobCard } from "./JobCard";
import { EmptyState, IconPlay, IconTimer, Spinner } from "./ui";

interface Props {
  games: Game[] | null;
  selectedIds: Set<number>;
  maxSelect: number;
  jobs: Job[];
  onClearSelection: () => void;
  onJobsChanged: (jobs: Job[]) => void;
  refreshJobs: () => Promise<void>;
}

/** Tab "Treo tới 100%": form tạo job + bảng điều khiển job đang chạy. */
export function IdleTab({
  games,
  selectedIds,
  maxSelect,
  jobs,
  onClearSelection,
  onJobsChanged,
  refreshJobs,
}: Props) {
  const { push } = useToast();
  const { t } = useI18n();
  const [hoursPerGame, setHoursPerGame] = useState(200);
  const [jitterPct, setJitterPct] = useState(10);
  const [starting, setStarting] = useState(false);
  const [busyJobIds, setBusyJobIds] = useState<Set<string>>(new Set());

  const atLimit = selectedIds.size >= maxSelect;
  const selectedNames =
    games?.filter((g) => selectedIds.has(g.appId)).map((g) => g.name) ?? [];

  async function startJobs() {
    if (selectedIds.size === 0 || starting) return;
    setStarting(true);
    try {
      const res = await api.createJobs(
        [...selectedIds],
        hoursPerGame,
        jitterPct,
      );
      push({
        kind: "gold",
        title: t(
          res.jobs.length === 1 ? "idle.startedTitle_one" : "idle.startedTitle_other",
          { count: res.jobs.length },
        ),
        detail: t("idle.startedDetail"),
      });
      onClearSelection();
      await refreshJobs();
    } catch (err) {
      push({
        kind: "error",
        title: t("idle.createError"),
        detail: err instanceof Error ? err.message : undefined,
      });
    } finally {
      setStarting(false);
    }
  }

  function markBusy(id: string, busy: boolean) {
    setBusyJobIds((prev) => {
      const next = new Set(prev);
      if (busy) next.add(id);
      else next.delete(id);
      return next;
    });
  }

  async function jobAction(
    job: Job,
    action: (id: string) => Promise<void>,
    okAction: string,
  ) {
    markBusy(job.id, true);
    try {
      await action(job.id);
      push({
        kind: "info",
        title: t("idle.actionOk", { action: okAction, name: job.name }),
      });
      await refreshJobs();
    } catch (err) {
      push({
        kind: "error",
        title: t("idle.actionFailed", { name: job.name }),
        detail: err instanceof Error ? err.message : undefined,
      });
    } finally {
      markBusy(job.id, false);
    }
  }

  async function deleteJob(job: Job) {
    markBusy(job.id, true);
    try {
      await api.deleteJob(job.id);
      push({ kind: "info", title: t("idle.jobDeleted", { name: job.name }) });
      onJobsChanged(jobs.filter((j) => j.id !== job.id));
      await refreshJobs();
    } catch (err) {
      push({
        kind: "error",
        title: t("idle.jobDeleteError", { name: job.name }),
        detail: err instanceof Error ? err.message : undefined,
      });
    } finally {
      markBusy(job.id, false);
    }
  }

  return (
    <div className="idle-tab">
      <section className="panel glass">
        <div className="panel__head">
          <h2 className="panel__title">{t("idle.configTitle")}</h2>
          <span className={`chip${atLimit ? " chip--warn" : ""}`}>
            {t("idle.chipCount", { count: selectedIds.size, max: maxSelect })}
          </span>
        </div>

        {selectedIds.size === 0 ? (
          <p
            className="panel__note"
            dangerouslySetInnerHTML={{
              __html: t("idle.noteEmpty", { max: maxSelect }),
            }}
          />
        ) : (
          <p className="panel__note panel__note--names" title={selectedNames.join(", ")}>
            {selectedNames.slice(0, 4).join(" · ")}
            {selectedNames.length > 4 &&
              ` ${t("idle.moreGames", { count: selectedNames.length - 4 })}`}
          </p>
        )}
        {atLimit && (
          <p className="panel__warn" role="alert">
            {t("idle.limitWarn", { max: maxSelect })}
          </p>
        )}

        <div className="idle-form">
          <label className="field">
            <span className="field__label">{t("idle.hoursLabel")}</span>
            <input
              className="field__input num"
              type="number"
              min={1}
              max={10000}
              value={hoursPerGame}
              onChange={(e) =>
                setHoursPerGame(
                  Math.max(1, Math.min(10000, Number(e.target.value) || 1)),
                )
              }
            />
          </label>
          <label className="field">
            <span className="field__label">
              {t("idle.jitterLabel")}{" "}
              <span className="field__help" title={t("idle.jitterHelp")}>
                ?
              </span>
            </span>
            <input
              className="field__input num"
              type="number"
              min={0}
              max={100}
              value={jitterPct}
              onChange={(e) =>
                setJitterPct(
                  Math.max(0, Math.min(100, Number(e.target.value) || 0)),
                )
              }
            />
          </label>
          <button
            className="btn btn--gold"
            onClick={startJobs}
            disabled={selectedIds.size === 0 || starting}
          >
            {starting ? <Spinner /> : <IconPlay width={17} height={17} />}
            {t("idle.startButton")}
          </button>
        </div>
      </section>

      <section className="jobs">
        <div className="jobs__head">
          <h2 className="panel__title">{t("idle.jobsTitle")}</h2>
          {jobs.length > 0 && (
            <span className="jobs__count">
              {t(jobs.length === 1 ? "idle.jobsCount_one" : "idle.jobsCount_other", {
                count: jobs.length,
              })}
            </span>
          )}
        </div>

        {jobs.length === 0 ? (
          <EmptyState
            icon={<IconTimer width={30} height={30} />}
            title={t("idle.jobsEmptyTitle")}
            hint={t("idle.jobsEmptyHint")}
          />
        ) : (
          <div className="jobs__grid">
            {jobs.map((job) => (
              <JobCard
                key={job.id}
                job={job}
                busy={busyJobIds.has(job.id)}
                onPause={(j) => jobAction(j, api.pauseJob, t("idle.paused"))}
                onResume={(j) => jobAction(j, api.resumeJob, t("idle.resumed"))}
                onDelete={deleteJob}
              />
            ))}
          </div>
        )}
      </section>
    </div>
  );
}
