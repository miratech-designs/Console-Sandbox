/* 
🎉 You now have AWS-style auto-scaling workers in a C# console app!

This setup:
	•	Scales out when queue depth grows
	•	Scales in when idle
	•	Allows configurable min/max workers
	•	Lets you plug in better metrics later

If you’d like, I can help you:
	•	Add CPU-based scaling
	•	Add a web dashboard
	•	Convert this into a reusable library
	•	Switch to TPL Dataflow (which has built-in patterns for this)
 */

using System.Threading.Channels;

Channel<Func<Task>> channel = Channel.CreateUnbounded<Func<Task>>();
List<(Task worker, CancellationTokenSource cts)> workers = new();

int minWorkers = 2;
int maxWorkers = 20;

CancellationTokenSource globalCts = new();
int currentWorkers = 0;

// Worker logic
async Task Worker(ChannelReader<Func<Task>> reader, CancellationToken token)
{
    while (await reader.WaitToReadAsync(token))
    {
        while (reader.TryRead(out var workItem))
        {
            await workItem();
        }
    }
}

// Add worker
void AddWorker()
{
    if (currentWorkers >= maxWorkers)
        return;

    var workerCts = CancellationTokenSource.CreateLinkedTokenSource(globalCts.Token);
    var task = Task.Run(() => Worker(channel.Reader, workerCts.Token));
    workers.Add((task, workerCts));
    currentWorkers++;

    Console.WriteLine($"➡️ Worker added. Total: {currentWorkers}");
}

// Remove worker
void RemoveWorker()
{
    if (currentWorkers <= minWorkers)
        return;

    var (task, cts) = workers[workers.Count - 1];
    workers.RemoveAt(workers.Count - 1);

    cts.Cancel();
    currentWorkers--;

    Console.WriteLine($"⬅️ Worker removed. Total: {currentWorkers}");
}

// Auto-scaler
async Task AutoScaler()
{
    while (!globalCts.Token.IsCancellationRequested)
    {
        int backlog = channel.Reader.Count;

        if (backlog > currentWorkers * 5)
            AddWorker();
        else if (backlog == 0)
            RemoveWorker();

        await Task.Delay(1000);
    }
}

// Example workload producer
async Task Producer()
{
    var writer = channel.Writer;

    while (true)
    {
        await writer.WriteAsync(async () =>
        {
            // Simulate work
            await Task.Delay(200);
        });

        await Task.Delay(20);
    }
}

// Start everything
EnsureMinWorkers();
Task.Run(AutoScaler);
Task.Run(Producer);

Console.ReadLine();
globalCts.Cancel();