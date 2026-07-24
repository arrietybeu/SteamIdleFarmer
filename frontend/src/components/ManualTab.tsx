import { useCallback, useEffect, useState } from "react";
import type { Achievement, Game } from "../types";
import { api } from "../api";
import { useToast } from "../toast";
import { useI18n } from "../i18n";
import {
  Badge,
  EmptyState,
  GameIcon,
  IconCheck,
  IconLock,
  IconTrophy,
  Skeleton,
  Spinner,
} from "./ui";

interface Props {
  game: Game | null;
}

/** Tab "Mở khóa thủ công": danh sách thành tựu của game, tick chọn rồi mở khóa (kiểu SAM). */
export function ManualTab({ game }: Props) {
  const { push } = useToast();
  const { t } = useI18n();
  const [achievements, setAchievements] = useState<Achievement[] | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [checked, setChecked] = useState<Set<string>>(new Set());
  const [unlocking, setUnlocking] = useState(false);

  const load = useCallback(
    async (appId: number) => {
      setAchievements(null);
      setLoadError(null);
      setChecked(new Set());
      try {
        const list = await api.achievements(appId);
        setAchievements(list);
      } catch (err) {
        setLoadError(
          err instanceof Error ? err.message : t("manual.loadErrorDefault"),
        );
        setAchievements([]);
      }
    },
    [t],
  );

  useEffect(() => {
    if (game) void load(game.appId);
    else {
      setAchievements(null);
      setChecked(new Set());
      setLoadError(null);
    }
  }, [game, load]);

  if (!game) {
    return (
      <EmptyState
        icon={<IconTrophy width={30} height={30} />}
        title={t("manual.noGameTitle")}
        hint={t("manual.noGameHint")}
      />
    );
  }

  const unlockable =
    achievements?.filter((a) => !a.unlocked && !a.protected) ?? [];
  const allChecked =
    unlockable.length > 0 && unlockable.every((a) => checked.has(a.apiName));
  const unlockedCount = achievements?.filter((a) => a.unlocked).length ?? 0;

  function toggleOne(apiName: string) {
    setChecked((prev) => {
      const next = new Set(prev);
      if (next.has(apiName)) next.delete(apiName);
      else next.add(apiName);
      return next;
    });
  }

  function toggleAll() {
    setChecked(
      allChecked ? new Set() : new Set(unlockable.map((a) => a.apiName)),
    );
  }

  async function unlockSelected() {
    if (!game || checked.size === 0 || unlocking) return;
    setUnlocking(true);
    try {
      const nameOf = new Map(
        (achievements ?? []).map((a) => [a.apiName, a.displayName]),
      );
      const res = await api.unlockAchievements(game.appId, [...checked]);
      for (const r of res.results) {
        const display = nameOf.get(r.apiName) ?? r.apiName;
        if (r.ok) {
          push({ kind: "gold", title: t("toast.unlocked", { name: display }) });
        } else {
          push({
            kind: "error",
            title: t("manual.unlockFailed", { name: display }),
            detail: r.error,
          });
        }
      }
      await load(game.appId);
    } catch (err) {
      push({
        kind: "error",
        title: t("manual.requestError"),
        detail: err instanceof Error ? err.message : undefined,
      });
    } finally {
      setUnlocking(false);
    }
  }

  return (
    <div className="manual-tab">
      <section className="panel glass manual-tab__toolbar">
        <div className="manual-tab__game">
          <GameIcon name={game.name} iconUrl={game.iconUrl} />
          <div>
            <h2 className="panel__title">{game.name}</h2>
            {achievements && (
              <p className="panel__note">
                {t("manual.progress", {
                  unlocked: unlockedCount,
                  total: achievements.length,
                })}
              </p>
            )}
          </div>
        </div>

        <div className="manual-tab__actions">
          <label
            className={`check-toggle${unlockable.length === 0 ? " check-toggle--off" : ""}`}
          >
            <input
              type="checkbox"
              checked={allChecked}
              disabled={unlockable.length === 0}
              onChange={toggleAll}
            />
            <span>{t("manual.selectAll")}</span>
          </label>
          <button
            className="btn btn--gold"
            onClick={unlockSelected}
            disabled={checked.size === 0 || unlocking}
          >
            {unlocking ? <Spinner /> : <IconTrophy width={17} height={17} />}
            {t("manual.unlockButton")}
            {checked.size > 0 && <span className="btn__count">{checked.size}</span>}
          </button>
        </div>
      </section>

      <section className="ach-list glass">
        {achievements === null &&
          Array.from({ length: 6 }, (_, i) => (
            <div key={i} className="ach-row ach-row--skeleton">
              <Skeleton className="skeleton--icon skeleton--icon-lg" />
              <div className="ach-row__meta">
                <Skeleton className="skeleton--line" />
                <Skeleton className="skeleton--line skeleton--short" />
              </div>
            </div>
          ))}

        {achievements !== null && loadError && (
          <EmptyState
            icon={<IconTrophy width={30} height={30} />}
            title={t("manual.loadErrorTitle")}
            hint={loadError}
          />
        )}

        {achievements !== null && !loadError && achievements.length === 0 && (
          <EmptyState
            icon={<IconTrophy width={30} height={30} />}
            title={t("manual.noAchTitle")}
            hint={t("manual.noAchHint")}
          />
        )}

        {achievements?.map((a) => {
          const isChecked = checked.has(a.apiName);
          const disabled = a.unlocked || a.protected;
          return (
            <label
              key={a.apiName}
              className={[
                "ach-row",
                a.unlocked ? "ach-row--unlocked" : "",
                a.protected && !a.unlocked ? "ach-row--protected" : "",
                isChecked ? "ach-row--checked" : "",
              ]
                .filter(Boolean)
                .join(" ")}
            >
              <span className="ach-row__check">
                {a.unlocked ? (
                  <span className="ach-row__done" title={t("manual.unlockedTitle")}>
                    <IconCheck width={15} height={15} />
                  </span>
                ) : (
                  <input
                    type="checkbox"
                    checked={isChecked}
                    disabled={disabled || unlocking}
                    onChange={() => toggleOne(a.apiName)}
                    aria-label={t("manual.checkAria", { name: a.displayName })}
                  />
                )}
              </span>

              {a.iconUrl ? (
                <img
                  className={`ach-row__icon${a.unlocked ? "" : " ach-row__icon--dim"}`}
                  src={a.iconUrl}
                  alt=""
                  loading="lazy"
                />
              ) : (
                <span className="ach-row__icon ach-row__icon--fallback">
                  <IconTrophy width={18} height={18} />
                </span>
              )}

              <span className="ach-row__meta">
                <span className="ach-row__name">
                  {a.displayName}
                  {a.hidden && (
                    <span className="ach-row__hidden" title={t("manual.hiddenTitle")}>
                      {t("manual.hidden")}
                    </span>
                  )}
                </span>
                <span className="ach-row__desc">
                  {a.description || (a.hidden ? t("manual.hiddenDesc") : "—")}
                </span>
              </span>

              {a.protected && !a.unlocked && (
                <Badge tone="danger" title={t("manual.protectedTitle")}>
                  <IconLock width={12} height={12} />
                  {t("manual.protected")}
                </Badge>
              )}
              {a.unlocked && <Badge tone="gold">{t("manual.unlockedBadge")}</Badge>}
            </label>
          );
        })}
      </section>
    </div>
  );
}
