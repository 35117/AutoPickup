// AutoPickup.cs
// 作者：35117+Deepseek-v4-flash-0731
// 版本 v26.8.12.1
// 功能：Unturned 自动拾取插件。玩家靠近掉落的物品时自动拾取，
//       支持黑白名单、拾取范围、拾取速度、最低耐久条件。
//       v26.8.12.2 新增：Alt+F 拾取时快捷加入白名单、背包 Alt+点击快捷加入黑名单、
//       拾取成功提示（物品名 + ID，可配置提示位置，参考自动合成插件）。
// 兼容：BepInEx 5，Unturned 3.26.3.8（U3-SDK）
// 编译：运行 build.bat，输出 BepInEx/Plugins/AutoPickupMod.dll

using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using SDG.Unturned;
using UnityEngine;

namespace AutoPickup
{
    [BepInPlugin("com.trae.autopickup", "AutoPickup 自动拾取", "26.8.12.1")]
    public class AutoPickupPlugin : BaseUnityPlugin
    {
        // 供 Harmony 补丁访问插件实例与日志
        internal static AutoPickupPlugin Instance;

        // 原版游戏内玩家拾取默认范围：第一人称 4 米，第三人称 6 米。
        // 插件默认取第三人称的 6 米作为默认值。
        private const float GAME_DEFAULT_PICKUP_RANGE = 6f;

        // 服务器端对拾取请求的距离校验上限（ReceiveTakeItemRequest 中 sqrMagnitude > 400 拒绝）。
        private const float SERVER_MAX_PICKUP_RANGE = 20f;

        // 服务器端拾取请求限速 10Hz（ratelimitHz = 10），拾取速度不能超过 10 个/秒。
        private const float SERVER_MAX_PICKUP_SPEED = 10f;

        private const float CONFIG_RELOAD_INTERVAL = 5f;

        // Alt+F 射线检测准星指向物品的交互距离（与原版第三人称一致）
        private const float INTERACT_RAY_DISTANCE = 6f;

        // 自动拾取后等待入包确认的时间窗口（秒），超时不再提示
        private const float PENDING_NOTIFY_TIMEOUT = 3f;

        private ConfigEntry<bool> cfgEnabled;
        private ConfigEntry<float> cfgPickupRange;
        private ConfigEntry<float> cfgPickupSpeed;
        private ConfigEntry<string> cfgListMode;
        private ConfigEntry<string> cfgBlacklist;
        private ConfigEntry<string> cfgWhitelist;
        private ConfigEntry<byte> cfgMinDurability;
        private ConfigEntry<bool> cfgAltFWhitelist;
        private ConfigEntry<bool> cfgAltClickBlacklist;
        private ConfigEntry<string> cfgNotifyTarget;

        private readonly HashSet<ushort> blacklistIds = new HashSet<ushort>();
        private readonly HashSet<ushort> whitelistIds = new HashSet<ushort>();
        private bool isWhitelistMode;

        private float pickupTimer;
        private DateTime lastConfigWriteTime;
        private float nextConfigCheckTime;

        // 自动拾取发起后等待入包确认的物品（id -> 时间戳），用于拾取成功提示
        private readonly Dictionary<ushort, float> pendingNotifyItems = new Dictionary<ushort, float>();
        private Player subscribedPlayer;

