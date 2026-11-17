# PSD2UGUI

PSD2UGUI 将 Photoshop 图稿转换为 Unity UGUI Prefab。各 UI 框架可以用自己的生成器读取 Prefab 中的节点名、组件和事件，并生成绑定脚本。

## 上手

1. 将工具导入 Unity 项目的 Assets，安装 UGUI 和 TextMeshPro，并为 PSD2UIForm 提供配置和控件模板。移植已有配置资产时需保留相应脚本和模板的 .meta GUID。
2. 使用 PSExtension 下的安装脚本安装 Smart Export 4.4.5，导出 PSD 和图层 manifest。
3. 在 Unity 中使用 **Psd2UIForm Editor** 解析 PSD，核对识别结果并生成 UGUI Prefab。需要时可在 Inspector 中使用 AI 识别，确认后应用图层结构改动。
4. 将最终 Prefab 交给 UI 框架自带的绑定工具。以完整节点名作为绑定键时，保留按钮编号，例如 `shop_btn_confirm_01` 和 `shop_btn_confirm_03`。框架可结合实际组件类型读取事件，如 UGUI Button 的 `onClick`。

在同一个绑定作用域内，检查节点名转换成代码标识符后仍然唯一。Smart Export 生成的编号会随图层顺序变动，重新导出前应保留依赖稳定字段名的节点名称。

## 验证

使用 Unity 2022.3 参考程序集完成离线 C# 编译。本次未运行 Unity 编辑器和 Edit Mode 测试。
