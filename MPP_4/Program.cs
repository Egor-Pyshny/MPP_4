using System.Collections.Concurrent;
using System.Data;
using System.Runtime.Serialization;
using TestGenerator.TestGenerators;

BlockingCollection<string> sourceFilesQueue = new BlockingCollection<string>();
BlockingCollection<string> testsTextQueue = new BlockingCollection<string>();
BlockingCollection<Task> tasksCollection = new BlockingCollection<Task>();
List<Task> tasks = new List<Task>();

Generator g = new Generator();


void StartGenerating(List<string> filePathes, List<int> parallelRestrictions) {
    Task loadingTask = LoadFilesAsync(filePathes, parallelRestrictions[0]);
    Task GeneratingTests = GenerateAll(parallelRestrictions[1]);
}

async Task LoadFilesAsync(List<string> filePathes, int maxP)
{
    await Task.Run(() =>
    {
        SemaphoreSlim semaphore = new SemaphoreSlim(maxP);
        Parallel.ForEach(filePathes, file =>
        {
            semaphore.Wait();
            try
            {
                sourceFilesQueue.Add(file);
            }
            finally
            {
                semaphore.Release();
            }
        });
    });
    sourceFilesQueue.CompleteAdding();
}

async Task GenerateAll(int maxP) {
    await Task.Run(() =>
    {
        while (!sourceFilesQueue.IsCompleted || sourceFilesQueue.Count > 0)
        {
            if (tasks.Count > maxP)
            {
                var file = sourceFilesQueue.Take();
                Task task = g.GenerateTestClasses(file);
                task.ContinueWith(t => {
                    tasks.Remove(t);
                    //testsTextQueue.
                    });
                tasks.Add(task);
            }
        }
    });

}



ConcurrentQueue<int> queue = new ConcurrentQueue<int>();

async Task Main()
{
    // Запуск асинхронного метода, который добавляет элементы в очередь
    Task consumerTask = ConsumeAsync();
    Task producerTask = ProduceAsync();

    // Запуск асинхронного метода, который берет элементы из очереди
    

    // Ожидание завершения обоих задач
    await Task.WhenAll(producerTask, consumerTask);

    Console.WriteLine("Все элементы добавлены и извлечены из очереди.");
}

async Task ProduceAsync()
{
    for (int i = 0; i < 10; i++)
    {
        await Task.Delay(1);
        queue.Enqueue(i);
        Console.WriteLine($"Элемент {i} добавлен в очередь.");
    }
}

async Task ConsumeAsync()
{
    while (true)
    {

        await Task.Delay(1);
        if (queue.TryDequeue(out int item))
        {
            Console.WriteLine($"Элемент {item} извлечен из очереди.");
        }

    }
}


await Main();

public class Class1 {
    public int Test(int a, int b, int c) { return 5; }
    public void Test(int a, int b) {}
    public void Test1() { }
    public void Test2() { }
    private void Test3() { }

    public Class1(IDataReader a, ISerializable m, IObjectReference o, string c) { }
    public Class1() { }
 }