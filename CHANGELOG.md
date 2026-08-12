# 更新日志

## v26.8.12.2（2026-08-12）

### 变更
- 移除「背包 Alt+单击 加入黑名单」方案（Alt+左键与自动合成插件冲突）
- 新增：右键物品界面（介绍/使用菜单）右上角 X 按钮，悬停提示「加入自动拾取黑名单」，点击将当前物品加入/移出黑名单
- 配置项 AltClickAddBlacklist 移除，新增 BlacklistButton（Shortcuts 节，默认 true）

### 说明
- 作者：35117+Deepseek-v4-flash-0731
- 兼容 BepInEx 5，Unturned 3.26.3.8（U3-SDK）

## v26.8.12.1（2026-08-12）

### 新增
- 自动拾取：范围内掉物自动入包（原版拾取流程，自动找空格）
- 黑白名单模式：Blacklist / Whitelist，游戏内物品选择器编辑
- 拾取范围（默认 6 米，原版第三人称距离；最大 20 米受服务器校验）
- 拾取速度（每秒个数，上限 10 受服务器限速）
- 条件拾取：耐久低于设定值不拾取
- 快捷标记：拾取时 Alt+F 切换白名单
- 拾取成功提示（物品名 + ID）：Off / Popup / Chat 可选
- 配置热重载（5 秒轮询）
- PluginManager 兼容（ItemList / Cycle 标签）

### 说明
- 作者：35117+Deepseek-v4-flash-0731
- 兼容 BepInEx 5，Unturned 3.26.3.8（U3-SDK）
