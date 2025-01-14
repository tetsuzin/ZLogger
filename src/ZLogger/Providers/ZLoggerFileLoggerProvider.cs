﻿using Microsoft.Extensions.Logging;

namespace ZLogger.Providers;

[ProviderAlias("ZLoggerFile")]
public class ZLoggerFileLoggerProvider : ILoggerProvider, ISupportExternalScope, IAsyncDisposable
{
    readonly ZLoggerOptions options;
    readonly AsyncStreamLineMessageWriter streamWriter;
    IExternalScopeProvider? scopeProvider;

    public ZLoggerFileLoggerProvider(string filePath, ZLoggerOptions options)
    {
        var di = new FileInfo(filePath).Directory;
        if (!di!.Exists)
        {
            di.Create();
        }

        this.options = options;

        // useAsync:false, use sync(in thread) processor, don't use FileStream buffer(use buffer size = 1).
        var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite, 1, false);
        this.streamWriter = new AsyncStreamLineMessageWriter(stream, this.options);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new ZLoggerLogger(categoryName, streamWriter, options, options.IncludeScopes ? scopeProvider : null);
    }

    public void Dispose()
    {
        streamWriter.DisposeAsync().AsTask().Wait();
    }

    public async ValueTask DisposeAsync()
    {
        await streamWriter.DisposeAsync().ConfigureAwait(false);
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        this.scopeProvider = scopeProvider;
    }
}
