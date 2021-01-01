﻿//#if !NO_CODEGEN
//using System;
//using System.CodeDom.Compiler;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.IO;
//using System.Xml;
//using Microsoft.CSharp;
//using Microsoft.VisualBasic;
//using Xunit;
//using ProtoBuf;
//using ProtoBuf.CodeGenerator;

//namespace Examples.ProtoGen
//{

//    public class Generator
//    {
//        public static string GetCode(params string[] args)
//        {
//            return GetCode(Console.Error, args);
//        }
//        public static string GetCode(TextWriter stderr, params string[] args)
//        {

//            // ensure we have quiet mode enabled
//            if (Array.IndexOf(args, "-q") < 0)
//            {
//                Array.Resize(ref args, args.Length + 1);
//                args[args.Length - 1] = "-q";
//            }

//            StringWriter sw = new StringWriter();
//            var opt = CommandLineOptions.Parse(sw, args);
//            opt.ErrorWriter = stderr;
//            opt.Execute();
//            return sw.ToString();

//        }

//        [Fact]
//        public void TestPersonAsCSharp()
//        {
//            string csharp = GetCode(@"-i:ProtoGen\person.proto", "-p:detectMissing");
//            File.WriteAllText(@"ProtoGen\person.cs", csharp);
//            TestCompileCSharp(csharp);
//        }
//        [Fact]
//        public void TestKeywords()
//        {
//            string csharp = GetCode(@"-i:ProtoGen\keywords.proto", "-p:detectMissing");
//            File.WriteAllText(@"ProtoGen\keywords.cs", csharp);
//            TestCompileCSharp(csharp);
//        }
//        [Fact]
//        public void TestRpcAsCSharp()
//        {
//            string csharp = GetCode(@"-i:ProtoGen\rpc.proto", "-p:clientProxy");
//            File.WriteAllText(@"ProtoGen\rpc.cs", csharp);
//            TestCompileCSharp(csharp, @"C:\Program Files\Reference Assemblies\Microsoft\Framework\v3.0\System.ServiceModel.dll");
//        }
//        [Fact, Ignore("interface needs some work")]
//        public void TestRpcAsVB()
//        {
//            string vb = GetCode(@"-i:ProtoGen\rpc.proto", "-t:vb", "-p:clientProxy");
//            File.WriteAllText(@"ProtoGen\rpc.vb", vb);
//            TestCompileVisualBasic(vb, @"C:\Program Files\Reference Assemblies\Microsoft\Framework\v3.0\System.ServiceModel.dll");
//        }
//        [Fact]
//        public void TestLITE()
//        {
//            string csharp = GetCode(@"-i:ProtoGen\LITE.proto");
//            File.WriteAllText(@"ProtoGen\LITE.cs", csharp);
//            TestCompileCSharp(csharp, @"C:\Program Files\Reference Assemblies\Microsoft\Framework\v3.0\System.ServiceModel.dll");
//        }

//        [Fact]
//        public void TestWithBomAsCSharp()
//        {
//            using (StringWriter stderr = new StringWriter())
//            {
//                try
//                {

//                    GetCode(stderr, @"-i:ProtoGen\WithBom.proto");
//                    Assert.Fail("Should have failed parsing WithBom.proto");
//                }
//                catch (ProtoParseException)
//                {
//                    string s = stderr.ToString();
//                    Assert.True(s.Contains("The input file should be UTF8 without a byte-order-mark"));
//                }
//            }
//        }

//        [Fact]
//        public void TestWithBomAsCSharpWriteErrorsToFile()
//        {
//            StringWriter stderr = new StringWriter();
//            try
//            {

//                File.WriteAllText("errors.txt", "");
//                GetCode(stderr, @"-i:ProtoGen\WithBom.proto", "-writeErrors", "-o:errors.txt");
//                Assert.Fail("Should have failed parsing WithBom.proto");
//            }
//            catch (ProtoParseException)
//            {
//                string s = stderr.ToString();
//                Assert.Equal("", s);
//                s = File.ReadAllText("errors.txt");
//                Assert.True(s.Contains("The input file should be UTF8 without a byte-order-mark"));
//            }
//        }


