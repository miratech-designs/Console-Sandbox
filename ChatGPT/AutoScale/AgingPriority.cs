/*
Formula : EffectiveWeight = PriorityWeight + (AgeSeconds * AgingFactor)


*/
public class ScheduledWorkItem
{
    public Func<Task> Work { get; }
    public DateTime EnqueuedAt { get; }

    public ScheduledWorkItem(Func<Task> work)
    {
        Work = work;
        EnqueuedAt = DateTime.UtcNow;
    }
}

private readonly Dictionary<WorkPriority, Channel<ScheduledWorkItem>> _channels;

public async ValueTask Enqueue(WorkPriority priority, Func<Task> work)
{
    var item = new ScheduledWorkItem(work);
    await _channels[priority].Writer.WriteAsync(item);
}

AgingFactor = 0.2  // every 5 seconds adds 1 point of weight


private WorkPriority WeightedChoiceWithAging(List<WorkPriority> available)
{
    double totalWeight = 0;
    var bucketedWeights = new Dictionary<WorkPriority, double>();

    foreach (var p in available)
    {
        int backlog = _channels[p].Reader.Count;
        if (backlog == 0) continue;

        // Age is determined by oldest item in the priority queue
        var reader = _channels[p].Reader;
        DateTime oldest = FindOldestItem(reader);

        double ageSeconds = (DateTime.UtcNow - oldest).TotalSeconds;

        double baseWeight = _config.PriorityWeights[p];
        double agingBoost = ageSeconds * _config.AgingFactor;

        double effective = baseWeight + agingBoost;

        bucketedWeights[p] = effective;
        totalWeight += effective;
    }

    // Weighted random selection
    double roll = Random.Shared.NextDouble() * totalWeight;
    double cumulative = 0;

    foreach (var p in available)
    {
        cumulative += bucketedWeights[p];
        if (roll <= cumulative)
            return p;
    }

    return available.First();
}

private readonly Dictionary<WorkPriority, DateTime> _oldestTimestamp =
    Enum.GetValues<WorkPriority>().ToDictionary(p => p, p => DateTime.MaxValue);

public async ValueTask Enqueue(WorkPriority priority, Func<Task> work)
{
    var item = new ScheduledWorkItem(work);
    await _channels[priority].Writer.WriteAsync(item);

    // Track earliest enqueue time
    if (item.EnqueuedAt < _oldestTimestamp[priority])
        _oldestTimestamp[priority] = item.EnqueuedAt;
}

// After TryRead succeeds:
if (!reader.TryPeekOldest(out var newTime))
    _oldestTimestamp[p] = DateTime.MaxValue;
else
    _oldestTimestamp[p] = newTime;

private async Task WorkerLoop(CancellationToken token)
{
    var priorities = Enum.GetValues<WorkPriority>();

    while (!token.IsCancellationRequested)
    {
        var available = priorities
            .Where(p => _channels[p].Reader.Count > 0)
            .ToList();

        if (available.Count == 0)
        {
            await Task.WhenAny(
                _channels.Values.Select(c => c.Reader.WaitToReadAsync(token).AsTask())
            );
            continue;
        }

        // Fair + aging scheduler
        var selectedPriority = WeightedChoiceWithAging(available);
        var reader = _channels[selectedPriority].Reader;

        if (reader.TryRead(out var item))
        {
            // update oldest timestamp
            UpdateOldestTimestamp(selectedPriority);

            try { await item.Work(); }
            catch (Exception ex)
            {
                Console.WriteLine($"Worker error: {ex}");
            }
        }
    }
}

