using System.Collections.Generic;
using UnityEngine;

public static class LocalizationManager
{
    public enum Language { English, Vietnamese }
    public static Language CurrentLanguage { get; private set; } = Language.English;

    private static readonly Dictionary<Language, Dictionary<string, string>> _localizedText = new Dictionary<Language, Dictionary<string, string>>
    {
        [Language.English] = new Dictionary<string, string>
        {
            // Login
            ["login_title"] = "USER LOGIN",
            ["btn_login"] = "LOGIN",
            ["btn_goto_register"] = "CREATE ACCOUNT",
            ["lbl_email"] = "EMAIL",
            ["lbl_password"] = "PASSWORD",

            // Register
            ["reg_title"] = "NEW ACCOUNT",
            ["lbl_display_name"] = "DISPLAY NAME",
            ["lbl_confirm_password"] = "CONFIRM PASSWORD",
            ["btn_register"] = "CREATE ACCOUNT",
            ["btn_back_to_login"] = "< BACK TO LOGIN",

            // OTP
            ["otp_title"] = "VERIFY EMAIL",
            ["btn_verify"] = "VERIFY",
            ["btn_resend"] = "RESEND CODE",
            ["btn_cancel"] = "< CANCEL",
            ["otp_msg"] = "We've sent a code to your email.",
            ["otp_spam_note"] = "* Don't see it? Check your Spam/Junk folder.",
            ["lbl_otp_code"] = "OTP CODE",

            // Main Menu
            ["btn_play"] = "PLAY",
            ["btn_options"] = "OPTIONS",
            ["btn_quit"] = "QUIT",

            // Profile
            ["lbl_logged_in_as"] = "LOGGED IN AS:",
            ["btn_logout"] = "LOGOUT",

            // Options
            ["options_title"] = "SYSTEM OPTIONS",
            ["tab_audio"] = "AUDIO",
            ["tab_graphics"] = "GRAPHICS",
            ["tab_controls"] = "CONTROLS",
            ["tab_general"] = "GENERAL",
            ["lbl_language"] = "LANGUAGE",
            ["lbl_master_volume"] = "MASTER VOLUME",
            ["lbl_music_bgm"] = "MUSIC (BGM)",
            ["lbl_effects_sfx"] = "EFFECTS (SFX)",
            ["lbl_resolution"] = "RESOLUTION",
            ["lbl_graphics_quality"] = "GRAPHICS QUALITY",
            ["lbl_fullscreen"] = "FULLSCREEN MODE",
            ["lbl_vsync"] = "ENABLE V-SYNC",
            ["lbl_mouse_sens"] = "MOUSE SENSITIVITY",
            ["lbl_invert_y"] = "INVERT Y-AXIS",
            
            ["quality_ultra"] = "Ultra (Cinematic)",
            ["quality_high"] = "High",
            ["quality_medium"] = "Medium",
            ["quality_low"] = "Low (Performance)",

            ["btn_apply"] = "APPLY CHANGES",
            ["btn_cancel_options"] = "CANCEL",

            // Network Lobby
            ["lobby_title"] = "SESSION LOBBY",
            ["btn_create_host"] = "CREATE (HOST)",
            ["btn_join_client"] = "JOIN (CLIENT)",
            ["btn_back"] = "< BACK",

            // Create Room
            ["host_title"] = "HOST SESSION",
            ["lbl_room_name"] = "HOST / ROOM NAME",
            ["lbl_privacy"] = "PRIVACY",
            ["lbl_public"] = "Public",
            ["lbl_private"] = "Private",
            ["lbl_room_password"] = "PASSWORD",
            ["btn_start_host"] = "START HOSTING",

            // Join Room
            ["browser_title"] = "SESSION BROWSER",
            ["btn_join_id"] = "JOIN BY ID",
            ["col_id"] = "ROOM ID",
            ["col_name"] = "ROOM NAME",
            ["col_host"] = "HOST",
            ["col_players"] = "PLAYERS",
            ["col_action"] = "ACTION",
            ["btn_join"] = "JOIN",

            // Join Auth
            ["auth_title"] = "ENTER PASSWORD",
            ["lbl_auth_room"] = "SESSION:",
            ["lbl_auth_password"] = "ROOM PASSWORD",
            ["btn_connect"] = "CONNECT",

            // Confirm Overlay
            ["confirm_title"] = "APPLY SETTINGS?",
            ["confirm_msg"] = "Changes will be applied immediately.",
            ["confirm_save_title"] = "SAVE CHANGES?",
            ["confirm_save_msg"] = "You have unsaved changes. Do you want to save them before leaving?",
            ["btn_confirm_yes"] = "CONFIRM",
            ["btn_confirm_no"] = "CANCEL",
            ["btn_save_yes"] = "SAVE",
            ["btn_save_no"] = "DON'T SAVE",
            ["lobby_empty"] = "NO ACTIVE SESSIONS FOUND",

            // HUD / Inventory
            ["hud_tab_close"] = "Press [TAB] to close",
            ["hud_upgrades_title"] = "STATS UPGRADES",
            ["hud_points_format"] = "Points: {0}",
            ["hud_stat_hp"] = "HEALTH (HP)",
            ["hud_stat_mp"] = "MANA (MP)",
            ["hud_stat_cooldown"] = "COOLDOWN (-%)",
            ["hud_stat_damage"] = "DAMAGE",
            ["hud_upgrade_lv_hp"] = "Lv. {0} (+{1} HP)",
            ["hud_upgrade_lv_mp"] = "Lv. {0} (+{1} MP)",
            ["hud_upgrade_lv_cooldown"] = "Lv. {0} ({1}% reduction)",
            ["hud_upgrade_lv_damage"] = "Lv. {0} (+{1} Damage)",
            ["hud_warning_skill_locked"] = "SKILLS ARE CURRENTLY LOCKED!",
            ["hud_warning_weapon_locked"] = "WEAPON IS CURRENTLY LOCKED!"
        },

        [Language.Vietnamese] = new Dictionary<string, string>
        {
            // Login
            ["login_title"] = "ĐĂNG NHẬP",
            ["btn_login"] = "ĐĂNG NHẬP",
            ["btn_goto_register"] = "TẠO TÀI KHOẢN",
            ["lbl_email"] = "EMAIL",
            ["lbl_password"] = "MẬT KHẨU",

            // Register
            ["reg_title"] = "TÀI KHOẢN MỚI",
            ["lbl_display_name"] = "TÊN HIỂN THỊ",
            ["lbl_confirm_password"] = "XÁC NHẬN MẬT KHẨU",
            ["btn_register"] = "TẠO TÀI KHOẢN",
            ["btn_back_to_login"] = "< QUAY LẠI",

            // OTP
            ["otp_title"] = "XÁC MINH EMAIL",
            ["btn_verify"] = "XÁC MINH",
            ["btn_resend"] = "GỬI LẠI MÃ",
            ["btn_cancel"] = "< HỦY",
            ["otp_msg"] = "Chúng tôi đã gửi mã tới email của bạn.",
            ["otp_spam_note"] = "* Không thấy? Hãy kiểm tra Thư rác nhé.",
            ["lbl_otp_code"] = "MÃ XÁC THỰC",

            // Main Menu
            ["btn_play"] = "VÀO GAME",
            ["btn_options"] = "CÀI ĐẶT",
            ["btn_quit"] = "THOÁT",

            // Profile
            ["lbl_logged_in_as"] = "ĐĂNG NHẬP BỞI:",
            ["btn_logout"] = "ĐĂNG XUẤT",

            // Options
            ["options_title"] = "CÀI ĐẶT",
            ["tab_audio"] = "ÂM THANH",
            ["tab_graphics"] = "ĐỒ HỌA",
            ["tab_controls"] = "ĐIỀU KHIỂN",
            ["tab_general"] = "CHUNG",
            ["lbl_language"] = "NGÔN NGỮ",
            ["lbl_master_volume"] = "ÂM THANH TỔNG",
            ["lbl_music_bgm"] = "NHẠC NỀN",
            ["lbl_effects_sfx"] = "HIỆU ỨNG",
            ["lbl_resolution"] = "ĐỘ PHÂN GIẢI",
            ["lbl_graphics_quality"] = "CHẤT LƯỢNG ĐỒ HỌA",
            ["lbl_fullscreen"] = "TOÀN MÀN HÌNH",
            ["lbl_vsync"] = "ĐỒNG BỘ V-SYNC",
            ["lbl_mouse_sens"] = "ĐỘ NHẠY CHUỘT",
            ["lbl_invert_y"] = "ĐẢO TRỤC Y",

            ["quality_ultra"] = "Cực cao (Điện ảnh)",
            ["quality_high"] = "Cao",
            ["quality_medium"] = "Trung bình",
            ["quality_low"] = "Thấp (Hiệu năng)",

            ["btn_apply"] = "ÁP DỤNG",
            ["btn_cancel_options"] = "HỦY",

            // Network Lobby
            ["lobby_title"] = "SẢNH CHỜ",
            ["btn_create_host"] = "TẠO PHÒNG (HOST)",
            ["btn_join_client"] = "VÀO PHÒNG (CLIENT)",
            ["btn_back"] = "< QUAY LẠI",

            // Create Room
            ["host_title"] = "TẠO MÁY CHỦ",
            ["lbl_room_name"] = "TÊN PHÒNG / CHỦ PHÒNG",
            ["lbl_privacy"] = "QUYỀN RIÊNG TƯ",
            ["lbl_public"] = "Công khai",
            ["lbl_private"] = "Riêng tư",
            ["lbl_room_password"] = "MẬT KHẨU PHÒNG",
            ["btn_start_host"] = "BẮT ĐẦU HOST",

            // Join Room
            ["browser_title"] = "DANH SÁCH PHÒNG",
            ["btn_join_id"] = "VÀO BẰNG ID",
            ["col_id"] = "ID PHÒNG",
            ["col_name"] = "TÊN PHÒNG",
            ["col_host"] = "CHỦ PHÒNG",
            ["col_players"] = "NGƯỜI CHƠI",
            ["col_action"] = "HÀNH ĐỘNG",
            ["btn_join"] = "VÀO",

            // Join Auth
            ["auth_title"] = "NHẬP MẬT KHẨU",
            ["lbl_auth_room"] = "PHÒNG:",
            ["lbl_auth_password"] = "MẬT KHẨU PHÒNG",
            ["btn_connect"] = "KẾT NỐI",

            // Confirm Overlay
            ["confirm_title"] = "LƯU CÀI ĐẶT?",
            ["confirm_msg"] = "Các thay đổi sẽ được áp dụng ngay lập tức.",
            ["confirm_save_title"] = "LƯU THAY ĐỔI?",
            ["confirm_save_msg"] = "Bạn có thay đổi chưa lưu. Bạn có muốn lưu chúng trước khi thoát không?",
            ["btn_confirm_yes"] = "XÁC NHẬN",
            ["btn_confirm_no"] = "HỦY",
            ["btn_save_yes"] = "CÓ, LƯU LẠI",
            ["btn_save_no"] = "KHÔNG LƯU",
            ["lobby_empty"] = "HIỆN KHÔNG CÓ PHÒNG NÀO TRỐNG",

            // HUD / Inventory
            ["hud_tab_close"] = "Ấn phím [TAB] để đóng",
            ["hud_upgrades_title"] = "NÂNG CẤP CHỈ SỐ",
            ["hud_points_format"] = "Điểm cộng: {0}",
            ["hud_stat_hp"] = "MÁU (HP)",
            ["hud_stat_mp"] = "MANA (MP)",
            ["hud_stat_cooldown"] = "HỒI CHIÊU (-%)",
            ["hud_stat_damage"] = "SÁT THƯƠNG",
            ["hud_upgrade_lv_hp"] = "Lv. {0} (+{1} HP)",
            ["hud_upgrade_lv_mp"] = "Lv. {0} (+{1} MP)",
            ["hud_upgrade_lv_cooldown"] = "Lv. {0} ({1}% giảm)",
            ["hud_upgrade_lv_damage"] = "Lv. {0} (+{1} S.Thương)",
            ["hud_warning_skill_locked"] = "Kỹ Năng Đang Bị Khóa!",
            ["hud_warning_weapon_locked"] = "Vũ Khí Đang Bị Khóa!"
        }

    };

    public static void Initialize()
    {
        string saved = PlayerPrefs.GetString("Language", "English");
        CurrentLanguage = saved == "Vietnamese" ? Language.Vietnamese : Language.English;
    }

    public static void SetLanguage(Language lang)
    {
        CurrentLanguage = lang;
        PlayerPrefs.SetString("Language", lang.ToString());
        PlayerPrefs.Save();
    }

    public static void SetPreviewLanguage(Language lang)
    {
        CurrentLanguage = lang;
    }

    public static string Get(string key)
    {
        if (_localizedText[CurrentLanguage].TryGetValue(key, out string val))
            return val;
        return key;
    }
}
