using System.Data;
using System.Runtime.Serialization;
using TestGenerator.TestGenerators;

Generator g = new Generator();
g.Genrate(typeof(Class1));


public class Class1 {
    public int Test(int a, int b, int c) { return 5; }
    public void Test1() { }
    public void Test2() { }
    private void Test3() { }

    public Class1(IDataReader a, ISerializable m, IObjectReference o, float b, string c) { }
 }