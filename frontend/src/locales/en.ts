/**
 * English dictionary (default language).
 * This object defines the full set of translation keys — `vi.ts` must mirror it
 * exactly (enforced by the type in `i18n.tsx`).
 *
 * `{name}` style placeholders are filled in at runtime by `t(key, params)`.
 * A handful of values contain inline HTML (<strong>, <em>, <code>) and are
 * rendered with `dangerouslySetInnerHTML` — the content is authored here, never
 * user input, so it is safe.
 */
export const en = {
  /* ---------------------------------------------------------------- brand -- */
  "brand.name": "Grind Forge",
  "brand.taglineAuth": "Steam idle & achievement farmer",
  "brand.taglineBar": "Steam idle & achievement",

  /* ----------------------------------------------------------------- boot -- */
  "boot.checking": "Checking your session…",

  /* ------------------------------------------------------- language / foot -- */
  "lang.switcher": "Language",
  "footer.madeBy": "Made by",
  "footer.source": "Source",

  /* --------------------------------------------------------- global toasts -- */
  "error.serverConnect": "Couldn't reach the server.",
  "toast.gamesLoadError": "Couldn't load your game library",
  "toast.unlocked": "Unlocked: {name}",
  "toast.qrCreateError": "Couldn't generate a QR code",
  "toast.loggedOut": "Logged out",
  "toast.maxSelectTitle": "Up to {max} games per idle run",
  "toast.maxSelectDetail": "Deselect some before adding others.",
  "toast.close": "Dismiss notification",

  /* ----------------------------------------------------------------- tabs -- */
  "tabs.aria": "Features",
  "tabs.idle": "Idle to 100%",
  "tabs.manual": "Manual unlock",

  /* ------------------------------------------------------------- auth gate -- */
  "auth.intro":
    "Sign in with your Steam account. Each browser is its own separate session — everyone farms on their own machine.",
  "auth.loginButton": "Sign in with Steam",
  "auth.errorDefault": "Something went wrong while signing in. Please try again.",
  "auth.errorDefaultShort": "Something went wrong while signing in.",
  "auth.qrAlt": "Steam login QR code",
  "auth.qrScanBefore": "Open the ",
  "auth.qrScanApp": "Steam Mobile",
  "auth.qrScanAfter": " app → Scan the QR code",
  "auth.qrRefreshHint":
    "The code refreshes periodically — just leave this screen open.",
  "auth.loggingIn": "Signing in…",
  "auth.newCode": "Generate a new code",
  "auth.legendEyebrow": "The Legend",
  "auth.foot":
    "They say a workshop toils through the night — forging trophies no mortal has time to earn, so heroes may sleep and wake to glory already won.",

  /* --------------------------------------------------------- privacy policy -- */
  "policy.summary": "Privacy & security policy",
  "policy.lead":
    "This site uses only the minimum data needed to idle your games & unlock achievements for you — full transparency below.",
  "policy.openSourceLead": "This project is open source —",
  "policy.openSourceLink": "view the source on GitHub",
  "policy.h.collect": "What we collect & store",
  "policy.collect1":
    "<strong>Steam login token (refresh token)</strong> obtained via QR — it is <strong>encrypted (AES-GCM)</strong> and stored on the server, only to keep your session alive and idle your games. <em>It is not your password</em> — this site never sees your Steam password.",
  "policy.collect2":
    "<strong>Session cookie (<code>sid</code>)</strong>: a random, httpOnly token that ties your browser to your session. It holds no personal information.",
  "policy.collect3":
    "<strong>Games, achievements, playtime</strong>: fetched from Steam for display; the idle jobs you create are saved in the server's database.",
  "policy.collect4":
    "We do <strong>not</strong> collect your email, password, or any other personal data. We do <strong>not</strong> share anything with third parties.",
  "policy.h.safety": "Safety",
  "policy.safety1":
    "You sign in with <strong>Steam's own official QR flow</strong> — you confirm it yourself in the Steam Mobile app.",
  "policy.safety2":
    "Each browser is an <strong>independent session</strong>; you can only act on the account you signed in with.",
  "policy.safety3":
    "This tool <strong>does not touch</strong> online / anti-cheat games; protected achievements are skipped automatically.",
  "policy.safety4":
    "Clicking <strong>Log out</strong> <strong>deletes</strong> your token and your jobs from the server.",
  "policy.h.honest": "Honest disclaimer",
  "policy.honest1":
    "The token is encrypted, but <strong>whoever operates the server technically still holds it</strong> — while the token is valid it can control your Steam account. <strong>Only sign in if you trust whoever runs this site.</strong>",
  "policy.honest2":
    "This is a tool that automatically idles playtime / unlocks achievements — it <strong>violates Steam's Terms of Service</strong> and carries a risk of Valve limiting your account. Consider carefully before using it.",

  /* --------------------------------------------------------------- top bar -- */
  "topbar.defaultPersona": "Steam user",
  "topbar.wsOnlineTitle": "Live connection (WebSocket)",
  "topbar.wsOfflineTitle":
    "Live connection lost — falling back to periodic updates",
  "topbar.live": "Live",
  "topbar.offline": "Offline",
  "topbar.steamIdTitle": "SteamID: {id}",
  "topbar.logout": "Log out",

  /* -------------------------------------------------------------- sidebar -- */
  "sidebar.title": "Library",
  "sidebar.count_one": "1 game",
  "sidebar.count_other": "{count} games",
  "sidebar.searchPlaceholder": "Search games…",
  "sidebar.searchAria": "Search games in the library",
  "sidebar.hintIdlePrefix": "Click to pick games to idle — ",
  "sidebar.hintManual": "Click a game to see its achievements",
  "sidebar.listAria": "Game list",
  "sidebar.emptySearchTitle": "No games found",
  "sidebar.emptyTitle": "Library is empty",
  "sidebar.emptySearchHint": "Try a different search term.",
  "sidebar.emptyHint": "This account has no games to show yet.",
  "sidebar.maxTitle": "Up to {max} games at a time",
  "sidebar.noStats": "· no achievements",
  "sidebar.noStatsTitle": "This game has no achievements",

  /* ----------------------------------------------------------- idle tab -- */
  "idle.startedTitle_one": "Started idling 1 game",
  "idle.startedTitle_other": "Started idling {count} games",
  "idle.startedDetail": "Track progress in the panel below.",
  "idle.createError": "Couldn't create the job",
  "idle.paused": "Paused",
  "idle.resumed": "Resumed",
  "idle.actionOk": "{action}: {name}",
  "idle.actionFailed": "Action failed: {name}",
  "idle.jobDeleted": "Job deleted: {name}",
  "idle.jobDeleteError": "Couldn't delete job: {name}",
  "idle.configTitle": "Idle settings",
  "idle.chipCount": "{count}/{max} games",
  "idle.noteEmpty":
    "Pick games from the <strong>Library</strong> (up to {max} at a time — Steam's limit).",
  "idle.moreGames": "· +{count} more",
  "idle.limitWarn": "You've hit the limit of {max} games per run.",
  "idle.hoursLabel": "Hours per game",
  "idle.jitterLabel": "Jitter %",
  "idle.jitterHelp": "Randomly shifts unlock timing so it looks more natural",
  "idle.startButton": "Start idling",
  "idle.jobsTitle": "Running jobs",
  "idle.jobsCount_one": "1 job",
  "idle.jobsCount_other": "{count} jobs",
  "idle.jobsEmptyTitle": "No jobs yet",
  "idle.jobsEmptyHint":
    "Pick games and hit “Start idling” — progress will show here in real time.",

  /* --------------------------------------------------------------- job card -- */
  "job.state.pending": "Pending",
  "job.state.running": "Idling",
  "job.state.paused": "Paused",
  "job.state.completed": "Completed",
  "job.state.failed": "Failed",
  "job.achievements": "Achievements",
  "job.playtime": "Playtime",
  "job.nextUnlockIn": "Next unlock in",
  "job.allDone": "100% of achievements unlocked",
  "job.pausedNote": "Paused",
  "job.failedNote": "Job ran into an error",
  "job.doneNote": "Done",
  "job.waitingNote": "Waiting its turn",
  "job.playtimeOnly": "Nothing left to unlock — accruing playtime only",
  "job.pause": "Pause",
  "job.pauseAria": "Pause {name}",
  "job.resume": "Resume",
  "job.resumeAria": "Resume {name}",
  "job.delete": "Delete job",
  "job.deleteAria": "Delete job {name}",

  /* ------------------------------------------------------------- manual tab -- */
  "manual.loadErrorDefault": "Couldn't load achievements.",
  "manual.noGameTitle": "No game selected",
  "manual.noGameHint":
    "Click a game in the Library on the left to see its achievements.",
  "manual.progress": "{unlocked}/{total} achievements unlocked",
  "manual.selectAll": "Select all (unlockable)",
  "manual.unlockButton": "Unlock selected",
  "manual.unlockFailed": "Unlock failed: {name}",
  "manual.requestError": "Couldn't send the unlock request",
  "manual.loadErrorTitle": "Couldn't load achievements",
  "manual.noAchTitle": "This game has no achievements",
  "manual.noAchHint": "Pick another game from the Library.",
  "manual.unlockedTitle": "Unlocked",
  "manual.checkAria": "Select achievement {name}",
  "manual.hidden": "hidden",
  "manual.hiddenTitle": "Hidden achievement — Steam conceals its description",
  "manual.hiddenDesc": "Description kept secret.",
  "manual.protectedTitle": "Cannot be unlocked (server-side)",
  "manual.protected": "Protected",
  "manual.unlockedBadge": "Unlocked",

  /* ---------------------------------------------------------- api errors -- */
  "api.networkError":
    "Couldn't reach the server. Make sure the backend is running.",
  "api.serverError": "Server error (HTTP {status})",

  /* ---------------------------------------------------------------- units -- */
  "unit.hours": "h",
  "unit.hoursShort": "h",
};
