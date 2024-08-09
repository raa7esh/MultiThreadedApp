
internal class Program
{
    private static void Main(string[] args)
    {
        var functions = new List<Fun>
            {
                () => LongRunningTask(1000),
                () => LongRunningTask(500),
                () => { throw new Exception("Error in function 3"); },
                () => LongRunningTask(2000)
            };

        try
        {
            var results = ExecuteParallelMap(2, functions, 3000);
            DisplayResults(results);
        }
        catch (TimeoutException ex)
        {
            Console.WriteLine($"Timeout: {ex.Message}");
        }
        object LongRunningTask(int milliseconds)
        {
            Thread.Sleep(milliseconds);
            return $"Completed in {milliseconds} ms";
        }
        List<Result> ExecuteParallelMap(int maxConcurrency, List<Fun> functions, int timeoutMsec)
        {
            var results = new List<Result>(functions.Count);
            var cancellationTokenSource = new CancellationTokenSource();
            var semaphore = new SemaphoreSlim(maxConcurrency);

            var tasks = functions.Select(async function =>
            {
                await semaphore.WaitAsync(cancellationTokenSource.Token);
                try
                {
                    return await ExecuteFunctionAsync(function, cancellationTokenSource.Token);
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            try
            {
                Task.WaitAll(tasks.Select(t => t).ToArray(), timeoutMsec);
            }
            catch (AggregateException ex)
            {
                foreach (var innerEx in ex.InnerExceptions)
                {
                    results.Add(new Result { Exception = innerEx });
                }
            }

            foreach (var task in tasks)
            {
                results.Add(task.Result);
            }

            return results;
        }

        async Task<Result> ExecuteFunctionAsync(Fun function, CancellationToken cancellationToken)
        {
            try
            {
                return new Result
                {
                    Value = await Task.Run(() => function(), cancellationToken)
                };
            }
            catch (Exception ex)
            {
                return new Result
                {
                    Exception = ex
                };
            }
        }

        void DisplayResults(List<Result> results)
        {
            foreach (var result in results)
            {
                if (result.Exception is not null)
                {
                    Console.WriteLine($"Error: {result.Exception.Message}");
                }
                else
                {
                    Console.WriteLine($"Result: {result.Value}");
                }
            }
        }
    }
}

delegate object Fun();
class Result
{
    public object? Value { get; set; }
    public Exception? Exception { get; set; }
}