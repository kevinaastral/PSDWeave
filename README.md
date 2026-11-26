# PSD2UGUI

PSD2UGUI 将 Photoshop 图稿转换为 Unity UGUI Prefab。各 UI 框架可以用自己的生成器读取 Prefab 中的节点名、组件和事件，并生成绑定脚本。

## 通过 Unity Package Manager 安装

此包以 Unity 2022.3 LTS 为基线。打开 **Window → Package Manager**，点击 **+ → Add package from git URL…**，粘贴以下地址并点击 **Add**。

```text
https://github.com/kevinaastral/PSDWeave.git
```

请先安装 Git，并为 Git 配置有权读取此私有仓库的 GitHub 凭据。Package Manager 会一并解析 UGUI 和 TextMeshPro 依赖。

如果从 `Assets` 下的旧版迁移，先用配置 Inspector 的 **导出配置json** 备份规则，并保留项目自己的控件模板及其 `.meta`。移走旧版插件脚本后，通过 Git URL 安装，在下一节所述的新配置资产中使用 **从json导入配置**。引用了旧脚本 GUID 的 PSD 解析 Prefab 可从原始 PSD 重新生成。

## 上手

1. 在项目的 `Assets` 下创建 **Create → ScriptableObject → Psd2UIForm Config【Psd2UIForm工具配置】**，设置 `UI Form Template`，并为默认文本类型、默认图片类型、`FillColor` 和 `Null` 配置规则。为项目用到的控件类型分配对应的模板和规则。
2. 从 GitHub 克隆或下载仓库，在 `PSExtension` 目录运行安装脚本以安装 Photoshop 的 Smart Export 4.4.5，再导出 PSD 和图层 manifest。
3. 在 Unity 中使用 **Psd2UIForm Editor** 解析 PSD，核对识别结果并生成 UGUI Prefab。需要时可在 Inspector 中使用 AI 识别，确认后应用图层结构改动。
4. 将最终 Prefab 交给 UI 框架自带的绑定工具。以完整节点名作为绑定键时，保留按钮编号，例如 `shop_btn_confirm_01` 和 `shop_btn_confirm_03`。框架可结合实际组件类型读取事件，如 UGUI Button 的 `onClick`。

在同一个绑定作用域内，检查节点名转换成代码标识符后仍然唯一。Smart Export 生成的编号会随图层顺序变动，重新导出前应保留依赖稳定字段名的节点名称。

## 验证

原始源码已使用 Unity 2022.3 参考程序集完成离线 C# 编译。Git 安装配置已通过静态检查，尚未在 Unity 编辑器中安装或运行 Edit Mode 测试。
