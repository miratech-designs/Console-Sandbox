/*

*/
PriorityWeights = new()
{
    { WorkPriority.High,   10 },
    { WorkPriority.Normal,  3 },
    { WorkPriority.Low,     1 }
}

private async Task WorkerLoop(CancellationToken token)
{
    var priorities = Enum.GetValues<WorkPriority>();

    while (!token.IsCancellationRequested)
    {
        // Collect non-empty queues
        var available = priorities
            .Where(p => _channels[p].Reader.Count > 0)
            .ToList();

        if (available.Count == 0)
        {
            // No work anywhere
            await Task.WhenAny(
                _channels.Values.Select(c => c.Reader.WaitToReadAsync(token).AsTask())
            );
            continue;
        }

        // Pick priority using weighted fair scheduling
        var selectedPriority = WeightedChoice(available);

        var reader = _channels[selectedPriority].Reader;

        // Try to read exactly ONE item from selected queue
        if (reader.TryRead(out var workItem))
        {
            try { await workItem(); }
            catch (Exception ex)
            {
                Console.WriteLine($"Worker error: {ex}");
            }
        }
    }
}

private WorkPriority WeightedChoice(List<WorkPriority> available)
{
    // Sum weights of all priorities that currently have backlog
    int totalWeight = available.Sum(p => _config.PriorityWeights[p]);

    int roll = Random.Shared.Next(1, totalWeight + 1);

    int cumulative = 0;

    foreach (var p in available)
    {
        cumulative += _config.PriorityWeights[p];
        if (roll <= cumulative)
            return p;
    }

    // Should never reach here, but fallback to highest priority
    return available.First();
}