//        [Fact]
//        public void TestPersonAsVB()
//        {
//            string code = GetCode(@"-i:ProtoGen\person.proto", "-t:vb");
//            File.WriteAllText(@"ProtoGen\person.vb", code);
//            TestCompileVisualBasic(code);
//        }

//        [Fact]
//        public void TestPersonAsXml()
//        {
//            string csharp = GetCode(@"-i:ProtoGen\person.proto", "-t:xml");
//            File.WriteAllText(@"ProtoGen\person.xml", csharp);
//        }

//        [Fact]
//        public void TestSearchRequestAsXml()
//        {
//            string csharp = GetCode(@"-i:ProtoGen\searchRequest.proto", "-t:xml");
//            File.WriteAllText(@"ProtoGen\searchRequest.xml", csharp);
//        }
//        [Fact]
//        public void TestDescriptorAsXml()
//        {
//            string xml = GetCode(@"-i:ProtoGen\descriptor.proto", @"-i:protobuf-net.proto", "-t:xml");
//            TestLoadXml(xml);
//            File.WriteAllText("descriptor.xml", xml);
//        }

//        [Fact]
//        public void TestDescriptorAsXmlToFile()
//        {
//            GetCode(@"-i:ProtoGen\descriptor.proto", "-o:descriptor.xml", "-t:xml");
//            string viaFile = File.ReadAllText("descriptor.xml");

//            TestLoadXml(viaFile);

//            string viaWriter = GetCode(@"-i:ProtoGen\descriptor.proto", "-t:xml");
//            Assert.Equal(viaFile, viaWriter);
//        }


//        [Fact]
//        public void TestDescriptorAsCSharpBasic()
//        {
//            string code = GetCode(@"-i:ProtoGen\descriptor.proto", @"-i:protobuf-net.proto");
//            TestCompileCSharp(code);
//        }
//        [Fact]
//        public void TestEmptyAsCSharpBasic()
//        {
//            string code = GetCode(@"-i:ProtoGen\empty.proto");
//            TestCompileCSharp(code);
//        }

//        [Fact]
//        public void TestPersonAsCSharpCased()
//        {
//            string code = GetCode(@"-i:ProtoGen\person.proto", "-p:fixCase");
//            File.WriteAllText(@"ProtoGen\personCased.cs", code);
//            TestCompileCSharp(code);
//        }

//        [Fact(Skip = "Hmmm... case-sensitive Location.Location is an issue for this one?")]
//        public void TestDescriptorAsVB_Basic()
//        {
//            string code = GetCode(@"-i:ProtoGen\descriptor.proto", "-t:vb");
//            File.WriteAllText(@"ProtoGen\descriptor.vb", code);
//            TestCompileVisualBasic(code);
//        }

//        [Fact]
//        public void TestDescriptorAsCSharpDetectMissing()
//        {
//            string code = GetCode(@"-i:ProtoGen\descriptor.proto", "-p:detectMissing");
//            TestCompileCSharp(code);
//        }

//        [Fact, ExpectedException(typeof(InvalidOperationException))]
//        public void TestDescriptorAsCSharpPartialMethodsLangVer2()
//        {
//            string code = GetCode(@"-i:ProtoGen\descriptor.proto", "-p:partialMethods");
//            TestCompileCSharpV2(code);
//        }

//        [Fact]
//        public void TestDescriptorAsCSharpPartialMethodsLangVer3()
//        {
//            string code = GetCode(@"-i:ProtoGen\descriptor.proto", "-p:partialMethods");
//            TestCompileCSharpV3(code);
//        }

//        [Fact]
//        public void TestDescriptorAsCSharpPartialMethodsLangVer3DetectMissing()
//        {
//            string code = GetCode(@"-i:ProtoGen\descriptor.proto", "-p:partialMethods", "-p:detectMissing");
//            TestCompileCSharpV3(code);
//        }

