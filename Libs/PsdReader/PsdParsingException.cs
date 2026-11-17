// PSD Reader - PSD 解析异常

using System;

namespace PsdReader
{
    /// <summary>
    /// PSD 文件解析过程中的异常
    /// </summary>
    public class PsdParsingException : Exception
    {
        public PsdParsingException()
            : base("PSD 文件解析错误") { }

        public PsdParsingException(string message)
            : base(message) { }

        public PsdParsingException(string format, params object[] args)
            : base(string.Format(format, args)) { }

        public PsdParsingException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
