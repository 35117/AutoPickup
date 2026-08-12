# AutoPickup（Unturned 自动拾取）

作者：35117+Deepseek-v4-flash-0731

Unturned 自动拾取插件：玩家靠近掉落的物品时自动拾取，支持黑白名单、拾取范围、拾取速度、最低耐久条件、快捷标记名单与拾取提示。

## 版本号规则

版本号格式为 `年.月.日.第几版`，例如 `26.8.12.2` 表示 2026 年 8 月 12 日当天上传的第 2 版。

## 安装

1. 安装 [BepInEx 5](https://docs.bepinex.dev/)（x64 版本）到游戏根目录。
2. 从 [Release](https://github.com/35117/AutoPickup/releases) 下载 `AutoPickupMod-版本号.zip`，解压后把 `BepInEx` 文件夹覆盖到游戏根目录。
3. 启动游戏，首次启动自动生成配置文件 `BepInEx/config/com.trae.autopickup.cfg`。

## 功能

- 自动拾取范围内掉落的物品（与原版按 F 拾取同一套流程，自动寻找空格入包）
- 黑白名单模式，支持在游戏内物品选择器可视化编辑
- 拾取范围、拾取速度可调（默认取原版第三人称 6 米；受服务器 20 米校验与 10Hz 限速约束）
- 条件拾取：物品耐久低于设定值不拾取
- 拾取时按住 Alt 按交互键（默认 F）：将准星指向的物品加入/移出白名单
- 右键物品界面（介绍/使用菜单）右上角 X 按钮：点击将该物品加入/移出黑名单（悬停有提示）
- 拾取成功提示（物品名 + ID），提示位置可配置（弹窗 / 聊天栏）
- 配置文件外部修改后 5 秒内自动热重载
- 兼容 PluginManager（列表用 ItemList 标签、模式用 Cycle 标签）

## 配置

`BepInEx/config/com.trae.autopickup.cfg`

| 节 | 键 | 默认值 | 说明 |
|----|----|----|----|
| General | Enabled | true | 是否开启自动拾取 |
| General | PickupRange | 6 | 拾取范围（米），原版默认：第一人称 4 米、第三人称 6 米；最大 20 |
| General | PickupSpeed | 5 | 拾取速度（每秒拾取个数），上限 10（服务器限速） |
| General | ListMode | Blacklist | 名单模式：Blacklist / Whitelist |
| Lists | Blacklist | （空） | 黑名单物品 ID 列表，逗号分隔 |
| Lists | Whitelist | （空） | 白名单物品 ID 列表，逗号分隔 |
| Pickup | MinDurability | 0 | 条件拾取：耐久低于此值不拾取（0-100，0 不限制） |
| Pickup | NotifyTarget | Off | 拾取成功提示位置：Off / Popup / Chat |
| Shortcuts | AltFAddWhitelist | true | 拾取时 Alt+交互键（默认 F）切换白名单 |
| Shortcuts | BlacklistButton | true | 右键物品界面右上角 X 按钮，点击切换黑名单 |

## 使用技巧

- 想要某物品：准星对着掉物按 Alt+F，加入白名单（再按一次移出）
- 不想要某物品：右键该物品打开介绍界面，点击右上角 X 按钮（再点一次移出）
- 提示位置设为 Off 时，快捷标记与拾取成功均不弹提示（日志仍记录）

## 编译

环境要求：.NET Framework 4.x、C# 5 语法、csc.exe。

运行 `build.bat`，输出 `BepInEx/Plugins/AutoPickupMod.dll`。

## 更新日志

见 [CHANGELOG.md](CHANGELOG.md)
