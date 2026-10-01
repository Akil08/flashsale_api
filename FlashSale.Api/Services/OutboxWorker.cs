using FlashSale.Api.Data;
using FlashSale.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace FlashSale.Api.Services;

public class OutboxWorker : BackgroundService
{

    // wehn we  r caling the ouxboxer, we have to provide the sservice,
    // but how come as supose when we r askign getproductwithid ,
    // we can easiy give the method the id so it can call the db wiht that id,
    // but for this serviece how come he ahve the serivcceprovider obj ?
    // where that will it come form ?
    // The IServiceProvider is injected into the OutboxWorker class by the ASP.NET Core dependency injection system.
    // When the application starts, the DI container creates an instance of OutboxWorker and provides
    // the IServiceProvider automatically. This allows the worker to create scopes and resolve services 
    // like AppDbContext at runtime. 

    // so thsi line : builder.Services.AddHostedService<OutboxWorker>();
    // for this line the di auto injet the   _serviceProvider; ?
    // Yes, exactly. When you register the OutboxWorker as a hosted service using 
    // builder.Services.AddHostedService<OutboxWorker>(),
    // the ASP.NET Core dependency injection system automatically injects the required 
    // dependencies into the constructor of OutboxWorker, including the IServiceProvider and ILogger<OutboxWorker>.
    /*

    that means this code {
       public OutboxWorker(IServiceProvider serviceProvider, ILogger<OutboxWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }
    is called by the di system, and it will provide the serviceprovider obj and logger obj
    Yes, that's correct. The ASP.NET Core dependency injection system automatically calls the constructor of the
    OutboxWorker class and provides the required IServiceProvider and ILogger<OutboxWorker> instances.
    }

    */

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxWorker> _logger;
    
    public OutboxWorker(IServiceProvider serviceProvider, ILogger<OutboxWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    // is thsi workerr running in a separate thread, or is it running in the same thread as the main app ?
    // The OutboxWorker runs in a separate background thread managed by the ASP.NET Core runtime.
    // It operates independently of the main application thread, allowing it to process outbox messages without 
    // blocking the main request/response flow of the application.
    // but does not it still uses a thread pool there form the main app ? 
    // Yes, the OutboxWorker uses a thread from the .NET thread pool, which is shared with the main application.
    // However, it runs independently in the background, allowing it to perform its tasks without interfering
    // with the main application thread that handles incoming HTTP requests.
    // also does our code has sleeper for the BW ? i can not see any, can u ? 
    // Yes, the OutboxWorker code includes a delay (sleep) mechanism. When there are no unprocessed messages to handle,
    // it waits for 5 seconds before checking again. This is done using Task.Delay:

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox Worker is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 1. Create a fresh DI scope for this batch of work
                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // 2. Find up to 10 unprocessed messages
                var messages = await dbContext.OutboxMessages
                    .Where(m => m.ProcessedAt == null)
                    .OrderBy(m => m.CreatedAt)
                    // as it is takng by time, shold not it tkae by the time by oldest ?
                    // Yes, the messages are ordered by CreatedAt in ascending order,
                    //  which means the oldest messages will be processed first. 
                    // This ensures that messages are handled in the order they were created,
                    //  maintaining the correct sequence of events.
                    .Take(10)
                    .ToListAsync(stoppingToken);

                if (messages.Count == 0)
                {
                    // No work to do, wait 5 seconds before checking again
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    continue;
                }

                // 3. Process each message
                foreach (var message in messages)
                {
                    var payload = JsonSerializer.Deserialize<JsonElement>(message.Payload);
                    var buyerId = payload.GetProperty("buyerId").GetGuid();
                    var purchaseId = payload.GetProperty("purchaseId").GetGuid();

                    var notification = new Notification
                    {
                        UserId = buyerId,
                        Message = $"Your purchase (ID: {purchaseId}) was successful!",
                        CreatedAt = DateTime.UtcNow
                    };
                    dbContext.Notifications.Add(notification);

                    // Mark as processed
                    message.ProcessedAt = DateTime.UtcNow;
                }

                // 4. Save all changes atomically
                await dbContext.SaveChangesAsync(stoppingToken);
                _logger.LogInformation("Successfully processed {Count} outbox messages.", messages.Count);
            }
            catch (OperationCanceledException)
            {
                // Expected when the application is shutting down
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while processing outbox messages. Retrying in 5 seconds.");
                // Wait before retrying to prevent tight error loops
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Outbox Worker is stopping.");
    }
}