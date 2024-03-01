using System;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using Formatter = Microsoft.CodeAnalysis.Formatting.Formatter;
using Microsoft.CodeAnalysis.Options;
using Microsoft.CodeAnalysis.CSharp.Formatting;
using MPP_4.TestGenerators;
using System.Data;


Generator g = new Generator();
g.Genrate(typeof(Class1));


public class Class1 {
    public int Test(int a, int b, int c) { return 5; }
    public void Test1() { }
    public void Test2() { }
    private void Test3() { }

    public Class1(IDataReader a, ISerializable m, IOperation o, float b, string c) { }
 }