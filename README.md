# PSD2UGUI

[简体中文](README.zh-CN.md)

PSD2UGUI converts Photoshop designs into Unity UGUI prefabs. Each UI framework can use its own generator to read node names, components, and events from the prefab and generate binding scripts.

## Install with Unity Package Manager

Install Git first. The package targets Unity 2022.3 LTS. In Unity, open **Window → Package Manager**, select **+ → Add package from git URL…**, paste the following URL, and click **Add**.

```text
https://github.com/kevinaastral/PSDWeave.git
```

Package Manager resolves the UGUI and TextMeshPro dependencies automatically.

If you're migrating an earlier installation under `Assets`, back up your rules with **导出配置json** (Export config JSON) in the configuration Inspector. Keep your project's control templates and their `.meta` files. Move the old plugin scripts out of the project, install the package from the Git URL, and create a new configuration asset as described below. Use **从json导入配置** (Import config JSON) to restore the rules. Regenerate parsed PSD prefabs that reference old script GUIDs from their original PSDs.

## Getting started

1. In your project's `Assets` folder, select **Create → ScriptableObject → Psd2UIForm Config【Psd2UIForm工具配置】** to create a configuration asset. Set `UI Form Template` and add rules for the default text type, the default image type, `FillColor`, and `Null`. Assign templates and rules for the controls your project uses.
2. Clone or download this repository and run the installer in `PSExtension` to install Smart Export 4.4.5 for Photoshop. Use Smart Export to export the PSD and its layer manifest.
3. In Unity, use **Psd2UIForm Editor** to parse the PSD, review the detected layers, and generate a UGUI prefab. If you use AI recognition in the Inspector, review the proposed changes before applying them to the layer hierarchy.
4. Pass the final prefab to your UI framework's binding tool. If the tool uses the full node name as a binding key, keep button numbers such as `shop_btn_confirm_01` and `shop_btn_confirm_03`. The framework can read events from the component type, such as a UGUI Button's `onClick`.

Within each binding scope, check that node names remain unique after conversion to code identifiers. Smart Export may renumber nodes when the layer order changes. Before re-exporting, preserve the names of nodes whose generated field names need to stay stable.

## Verification

The original source was compiled offline against the Unity 2022.3 reference assemblies. The Git package configuration passed static checks. Installation through the Unity Editor and Edit Mode tests remain unverified.
