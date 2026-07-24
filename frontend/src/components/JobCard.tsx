import { useEffect, useState } from "react";
import type { Job, JobState } from "../types";
import { formatCountdown, formatHours } from "../format";
import { useI18n } from "../i18n";
import type { TranslationKey } from "../i18n";
import { Badge, IconPause, IconPlay, IconTimer, IconTrash, ProgressBar } from "./ui";

const STATE_LABEL_KEY: Record<JobState, TranslationKey> = {
  pending: "job.state.pending",
  running: "job.state.running",
  paused: "job.state.paused",
  completed: "job.state.completed",
  failed: "job.state.failed",
};

const STATE_TONE: Record<JobState, "gold" | "ok" | "danger" | "info" | "muted"> = {
  pending: "info",
  running: "ok",
  paused: "muted",
  completed: "gold",
  failed: "danger",
};

interface Props {
  job: Job;
  busy: boolean;
  onPause: (job: Job) => void;
  onResume: (job: Job) => void;
  onDelete: (job: Job) => void;
}

export function JobCard({ job, busy, onPause, onResume, onDelete }: Props) {
  const { t, lang } = useI18n();
  // Đếm ngược cục bộ mỗi giây giữa hai lần server cập nhật.
  const [countdown, setCountdown] = useState<number | null>(
    job.nextUnlockInSec ?? null,
  );

  useEffect(() => {
    setCountdown(job.nextUnlockInSec ?? null);
  }, [job.nextUnlockInSec, job.unlockedCount, job.state]);

  const hasCountdown = countdown !== null;
  useEffect(() => {
    if (!hasCountdown || job.state !== "running") return;
    const timer = setInterval(() => {
      setCountdown((c) => (c === null ? null : Math.max(0, c - 1)));
    }, 1000);
    return () => clearInterval(timer);
  }, [hasCountdown, job.state]);

  const achievementsDone =
    job.totalAchievable > 0 && job.unlockedCount >= job.totalAchievable;

  return (
    <article className={`job-card glass job-card--${job.state}`}>
      <header className="job-card__head">
        <h3 className="job-card__name" title={job.name}>
          {job.name}
        </h3>
        <Badge tone={STATE_TONE[job.state]}>{t(STATE_LABEL_KEY[job.state])}</Badge>
      </header>

      <div className="job-card__stats">
        <div className="job-card__stat">
          <span className="job-card__stat-label">{t("job.achievements")}</span>
          <span className="job-card__stat-value num">
            {job.unlockedCount}
            <span className="job-card__stat-dim">/{job.totalAchievable}</span>
          </span>
        </div>
        <div className="job-card__stat">
          <span className="job-card__stat-label">{t("job.playtime")}</span>
          <span className="job-card__stat-value num">
            {formatHours(job.hoursElapsed, lang, t("unit.hours"))}
            <span className="job-card__stat-dim">
              {" "}/ {formatHours(job.hoursTarget, lang, t("unit.hours"))}
            </span>
          </span>
        </div>
      </div>

      <ProgressBar
        value={job.unlockedCount}
        max={job.totalAchievable}
        gold={achievementsDone || job.state === "completed"}
      />

      <div className="job-card__foot">
        <span className="job-card__next">
          <IconTimer width={15} height={15} />
          {job.state === "running" && countdown !== null ? (
            <>
              {t("job.nextUnlockIn")}{" "}
              <strong className="num">
                {formatCountdown(countdown, t("unit.hoursShort"))}
              </strong>
            </>
          ) : achievementsDone ? (
            t("job.allDone")
          ) : job.state === "paused" ? (
            t("job.pausedNote")
          ) : job.state === "failed" ? (
            t("job.failedNote")
          ) : job.state === "completed" ? (
            t("job.doneNote")
          ) : job.totalAchievable === 0 ? (
            // Idling normally, there is just nothing to drip (all protected or already unlocked).
            t("job.playtimeOnly")
          ) : (
            t("job.waitingNote")
          )}
        </span>

        <div className="job-card__actions">
          {(job.state === "running" || job.state === "pending") && (
            <button
              className="icon-btn"
              onClick={() => onPause(job)}
              disabled={busy}
              title={t("job.pause")}
              aria-label={t("job.pauseAria", { name: job.name })}
            >
              <IconPause width={16} height={16} />
            </button>
          )}
          {job.state === "paused" && (
            <button
              className="icon-btn icon-btn--ok"
              onClick={() => onResume(job)}
              disabled={busy}
              title={t("job.resume")}
              aria-label={t("job.resumeAria", { name: job.name })}
            >
              <IconPlay width={16} height={16} />
            </button>
          )}
          <button
            className="icon-btn icon-btn--danger"
            onClick={() => onDelete(job)}
            disabled={busy}
            title={t("job.delete")}
            aria-label={t("job.deleteAria", { name: job.name })}
          >
            <IconTrash width={16} height={16} />
          </button>
        </div>
      </div>
    </article>
  );
}
