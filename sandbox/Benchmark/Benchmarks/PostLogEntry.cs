using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using log4net.Repository.Hierarchy;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using NLog.Targets.Wrappers;
using Serilog;
using ZLogger;
using ZLogger.Formatters;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Benchmark.Benchmarks;

// file class MsExtConsoleLoggerFormatter : ConsoleFormatter
// {
//     public class Options : ConsoleFormatterOptions
//     {
//     }
//
//     public MsExtConsoleLoggerFormatter() : base("Benchmark")
//     {
//     }
//
//     public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider scopeProvider, TextWriter textWriter)
//     {
//         var message = logEntry.Formatter.Invoke(logEntry.State, logEntry.Exception);
//         var timestamp = DateTime.UtcNow;
//         textWriter.Write(timestamp);
//         textWriter.Write(" [");
//         textWriter.Write(logEntry.LogLevel);
//         textWriter.Write("] ");
//         textWriter.WriteLine(message);
//     }
// }

file class BenchmarkConfig : ManualConfig
{
    public BenchmarkConfig()
    {
        AddDiagnoser(MemoryDiagnoser.Default);
        AddJob(Job.ShortRun);
    }
}

file class NullProcessor : IAsyncLogProcessor
{
    Channel<IZLoggerEntry> channel;

    public NullProcessor()
    {

        this.channel = Channel.CreateUnbounded<IZLoggerEntry>(new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = false, // always should be in async loop.
            SingleWriter = false,
            SingleReader = true,
        });
    }

    public ValueTask DisposeAsync()
    {
        return default;
    }

    public void Post(IZLoggerEntry log)
    {
        channel.Writer.TryWrite(log);
        log.Return();
    }
}

[Config(typeof(BenchmarkConfig))]
public class PostLogEntry
{
    static readonly string NullDevicePath = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "NUL" : "/dev/null";

    ILogger zLogger = default!;
    ILogger msExtConsoleLogger = default!;
    ILogger serilogMsExtLogger = default!;
    ILogger nLogMsExtLogger = default!;

    Serilog.ILogger serilogLogger = default!;
    NLog.Logger nLogLogger = default!;

    List<IDisposable> disposables = new List<IDisposable>();

    [GlobalSetup]
    public void SetUp()
    {
        System.Console.SetOut(TextWriter.Null);

        // ZLogger

        var zLoggerFactory = LoggerFactory.Create(logging =>
        {
            logging.AddZLogger(builder =>
            {
                //builder.AddLogProcessor(new NullProcessor());

                //builder.AddStream(Stream.Null);

                builder.AddStream(Stream.Null, options =>
                {
                    options.UsePlainTextFormatter(formatter => formatter.SetPrefixFormatter($"{0} [{1}]", (template, info) => template.Format(info.Timestamp, info.LogLevel)));
                });
            });
        });
        disposables.Add(zLoggerFactory);

        zLogger = zLoggerFactory.CreateLogger<PostLogEntry>();



        // Microsoft.Extensions.Logging.Console

        var msExtConsoleLoggerFactory = LoggerFactory.Create(logging =>
        {
            logging
                .AddConsole(options =>
                {
                    options.FormatterName = "BenchmarkPlainText";
                })
                .AddConsoleFormatter<BenchmarkPlainTextConsoleFormatter, BenchmarkPlainTextConsoleFormatter.Options>();
        });
        disposables.Add(msExtConsoleLoggerFactory);

        msExtConsoleLogger = msExtConsoleLoggerFactory.CreateLogger<Program>();

        // Serilog

        serilogLogger = new LoggerConfiguration()
            .WriteTo.Async(a => a.TextWriter(TextWriter.Null))
            .CreateLogger();

        var serilogMsExtLoggerFactory = LoggerFactory.Create(logging =>
        {
            logging.AddSerilog(new LoggerConfiguration()
                .WriteTo.Async(a => a.TextWriter(TextWriter.Null))
                .CreateLogger());
        });
        disposables.Add(serilogMsExtLoggerFactory);

        serilogMsExtLogger = serilogMsExtLoggerFactory.CreateLogger<PostLogEntry>();

        // NLog
        var nLogLayout = new NLog.Layouts.SimpleLayout("${longdate} [${level}] ${message}");
        {
            var nLogConfig = new NLog.Config.LoggingConfiguration(new LogFactory());
            var target = new NLog.Targets.FileTarget("Null")
            {
                FileName = NullDevicePath,
                Layout = nLogLayout
            };
            var asyncTarget = new NLog.Targets.Wrappers.AsyncTargetWrapper(target, 10000, AsyncTargetWrapperOverflowAction.Grow)
            {
                TimeToSleepBetweenBatches = 0
            };
            nLogConfig.AddTarget(asyncTarget);
            nLogConfig.AddRuleForAllLevels(asyncTarget);
            nLogConfig.LogFactory.Configuration = nLogConfig;

            nLogLogger = nLogConfig.LogFactory.GetLogger("NLog");
        }
        {
            var nLogConfigForMsExt = new NLog.Config.LoggingConfiguration(new LogFactory());
            var target = new NLog.Targets.FileTarget("Null")
            {
                FileName = NullDevicePath,
                Layout = nLogLayout
            };
            var asyncTarget = new NLog.Targets.Wrappers.AsyncTargetWrapper(target, 10000, AsyncTargetWrapperOverflowAction.Grow)
            {
                TimeToSleepBetweenBatches = 0
            };
            nLogConfigForMsExt.AddTarget(asyncTarget);
            nLogConfigForMsExt.AddRuleForAllLevels(asyncTarget);
            nLogConfigForMsExt.LogFactory.Configuration = nLogConfigForMsExt;

            var nLogMsExtLoggerFactory = LoggerFactory.Create(logging =>
            {
                logging.AddNLog(nLogConfigForMsExt);
            });
            nLogMsExtLogger = nLogMsExtLoggerFactory.CreateLogger<PostLogEntry>();

            disposables.Add(nLogMsExtLoggerFactory);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (var item in disposables)
        {
            item.Dispose();
        }
    }

    [Benchmark]
    public void ZLogger_ZLog()
    {
        const int x = 100;
        const int y = 200;
        const int z = 300;
        zLogger.ZLogInformation($"foo{x} bar{y} nazo{z}");
    }

    [Benchmark]
    public void MsExtConsole_Log()
    {
        msExtConsoleLogger.LogInformation("x={X} y={Y} z={Z}", 100, 200, 300);
    }

    [Benchmark]
    public void SerilogMsExt_Log()
    {
        serilogMsExtLogger.LogInformation("x={X} y={Y} z={Z}", 100, 200, 300);
    }

    [Benchmark]
    public void NLogMsExt_Log()
    {
        nLogMsExtLogger.LogInformation("x={X} y={Y} z={Z}", 100, 200, 300);
    }

    [Benchmark]
    public void Serilog_Log()
    {
        serilogLogger.Information("x={X} y={Y} z={Z}", 100, 200, 300);
    }

    [Benchmark]
    public void NLog_Log()
    {
        nLogLogger.Info("x={X} y={Y} z={Z}", 100, 200, 300);
    }
}