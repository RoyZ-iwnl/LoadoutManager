using System.Collections.Generic;
using MelonLoader;

namespace LoadoutManager
{
    public enum Language
    {
        English,
        Chinese
    }

    public static class LocalizationManager
    {
        public static Language CurrentLanguage { get; private set; } = Language.English;

        private static Dictionary<string, Dictionary<Language, string>> translations = new Dictionary<string, Dictionary<Language, string>>()
        {
            // Window title
            { "window_title", new Dictionary<Language, string> {
                { Language.English, "Loadout Manager" },
                { Language.Chinese, "弹药配置" }
            }},

            // Weapon section
            { "weapon", new Dictionary<Language, string> {
                { Language.English, "Weapon" },
                { Language.Chinese, "武器" }
            }},

            // Ammo rack section
            { "ammo_rack", new Dictionary<Language, string> {
                { Language.English, "Ammo Rack" },
                { Language.Chinese, "弹药架" }
            }},
            { "capacity", new Dictionary<Language, string> {
                { Language.English, "Capacity" },
                { Language.Chinese, "容量" }
            }},
            { "rack_total", new Dictionary<Language, string> {
                { Language.English, "Rack Total" },
                { Language.Chinese, "弹药架总计" }
            }},
            { "total_ammo_budget", new Dictionary<Language, string> {
                { Language.English, "Total Ammo Budget" },
                { Language.Chinese, "总弹药预算" }
            }},

            // Rack0 hidden message
            { "rack_0_hidden", new Dictionary<Language, string> {
                { Language.English, "Rack 0 hidden (autocannon feed rack)" },
                { Language.Chinese, "Rack 0 已隐藏（机炮待发架）" }
            }},
            { "rack_0_hidden_comment", new Dictionary<Language, string> {
                { Language.English, "Rack0 is the ready-to-fire ammo on feed, default 0 in game" },
                { Language.Chinese, "Rack0即为待发弹药架上的弹药，原版游戏对于机炮车一般默认为0" }
            }},

            // Loaded ammo section
            { "loaded_ammo", new Dictionary<Language, string> {
                { Language.English, "Loaded Ammo" },
                { Language.Chinese, "已装填弹链" }
            }},
            { "fixed_total_loaded", new Dictionary<Language, string> {
                { Language.English, "Fixed Total Loaded" },
                { Language.Chinese, "固定总装填" }
            }},

            // Current ammo section
            { "current_ammo", new Dictionary<Language, string> {
                { Language.English, "Current Ammo" },
                { Language.Chinese, "当前弹药" }
            }},
            { "loaded", new Dictionary<Language, string> {
                { Language.English, "Loaded" },
                { Language.Chinese, "已装填" }
            }},
            { "ammo_box", new Dictionary<Language, string> {
                { Language.English, "Ammo Box" },
                { Language.Chinese, "备弹盒" }
            }},
            { "rounds_per_box", new Dictionary<Language, string> {
                { Language.English, "rounds per box" },
                { Language.Chinese, "每盒发数" }
            }},

            // Chambered ammo section
            { "chambered_ammo", new Dictionary<Language, string> {
                { Language.English, "Chambered Ammo" },
                { Language.Chinese, "膛内弹药" }
            }},
            { "selected", new Dictionary<Language, string> {
                { Language.English, "Selected" },
                { Language.Chinese, "已选择" }
            }},
            { "reloading_warning", new Dictionary<Language, string> {
                { Language.English, "Weapon is reloading, chambered ammo selection disabled" },
                { Language.Chinese, "武器正在填装，膛内弹药选择已禁用" }
            }},

            // Buttons
            { "apply", new Dictionary<Language, string> {
                { Language.English, "Apply" },
                { Language.Chinese, "应用" }
            }},
            { "cancel", new Dictionary<Language, string> {
                { Language.English, "Cancel" },
                { Language.Chinese, "取消" }
            }},
            { "fill", new Dictionary<Language, string> {
                { Language.English, "Fill" },
                { Language.Chinese, "拉满" }
            }},
            { "clear", new Dictionary<Language, string> {
                { Language.English, "Clear" },
                { Language.Chinese, "清空" }
            }},

            // Debug/Log messages
            { "log_mod_initialized", new Dictionary<Language, string> {
                { Language.English, "Loadout Manager initialized" },
                { Language.Chinese, "Loadout Manager 已初始化" }
            }},
            { "log_game_ready", new Dictionary<Language, string> {
                { Language.English, "Game ready, monitoring vehicle changes" },
                { Language.Chinese, "游戏准备完成，开始监控载具切换" }
            }},
            { "log_new_vehicle", new Dictionary<Language, string> {
                { Language.English, "New vehicle detected: {0}" },
                { Language.Chinese, "检测到新载具: {0}" }
            }},
            { "log_weapons_found", new Dictionary<Language, string> {
                { Language.English, "Found {0} weapon systems, showing UI" },
                { Language.Chinese, "找到 {0} 个武器系统，显示UI" }
            }},
            { "log_no_weapons", new Dictionary<Language, string> {
                { Language.English, "No weapon systems found" },
                { Language.Chinese, "未找到武器系统" }
            }},
            { "log_capture_start", new Dictionary<Language, string> {
                { Language.English, "Starting vehicle state capture" },
                { Language.Chinese, "开始捕获载具状态" }
            }},
            { "log_convert_failed", new Dictionary<Language, string> {
                { Language.English, "Cannot convert to Vehicle type" },
                { Language.Chinese, "无法转换为Vehicle类型" }
            }},
            { "log_no_loadout_manager", new Dictionary<Language, string> {
                { Language.English, "LoadoutManager component not found" },
                { Language.Chinese, "未找到LoadoutManager组件" }
            }},
            { "log_no_weapons_manager", new Dictionary<Language, string> {
                { Language.English, "Weapons not found" },
                { Language.Chinese, "未找到武器" }
            }},
            { "log_found_loadout_weapons", new Dictionary<Language, string> {
                { Language.English, "Found LoadoutManager and {0} weapons" },
                { Language.Chinese, "找到LoadoutManager和 {0} 个武器" }
            }},
            { "log_weapon_added", new Dictionary<Language, string> {
                { Language.English, "Weapon state added, ammo types: {0}" },
                { Language.Chinese, "添加武器状态，弹药类型数: {0}" }
            }},
            { "log_capture_failed", new Dictionary<Language, string> {
                { Language.English, "Vehicle state capture failed: {0}" },
                { Language.Chinese, "捕获载具状态失败: {0}" }
            }},
            { "log_loadout_invalid", new Dictionary<Language, string> {
                { Language.English, "LoadoutManager or RackLoadouts invalid" },
                { Language.Chinese, "LoadoutManager或RackLoadouts无效" }
            }},
            { "log_ammo_data_invalid", new Dictionary<Language, string> {
                { Language.English, "TotalAmmoCounts or AmmoClips invalid" },
                { Language.Chinese, "TotalAmmoCounts或AmmoClips无效" }
            }},
            { "log_found_racks_ammo", new Dictionary<Language, string> {
                { Language.English, "Found {0} racks, {1} ammo types" },
                { Language.Chinese, "找到 {0} 个弹药架, {1} 种弹药" }
            }},
            { "log_ammo_read_success", new Dictionary<Language, string> {
                { Language.English, "Successfully read {0} ammo types" },
                { Language.Chinese, "成功读取 {0} 种弹药状态" }
            }},
            { "log_ammo_read_failed", new Dictionary<Language, string> {
                { Language.English, "Failed to read ammo state: {0}" },
                { Language.Chinese, "读取弹药状态失败: {0}" }
            }},
            { "log_applied", new Dictionary<Language, string> {
                { Language.English, "Loadout applied" },
                { Language.Chinese, "弹药配置已应用" }
            }},
            { "log_apply_failed", new Dictionary<Language, string> {
                { Language.English, "Failed to apply changes: {0}" },
                { Language.Chinese, "应用更改失败: {0}" }
            }},
            { "log_ui_mode_enter_failed", new Dictionary<Language, string> {
                { Language.English, "Failed to enter UI interaction mode: {0}" },
                { Language.Chinese, "进入UI交互模式失败: {0}" }
            }},
            { "log_ui_mode_exit_failed", new Dictionary<Language, string> {
                { Language.English, "Failed to exit UI interaction mode: {0}" },
                { Language.Chinese, "退出UI交互模式失败: {0}" }
            }},
            { "log_empty_rack_failed", new Dictionary<Language, string> {
                { Language.English, "Failed to empty rack: {0}" },
                { Language.Chinese, "清空弹药架失败: {0}" }
            }},
            { "log_fill_rack_failed", new Dictionary<Language, string> {
                { Language.English, "Failed to fill rack: {0}" },
                { Language.Chinese, "填充弹药架失败: {0}" }
            }},
        };

        public static string Get(string key)
        {
            if (translations.ContainsKey(key) && translations[key].ContainsKey(CurrentLanguage))
            {
                return translations[key][CurrentLanguage];
            }
            return key;
        }

        public static string Get(string key, params object[] args)
        {
            string text = Get(key);
            return string.Format(text, args);
        }

        public static void SetLanguage(Language language)
        {
            CurrentLanguage = language;
        }

        public static void SetLanguage(int languageIndex)
        {
            CurrentLanguage = languageIndex == 1 ? Language.Chinese : Language.English;
        }
    }
}