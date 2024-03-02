using System.Collections.Concurrent;
using System.Data;
using System.Runtime.Serialization;
using System.Threading.Tasks.Dataflow;
using TestGenerator.TestGenerators;


async Task<string> Read(string path) {
    return await File.ReadAllTextAsync(path);
}

async Task Write(ConcurrentDictionary<string, string> dict)
{
    var dir = Directory.CreateDirectory("TESTS");
    foreach (var item in dict)
    {
        if (dir.EnumerateFiles().Where(f => f.Name == item.Key).ToList().Count == 1) {
            throw new Exception();
        }
        int copyNumber = 1;
        string filename = $"TESTS\\{item.Key}.cs";
        while(File.Exists(filename)) filename = $"TESTS\\{item.Key}({copyNumber++}).cs";
        var f = File.Create(filename);
        StreamWriter stream = new StreamWriter(f);
        await stream.WriteLineAsync(item.Value);
        await stream.FlushAsync();
        stream.Close();
    }

}

void GenerateTestClasses(string[] pathes, int[] MaxDegreesOfParallelism)
{ 
    Generator g = new Generator();
    var buffer = new BufferBlock<string>();

    var readerOptions = new ExecutionDataflowBlockOptions
    {
        MaxDegreeOfParallelism = MaxDegreesOfParallelism[0],
    };
    var reader = new TransformBlock<string, string>(Read, readerOptions);

    var generatorOptions = new ExecutionDataflowBlockOptions
    {
        MaxDegreeOfParallelism = MaxDegreesOfParallelism[1],
    };
    var generator = new TransformBlock<string, ConcurrentDictionary<string, string>>(g.GenerateTestClasses, generatorOptions);

    var writerOptions = new ExecutionDataflowBlockOptions
    {
        MaxDegreeOfParallelism = MaxDegreesOfParallelism[2],
    };
    var writer = new ActionBlock<ConcurrentDictionary<string, string>>(Write, generatorOptions);

    buffer.LinkTo(reader);
    reader.LinkTo(generator);
    generator.LinkTo(writer);

    buffer.Completion.ContinueWith(task => reader.Complete());
    reader.Completion.ContinueWith(task => generator.Complete());
    generator.Completion.ContinueWith(task => writer.Complete());

    foreach (var item in pathes)
    {
        buffer.Post(item);
    }
    buffer.Complete();

    writer.Completion.Wait();
}

int[] MaxDegreesOfParallelism = new int[] { 4, 4, 4 };
string[] pathes = new string[] { 
    "C:\\Users\\Пользователь\\source\\repos\\MPP_4\\MPP_4\\Program.cs" 
};
GenerateTestClasses(pathes, MaxDegreesOfParallelism);

public class Class1 {
    public int Test(int a, int b, int c) { return 5; }
    public void Test(IDataReader a, int b) {}
    public void Test1() { }
    public void Test2() { }
    private void Test3() { }

    public Class1(IDataReader a, ISerializable m, IObjectReference o, string c) { }
    public Class1() { }
 }