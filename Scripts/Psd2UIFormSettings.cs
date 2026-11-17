/*
插件获取:
https://efunstudio.cn
https://shop106471535.taobao.com
*/

#if UNITY_EDITOR

namespace UGF.EditorTools.Psd2UGUI
{
    [FilePath("ProjectSettings/Psd2UIFormSettings.asset")]
    [System.Reflection.Obfuscation(Feature = "renaming", ApplyToMembers = true)]
    public class Psd2UIFormSettings : ScriptableSingleton<Psd2UIFormSettings>
    {
        /// <summary>
        /// UI图片导出根目录
        /// </summary>
        public string UIImagesOutputDir;
        /// <summary>
        /// UI预制体静默导出目录
        /// </summary>
        public string UIFormOutputDir = "Assets";
        /// <summary>
        /// 使用静默导出路径(点击导出UIForm后不弹出路径选择)
        /// </summary>
        public bool UseUIFormOutputDir = true;
        /// <summary>
        /// 导出图片后自动压缩图片文件
        /// </summary>
        public bool CompressImage = false;
        /// <summary>
        /// 将超出画布范围的图层裁剪到画布内
        /// </summary>
        public bool EnableCanvasClipping = true;
        /// <summary>
        /// 对大尺寸图片自动执行九宫格裁剪
        /// </summary>
        public bool EnableNineSliceCrop = true;
        /// <summary>
        /// 是否单独导出效果图（_effect.png），关闭后只导出带效果的合成图
        /// </summary>
        public bool ExportSeparateEffects = false;

        public string LastUIFormOutputDir = "Assets";
    }
}
#endif
