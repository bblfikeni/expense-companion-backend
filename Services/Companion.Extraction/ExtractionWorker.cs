using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using Companion.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Companion.Extraction;

/// <summary>
/// Background service that processes receipt extraction messages from RabbitMQ.
/// </summary>
public sealed class ExtractionWorker : BackgroundService
{
    public ExtractionWorker(ILogger<ExtractionWorker> logger) { }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}