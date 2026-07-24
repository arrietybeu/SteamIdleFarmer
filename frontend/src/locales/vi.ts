/**
 * Vietnamese dictionary. Must mirror every key in `en.ts` — the type in
 * `i18n.tsx` (`Record<keyof typeof en, string>`) fails the build if a key is
 * missing or extra.
 */
import { en } from "./en";

export const vi: Record<keyof typeof en, string> = {
  /* ---------------------------------------------------------------- brand -- */
  "brand.name": "Xưởng Cày",
  "brand.taglineAuth": "Steam idle & achievement farmer",
  "brand.taglineBar": "Steam idle & achievement",

  /* ----------------------------------------------------------------- boot -- */
  "boot.checking": "Đang kiểm tra phiên đăng nhập…",

  /* ------------------------------------------------------- language / foot -- */
  "lang.switcher": "Chuyển ngôn ngữ",
  "footer.madeBy": "Tác giả",
  "footer.source": "Nguồn",

  /* --------------------------------------------------------- global toasts -- */
  "error.serverConnect": "Không kết nối được máy chủ.",
  "toast.gamesLoadError": "Không tải được thư viện game",
  "toast.unlocked": "Đã mở khóa: {name}",
  "toast.qrCreateError": "Không tạo được mã QR",
  "toast.loggedOut": "Đã đăng xuất",
  "toast.maxSelectTitle": "Tối đa {max} game mỗi lần treo",
  "toast.maxSelectDetail": "Bỏ chọn bớt rồi thêm game khác nhé.",
  "toast.close": "Đóng thông báo",

  /* ----------------------------------------------------------------- tabs -- */
  "tabs.aria": "Chức năng",
  "tabs.idle": "Treo tới 100%",
  "tabs.manual": "Mở khóa thủ công",

  /* ------------------------------------------------------------- auth gate -- */
  "auth.intro":
    "Đăng nhập bằng tài khoản Steam của bạn. Mỗi trình duyệt là một phiên riêng — máy ai người nấy cày.",
  "auth.loginButton": "Đăng nhập Steam",
  "auth.errorDefault": "Có lỗi khi đăng nhập. Thử lại nhé.",
  "auth.errorDefaultShort": "Có lỗi khi đăng nhập.",
  "auth.qrAlt": "Mã QR đăng nhập Steam",
  "auth.qrScanBefore": "Mở app ",
  "auth.qrScanApp": "Steam Mobile",
  "auth.qrScanAfter": " → Quét mã QR",
  "auth.qrRefreshHint": "Mã tự làm mới định kỳ — cứ để nguyên màn hình này.",
  "auth.loggingIn": "Đang đăng nhập…",
  "auth.newCode": "Tạo mã mới",
  "auth.legendEyebrow": "Truyền thuyết kể rằng",
  "auth.foot":
    "Tương truyền có một xưởng miệt mài suốt đêm — rèn nên những thành tựu chẳng ai đủ thời gian chạm tới, để người hùng cứ ngủ, tỉnh dậy vinh quang đã sẵn.",

  /* --------------------------------------------------------- privacy policy -- */
  "policy.summary": "Chính sách bảo mật & quyền riêng tư",
  "policy.lead":
    "Web này chỉ dùng lượng dữ liệu tối thiểu để treo game & mở thành tựu giúp bạn — minh bạch đầy đủ dưới đây.",
  "policy.openSourceLead": "Dự án này là mã nguồn mở —",
  "policy.openSourceLink": "xem mã nguồn trên GitHub",
  "policy.h.collect": "Thu thập & lưu gì",
  "policy.collect1":
    "<strong>Token đăng nhập Steam (refresh token)</strong> lấy qua QR — được <strong>mã hoá (AES-GCM)</strong> rồi lưu trên máy chủ, chỉ để giữ phiên và treo game. <em>Không phải mật khẩu</em> — web không bao giờ thấy mật khẩu Steam của bạn.",
  "policy.collect2":
    "<strong>Cookie phiên (<code>sid</code>)</strong>: một mã ngẫu nhiên (httpOnly) để gắn trình duyệt với phiên của bạn. Không chứa thông tin cá nhân.",
  "policy.collect3":
    "<strong>Game, thành tựu, giờ chơi</strong>: lấy từ Steam để hiển thị; các job treo bạn tạo được lưu trong cơ sở dữ liệu của máy chủ.",
  "policy.collect4":
    "<strong>Không</strong> thu thập email, mật khẩu hay dữ liệu cá nhân nào khác. <strong>Không</strong> chia sẻ cho bên thứ ba.",
  "policy.h.safety": "An toàn",
  "policy.safety1":
    "Đăng nhập bằng <strong>QR chính chủ của Steam</strong> — bạn tự xác nhận trên app Steam Mobile.",
  "policy.safety2":
    "Mỗi trình duyệt là <strong>phiên độc lập</strong>; bạn chỉ thao tác được trên tài khoản mình đăng nhập.",
  "policy.safety3":
    "Web <strong>không đụng</strong> game online/anti-cheat; thành tựu protected tự bị bỏ qua.",
  "policy.safety4":
    "Bấm <strong>Đăng xuất</strong> sẽ <strong>xoá</strong> token và các job của bạn khỏi máy chủ.",
  "policy.h.honest": "Lưu ý thành thật",
  "policy.honest1":
    "Token được mã hoá, nhưng <strong>người vận hành máy chủ về mặt kỹ thuật vẫn nắm token</strong> — khi token còn hạn, nó có thể điều khiển tài khoản Steam của bạn. <strong>Chỉ đăng nhập nếu bạn tin người chạy web này.</strong>",
  "policy.honest2":
    "Đây là công cụ tự động treo giờ/thành tựu — <strong>vi phạm điều khoản (ToS) của Steam</strong>, có rủi ro bị Valve giới hạn tài khoản. Tự cân nhắc trước khi dùng.",

  /* --------------------------------------------------------------- top bar -- */
  "topbar.defaultPersona": "Người dùng Steam",
  "topbar.wsOnlineTitle": "Kết nối trực tiếp (WebSocket)",
  "topbar.wsOfflineTitle":
    "Mất kết nối trực tiếp — đang dùng chế độ cập nhật định kỳ",
  "topbar.live": "Trực tiếp",
  "topbar.offline": "Ngoại tuyến",
  "topbar.steamIdTitle": "SteamID: {id}",
  "topbar.logout": "Đăng xuất",

  /* -------------------------------------------------------------- sidebar -- */
  "sidebar.title": "Thư viện",
  "sidebar.count_one": "{count} game",
  "sidebar.count_other": "{count} game",
  "sidebar.searchPlaceholder": "Tìm game…",
  "sidebar.searchAria": "Tìm game trong thư viện",
  "sidebar.hintIdlePrefix": "Bấm để chọn game đem treo — ",
  "sidebar.hintManual": "Bấm một game để xem thành tựu",
  "sidebar.listAria": "Danh sách game",
  "sidebar.emptySearchTitle": "Không tìm thấy game nào",
  "sidebar.emptyTitle": "Thư viện trống",
  "sidebar.emptySearchHint": "Thử từ khóa khác xem sao.",
  "sidebar.emptyHint": "Tài khoản này chưa có game nào hiện được.",
  "sidebar.maxTitle": "Tối đa {max} game mỗi lần",
  "sidebar.noStats": "· không có thành tựu",
  "sidebar.noStatsTitle": "Game không có bảng thành tựu",

  /* ----------------------------------------------------------- idle tab -- */
  "idle.startedTitle_one": "Bắt đầu treo {count} game",
  "idle.startedTitle_other": "Bắt đầu treo {count} game",
  "idle.startedDetail": "Theo dõi tiến độ ở bảng bên dưới.",
  "idle.createError": "Không tạo được job",
  "idle.paused": "Đã tạm dừng",
  "idle.resumed": "Chạy tiếp",
  "idle.actionOk": "{action}: {name}",
  "idle.actionFailed": "Thao tác thất bại: {name}",
  "idle.jobDeleted": "Đã xóa job: {name}",
  "idle.jobDeleteError": "Không xóa được job: {name}",
  "idle.configTitle": "Cấu hình treo",
  "idle.chipCount": "{count}/{max} game",
  "idle.noteEmpty":
    "Chọn game trong <strong>Thư viện</strong> (tối đa {max} game mỗi lần — giới hạn của Steam).",
  "idle.moreGames": "· +{count} game khác",
  "idle.limitWarn": "Đã chạm giới hạn {max} game một lần chạy.",
  "idle.hoursLabel": "Số giờ mỗi game",
  "idle.jitterLabel": "Jitter %",
  "idle.jitterHelp":
    "Xê dịch ngẫu nhiên thời điểm mở khóa để trông tự nhiên hơn",
  "idle.startButton": "Bắt đầu treo",
  "idle.jobsTitle": "Job đang chạy",
  "idle.jobsCount_one": "{count} job",
  "idle.jobsCount_other": "{count} job",
  "idle.jobsEmptyTitle": "Chưa có job nào",
  "idle.jobsEmptyHint":
    "Chọn game rồi bấm “Bắt đầu treo” — tiến độ sẽ hiện ở đây theo thời gian thực.",

  /* --------------------------------------------------------------- job card -- */
  "job.state.pending": "Chờ chạy",
  "job.state.running": "Đang treo",
  "job.state.paused": "Tạm dừng",
  "job.state.completed": "Hoàn tất",
  "job.state.failed": "Lỗi",
  "job.achievements": "Thành tựu",
  "job.playtime": "Giờ chơi",
  "job.nextUnlockIn": "Mở khóa tiếp sau",
  "job.allDone": "Đã đủ 100% thành tựu",
  "job.pausedNote": "Đang tạm dừng",
  "job.failedNote": "Job gặp lỗi",
  "job.doneNote": "Đã xong",
  "job.waitingNote": "Đang chờ lượt",
  "job.playtimeOnly": "Không còn thành tựu để mở — chỉ cộng giờ chơi",
  "job.pause": "Tạm dừng",
  "job.pauseAria": "Tạm dừng {name}",
  "job.resume": "Chạy tiếp",
  "job.resumeAria": "Chạy tiếp {name}",
  "job.delete": "Xóa job",
  "job.deleteAria": "Xóa job {name}",

  /* ------------------------------------------------------------- manual tab -- */
  "manual.loadErrorDefault": "Không tải được thành tựu.",
  "manual.noGameTitle": "Chưa chọn game",
  "manual.noGameHint":
    "Bấm một game trong Thư viện bên trái để xem danh sách thành tựu.",
  "manual.progress": "{unlocked}/{total} thành tựu đã mở",
  "manual.selectAll": "Chọn tất cả (mở được)",
  "manual.unlockButton": "Mở khóa đã chọn",
  "manual.unlockFailed": "Mở khóa thất bại: {name}",
  "manual.requestError": "Không gửi được yêu cầu mở khóa",
  "manual.loadErrorTitle": "Không tải được thành tựu",
  "manual.noAchTitle": "Game này không có thành tựu",
  "manual.noAchHint": "Chọn game khác trong Thư viện nhé.",
  "manual.unlockedTitle": "Đã mở khóa",
  "manual.checkAria": "Chọn thành tựu {name}",
  "manual.hidden": "ẩn",
  "manual.hiddenTitle": "Thành tựu ẩn — mô tả bị Steam giấu",
  "manual.hiddenDesc": "Mô tả được giữ kín.",
  "manual.protectedTitle": "Không thể mở (server-side)",
  "manual.protected": "Protected",
  "manual.unlockedBadge": "Đã mở",

  /* ---------------------------------------------------------- api errors -- */
  "api.networkError": "Không kết nối được máy chủ. Kiểm tra backend đang chạy.",
  "api.serverError": "Lỗi máy chủ (HTTP {status})",

  /* ---------------------------------------------------------------- units -- */
  "unit.hours": "giờ",
  "unit.hoursShort": "g",
};
