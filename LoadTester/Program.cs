using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        string url = args.Length > 0 ? args[0] : "http://localhost:5151/";
        int concurrentRequests = args.Length > 1 ? int.Parse(args[1]) : 100;
        int durationSeconds = args.Length > 2 ? int.Parse(args[2]) : 10;

        Console.WriteLine($"Starting Load Test");
        Console.WriteLine($"Target: {url}");
        Console.WriteLine($"Concurrency: {concurrentRequests}");
        Console.WriteLine($"Duration: {durationSeconds}s\n");

        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 1000
        };
        var client = new HttpClient(handler);

        int successCount = 0;
        int rateLimitedCount = 0;
        int errorCount = 0;

        var timer = new Stopwatch();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
        
        timer.Start();

        var tasks = new Task[concurrentRequests];
        for (int i = 0; i < concurrentRequests; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                while (!cts.IsCancellationRequested)
                {
                    try
                    {
                        var response = await client.GetAsync(url, cts.Token);
                        if (response.IsSuccessStatusCode)
                            Interlocked.Increment(ref successCount);
                        else if ((int)response.StatusCode == 429)
                            Interlocked.Increment(ref rateLimitedCount);
                        else
                            Interlocked.Increment(ref errorCount);
                    }
                    catch (OperationCanceledException) { }
                    catch
                    {
                        if (!cts.IsCancellationRequested)
                            Interlocked.Increment(ref errorCount);
                    }
                }
            });
        }

        await Task.WhenAll(tasks);
        timer.Stop();

        int totalRequests = successCount + rateLimitedCount + errorCount;
        double rps = totalRequests / timer.Elapsed.TotalSeconds;

        Console.WriteLine("=== RESULTS ===");
        Console.WriteLine($"Total Requests: {totalRequests}");
        Console.WriteLine($"Throughput:     {rps:F2} req/s");
        Console.WriteLine($"Success (2xx):  {successCount}");
        Console.WriteLine($"Rate Limit (429): {rateLimitedCount}");
        Console.WriteLine($"Errors (5xx):   {errorCount}");
    }
}
