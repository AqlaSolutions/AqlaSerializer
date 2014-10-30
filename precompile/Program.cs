﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using AqlaSerializer.Meta;

namespace AqlaSerializer.Precompile
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                Console.WriteLine("aqlaserializer pre-compiler");
                PreCompileContext ctx;
                if (!CommandLineAttribute.TryParse(args, out ctx))
                {
                    return -1;
                }

                if (ctx.Help)
                {
                    Console.WriteLine();
                    Console.WriteLine();
                    Console.WriteLine(ctx.GetUsage());
                    return -1;
                }
                if (!ctx.SanityCheck()) return -1;

                bool allGood = ctx.Execute();
                return allGood ? 0 : -1;
            }
            catch (Exception ex)
            {
                while (ex != null)
                {
                    Console.Error.WriteLine(ex.Message);
                    Console.Error.WriteLine(ex.StackTrace);
                    Console.Error.WriteLine();
                    ex = ex.InnerException;
                }
                return -1;
            }
        }
    }

    /// <summary>
    /// Defines the rules for a precompilation operation
    /// </summary>
    public class PreCompileContext
    {
        /// <summary>
        /// The target framework to use
        /// </summary>
        [CommandLine("f"), CommandLine("framework")]
        public string Framework { get; set; }

        private readonly List<string> probePaths = new List<string>();

        /// <summary>
        /// Locations to check for referenced assemblies
        /// </summary>
        [CommandLine("p"), CommandLine("probe")]
        public List<string> ProbePaths { get { return probePaths; } }

        private readonly List<string> inputs = new List<string>();
        /// <summary>
        /// The paths for assemblies to process
        /// </summary>
        [CommandLine("")]
        public List<string> Inputs { get { return inputs; } }

        /// <summary>
        /// The type name of the serializer to generate
        /// </summary>
        [CommandLine("t"), CommandLine("type")]
        public string TypeName { get; set; }

        /// <summary>
        /// The name of the assembly to generate
        /// </summary>
        [CommandLine("o"), CommandLine("out")]
        public string AssemblyName { get; set; }

        /// <summary>
        /// Show help
        /// </summary>
        [CommandLine("?"), CommandLine("help"), CommandLine("h")]
        public bool Help { get; set; }

        /// <summary>
        /// The accessibility of the generated type
        /// </summary>
        [CommandLine("access")]
        public AqlaSerializer.Meta.RuntimeTypeModel.Accessibility Accessibility { get; set; }

        /// <summary>
        /// The path to the file to use to sign the assembly
        /// </summary>
        [CommandLine("keyfile")]
        public string KeyFile { get; set; }
        /// <summary>
        /// The container to use to sign the assembly
        /// </summary>
        [CommandLine("keycontainer")]
        public string KeyContainer { get; set; }
        /// <summary>
        /// The public key (in hexadecimal) to use to sign the assembly
        /// </summary>
        [CommandLine("publickey")]
        public string PublicKey { get; set; }

        /// <summary>
        /// Create a new instance of PreCompileContext
        /// </summary>
        public PreCompileContext()
        {
            Accessibility = AqlaSerializer.Meta.RuntimeTypeModel.Accessibility.Public;
        }

        static string TryInferFramework(string path)
        {
            string imageRuntimeVersion = null;
            try
            {
                using (var uni = new IKVM.Reflection.Universe())
                {
                    uni.AssemblyResolve += (s, a) => ((IKVM.Reflection.Universe)s).CreateMissingAssembly(a.Name);
                    var asm = uni.LoadFile(path);
                    imageRuntimeVersion = asm.ImageRuntimeVersion;

                    var attr = uni.GetType("System.Attribute, mscorlib");

                    foreach(var attrib in asm.__GetCustomAttributes(attr, false))
                    {
                        if (attrib.Constructor.DeclaringType.FullName == "System.Runtime.Versioning.TargetFrameworkAttribute"
                            && attrib.ConstructorArguments.Count == 1)
                        {
                            var parts = ((string)attrib.ConstructorArguments[0].Value).Split(',');
                            string runtime = null, version = null, profile = null;
                            for (int i = 0; i < parts.Length; i++)
                            {
                                int idx = parts[i].IndexOf('=');
                                if (idx < 0)
                                {
                                    runtime = parts[i];
                                } else
                                {
                                    switch(parts[i].Substring(0,idx))
                                    {
                                        case "Version":
                                            version = parts[i].Substring(idx + 1);
                                            break;
                                        case "Profile":
                                            profile = parts[i].Substring(idx + 1);
                                            break;
                                    }
                                }
                            }
                            if(runtime != null)
                            {
                                var sb = new StringBuilder(runtime);
                                if(version != null)
                                {
                                    sb.Append(Path.DirectorySeparatorChar).Append(version);
                                }
                                if (profile != null)
                                {
                                    sb.Append(Path.DirectorySeparatorChar).Append("Profile").Append(Path.DirectorySeparatorChar).Append(profile);
                                }
                                string targetFramework = sb.ToString();
                                return targetFramework;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) {
                // not really fussed; we could have multiple inputs to try, and the user
                // can always use -f:blah to specify it explicitly
                Debug.WriteLine(ex.Message);
            }

            if (!string.IsNullOrEmpty(imageRuntimeVersion))
            {
                string frameworkPath = Path.Combine(
                    Environment.ExpandEnvironmentVariables(@"%windir%\Microsoft.NET\Framework"),
                    imageRuntimeVersion);
                if (Directory.Exists(frameworkPath)) return frameworkPath;
            }

            return null;
        }
        /// <summary>
        /// Check the context for obvious errrs
        /// </summary>
        public bool SanityCheck()
        {
            bool allGood = true;
            if (inputs.Count == 0)
            {
                Console.Error.WriteLine("No input assemblies");
                allGood = false;
            }
            if (string.IsNullOrEmpty(TypeName))
            {
                Console.Error.WriteLine("No serializer type-name specified");
                allGood = false;
            }
            if (string.IsNullOrEmpty(AssemblyName))
            {
                Console.Error.WriteLine("No output assembly file specified");
                allGood = false;
            }

            if (string.IsNullOrEmpty(Framework))
            {
                foreach (var inp in inputs)
                {
                    string tmp = TryInferFramework(inp);
                    if (tmp != null)
                    {
                        Console.WriteLine("Detected framework: " + tmp);
                        Framework = tmp;
                        break;
                    }
                }
                
            }
            if (string.IsNullOrEmpty(Framework))
            {
                Console.WriteLine("No framework specified; defaulting to " + Environment.Version);
                probePaths.Add(Path.GetDirectoryName(typeof(string).Assembly.Location));
            }
            else
            {
                if (Directory.Exists(Framework))
                { // very clear and explicit
                    probePaths.Add(Framework);
                }
                else
                {
                    string root = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
                    if (string.IsNullOrEmpty(root)) root = Environment.GetEnvironmentVariable("ProgramFiles");
                    root = Path.Combine(root, @"Reference Assemblies\Microsoft\Framework\");
                    if (!Directory.Exists(root))
                    {
                        Console.Error.WriteLine("Framework reference assemblies root folder could not be found");
                        allGood = false;
                    }
                    else
                    {
                        string frameworkRoot = Path.Combine(root, Framework);
                        if (Directory.Exists(frameworkRoot))
                        {
                            // fine
                            probePaths.Add(frameworkRoot);
                        }
                        else
                        {
                            Console.Error.WriteLine("Framework not found: " + Framework);
                            Console.Error.WriteLine("Available frameworks are:");
                            string[] files = Directory.GetFiles(root, "mscorlib.dll", SearchOption.AllDirectories);
                            foreach (var file in files)
                            {
                                string dir = Path.GetDirectoryName(file) ?? string.Empty;
                                if (dir.StartsWith(root)) dir = dir.Substring(root.Length);
                                Console.Error.WriteLine(dir);
                            }
                            allGood = false;
                        }
                    }
                }
            }
            if (!string.IsNullOrEmpty(KeyFile) && !File.Exists(KeyFile))
            {
                Console.Error.WriteLine("Key file not found: " + KeyFile);
                allGood = false;
            }

            foreach (var inp in inputs)
            {
                if(File.Exists(inp)) {
                    string dir = Path.GetDirectoryName(inp);
                    if(!probePaths.Contains(dir)) probePaths.Add(dir);
                }
                else
                {
                    Console.Error.WriteLine("Input not found: " + inp);
                    allGood = false;
                }                
            }
            return allGood;
        }

        IEnumerable<string> ProbeForFiles(string file)
        {
            foreach (var probePath in probePaths)
            {
                string combined = Path.Combine(probePath, file);
                if (File.Exists(combined))
                {
                    yield return combined;
                }
            }
        }
        /// <summary>
        /// Perform the precompilation operation
        /// </summary>
        public bool Execute()
        {
            // model to work with
            var model = TypeModel.Create();

            model.Universe.AssemblyResolve += (sender, args) =>
            {
                string nameOnly = args.Name.Split(',')[0];

                if (nameOnly == "IKVM.Reflection" && args.RequestingAssembly != null && args.RequestingAssembly.FullName.StartsWith("aqlaserializer"))
                {
                    throw new InvalidOperationException("This operation needs access to the aqlaserializer.dll used by your library, **in addition to** the aqlaserializer.dll that is included with the precompiler; the easiest way to do this is to ensure the referenced aqlaserializer.dll is in the same folder as your library.");
                }
                var uni = model.Universe;
                foreach (var tmp in uni.GetAssemblies())
                {
                    if (tmp.GetName().Name == nameOnly) return tmp;
                }
                var asm = ResolveNewAssembly(uni, nameOnly + ".dll");
                if(asm != null) return asm;
                asm = ResolveNewAssembly(uni, nameOnly + ".exe");
                if(asm != null) return asm;

                throw new InvalidOperationException("All assemblies must be resolved explicity; did not resolve: " + args.Name);
            };
            bool allGood = true;
            var mscorlib = ResolveNewAssembly(model.Universe, "mscorlib.dll");
            if (mscorlib == null)
            {
                Console.Error.WriteLine("mscorlib.dll not found!");
                allGood = false;
            }
            ResolveNewAssembly(model.Universe, "System.dll"); // not so worried about whether that one exists...
            if (ResolveNewAssembly(model.Universe, "aqlaserializer.dll") == null)
            {
                Console.Error.WriteLine("aqlaserializer.dll not found!");
                allGood = false;
            }
            if (!allGood) return false;
            var assemblies = new List<IKVM.Reflection.Assembly>();
            MetaType metaType = null;
            foreach (var file in inputs)
            {
                assemblies.Add(model.Load(file));
            }
            // scan for obvious protobuf types
            var attributeType = model.Universe.GetType("System.Attribute, mscorlib");
            var toAdd = new List<IKVM.Reflection.Type>();
            foreach (var asm in assemblies)
            {
                foreach (var type in asm.GetTypes())
                {
                    bool add = false;
                    if (!(type.IsClass || type.IsValueType)) continue;

                    foreach (var attrib in type.__GetCustomAttributes(attributeType, true))
                    {
                        string name = attrib.Constructor.DeclaringType.FullName;
                        switch(name) 
                        {
                            case "AqlaSerializer.SerializableTypeAttribute":
                            case "ProtoBuf.ProtoContractAttribute":
                                add = true;
                                break;
                        }
                        if (add) break;
                    }
                    if (add) toAdd.Add(type);
                }
            }

            if (toAdd.Count == 0)
            {
                Console.Error.WriteLine("No [ProtoBuf.ProtoContract] types found; nothing to do!");
                return false;
            }

            // add everything we explicitly know about
            toAdd.Sort((x, y) => System.String.CompareOrdinal(x.FullName, y.FullName));            
            foreach (var type in toAdd)
            {
                Console.WriteLine("Adding " + type.FullName + "...");
                var tmp = model.Add(type, true);
                if (metaType == null) metaType = tmp; // use this as the template for the framework version
            }
            // add everything else we can find
            model.Cascade();
            var inferred = new List<IKVM.Reflection.Type>();
            foreach (MetaType type in model.MetaTypes)
            {
                if(!toAdd.Contains(type.Type)) inferred.Add(type.Type);
            }
            inferred.Sort((x, y) => System.String.CompareOrdinal(x.FullName, y.FullName));
            foreach (var type in inferred)
            {
                Console.WriteLine("Adding " + type.FullName + "...");
            }

            
            // configure the output file/serializer name, and borrow the framework particulars from
            // the type we loaded
            var options = new RuntimeTypeModel.CompilerOptions
            {
                TypeName = TypeName,
                OutputPath = AssemblyName,
                ImageRuntimeVersion = mscorlib.ImageRuntimeVersion,
                MetaDataVersion = 0x20000, // use .NET 2 onwards
                KeyContainer = KeyContainer,
                KeyFile = KeyFile,
                PublicKey = PublicKey
            };
            if (mscorlib.ImageRuntimeVersion == "v1.1.4322")
            { // .NET 1.1-style
                options.MetaDataVersion = 0x10000;
            }
            if (metaType != null)
            {
                options.SetFrameworkOptions(metaType);
            }
            options.Accessibility = this.Accessibility;
            Console.WriteLine("Compiling " + options.TypeName + " to " + options.OutputPath + "...");
            // GO WORK YOUR MAGIC, CRAZY THING!!
            model.Compile(options);
            Console.WriteLine("All done");

            return true;

        }
        
        private IKVM.Reflection.Assembly ResolveNewAssembly(IKVM.Reflection.Universe uni, string fileName)
        {
            foreach (var match in ProbeForFiles(fileName))
            {
                var asm = uni.LoadFile(match);
                if (asm != null)
                {
                    Console.WriteLine("Resolved " + match);
                    return asm;
                }
            }
            return null;
        }

        /// <summary>
        /// Return the syntax guide for the utility
        /// </summary>
        public string GetUsage()
        {
            return
                @"Generates a serialization dll that can be used with just the
(platform-specific) protobuf-net core, allowing fast and efficient
serialization even on light frameworks (CF, SL, SP7, Metro, etc).

The input assembly(ies) is(are) anaylsed for types decorated with
[ProtoBuf.ProtoContract]. All such types are added to the model, as are any
types that they require.

Note: the compiler must be able to resolve a aqlaserializer.dll
that is suitable for the target framework; this is done most simply
by ensuring that the appropriate aqlaserializer.dll is next to the
input assembly.

Options:

    -f[ramework]:<framework>
           Can be an explicit path, or a path relative to:
           Reference Assemblies\Microsoft\Framework
    -o[ut]:<file>
           Output dll path
    -t[ype]:<typename>
           Type name of the serializer to generate
    -p[robe]:<path>
           Additional directory to probe for assemblies
    -access:<access>
           Specify accessibility of generated serializer
           to 'Public' or 'Internal'
    -keyfile:<file>
           Sign with the file (snk, etc) specified
    -keycontainer:<container>
           Sign with the container specified
    -publickey:<key>
           Sign with the public key specified (as hex)
    <file>
           Input file to analyse

Example:

    precompile -f:.NETCore\v4.5 MyDtos\My.dll -o:MySerializer.dll
        -t:MySerializer";
        }
    }
    /// <summary>
    /// Defines a mapping from command-line attributes to properties
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class CommandLineAttribute : Attribute
    {
        /// <summary>
        /// Attempt to parse the incoming command-line switches, matching by prefix
        /// onto properties of the specified type
        /// </summary>
        public static bool TryParse<T>(string[] args, out T result) where T : class, new()
        {
            result = new T();
            bool allGood = true;
            var props = typeof(T).GetProperties();

            char[] leadChars = {'/', '+', '-'};
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i].Trim(), prefix, value;
                if(arg.IndexOfAny(leadChars) == 0)
                {
                    int idx = arg.IndexOf(':');
                    if (idx < 0)
                    {
                        prefix = arg.Substring(1);
                        value = "";
                    }
                    else
                    {
                        prefix = arg.Substring(1,idx - 1);
                        value = arg.Substring(idx + 1);
                    }    
                }
                else
                {
                    prefix = "";
                    value = arg;
                }
                
                System.Reflection.PropertyInfo foundProp = null;

                foreach (var prop in props)
                {
                    foreach (CommandLineAttribute atttib in prop.GetCustomAttributes(typeof(CommandLineAttribute), true))
                    {
                        if (atttib.Prefix == prefix)
                        {
                            foundProp = prop;
                            break;
                        }
                    }
                    if (foundProp != null) break;
                }

                if (foundProp == null)
                {
                    allGood = false;
                    Console.Error.WriteLine("Argument not understood: " + arg);
                }
                else
                {
                    if (foundProp.PropertyType == typeof(string))
                    {
                        foundProp.SetValue(result, value, null);
                    }
                    else if (foundProp.PropertyType == typeof(List<string>))
                    {
                        ((List<string>)foundProp.GetValue(result, null)).Add(value);
                    }
                    else if (foundProp.PropertyType == typeof(bool))
                    {
                        foundProp.SetValue(result, true, null);
                    }
                    else if (foundProp.PropertyType.IsEnum)
                    {
                        object parsedValue;
                        try {
                            parsedValue = Enum.Parse(foundProp.PropertyType, value, true); 
                        } catch {
                            Console.Error.WriteLine("Invalid option for: " + arg);
                            Console.Error.WriteLine("Options: " + string.Join(", ", Enum.GetNames(foundProp.PropertyType)));
                            allGood = false;
                            parsedValue = null;
                        }
                        if (parsedValue != null) foundProp.SetValue(result, parsedValue, null);
                    }
                }
            }

            return allGood;
        }
        private readonly string prefix;
        /// <summary>
        /// Create a new CommandLineAttribute object for the given prefix
        /// </summary>
        public CommandLineAttribute(string prefix) { this.prefix = prefix; }
        /// <summary>
        /// The prefix to recognise this command-line switch
        /// </summary>
        public string Prefix { get { return prefix; } }
    }

}
