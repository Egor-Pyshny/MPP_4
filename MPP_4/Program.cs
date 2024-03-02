using System.Collections.Concurrent;
using System.Data;
using System.Runtime.Serialization;
using System.Threading.Tasks.Dataflow;
using TestGenerator.TestGenerators;

Generator g = new Generator();
string[] pathes = new string[] { "C:\\Users\\user\\Source\\Repos\\Egor-Pyshny\\MPP_4\\MPP_4\\Program.cs" };
var buffer = new BufferBlock<string>();

var readerOptions = new ExecutionDataflowBlockOptions
{
    MaxDegreeOfParallelism = 4,
    BoundedCapacity = 100
};
var reader = new TransformBlock<string, string>(readerF, readerOptions);

var generatorOptions = new ExecutionDataflowBlockOptions
{
    MaxDegreeOfParallelism = 4,
    BoundedCapacity = 100
};
var generator = new TransformBlock<string, ConcurrentDictionary<string, string>>(g.GenerateTestClasses, generatorOptions);

var writerOptions = new ExecutionDataflowBlockOptions
{
    MaxDegreeOfParallelism = 4,
    BoundedCapacity = 100
};
var writer = new ActionBlock<ConcurrentDictionary<string, string>>(writerF, generatorOptions);

buffer.LinkTo(reader);
reader.LinkTo(generator);
generator.LinkTo(writer);

foreach (var item in pathes)
{
    buffer.Post(item);
}

writer.Completion.Wait();

async Task<string> readerF(string path) {
    return await File.ReadAllTextAsync(path);
}


async Task writerF(ConcurrentDictionary<string, string> dict)
{
    var dir = Directory.CreateDirectory("C:\\Users\\user\\Desktop\\test");
    foreach (var item in dict)
    {
        if (dir.EnumerateFiles().Where(f => f.Name == item.Key).ToList().Count == 1) {
            throw new Exception();
        }
        var f = File.Create("C:\\Users\\user\\Desktop\\test\\txt.txt");
        StreamWriter stream = new StreamWriter(f);
        await stream.WriteLineAsync(item.Value);
        await stream.FlushAsync();
        stream.Close();
    }

}

public class Class1 {
    public int Test(int a, int b, int c) { return 5; }
    public void Test(int a, int b) {}
    public void Test1() { }
    public void Test2() { }
    private void Test3() { }

    public Class1(IDataReader a, ISerializable m, IObjectReference o, string c) { }
    public Class1() { }
 }