        private void Awake()
        {
            try
            {
                cfgEnabled = Config.Bind("General", "Enabled", true, "是否开启自动拾取");

                cfgPickupRange = Config.Bind("General", "PickupRange", GAME_DEFAULT_PICKUP_RANGE,
                    new ConfigDescription("拾取范围（米）。原版默认：第一人称 4 米、第三人称 6 米；服务器限制最大 20 米",
                        new AcceptableValueRange<float>(1f, SERVER_MAX_PICKUP_RANGE)));

                cfgPickupSpeed = Config.Bind("General", "PickupSpeed", 5f,
                    new ConfigDescription("拾取速度（每秒最多拾取物品个数），上限 10（受服务器限速）",
                        new AcceptableValueRange<float>(0.5f, SERVER_MAX_PICKUP_SPEED)));

                cfgListMode = Config.Bind("General", "ListMode", "Blacklist",
                    new ConfigDescription("名单模式：Blacklist=黑名单（名单内的物品不拾取）/ Whitelist=白名单（只拾取名单内的物品）",
                        new AcceptableValueList<string>("Blacklist", "Whitelist"),
                        new object[] { "Unturned.Cycle" }));

                cfgBlacklist = Config.Bind("Lists", "Blacklist", "",
                    new ConfigDescription("黑名单物品 ID 列表，多个用英文逗号分隔（例如 6666, 6667）",
                        null, new object[] { "Unturned.ItemList" }));

                cfgWhitelist = Config.Bind("Lists", "Whitelist", "",
                    new ConfigDescription("白名单物品 ID 列表，多个用英文逗号分隔（例如 6666, 6667）",
                        null, new object[] { "Unturned.ItemList" }));

                cfgMinDurability = Config.Bind("Pickup", "MinDurability", (byte)0,
                    new ConfigDescription("条件拾取：物品耐久低于此值不拾取（0-100，0 表示不限制）",
                        new AcceptableValueRange<byte>(0, 100)));

                // ---- 快捷标记（v26.8.12.2 新增）----
                cfgAltFWhitelist = Config.Bind("Shortcuts", "AltFAddWhitelist", true,
                    "拾取时按住 Alt 并按交互键（默认 F）：将准星指向的物品加入/移出白名单（再按一次移出）");

                cfgAltClickBlacklist = Config.Bind("Shortcuts", "AltClickAddBlacklist", true,
                    "背包中按住 Alt 单击物品：将该物品加入/移出黑名单（再点一次移出）。原版背包无悬停物品 API，" +
                    "故参考自动合成插件采用 Alt+点击 方案（背包界面中 Alt+F 无效）");

                // ---- 拾取提示（v26.8.12.2 新增，参考自动合成插件）----
                cfgNotifyTarget = Config.Bind("Pickup", "NotifyTarget", "Off",
                    new ConfigDescription("拾取成功提示位置：Off=关闭，Popup=屏幕中下方提示栏，Chat=聊天栏",
                        new AcceptableValueList<string>("Off", "Popup", "Chat"),
                        new object[] { "Unturned.Cycle" }));

                ParseRules();

                lastConfigWriteTime = File.GetLastWriteTimeUtc(Config.ConfigFilePath);

                Instance = this;
                Harmony.CreateAndPatchAll(typeof(AutoPickupPlugin).Assembly);

                Player.onPlayerCreated += OnPlayerCreated;
                Player.onPlayerDestroyed += OnPlayerDestroyed;

                Logger.LogInfo("[AutoPickup] 插件启动完成，作者 35117+Deepseek-v4-flash-0731，版本 26.8.12.1");
            }
            catch (Exception e)
            {
                Logger.LogError("[AutoPickup] 初始化异常：" + e);
            }
        }

        private void OnDestroy()
        {
            Player.onPlayerCreated -= OnPlayerCreated;
            Player.onPlayerDestroyed -= OnPlayerDestroyed;
            UnsubscribeInventory();
            Instance = null;
        }

        private void OnPlayerCreated(Player player)
        {
            if (player != Player.LocalPlayer)
            {
                return;
            }

            SubscribeInventory(player);
        }

        private void OnPlayerDestroyed(Player player)
        {
            if (subscribedPlayer == player)
            {
                UnsubscribeInventory();
            }
        }

        private void SubscribeInventory(Player player)
        {
            UnsubscribeInventory();
            subscribedPlayer = player;
            player.inventory.onInventoryAdded += OnItemAdded;
        }

        private void UnsubscribeInventory()
        {
            if (subscribedPlayer != null)
            {
                subscribedPlayer.inventory.onInventoryAdded -= OnItemAdded;
                subscribedPlayer = null;
            }
        }

