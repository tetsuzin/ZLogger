﻿using System;
using ZLogger.Formatters;

namespace ZLogger
{
    public class ZLoggerOptions
    {
        public Action<LogInfo, Exception>? InternalErrorLogger { get; set; }
        public TimeSpan? FlushRate { get; set; }

        Func<IZLoggerFormatter> formatterFactory = DefaultFormatterFactory;

        public IZLoggerFormatter CreateFormatter() => throw new NotImplementedException();

        public ZLoggerOptions UseFormatter(Func<IZLoggerFormatter> formatterFactory)
        {
            this.formatterFactory = formatterFactory;
            return this;
        }

        public ZLoggerOptions UsePlainTextFormatter()
        {
            return UsePlainTextFormatter(_ => { });
        }

        public ZLoggerOptions UsePlainTextFormatter(Action<PlainTextZLoggerFormatter> formatterConfigure)
        {
            UseFormatter(() =>
            {
                var formatter = new PlainTextZLoggerFormatter();
                formatterConfigure.Invoke(formatter);
                return formatter;
            });
            return this;
        }

        static IZLoggerFormatter DefaultFormatterFactory()
        {
            return new PlainTextZLoggerFormatter();
        }
    }
}
