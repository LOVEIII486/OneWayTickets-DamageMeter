# OWT Damage Meter（武器伤害统计）

《再再启程 One Way Tickets》的 BepInEx 5 插件。旅途中按装置显示名称、累计伤害，并给出 DPS 与伤害占比，名称与图标底色按品质着色，方便比较不同武器的实际输出。闪电、爆炸与飞镖等衍生伤害会尽量归回触发它的武器。

本模组和 [OWT Auto Repair](https://github.com/LOVEIII486/OneWayTickets-AutoRepair) 是两个独立仓库，可以同时安装。

## 要求

- Windows 64 位
- Steam 版《再再启程 One Way Tickets》
- BepInEx 5.4.23.5 x64，整合包已附带

## 下载

Releases 页面提供：

- 整合包：BepInEx 5.4.23.5 x64 和 OWT Damage Meter，附带安装说明与第三方许可证
- `OWT.DamageMeter.dll`：单独的伤害统计插件
- `SHA256SUMS.txt`：以上文件的校验值

## 安装

### 使用整合包

1. 退出游戏。
2. 下载整合包，解压到游戏根目录，和 `再再启程.exe` 放在同一层。
3. 提示合并或覆盖时选择“是”。
4. 从 Steam 启动游戏。

整合包已经包含 BepInEx，不需要另外安装。配置文件会在首次加载时生成。

### 自己安装 BepInEx

1. 下载 BepInEx 5.4.23.5 x64：
   - [BepInEx 用户文档](https://docs.bepinex.dev/articles/user_guide/installation/index.html)
   - [5.4.23.5 发布页](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5)
   - [BepInEx_win_x64_5.4.23.5.zip](https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip)
2. 把以下内容解压到游戏根目录：
   - `winhttp.dll`
   - `doorstop_config.ini`
   - `.doorstop_version`
   - `changelog.txt`
   - `BepInEx` 文件夹
3. 把 `OWT.DamageMeter.dll` 放进 `BepInEx/plugins`。
4. 从 Steam 启动游戏。

这个插件按 BepInEx 5（Mono）构建。请使用 5.4.23.5 x64；BepInEx 6 的目录结构和 API 不同，不适用。

## 同时安装两个模组

1. 先按上面的方式安装 Damage Meter。
2. 下载 [OWT Auto Repair](https://github.com/LOVEIII486/OneWayTickets-AutoRepair) 的整合包，解压到游戏根目录，提示合并时选择“是”。
3. 两个插件都会位于 `BepInEx/plugins`，配置文件分别生成。
4. 从 Steam 启动游戏。

如果先装了 Auto Repair，也可以单独安装 Damage Meter，把 `OWT.DamageMeter.dll` 放进 `BepInEx/plugins`。两个整合包使用同一版 BepInEx，先后解压到同一目录即可。

## 操作

- `F10`：显示或隐藏面板。隐藏后右下角留下一个显示 DPS 的悬浮球。
- 点击悬浮球：重新展开面板；拖动悬浮球只移动位置，不会展开。
- 收起：隐藏面板并进入悬浮球。
- 拖动面板：移动位置，位置自动保存。
- 拖动悬浮球：移动悬浮球，位置自动保存。
- 拖动右下角把手：左右改宽度，上下改高度，比例自动保存。
- 重置：面板和悬浮球回到默认位置，宽度和高度回到 100%。

## 配置

配置文件：`BepInEx/config/owt.damagemeter.cfg`，首次启动时自动生成。修改后重启游戏生效。

## 卸载

删除 `BepInEx/plugins/OWT.DamageMeter.dll`。配置文件可以保留，也可以一起删除。

如果不再使用任何模组，再删除游戏根目录的以下内容：

- `winhttp.dll`
- `doorstop_config.ini`
- `.doorstop_version`
- `changelog.txt`
- `BepInEx` 文件夹

## 从源码构建

需要 .NET SDK 8 或更高版本和游戏本体的 Managed DLL。步骤见 [BUILDING.md](BUILDING.md)。

## 许可证

MIT，见 [LICENSE](LICENSE)。

本项目与游戏开发商无关。