        private void Update()
        {
            if (!cfgEnabled.Value)
            {
                return;
            }

            // 配置文件外部修改后热重载
            if (Time.realtimeSinceStartup >= nextConfigCheckTime)
            {
                nextConfigCheckTime = Time.realtimeSinceStartup + CONFIG_RELOAD_INTERVAL;
                try
                {
                    if (File.GetLastWriteTimeUtc(Config.ConfigFilePath) != lastConfigWriteTime)
                    {
                        Config.Reload();
                        lastConfigWriteTime = File.GetLastWriteTimeUtc(Config.ConfigFilePath);
                        ParseRules();
                        Logger.LogInfo("[AutoPickup] 配置已热重载");
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError("[AutoPickup] 配置读取异常：" + e);
                }
            }

            // 仅本地玩家生效（单机 / 客户端）；纯专用服务器无本地玩家，直接跳过
            Player player = Player.LocalPlayer;
            if (player == null)
            {
                return;
            }

            if (!player.life.IsAlive)
            {
                return;
            }

            if (player.stance.stance == EPlayerStance.DRIVING || player.stance.stance == EPlayerStance.SITTING)
            {
                return;
            }

            // Alt + 交互键（默认 F）：将准星指向的掉物加入/移出白名单。
            // 非 UI 模式（showCursor == false）才执行；背包界面中请用 Alt+点击。
            if (cfgAltFWhitelist.Value && !PlayerUI.window.showCursor && IsAltHeld() && Input.GetKeyDown(ControlsSettings.interact))
            {
                TryToggleWhitelistFromRay();
            }

            if (ItemManager.clampedItems == null || ItemManager.clampedItems.Count == 0)
            {
                return;
            }

            // 拾取速度：两次拾取之间的间隔 = 1 / 速度 秒
            pickupTimer -= Time.deltaTime;
            if (pickupTimer > 0f)
            {
                return;
            }
            pickupTimer = 1f / cfgPickupSpeed.Value;

            float sqrRange = cfgPickupRange.Value * cfgPickupRange.Value;
            Vector3 playerPosition = player.transform.position;

            for (int i = 0; i < ItemManager.clampedItems.Count; i++)
            {
                InteractableItem drop = ItemManager.clampedItems[i];
                if (drop == null || drop.item == null)
                {
                    continue;
                }

                Vector3 delta = drop.transform.position - playerPosition;
                if (delta.sqrMagnitude > sqrRange)
                {
                    continue;
                }

                ushort itemId = drop.item.id;

                // 黑白名单检查
                if (isWhitelistMode)
                {
                    if (!whitelistIds.Contains(itemId))
                    {
                        continue;
                    }
                }
                else if (blacklistIds.Contains(itemId))
                {
                    continue;
                }

                // 条件拾取：耐久（quality，0-100）低于配置值不拾取
                if (cfgMinDurability.Value > 0 && drop.item.quality < cfgMinDurability.Value)
                {
                    continue;
                }

                // 记录待提示，等待入包成功后再播报（避免拾取失败误报）
                pendingNotifyItems[itemId] = Time.realtimeSinceStartup;

                // 与玩家按 F 拾取走同一套流程（自动定位空格入包）
                drop.use();
                return; // 每个周期只拾取 1 个，由拾取速度控制频率
            }

            // 清理超时未匹配的待提示记录
            CleanupExpiredPendingNotify();
        }

        private void CleanupExpiredPendingNotify()
        {
            if (pendingNotifyItems.Count == 0)
            {
                return;
            }

            List<ushort> expired = null;
            float now = Time.realtimeSinceStartup;
            foreach (KeyValuePair<ushort, float> pair in pendingNotifyItems)
            {
                if (now - pair.Value > PENDING_NOTIFY_TIMEOUT)
                {
                    if (expired == null)
                    {
                        expired = new List<ushort>();
                    }
                    expired.Add(pair.Key);
                }
            }

            if (expired != null)
            {
                for (int i = 0; i < expired.Count; i++)
                {
                    pendingNotifyItems.Remove(expired[i]);
                }
            }
        }

        // 入包成功回调：匹配自动拾取发起的物品并提示
        private void OnItemAdded(byte page, byte index, ItemJar jar)
        {
            if (jar == null || jar.item == null)
            {
                return;
            }

            // page 0-6 为玩家背包页，7 为存储页（箱子等），只提示进入背包的
            if (page > PlayerInventory.PANTS)
            {
                return;
            }

            float addTime;
            if (pendingNotifyItems.TryGetValue(jar.item.id, out addTime))
            {
                if (Time.realtimeSinceStartup - addTime <= PENDING_NOTIFY_TIMEOUT)
                {
                    pendingNotifyItems.Remove(jar.item.id);
                    ItemAsset asset = jar.item.GetAsset();
                    string name = asset != null ? asset.FriendlyName : jar.item.id.ToString();
                    SendNotify("已自动拾取 ID " + jar.item.id + "（" + name + "）", Color.cyan);
                }
            }
        }

        // 射线检测准星指向的掉物并切换白名单
        private void TryToggleWhitelistFromRay()
        {
            try
            {
                Ray ray = new Ray(MainCamera.instance.transform.position, MainCamera.instance.transform.forward);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit, INTERACT_RAY_DISTANCE, RayMasks.PLAYER_INTERACT))
                {
                    if (hit.collider != null)
                    {
                        InteractableItem drop = hit.collider.GetComponentInParent<InteractableItem>();
                        if (drop != null && drop.item != null)
                        {
                            ToggleWhitelist(drop.item.id);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.LogError("[AutoPickup] Alt+F 白名单标记异常：" + e);
            }
        }

        // Alt+点击：切换黑名单（Harmony 补丁调用）
        internal void ToggleBlacklist(ushort itemId)
        {
            if (cfgBlacklist == null)
            {
                return;
            }

            bool added = ToggleCsvEntry(cfgBlacklist, itemId.ToString());
            Config.Save();
            ParseRules();
            string msg = added ? "已将物品 ID " + itemId + " 加入黑名单" : "已将物品 ID " + itemId + " 移出黑名单";
            SendNotify(msg, added ? Color.green : Color.yellow);
            Logger.LogInfo("[AutoPickup] " + msg);
        }

        // Alt+F：切换白名单
        internal void ToggleWhitelist(ushort itemId)
        {
            if (cfgWhitelist == null)
            {
                return;
            }

            bool added = ToggleCsvEntry(cfgWhitelist, itemId.ToString());
            Config.Save();
            ParseRules();
            string msg = added ? "已将物品 ID " + itemId + " 加入白名单" : "已将物品 ID " + itemId + " 移出白名单";
            SendNotify(msg, added ? Color.green : Color.yellow);
            Logger.LogInfo("[AutoPickup] " + msg);
        }

        // 在逗号分隔的配置项中切换一个条目（存在则移除，不存在则追加），返回是否新增
        private bool ToggleCsvEntry(ConfigEntry<string> entry, string entryValue)
        {
            List<string> items = new List<string>();
            string current = entry.Value;
            if (!string.IsNullOrWhiteSpace(current))
            {
                foreach (string part in current.Split(','))
                {
                    string trimmed = part != null ? part.Trim() : string.Empty;
                    if (trimmed.Length > 0)
                    {
                        items.Add(trimmed);
                    }
                }
            }

            bool added;
            if (items.Contains(entryValue))
            {
                items.Remove(entryValue);
                added = false;
            }
            else
            {
                items.Add(entryValue);
                added = true;
            }

            entry.Value = string.Join(", ", items.ToArray());
            return added;
        }

        // 统一提示出口（参考自动合成插件：Popup=屏幕中下方提示栏，Chat=聊天栏）
        private void SendNotify(string message, Color color)
        {
            string target = cfgNotifyTarget != null ? cfgNotifyTarget.Value : "Off";
            if (string.Equals(target, "Popup", StringComparison.OrdinalIgnoreCase))
            {
                PlayerUI.message(EPlayerMessage.NPC_CUSTOM, message, 3f);
            }
            else if (string.Equals(target, "Chat", StringComparison.OrdinalIgnoreCase))
            {
                ChatManager.serverSendMessage("[AutoPickup] " + message, color);
            }
        }

        // 是否按住 Alt 键
        private static bool IsAltHeld()
        {
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }

        internal bool IsAltClickBlacklistEnabled()
        {
            return cfgAltClickBlacklist != null && cfgAltClickBlacklist.Value;
        }

        internal static void LogErrorStatic(string message)
        {
            UnityEngine.Debug.LogError(message);
        }

        private void ParseRules()
        {
            blacklistIds.Clear();
            whitelistIds.Clear();

            ParseIdList(cfgBlacklist.Value, blacklistIds);
            ParseIdList(cfgWhitelist.Value, whitelistIds);

            isWhitelistMode = cfgListMode.Value == "Whitelist";

            Logger.LogInfo("[AutoPickup] 规则已解析：模式=" + (isWhitelistMode ? "白名单" : "黑名单")
                + "，黑名单 " + blacklistIds.Count + " 条，白名单 " + whitelistIds.Count + " 条");
        }

        private static void ParseIdList(string raw, HashSet<ushort> target)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return;
            }

            string[] parts = raw.Split(',');
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.Length == 0)
                {
                    continue;
                }