//        [Fact]
//        public void TestDescriptorAsCSharpPartialMethodsLangVer3DetectMissingWithXml()
//        {
//            string code = GetCode(@"-i:ProtoGen\descriptor.proto", "-p:partialMethods", "-p:detectMissing", "-p:xml");
//            TestCompileCSharpV3(code);
//        }

//        [Fact]
//        public void TestDescriptorAsCSharpPartialMethodsLangVer3DetectMissingWithDataContract()
//        {
//            string code = GetCode(@"-i:ProtoGen\descriptor.proto", "-p:partialMethods", "-p:detectMissing", "-p:datacontract");
//            TestCompileCSharpV3(code, "System.Runtime.Serialization.dll");
//        }

//        [Fact]
//        public void TestEnums()
//        {
//            string code = GetCode(@"-i:ProtoGen\Enums.proto"); // "-p:partialMethods", "-p:detectMissing", "-p:datacontract");
//            TestCompileCSharpV3(code);
//        }
//        private static void TestLoadXml(string xml)
//        {
//            XmlDocument doc = new XmlDocument();
//            doc.LoadXml(xml);
//            Assert.False(string.IsNullOrEmpty(doc.OuterXml), "xml should be non-empty");
//        }

//        public static void TestCompileCSharp(string code, params string[] extraReferences)
//        {
//            TestCompile<CSharpCodeProvider>(null, code, extraReferences);
//        }
//        public static void TestCompileVisualBasic(string code, params string[] extraReferences)
//        {
//            TestCompile<VBCodeProvider>(null, code, extraReferences);
//        }
//        private static void TestCompileCSharpV2(string code, params string[] extraReferences)
//        {
//            // oddly enough, we mean v3.0 for CompilerVersion; not a typo
//            CSharpCodeProvider compiler = new CSharpCodeProvider(new Dictionary<string, string>() { { "CompilerVersion", "v3.0" } });
//            TestCompile(compiler, code, extraReferences);
//        }
//        private static void TestCompileCSharpV3(string code, params string[] extraReferences)
//        {
//            CSharpCodeProvider compiler = new CSharpCodeProvider(new Dictionary<string, string>() { { "CompilerVersion", "v3.5" } });
//            TestCompile(compiler, code, extraReferences);
//        }

//        [Conditional("DEBUG")]
//        static void DebugWriteAllText(string path, string contents)
//        {
//            File.WriteAllText(path, contents);
//        }

//        private static void TestCompile<T>(T compiler, string code, params string[] extraReferences)
//            where T : CodeDomProvider
//        {
//            if (compiler == null) compiler = (T)Activator.CreateInstance(typeof(T), nonPublic: true);
//            string path = Path.GetTempFileName();
//            try
//            {
//                List<string> refs = new List<string> {
//                    typeof(Uri).Assembly.Location,
//                    typeof(XmlDocument).Assembly.Location,
//                    typeof(Serializer).Assembly.Location
//                };
//                if (extraReferences != null && extraReferences.Length > 0)
//                {
//                    refs.AddRange(extraReferences);
//                }
//                CompilerParameters args = new CompilerParameters(refs.ToArray(), path, false);
//                CompilerResults results = compiler.CompileAssemblyFromSource(args, code);
//                DebugWriteAllText(Path.ChangeExtension("last.cs", compiler.FileExtension), code);
//                ShowErrors(results.Errors);
//                if (results.Errors.Count > 0)
//                {
//                    foreach (CompilerError err in results.Errors)
//                    {
//                        Console.Error.WriteLine(err);
//                    }
//                    throw new InvalidOperationException(
//                        string.Format("{0} found {1} code errors errors",
//                            typeof(T).Name, results.Errors.Count));
//                }

//            }
//            finally
//            {
//                try { File.Delete(path); }
//                catch { } // best effort
//            }
//        }
//        static void ShowErrors(CompilerErrorCollection errors)
//        {
//            if (errors.Count > 0)
//            {
//                foreach (CompilerError err in errors)
//                {
//                    Console.Error.Write(err.IsWarning ? "Warning: " : "Error: ");
//                    Console.Error.WriteLine(err.ErrorText);
//                }
//            }
//        }
//    }
//}
//#endif