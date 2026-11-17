// PSD Reader - 接口定义
// 定义 PSD 对象模型的核心接口

using System;
using System.Collections;
using System.Collections.Generic;

namespace PsdReader
{
    /// <summary>
    /// 图像像素源接口 — 提供通道数据和基本图像属性
    /// </summary>
    public interface IImageSource
    {
        int Width { get; }
        int Height { get; }
        int Depth { get; }
        IChannel[] Channels { get; }
        float Opacity { get; }
        bool HasImage { get; }
        bool HasMask { get; }
    }

    /// <summary>
    /// 图层树节点接口 — PsdDocument 和 PsdLayer 都实现此接口
    /// </summary>
    public interface IPsdLayer : IImageSource
    {
        BlendMode BlendMode { get; }
        IPsdLayer[] Childs { get; }
        bool IsClipping { get; }
        ILinkedLayer LinkedLayer { get; }
        string Name { get; }
        IPsdLayer Parent { get; }
        IProperties Resources { get; }
        PsdDocument Document { get; }
        int Left { get; }
        int Top { get; }
        int Right { get; }
        int Bottom { get; }
    }

    /// <summary>
    /// 颜色/透明通道接口
    /// </summary>
    public interface IChannel
    {
        byte[] Data { get; }
        ChannelType Type { get; }
    }

    /// <summary>
    /// 键值属性字典接口 — 用于存储图层的附加资源/元数据
    /// </summary>
    public interface IProperties : IEnumerable<KeyValuePair<string, object>>, IEnumerable
    {
        object this[string property] { get; }
        int Count { get; }
        bool Contains(string property);
    }

    /// <summary>
    /// 链接层（智能对象）接口 — 引用外部 PSD 文件
    /// </summary>
    public interface ILinkedLayer
    {
        PsdDocument Document { get; }
        Uri AbsoluteUri { get; }
        bool HasDocument { get; }
        Guid ID { get; }
        string Name { get; }
        int Width { get; }
        int Height { get; }
    }
}
