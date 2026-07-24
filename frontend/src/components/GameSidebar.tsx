import { useMemo, useState } from "react";
import type { Game } from "../types";
import { formatHours } from "../format";
import { useI18n } from "../i18n";
import { EmptyState, GameIcon, IconCheck, IconGamepad, IconSearch, Skeleton } from "./ui";

interface Props {
  games: Game[] | null; // null = đang tải
  mode: "idle" | "manual";
  selectedIds: Set<number>;
  manualGameId: number | null;
  maxSelect: number;
  onToggleIdle: (game: Game) => void;
  onPickManual: (game: Game) => void;
}

/** Thư viện game bên trái: tìm kiếm + chọn (đa chọn ở tab treo, đơn chọn ở tab thủ công). */
export function GameSidebar({
  games,
  mode,
  selectedIds,
  manualGameId,
  maxSelect,
  onToggleIdle,
  onPickManual,
}: Props) {
  const { t, lang } = useI18n();
  const [query, setQuery] = useState("");

  const filtered = useMemo(() => {
    if (!games) return null;
    const q = query.trim().toLowerCase();
    const list = q
      ? games.filter((g) => g.name.toLowerCase().includes(q))
      : games;
    return [...list].sort((a, b) => b.playtimeHours - a.playtimeHours);
  }, [games, query]);

  const atLimit = selectedIds.size >= maxSelect;

  return (
    <aside className="sidebar glass">
      <div className="sidebar__head">
        <h2 className="sidebar__title">{t("sidebar.title")}</h2>
        {games && (
          <span className="sidebar__count">
            {t(games.length === 1 ? "sidebar.count_one" : "sidebar.count_other", {
              count: games.length,
            })}
          </span>
        )}
      </div>

      <label className="search">
        <IconSearch width={16} height={16} />
        <input
          type="search"
          placeholder={t("sidebar.searchPlaceholder")}
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          aria-label={t("sidebar.searchAria")}
        />
      </label>

      <p className="sidebar__hint">
        {mode === "idle" ? (
          <>
            {t("sidebar.hintIdlePrefix")}
            <strong className={atLimit ? "text-warn" : ""}>
              {selectedIds.size}/{maxSelect}
            </strong>
          </>
        ) : (
          <>{t("sidebar.hintManual")}</>
        )}
      </p>

      <div className="sidebar__list" role="listbox" aria-label={t("sidebar.listAria")}>
        {filtered === null &&
          Array.from({ length: 8 }, (_, i) => (
            <div key={i} className="game-row game-row--skeleton">
              <Skeleton className="skeleton--icon" />
              <div className="game-row__meta">
                <Skeleton className="skeleton--line" />
                <Skeleton className="skeleton--line skeleton--short" />
              </div>
            </div>
          ))}

        {filtered !== null && filtered.length === 0 && (
          <EmptyState
            icon={<IconGamepad width={30} height={30} />}
            title={query ? t("sidebar.emptySearchTitle") : t("sidebar.emptyTitle")}
            hint={query ? t("sidebar.emptySearchHint") : t("sidebar.emptyHint")}
          />
        )}

        {filtered?.map((game) => {
          const isSelected =
            mode === "idle"
              ? selectedIds.has(game.appId)
              : manualGameId === game.appId;
          const disabled =
            mode === "idle" && !isSelected && atLimit;
          return (
            <button
              key={game.appId}
              className={`game-row${isSelected ? " game-row--selected" : ""}`}
              role="option"
              aria-selected={isSelected}
              disabled={disabled}
              title={disabled ? t("sidebar.maxTitle", { max: maxSelect }) : undefined}
              onClick={() =>
                mode === "idle" ? onToggleIdle(game) : onPickManual(game)
              }
            >
              <GameIcon name={game.name} iconUrl={game.iconUrl} />
              <span className="game-row__meta">
                <span className="game-row__name">{game.name}</span>
                <span className="game-row__sub">
                  {formatHours(game.playtimeHours, lang, t("unit.hours"))}
                  {!game.hasStats && (
                    <span
                      className="game-row__nostats"
                      title={t("sidebar.noStatsTitle")}
                    >
                      {t("sidebar.noStats")}
                    </span>
                  )}
                </span>
              </span>
              {isSelected && (
                <span className="game-row__tick" aria-hidden="true">
                  <IconCheck width={14} height={14} />
                </span>
              )}
            </button>
          );
        })}
      </div>
    </aside>
  );
}
