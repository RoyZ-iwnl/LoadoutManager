# Loadout Manager

[English Version](README.md)

一个用于 **Gunner HEAT PC** 的 [MelonLoader](https://melonwiki.xyz/) Mod，允许玩家自定义弹药架上的弹药分布。

## 功能

- **弹药架编辑器**：自定义各类型弹药在不同弹药架上的分布
- **机炮供弹支持**：调整机炮武器已装填弹链中的弹药组成
- **膛内弹药选择**：选择装填入膛的弹药类型
- **双语支持**：支持英文和中文界面

## 使用方法

1. 进入任意搭载武器的载具
2. 弹药配置窗口会自动弹出
3. 使用滑块调整弹药分布：
   - 设置各弹药架上各类型弹药的数量
   - 使用"填满"按钮将弹药架全部填满某一类型弹药
   - 使用"清空"按钮清空该类型弹药
4. 在底部选择膛内弹药类型
5. 点击"应用"保存更改

## 机炮载具说明

- **Rack 0** 代表供弹接口架上的待发弹药（弹药箱）
- 机炮载具默认隐藏 Rack 0，因为它不是常规弹药架
- 可以在"已装填弹药"区域调整弹链组成
- 已装填弹药总数是固定的，调整某一类型会自动重新平衡其他类型

## 安装

在 [GHPC Mod Manager](https://GHPC.DMR.gg/) 上一键安装

1. 为 GHPC 安装 [MelonLoader](https://melonwiki.xyz/#/?id=requirements)
2. 将 `LoadoutManager.dll` 放入 `Mods/` 文件夹

## 配置

首次启动后，编辑 `UserData/MelonPreferences.cfg`：

```
[LoadoutManager]
HideRack0ForAutocannon = true
UIScale = 1.0
Language = 1
```

| 选项 | 说明 | 默认值 |
|---|---|---|
| HideRack0ForAutocannon | 隐藏机炮载具的 Rack 0（Rack 0 为供弹接口架上的待发弹药） | true |
| UIScale | UI 缩放比例（0.5-2.0） | 1.0 |
| Language | 界面语言（0=英文，1=中文） | 0 |

## 制作

- 作者：RoyZ