                ushort id;
                if (ushort.TryParse(part, out id) && id > 0)
                {
                    target.Add(id);
                }
            }
        }
    }

    // Alt+左键点击背包物品：切换黑名单标记（跳过原拖拽行为）。
    // 原版背包 UI 无悬停物品公开 API，采用与自动合成插件一致的 Alt+点击方案。
    [HarmonyPatch(typeof(PlayerDashboardInventoryUI), "onGrabbedItem")]
    internal static class PatchItemAltClick
    {
        internal static bool Prefix(object item)
        {
            if (!IsAltHeld())
            {
                return true;
            }

            try
            {
                AutoPickupPlugin instance = AutoPickupPlugin.Instance;
                if (instance == null)
                {
                    return true;
                }

                if (!instance.IsAltClickBlacklistEnabled())
                {
                    return true;
                }

                SleekItem sleekItem = item as SleekItem;
                if (sleekItem != null && sleekItem.jar != null && sleekItem.jar.item != null)
                {
                    instance.ToggleBlacklist(sleekItem.jar.item.id);
                }
            }
            catch (Exception exception)
            {
                AutoPickupPlugin.LogErrorStatic("[AutoPickup] Alt点击标记黑名单异常：" + exception);
            }

            return false;
        }

        private static bool IsAltHeld()
        {
            return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
        }
    }
}
