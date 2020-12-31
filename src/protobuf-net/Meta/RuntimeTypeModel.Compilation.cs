// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if FEAT_COMPILER
#if NO_RUNTIME
#error Compiler can't work without runtime!
#endif
using System;
using System.Collections;
using System.Diagnostics;
#if !NO_GENERICS
using System.Collections.Generic;
#endif
#if !PORTABLE
using System.Runtime.Serialization;
#endif
using System.Text;

#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#if FEAT_COMPILER
using IKVM.Reflection.Emit;
using TriAxis.RunSharp;
#endif
#else
using System.Reflection;
#if FEAT_COMPILER
using System.Reflection.Emit;
using TriAxis.RunSharp;
#endif
#endif
#if FEAT_COMPILER
using AqlaSerializer.Compiler;
#endif
#if !FEAT_IKVM
using AqlaSerializer.Meta.Data;
#endif
using AqlaSerializer;
using AqlaSerializer.Serializers;
using System.Threading;
using System.IO;
using AltLinq; using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

namespace AqlaSerializer.Meta
{
#if !NO_GENERiCS
    using TypeSet = Dictionary<Type, object>;
    using TypeList = List<Type>;

#else
    using TypeSet = System.Collections.Hashtable;
    using TypeList = System.Collections.ArrayList;
#endif
    
    partial class RuntimeTypeModel
    {
        internal ITypeMapper RunSharpTypeMapper { get; }


#if !FX11
        /// <summary>
        /// Compiles the serializers individually; this is *not* a full
        /// standalone compile, but can significantly boost performance
        /// while allowing additional types to be added.
        /// </summary>
        /// <remarks>An in-place compile can access non-public types / members</remarks>
        public void CompileInPlace()
        {
#if DISABLE_COMPILE_IN_PLACE
            return;
#endif
            BuildAllSerializers();
            foreach (MetaType type in _types)
            {
                type.CompileInPlace();
            }
        }
#endif

#if FEAT_IKVM
        readonly IKVM.Reflection.Universe universe;
        /// <summary>
        /// Load an assembly into the model's universe
        /// </summary>
        public Assembly Load(string path)
        {
            return universe.LoadFile(path);
        }
        /// <summary>
        /// Gets the IKVM Universe that relates to this model
        /// </summary>
        public Universe Universe { get { return universe; } }

        /// <summary>
        /// Adds support for an additional type in this model, optionally
        /// applying inbuilt patterns. If the type is already known to the
        /// model, the existing type is returned **without** applying
        /// any additional behaviour.
        /// </summary>
        public MetaType Add(string assemblyQualifiedTypeName, bool applyDefaults)
        {
            Type type = universe.GetType(assemblyQualifiedTypeName, true);
            return Add(type, applyDefaults);
        }
        /// <summary>
        /// Adds support for an additional type in this model, optionally
        /// applying inbuilt patterns. If the type is already known to the
        /// model, the existing type is returned **without** applying
        /// any additional behaviour.
        /// </summary>
        public MetaType Add(System.Type type, bool applyDefaultBehaviour)
        {
            return Add(MapType(type), applyDefaultBehaviour);
        }
        /// <summary>
        /// Obtains the MetaType associated with a given Type for the current model,
        /// allowing additional configuration.
        /// </summary>
        public MetaType this[System.Type type] { get { return this[MapType(type)]; } }
        
#endif

        private void BuildAllSerializers()
        {
            // note that types.Count may increase during this operation, as some serializers
            // bring other types into play
            for (int i = 0; i < _types.Count; i++)
            {
                // the primary purpose of this is to force the creation of the Serializer
                MetaType mt = (MetaType)_types[i];
                if (mt.Serializer == null)
                    throw new InvalidOperationException("No serializer available for " + mt.Type.Name);
            }
        }
#if !SILVERLIGHT
        internal sealed class SerializerMethods : IComparable
        {
            int IComparable.CompareTo(object obj)
            {
                if (obj == null) throw new ArgumentException("obj");
                SerializerMethods other = (SerializerMethods)obj;

                // we want to bunch all the items with the same base-type together, but we need the items with a
                // different base **first**.
                if (this.BaseKey == this.MetaKey)
                {
                    if (other.BaseKey == other.MetaKey)
                    { // neither is a subclass
                        return this.MetaKey.CompareTo(other.MetaKey);
                    }
                    else
                    { // "other" (only) is involved in inheritance; "other" should be first
                        return 1;
                    }
                }
                else
                {
                    if (other.BaseKey == other.MetaKey)
                    { // "this" (only) is involved in inheritance; "this" should be first
                        return -1;
                    }
                    else
                    { // both are involved in inheritance
                        int result = this.BaseKey.CompareTo(other.BaseKey);
                        if (result == 0) result = this.MetaKey.CompareTo(other.MetaKey);
                        return result;
                    }
                }
            }
            public readonly int MetaKey, BaseKey;
            public readonly MetaType Type;
            public readonly SerializerMethodPair BasicPair;
            public readonly SerializerMethodPair RootPair;
            public readonly SerializerMethodPair LateReferencePair;
            
            public SerializerMethods(int metaKey, int baseKey, MetaType type, SerializerMethodPair basicPair, SerializerMethodPair rootPair, SerializerMethodPair lateReferencePair)
            {
                this.MetaKey = metaKey;
                this.BaseKey = baseKey;
                BasicPair = basicPair;
                RootPair = rootPair;
                LateReferencePair = lateReferencePair;
                this.Type = type;
            }
        }

        internal sealed class SerializerMethodPair
        {
            public readonly IProtoSerializerWithWireType Serializer;
            public readonly MethodBuilder Serialize, Deserialize;
            public readonly MethodContext SerializeBody, DeserializeBody;
            public SerializerMethodPair(IProtoSerializerWithWireType serializer, MethodBuilder serialize,
                MethodBuilder deserialize, MethodContext serializeBody, MethodContext deserializeBody)
            {
                this.Serialize = serialize;
                this.Deserialize = deserialize;
                this.SerializeBody = serializeBody;
                this.DeserializeBody = deserializeBody;
                Serializer = serializer;
            }
        }

        private static int _autoCompileCounter;

        /// <summary>
        /// Fully compiles the current model into a static-compiled model instance
        /// </summary>
        /// <remarks>A full compilation is restricted to accessing public types / members</remarks>
        /// <returns>An instance of the newly created compiled type-model</returns>
        public TypeModel Compile()
        {
#if DEBUG
            if (ValidateDll != null)
            {
                string name = "AutoCompile" + Interlocked.Increment(ref _autoCompileCounter) + ".dll";
                var r = Compile("AutoCompile", name);
                RaiseValidateDll(name);
                return r;
            }
#endif
            return Compile(null, null);
        }

        MethodContext Override(TypeBuilder type, string name)
        {
            MethodInfo baseMethod = type.BaseType.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);

            ParameterInfo[] parameters = baseMethod.GetParameters();
            Type[] paramTypes = new Type[parameters.Length];
            for (int i = 0; i < paramTypes.Length; i++)
            {
                paramTypes[i] = parameters[i].ParameterType;
            }
            MethodContext methodGenCtx;
            MethodBuilder newMethod = EmitDefineMethod(
                type,
                baseMethod.Name,
                (baseMethod.Attributes & ~MethodAttributes.Abstract &~MethodAttributes.NewSlot) | MethodAttributes.Final,
                baseMethod.CallingConvention,
                baseMethod.ReturnType,
                parameters.Select((p, i) => new MethodContext.ParameterGenInfo(p.ParameterType, p.Name, i + 1)).ToArray(),
                true,
                out methodGenCtx);
            type.DefineMethodOverride(newMethod, baseMethod);
            return methodGenCtx;
        }

#if FEAT_IKVM
        /// <summary>
        /// Inspect the model, and resolve all related types
        /// </summary>
        public void Cascade()
        {
            BuildAllSerializers();
        }
        /// <summary>
        /// Translate a System.Type into the universe's type representation
        /// </summary>
        protected internal override Type MapType(System.Type type, bool demand)
        {
            if (type == null) return null;
#if DEBUG
            if (type.Assembly == typeof(IKVM.Reflection.Type).Assembly)
            {
                throw new InvalidOperationException(string.Format(
                    "Somebody is passing me IKVM types! {0} should be fully-qualified at the call-site",
                    type.Name));
            }
#endif
            Type result = universe.GetType(type.AssemblyQualifiedName);
            
            if(result == null)
            {
                // things also tend to move around... *a lot* - especially in WinRT; search all as a fallback strategy
                foreach (Assembly a in universe.GetAssemblies())
                {
                    result = a.GetType(type.FullName);
                    if (result != null) break;
                }
                if (result == null && demand)
                {
                    throw new InvalidOperationException("Unable to map type: " + type.AssemblyQualifiedName);
                }
            }
            return result;
        }
#endif

        public enum CompilerIterativeMode
        {
            /// <summary>
            /// Always recompile
            /// </summary>
            Disabled=0,
            /// <summary>
            /// Appends iterative data which can be read in <see cref="ReadAndAppendData"/> mode
            /// </summary>
            AppendData,
            /// <summary>
            /// Same as <see cref="AppendData"/> but also checks previously written iterative data to avoid recompilation
            /// </summary>
            ReadAndAppendData
        }

        public enum CompilerParallelizationMode
        {
            Disabled = 0,
            PerType,
            Maximum,
        }

        /// <summary>
        /// Represents configuration options for compiling a model to 
        /// a standalone assembly.
        /// </summary>
        public sealed class CompilerOptions
        {
#if !FEAT_IKVM
            public CompilerParallelizationMode ParallelizationMode { get; set; }
#endif
            /// <summary>
            /// Also compile in place this model; default: true.
            /// </summary>
            public bool AlsoCompileInPlace { get; set; } = true;

            /// <summary>
            /// Allows to avoid unnecessary recompilation
            /// </summary>
            public CompilerIterativeMode IterativeMode { get; set; }

            /// <summary>
            /// Import framework options from an existing type
            /// </summary>
            public void SetFrameworkOptions(MetaType from)
            {
                if (from == null) throw new ArgumentNullException(nameof(@from));
                AttributeMap[] attribs = AttributeMap.Create(from.Model, from.Type.Assembly);
                foreach (AttributeMap attrib in attribs)
                {
                    if (attrib.AttributeType.FullName == "System.Runtime.Versioning.TargetFrameworkAttribute")
                    {
                        object tmp;
                        if (attrib.TryGet("FrameworkName", out tmp)) TargetFrameworkName = (string)tmp;
                        if (attrib.TryGet("FrameworkDisplayName", out tmp)) TargetFrameworkDisplayName = (string)tmp;
                        break;
                    }
                }
            }

            /// <summary>
            /// The TargetFrameworkAttribute FrameworkName value to burn into the generated assembly
            /// </summary>
            public string TargetFrameworkName { get; set; }

            /// <summary>
            /// The TargetFrameworkAttribute FrameworkDisplayName value to burn into the generated assembly
            /// </summary>
            public string TargetFrameworkDisplayName { get; set; }

            /// <summary>
            /// The name of the TypeModel class to create
            /// </summary>
            public string TypeName { get; set; }

            /// <summary>
            /// The path for the new dll
            /// </summary>
            public string OutputPath { get; set; }

            /// <summary>
            /// The runtime version for the generated assembly
            /// </summary>
            public string ImageRuntimeVersion { get; set; }

            /// <summary>
            /// The runtime version for the generated assembly
            /// </summary>
            public int MetaDataVersion { get; set; }


            /// <summary>
            /// The acecssibility of the generated serializer
            /// </summary>
            public Accessibility Accessibility { get; set; } = Accessibility.Public;

#if FEAT_IKVM
            /// <summary>
            /// The name of the container that holds the key pair.
            /// </summary>
            public string KeyContainer { get; set; }
            /// <summary>
            /// The path to a file that hold the key pair.
            /// </summary>
            public string KeyFile { get; set; }

            /// <summary>
            /// The public  key to sign the file with.
            /// </summary>
            public string PublicKey { get; set; }
#endif
        }
        /// <summary>
        /// Type accessibility
        /// </summary>
        public enum Accessibility
        {
            /// <summary>
            /// Available to all callers
            /// </summary>
            Public,
            /// <summary>
            /// Available to all callers in the same assembly, or assemblies specified via [InternalsVisibleTo(...)]
            /// </summary>
            Internal
        }
        /// <summary>
        /// Fully compiles the current model into a static-compiled serialization dll
        /// (the serialization dll still requires protobuf-net for support services).
        /// </summary>
        /// <remarks>A full compilation is restricted to accessing public types / members</remarks>
        /// <param name="name">The name of the TypeModel class to create</param>
        /// <param name="path">The path for the new dll</param>
        /// <returns>An instance of the newly created compiled type-model</returns>
        public TypeModel Compile(string name, string path)
        {
#if FAKE_COMPILE
            BuildAllSerializers();
            return this;
#endif
            CompilerOptions options = new CompilerOptions
            {
                TypeName = name,
                OutputPath = path
            };
            return Compile(options);
        }

        /// <summary>
        /// Fully compiles the current model into a static-compiled serialization dll
        /// (the serialization dll still requires protobuf-net for support services).
        /// </summary>
        /// <remarks>A full compilation is restricted to accessing public types / members</remarks>
        /// <returns>An instance of the newly created compiled type-model</returns>
        public TypeModel Compile(CompilerOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            string typeName = options.TypeName;
            string path = options.OutputPath;
            string assemblyName, moduleName;

            bool save = !Helpers.IsNullOrEmpty(path);
            if (Helpers.IsNullOrEmpty(typeName))
            {
                if (save) throw new ArgumentNullException("typeName");
                typeName = Guid.NewGuid().ToString();
            }

            if (path == null)
            {
                assemblyName = typeName;
                moduleName = assemblyName + ".dll";
            }
            else
            {
                assemblyName = new System.IO.FileInfo(System.IO.Path.GetFileNameWithoutExtension(path)).Name;
                moduleName = assemblyName + System.IO.Path.GetExtension(path);
            }

            BuildAllSerializers();

            CompiledAssemblyEqualityAttribute eqAttr = null;

            if (options.IterativeMode != CompilerIterativeMode.Disabled)
            {
                string outputPath = options.OutputPath;
                if (string.IsNullOrEmpty(outputPath))
                    throw new ArgumentException("Can't use IterativeMode parameter without OutputPath specified");
                if (string.IsNullOrEmpty(options.TypeName))
                    throw new ArgumentException("Can't use IterativeMode parameter without TypeName specified");

                var b = new DebugSchemaBuilder();
                foreach (var s in MetaTypes)
                {
                    if (s.RootSerializer is ForbiddenRootStub)
                        s.Serializer.WriteDebugSchema(b);
                    else
                        s.RootSerializer.WriteDebugSchema(b);
                }
                var hashFunction = SHA1.Create();
                string schema = b.ToString();
                string hash = Convert.ToBase64String(hashFunction.ComputeHash(Encoding.UTF8.GetBytes(schema)));
                System.Reflection.Assembly executingAssembly = System.Reflection.Assembly.GetExecutingAssembly();
                var ver = executingAssembly.GetName().Version;
                eqAttr = new CompiledAssemblyEqualityAttribute(ver.Major, ver.Minor, ver.Revision, ver.Build, hash, schema.Length, options.Accessibility == Accessibility.Public);

                if (File.Exists(outputPath) && options.IterativeMode == CompilerIterativeMode.ReadAndAppendData)
                {
                    try
                    {
                        if (CheckIterativeCompilation(outputPath, executingAssembly, eqAttr))
                        {
#if FEAT_IKVM
                            return null;
#else
                            return CreateCompiledTypeModel(System.Reflection.Assembly.LoadFrom(outputPath).GetType(options.TypeName, true));
#endif
                        }
                    }
                    catch (Exception e)
                    {
                        Debugger.Log(0, "AqlaSerializer", e.ToString());
#if DEBUG
                        if (Debugger.IsAttached)
                            Debugger.Break();
#endif
                    }
                }
            }

            if (options.AlsoCompileInPlace)
                CompileInPlace();
            Freeze();

#if FEAT_IKVM
            IKVM.Reflection.AssemblyName an = new IKVM.Reflection.AssemblyName();
            an.Name = assemblyName;
            AssemblyBuilder asm = universe.DefineDynamicAssembly(an, AssemblyBuilderAccess.Save);
            if (!Helpers.IsNullOrEmpty(options.KeyFile))
            {
                asm.__SetAssemblyKeyPair(new StrongNameKeyPair(File.OpenRead(options.KeyFile)));
            }
            else if (!Helpers.IsNullOrEmpty(options.KeyContainer))
            {
                asm.__SetAssemblyKeyPair(new StrongNameKeyPair(options.KeyContainer));
            }
            else if (!Helpers.IsNullOrEmpty(options.PublicKey))
            {
                asm.__SetAssemblyPublicKey(FromHex(options.PublicKey));
            }
            if(!Helpers.IsNullOrEmpty(options.ImageRuntimeVersion) && options.MetaDataVersion != 0)
            {
                asm.__SetImageRuntimeVersion(options.ImageRuntimeVersion, options.MetaDataVersion);
            }
            ModuleBuilder module = asm.DefineDynamicModule(moduleName, path);
#elif NETSTANDARD
            AssemblyName an = new AssemblyName();
            an.Name = assemblyName;
            AssemblyBuilder asm = AssemblyBuilder.DefineDynamicAssembly(an, AssemblyBuilderAccess.RunAndCollect);
            ModuleBuilder module = asm.DefineDynamicModule(moduleName);
#else
            AssemblyName an = new AssemblyName();
            an.Name = assemblyName;
            AssemblyBuilder asm = AppDomain.CurrentDomain.DefineDynamicAssembly(an,
                (save ? AssemblyBuilderAccess.RunAndSave : AssemblyBuilderAccess.Run)
                );
            ModuleBuilder module = save ? asm.DefineDynamicModule(moduleName, path)
                                        : asm.DefineDynamicModule(moduleName);
#endif

            WriteAssemblyAttributes(options, assemblyName, asm, eqAttr);

            TypeBuilder type = WriteBasicTypeModel(options, typeName, module);
            
            bool hasInheritance;
            SerializerMethods[] methodPairs;
            Compiler.CompilerContext.ILVersion ilVersion;

            WriteSerializers(options, assemblyName, type, out hasInheritance, out methodPairs, out ilVersion);
            
            ILGenerator il;
            int knownTypesCategory;
            FieldBuilder knownTypes;
            Type knownTypesLookupType;
            WriteGetKeyImpl(type, hasInheritance, methodPairs, ilVersion, assemblyName, out il, out knownTypesCategory, out knownTypes, out knownTypesLookupType);

            // trivial flags
            il = Override(type, "SerializeDateTimeKind").GetILGenerator();
            il.Emit(IncludeDateTimeKind ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ret);
            // end: trivial flags

            Compiler.CompilerContext ctx = WriteSerializeDeserializeJumpTable(assemblyName, type, methodPairs, ilVersion, ref il);

            WriteConstructors(type, methodPairs, ref il, knownTypesCategory, knownTypes, knownTypesLookupType, ctx);
            
            Type finalType = type.CreateType();
            if (!Helpers.IsNullOrEmpty(path))
            {
#if NETSTANDARD
                new Lokad.ILPack.AssemblyGenerator().GenerateAssembly(asm, path);
#else
                asm.Save(path);
#endif
                Helpers.DebugWriteLine("Wrote dll:" + path);
                _compiledToPath = path;
            }
#if FEAT_IKVM
            return null;
#else
            return CreateCompiledTypeModel(finalType);
#endif
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        bool CheckIterativeCompilation(string outputPath, System.Reflection.Assembly executingAssembly, CompiledAssemblyEqualityAttribute eqAttr)
        {
            // TODO use AssemblyLoadContext and AssemblyDependencyResolver instead of AppDomain
            // https://docs.microsoft.com/en-us/dotnet/standard/assembly/unloadability
            // but this is possible only in .net core, not in .net standard currently
#if NETSTANDARD
            return false;
#else
            // lock file
            //using (var f = File.Open(options.OutputPath, FileMode.Open, FileAccess.Read, FileShare.None))
            byte[] data;
            {
                bool same = false;
                var domain = AppDomain.CreateDomain(
                    "CompiledAssemblyCheck",
                    null,
                    new AppDomainSetup()
                    {
                        LoaderOptimization = LoaderOptimization.MultiDomainHost,
                        PrivateBinPath = Path.GetDirectoryName(Path.GetFullPath(outputPath)),
                        ShadowCopyFiles = "true"
                    });
                try
                {
                    //var data = new byte[f.Length];
                    //f.Read(data, 0, data.Length);

                    data = File.ReadAllBytes(outputPath);
                    CompiledAssemblyEqualityAttribute otherAttr = null;

                    string myPath = executingAssembly.GetName().CodeBase;
                    var proxy = (AssemblyAnyLoadProxy)domain.CreateInstanceFromAndUnwrap(myPath, typeof(AssemblyAnyLoadProxy).FullName);

                    try
                    {
                        proxy.ReflectionLoadFrom(myPath);
                    }
                    catch
                    {
                        try
                        {
                            proxy.ReflectionLoad(executingAssembly.FullName);
                        }
                        catch
                        {
                            try
                            {
                                proxy.ReflectionLoad(executingAssembly.GetName().Name);
                            }
                            catch
                            {
                                proxy.ReflectionLoad(File.ReadAllBytes(new Uri(myPath).LocalPath));
                            }
                        }
                    }

                    int outputAssemblyId = proxy.ReflectionLoad(data);

                    same = proxy.CompareAttribute(outputAssemblyId, typeof(CompiledAssemblyEqualityAttribute).FullName, eqAttr.GetConstructorArgs());

                }
                finally
                {
                    AppDomain.Unload(domain);
                }
                if (same) return true;
            }
#endif
            File.Delete(outputPath);
            return false;
        }

        [Serializable]
        class AssemblyAnyLoadProxy : MarshalByRefObject
        {
            List<System.Reflection.Assembly> _assemblies=new List<System.Reflection.Assembly>();

            public int Load(byte[] raw)
            {
                var asm = System.Reflection.Assembly.Load(raw);
                return Add(asm);
            }

            public int Load(string assemblyName)
            {
                var asm = System.Reflection.Assembly.Load(assemblyName);
                return Add(asm);
            }

            public int LoadFrom(string assemblyFile)
            {
                var asm = System.Reflection.Assembly.LoadFrom(assemblyFile);
                return Add(asm);
            }

            public int ReflectionLoad(byte[] raw)
            {
                var asm = System.Reflection.Assembly.ReflectionOnlyLoad(raw);
                return Add(asm);
            }

            public int ReflectionLoad(string assemblyName)
            {
                var asm = System.Reflection.Assembly.ReflectionOnlyLoad(assemblyName);
                return Add(asm);
            }

            public int ReflectionLoadFrom(string assemblyFile)
            {
                var asm = System.Reflection.Assembly.ReflectionOnlyLoadFrom(assemblyFile);
                return Add(asm);
            }

            int Add(System.Reflection.Assembly asm)
            {
                _assemblies.Add(asm);
                return _assemblies.Count - 1;
            }
            
            public bool CompareAttribute(int assembly, string fullName, object[] args)
            {
                var attrs = System.Reflection.CustomAttributeData.GetCustomAttributes(_assemblies[assembly]);
                System.Reflection.CustomAttributeData attr = attrs.FirstOrDefault(x => x.Constructor.DeclaringType.FullName == fullName);
                return attr?.ConstructorArguments.Select(x => x.Value).SequenceEqual(args) ?? false;
            }
        }
#if !FEAT_IKVM
        TypeModel CreateCompiledTypeModel(Type finalType)
        {
            var r = (TypeModel)Activator.CreateInstance(finalType);
            r.AllowReferenceVersioningSeeking = AllowReferenceVersioningSeeking;
            r.AllowStreamRewriting = AllowStreamRewriting;
            r.ForceSerializationDuringClone = ForceSerializationDuringClone;
            r.RecursionDepthLimit = RecursionDepthLimit;
            r.ReferenceVersioningSeekingObjectsPredictedSize = ReferenceVersioningSeekingObjectsPredictedSize;
            return r;
        }
#endif
        string _compiledToPath;

#if FEAT_IKVM
        private byte[] FromHex(string value)
        {
            if (Helpers.IsNullOrEmpty(value)) throw new ArgumentNullException("value");
            int len = value.Length / 2;
            byte[] result = new byte[len];
            for(int i = 0 ; i < len ; i++)
            {
                result[i] = Convert.ToByte(value.Substring(i * 2, 2), 16);
            }
            return result;
        }
#endif
        private void WriteConstructors(TypeBuilder type, SerializerMethods[] methodPairs, ref ILGenerator il, int knownTypesCategory, FieldBuilder knownTypes, Type knownTypesLookupType, Compiler.CompilerContext ctx)
        {
            type.DefineDefaultConstructor(MethodAttributes.Public);
            il = type.DefineTypeInitializer().GetILGenerator();
            switch (knownTypesCategory)
            {
                case KnownTypes_Array:
                    {
                        Compiler.CompilerContext.LoadValue(il, _types.Count);
                        il.Emit(OpCodes.Newarr, ctx.MapType(typeof(System.Type)));
                        int index = 0;
                        foreach (SerializerMethods pair in methodPairs)
                        {
                            il.Emit(OpCodes.Dup);
                            Compiler.CompilerContext.LoadValue(il, index);
                            il.Emit(OpCodes.Ldtoken, pair.Type.Type);
                            il.EmitCall(OpCodes.Call, ctx.MapType(typeof(System.Type)).GetMethod("GetTypeFromHandle"), null);
                            il.Emit(OpCodes.Stelem_Ref);
                            index++;
                        }
                        il.Emit(OpCodes.Stsfld, knownTypes);
                        il.Emit(OpCodes.Ret);
                    }
                    break;
                case KnownTypes_Dictionary:
                    {
                        Compiler.CompilerContext.LoadValue(il, _types.Count);
                        //LocalBuilder loc = il.DeclareLocal(knownTypesLookupType);
                        il.Emit(OpCodes.Newobj, knownTypesLookupType.GetConstructor(new Type[] { MapType(typeof(int)) }));
                        il.Emit(OpCodes.Stsfld, knownTypes);
                        int typeIndex = 0;
                        foreach (SerializerMethods pair in methodPairs)
                        {
                            il.Emit(OpCodes.Ldsfld, knownTypes);
                            il.Emit(OpCodes.Ldtoken, pair.Type.Type);
                            il.EmitCall(OpCodes.Call, ctx.MapType(typeof(System.Type)).GetMethod("GetTypeFromHandle"), null);
                            int keyIndex = typeIndex++, lastKey = pair.BaseKey;
                            if (lastKey != pair.MetaKey) // not a base-type; need to give the index of the base-type
                            {
                                keyIndex = -1; // assume epic fail
                                for (int j = 0; j < methodPairs.Length; j++)
                                {
                                    if (methodPairs[j].BaseKey == lastKey && methodPairs[j].MetaKey == lastKey)
                                    {
                                        keyIndex = j;
                                        break;
                                    }
                                }
                            }
                            Compiler.CompilerContext.LoadValue(il, keyIndex);
                            il.EmitCall(OpCodes.Callvirt, knownTypesLookupType.GetMethod("Add", new Type[] { MapType(typeof(System.Type)), MapType(typeof(int)) }), null);
                        }
                        il.Emit(OpCodes.Ret);
                    }
                    break;
                case KnownTypes_Hashtable:
                    {
                        Compiler.CompilerContext.LoadValue(il, _types.Count);
                        il.Emit(OpCodes.Newobj, knownTypesLookupType.GetConstructor(new Type[] { MapType(typeof(int)) }));
                        il.Emit(OpCodes.Stsfld, knownTypes);
                        int typeIndex = 0;
                        foreach (SerializerMethods pair in methodPairs)
                        {
                            il.Emit(OpCodes.Ldsfld, knownTypes);
                            il.Emit(OpCodes.Ldtoken, pair.Type.Type);
                            il.EmitCall(OpCodes.Call, ctx.MapType(typeof(System.Type)).GetMethod("GetTypeFromHandle"), null);
                            int keyIndex = typeIndex++, lastKey = pair.BaseKey;
                            if (lastKey != pair.MetaKey) // not a base-type; need to give the index of the base-type
                            {
                                keyIndex = -1; // assume epic fail
                                for (int j = 0; j < methodPairs.Length; j++)
                                {
                                    if (methodPairs[j].BaseKey == lastKey && methodPairs[j].MetaKey == lastKey)
                                    {
                                        keyIndex = j;
                                        break;
                                    }
                                }
                            }
                            Compiler.CompilerContext.LoadValue(il, keyIndex);
                            il.Emit(OpCodes.Box, MapType(typeof(int)));
                            il.EmitCall(OpCodes.Callvirt, knownTypesLookupType.GetMethod("Add", new Type[] { MapType(typeof(object)), MapType(typeof(object)) }), null);
                        }
                        il.Emit(OpCodes.Ret);
                    }
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        private Compiler.CompilerContext WriteSerializeDeserializeJumpTable(string assemblyName, TypeBuilder type, SerializerMethods[] methodPairs, Compiler.CompilerContext.ILVersion ilVersion, ref ILGenerator il)
        {
            Compiler.CompilerContext ctx;
            // arg0 = this, arg1 = key, arg2=obj, arg3=dest
            Compiler.CodeLabel[] jumpTable;
            {
                var genInfo = Override(type, "Serialize");
                il = genInfo.GetILGenerator();
                ctx = new Compiler.CompilerContext(genInfo, false, true, methodPairs, this, ilVersion, assemblyName, MapType(typeof(object)));
                jumpTable = new Compiler.CodeLabel[_types.Count];
                for (int i = 0; i < jumpTable.Length; i++)
                {
                    jumpTable[i] = ctx.DefineLabel();
                }
                il.Emit(OpCodes.Ldarg_1);
                ctx.Switch(jumpTable);
                ctx.Return();
                for (int i = 0; i < jumpTable.Length; i++)
                {
                    SerializerMethods pair = methodPairs[i];
                    ctx.MarkLabel(jumpTable[i]);
                    var labelNoRoot = ctx.DefineLabel();

                    il.Emit(OpCodes.Ldarg_S, 4);
                    ctx.BranchIfFalse(labelNoRoot, true);

                    WriteSerializePair(il, ctx, pair, pair.RootPair);

                    ctx.MarkLabel(labelNoRoot);

                    WriteSerializePair(il, ctx, pair, pair.BasicPair);
                }
            }

            {
                var genInfo = Override(type, "Deserialize");
                il = genInfo.GetILGenerator();
                ctx = new Compiler.CompilerContext(genInfo, false, false, methodPairs, this, ilVersion, assemblyName, MapType(typeof(object)));
                // arg0 = this, arg1 = key, arg2=obj, arg3=source
                for (int i = 0; i < jumpTable.Length; i++)
                {
                    jumpTable[i] = ctx.DefineLabel();
                }
                il.Emit(OpCodes.Ldarg_1);
                ctx.Switch(jumpTable);
                ctx.LoadNullRef();
                ctx.Return();
                for (int i = 0; i < jumpTable.Length; i++)
                {
                    SerializerMethods pair = methodPairs[i];
                    ctx.MarkLabel(jumpTable[i]);
                    var labelNoRoot = ctx.DefineLabel();

                    il.Emit(OpCodes.Ldarg_S, 4);
                    ctx.BranchIfFalse(labelNoRoot, true);

                    WriteDeserializePair(assemblyName, type, methodPairs, ilVersion, il, pair, pair.RootPair, i, ctx);

                    ctx.MarkLabel(labelNoRoot);

                    WriteDeserializePair(assemblyName, type, methodPairs, ilVersion, il, pair, pair.BasicPair, i, ctx);
                }
            }
            return ctx;
        }

        private void WriteDeserializePair(string assemblyName, TypeBuilder type, SerializerMethods[] methodPairs, CompilerContext.ILVersion ilVersion, ILGenerator il, SerializerMethods pair, SerializerMethodPair methodPair, int i,
                                          CompilerContext ctx)
        {
            Type keyType = pair.Type.Type;
            if (keyType.IsValueType)
            {
                il.Emit(OpCodes.Ldarg_2);
                il.Emit(OpCodes.Ldarg_3);
                il.EmitCall(OpCodes.Call, EmitBoxedSerializer(methodPair, type, i, keyType, methodPairs, this, ilVersion, assemblyName).Member, null);
                ctx.Return();
            }
            else
            {
                il.Emit(OpCodes.Ldarg_2);
                ctx.CastFromObject(keyType);
                il.Emit(OpCodes.Ldarg_3);
                il.EmitCall(OpCodes.Call, methodPair.Deserialize, null);
                ctx.Return();
            }
        }

        private static void WriteSerializePair(ILGenerator il, CompilerContext ctx, SerializerMethods pair, SerializerMethodPair methodPair)
        {
            il.Emit(OpCodes.Ldarg_2);
            ctx.CastFromObject(pair.Type.Type);
            il.Emit(OpCodes.Ldarg_3);
            il.EmitCall(OpCodes.Call, methodPair.Serialize, null);
            ctx.Return();
        }

        private const int KnownTypes_Array = 1, KnownTypes_Dictionary = 2, KnownTypes_Hashtable = 3, KnownTypes_ArrayCutoff = 20;
        private void WriteGetKeyImpl(TypeBuilder type, bool hasInheritance, SerializerMethods[] methodPairs, Compiler.CompilerContext.ILVersion ilVersion, string assemblyName, out ILGenerator il, out int knownTypesCategory, out FieldBuilder knownTypes, out Type knownTypesLookupType)
        {
            var genInfo = Override(type, "GetKeyImpl");
            il = genInfo.GetILGenerator();
            Compiler.CompilerContext ctx = new Compiler.CompilerContext(genInfo, false, false, methodPairs, this, ilVersion, assemblyName, MapType(typeof(System.Type), true));

            if (_types.Count <= KnownTypes_ArrayCutoff)
            {
                knownTypesCategory = KnownTypes_Array;
                knownTypesLookupType = MapType(typeof(System.Type[]), true);
            }
            else
            {

                knownTypesLookupType = MapType(typeof(System.Collections.Generic.Dictionary<System.Type, int>), false);
                if (knownTypesLookupType == null)
                {
                    knownTypesLookupType = MapType(typeof(Hashtable), true);
                    knownTypesCategory = KnownTypes_Hashtable;
                }
                else
                {
                    knownTypesCategory = KnownTypes_Dictionary;
                }
            }
            knownTypes = type.DefineField("knownTypes", knownTypesLookupType, FieldAttributes.Private | FieldAttributes.InitOnly | FieldAttributes.Static);

            switch (knownTypesCategory)
            {
                case KnownTypes_Array:
                    {
                        il.Emit(OpCodes.Ldsfld, knownTypes);
                        il.Emit(OpCodes.Ldarg_1);
                        // note that Array.IndexOf is not supported under CF
                        il.EmitCall(OpCodes.Callvirt, MapType(typeof(IList)).GetMethod(
                            "IndexOf", new Type[] { MapType(typeof(object)) }), null);
                        if (hasInheritance)
                        {
                            il.DeclareLocal(MapType(typeof(int))); // loc-0
                            il.Emit(OpCodes.Dup);
                            il.Emit(OpCodes.Stloc_0);

                            BasicList getKeyLabels = new BasicList();
                            int lastKey = -1;
                            for (int i = 0; i < methodPairs.Length; i++)
                            {
                                if (methodPairs[i].MetaKey == methodPairs[i].BaseKey) break;
                                if (lastKey == methodPairs[i].BaseKey)
                                {   // add the last label again
                                    getKeyLabels.Add(getKeyLabels[getKeyLabels.Count - 1]);
                                }
                                else
                                {   // add a new unique label
                                    getKeyLabels.Add(ctx.DefineLabel());
                                    lastKey = methodPairs[i].BaseKey;
                                }
                            }
                            Compiler.CodeLabel[] subtypeLabels = new Compiler.CodeLabel[getKeyLabels.Count];
                            getKeyLabels.CopyTo(subtypeLabels, 0);

                            ctx.Switch(subtypeLabels);
                            il.Emit(OpCodes.Ldloc_0); // not a sub-type; use the original value
                            il.Emit(OpCodes.Ret);

                            lastKey = -1;
                            // now output the different branches per sub-type (not derived type)
                            for (int i = subtypeLabels.Length - 1; i >= 0; i--)
                            {
                                if (lastKey != methodPairs[i].BaseKey)
                                {
                                    lastKey = methodPairs[i].BaseKey;
                                    // find the actual base-index for this base-key (i.e. the index of
                                    // the base-type)
                                    int keyIndex = -1;
                                    for (int j = subtypeLabels.Length; j < methodPairs.Length; j++)
                                    {
                                        if (methodPairs[j].BaseKey == lastKey && methodPairs[j].MetaKey == lastKey)
                                        {
                                            keyIndex = j;
                                            break;
                                        }
                                    }
                                    ctx.MarkLabel(subtypeLabels[i]);
                                    Compiler.CompilerContext.LoadValue(il, keyIndex);
                                    il.Emit(OpCodes.Ret);
                                }
                            }
                        }
                        else
                        {
                            il.Emit(OpCodes.Ret);
                        }
                    }
                    break;
                case KnownTypes_Dictionary:
                    {
                        LocalBuilder result = il.DeclareLocal(MapType(typeof(int)));
                        Label otherwise = il.DefineLabel();
                        il.Emit(OpCodes.Ldsfld, knownTypes);
                        il.Emit(OpCodes.Ldarg_1);
                        il.Emit(OpCodes.Ldloca_S, result);
                        il.EmitCall(OpCodes.Callvirt, knownTypesLookupType.GetMethod("TryGetValue", BindingFlags.Instance | BindingFlags.Public), null);
                        il.Emit(OpCodes.Brfalse_S, otherwise);
                        il.Emit(OpCodes.Ldloc_S, result);
                        il.Emit(OpCodes.Ret);
                        il.MarkLabel(otherwise);
                        il.Emit(OpCodes.Ldc_I4_M1);
                        il.Emit(OpCodes.Ret);
                    }
                    break;
                case KnownTypes_Hashtable:
                    {
                        Label otherwise = il.DefineLabel();
                        il.Emit(OpCodes.Ldsfld, knownTypes);
                        il.Emit(OpCodes.Ldarg_1);
                        il.EmitCall(OpCodes.Callvirt, knownTypesLookupType.GetProperty("Item").GetGetMethod(), null);
                        il.Emit(OpCodes.Dup);
                        il.Emit(OpCodes.Brfalse_S, otherwise);
#if FX11
                        il.Emit(OpCodes.Unbox, MapType(typeof(int)));
                        il.Emit(OpCodes.Ldobj, MapType(typeof(int)));
#else
                        if (ilVersion == Compiler.CompilerContext.ILVersion.Net1)
                        {
                            il.Emit(OpCodes.Unbox, MapType(typeof(int)));
                            il.Emit(OpCodes.Ldobj, MapType(typeof(int)));
                        }
                        else
                        {
                            il.Emit(OpCodes.Unbox_Any, MapType(typeof(int)));
                        }
#endif
                        il.Emit(OpCodes.Ret);
                        il.MarkLabel(otherwise);
                        il.Emit(OpCodes.Pop);
                        il.Emit(OpCodes.Ldc_I4_M1);
                        il.Emit(OpCodes.Ret);
                    }
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        internal MethodBuilder EmitDefineMethod(
            TypeBuilder typeBuilder, string name, MethodAttributes attributes, CallingConventions callingConvention,
            Type returnType, MethodContext.ParameterGenInfo[] parameters, bool isOverride, out MethodContext methodContext)
        {
            parameters = parameters.ToArray();
            MethodBuilder m = typeBuilder.DefineMethod(
                name,
                attributes,
                callingConvention,
                returnType,
                parameters.Select(p => p.Type).ToArray());

            var il = m.GetILGenerator();
            methodContext = new MethodContext(
                new MethodContext.MethodGenInfo(
                    name,
                    m,
                    false,
                    isOverride,
                    (attributes & MethodAttributes.Static) != 0,
                    returnType,
                    typeBuilder,
                    parameters),
                il,
                RunSharpTypeMapper);
            return m;
        }

        private void WriteSerializers(CompilerOptions options, string assemblyName, TypeBuilder type, out bool hasInheritance, out SerializerMethods[] methodPairs, out Compiler.CompilerContext.ILVersion ilVersion)
        {
            int index = 0;
            hasInheritance = false;
            methodPairs = new SerializerMethods[_types.Count];
            foreach (MetaType metaType in _types)
            {
                string writeName = "Write";
#if DEBUG
                writeName += metaType.Type.Name;
#endif

                string readName = "Read";
#if DEBUG
                readName += metaType.Type.Name;
#endif

                SerializerMethods methods = new SerializerMethods(
                    GetKey(metaType.Type, true, false), GetKey(metaType.Type, true, true), metaType,
                    DefineReadWriteMethods(type, metaType.Serializer, writeName, readName),
                    DefineReadWriteMethods(type, metaType.RootSerializer, writeName + "Root", readName + "Root"),
                    metaType.LateReferenceSerializer != null ? DefineReadWriteMethods(type, metaType.LateReferenceSerializer, writeName + "Late", readName + "Late") : null);
                methodPairs[index++] = methods;
                if (methods.MetaKey != methods.BaseKey) hasInheritance = true;
            }

            if (hasInheritance)
                Array.Sort(methodPairs);

            ilVersion = Compiler.CompilerContext.ILVersion.Net2;
            if (options.MetaDataVersion == 0x10000)
            {
                ilVersion = Compiler.CompilerContext.ILVersion.Net1; // old-school!
            }

            var exceptions = new List<Exception>();
            var tasks = new List<ManualResetEvent>();

            var ilVersionLocal = ilVersion;
            var methodPairsLocal = methodPairs;
#if FEAT_IKVM
            CompilerParallelizationMode parallelization = CompilerParallelizationMode.Disabled;
#else
            CompilerParallelizationMode parallelization = options.ParallelizationMode;
#endif
            try
            {
                int cores = Environment.ProcessorCount;
                int completed = 0;
                for (int i = 0; i < methodPairs.Length; i++)
                {
                    SerializerMethods methods = methodPairs[i];
                    
                    Action<SerializerMethodPair> emitWrite = pair =>
                        {
                            Compiler.CompilerContext ctx = new Compiler.CompilerContext(
                                pair.SerializeBody,
                                true,
                                true,
                                methodPairsLocal,
                                this,
                                ilVersionLocal,
                                assemblyName,
                                methods.Type.Type);

                            ctx.CheckAccessibility(pair.Deserialize.ReturnType);
                            pair.Serializer.EmitWrite(ctx, ctx.InputValue);
                            ctx.Return();
                        };
                    Action emitWriteAll = () =>
                    {
                        if (methods.LateReferencePair != null)
                            emitWrite(methods.LateReferencePair);
                        emitWrite(methods.BasicPair);
                        emitWrite(methods.RootPair);
                    };

                    Action<SerializerMethodPair> emitRead = pair =>
                        {
                            Compiler.CompilerContext ctx = new Compiler.CompilerContext(
                                pair.DeserializeBody,
                                true,
                                false,
                                methodPairsLocal,
                                this,
                                ilVersionLocal,
                                assemblyName,
                                methods.Type.Type);

                            pair.Serializer.EmitRead(ctx, ctx.InputValue);
                            if (!pair.Serializer.EmitReadReturnsValue)
                                ctx.LoadValue(ctx.InputValue);
                            ctx.Return();
                        };
                    Action emitReadAll = () =>
                    {
                        if (methods.LateReferencePair != null)
                            emitRead(methods.LateReferencePair);
                        emitRead(methods.BasicPair);
                        emitRead(methods.RootPair);
                    };

                    switch (parallelization)
                    {
                        case CompilerParallelizationMode.Maximum:
                            StartTask(tasks, emitWriteAll, exceptions);
                            StartTask(tasks, emitReadAll, exceptions);
                            //StartTask(tasks, emitWrite + emitRead, exceptions);
                            break;

                        case CompilerParallelizationMode.PerType:
                            StartTask(tasks, emitWriteAll + emitReadAll, exceptions);
                            break;

                        default:
                            // need to see exceptions at their place
                            if (Debugger.IsAttached)
                            {
                                emitWriteAll();
                                emitReadAll();
                            }
                            else
                                WrapTask(emitWriteAll + emitReadAll, exceptions).Invoke(null);
                            break;
                    }

                    if (parallelization != CompilerParallelizationMode.Disabled)
                    {
                        // do not launch too many?
                        for (int j = completed; j < tasks.Count - cores; j++, completed++)
                            tasks[j].WaitOne();
                    }
                }
            }
            finally
            {
                foreach (ManualResetEvent ev in tasks)
                {
                    ev.WaitOne();
                    ev.Close();
                }
            }

            Thread.MemoryBarrier();
            if (exceptions.Count > 0) throw new ProtoAggregateException(exceptions);
        }

        SerializerMethodPair DefineReadWriteMethods(TypeBuilder type, IProtoSerializerWithWireType ser, string writeName, string readName)
        {
            MethodContext writeGenCtx;
            MethodBuilder writeMethod = DefineWriteMethod(type, ser, writeName, out writeGenCtx);

            MethodContext readGenCtx;
            MethodBuilder readMethod = DefineReadMethod(type, ser, readName, out readGenCtx);

            return new SerializerMethodPair(ser, writeMethod, readMethod, writeGenCtx, readGenCtx);
        }

        MethodBuilder DefineReadMethod(TypeBuilder type, IProtoSerializerWithWireType serializer, string readName, out MethodContext readGenCtx)
        {
            MethodBuilder readMethod = EmitDefineMethod(
                type,
                readName,
                MethodAttributes.Private | MethodAttributes.Static,
                CallingConventions.Standard,
                serializer.ExpectedType,
                new[]
                {
                    new MethodContext.ParameterGenInfo(serializer.ExpectedType, "obj", 1),
                    new MethodContext.ParameterGenInfo(MapType(typeof(ProtoReader)), "source", 2),
                },
                false,
                out readGenCtx);
            return readMethod;
        }

        MethodBuilder DefineWriteMethod(TypeBuilder type, IProtoSerializerWithWireType serializer, string writeName, out MethodContext writeGenCtx)
        {
            return EmitDefineMethod(
                type,
                writeName,
                MethodAttributes.Private | MethodAttributes.Static,
                CallingConventions.Standard,
                MapType(typeof(void)),
                new[]
                {
                    new MethodContext.ParameterGenInfo(serializer.ExpectedType, "obj", 1),
                    new MethodContext.ParameterGenInfo(MapType(typeof(ProtoWriter)), "dest", 2),
                },
                false,
                out writeGenCtx);
        }
        
        static void StartTask(List<ManualResetEvent> tasks, Action action, IList<Exception> exceptions)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            var ev = new ManualResetEvent(false);
            tasks.Add(ev);

            ThreadPool.QueueUserWorkItem(WrapTask(action, exceptions),
                ev);
        }

        static WaitCallback WrapTask(Action action, IList<Exception> exceptions)
        {
            return s =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception e)
                    {
                        lock (exceptions)
                            exceptions.Add(e);
                    }
                    finally
                    {
                        (s as ManualResetEvent)?.Set();
                    }
                };
        }

        private TypeBuilder WriteBasicTypeModel(CompilerOptions options, string typeName, ModuleBuilder module)
        {
            Type baseType = MapType(typeof(TypeModel));
            TypeAttributes typeAttributes = (baseType.Attributes & ~TypeAttributes.Abstract) | TypeAttributes.Sealed;
            if (options.Accessibility == Accessibility.Internal)
            {
                typeAttributes &= ~TypeAttributes.Public;
            }

            TypeBuilder type = module.DefineType(typeName, typeAttributes, baseType);
            return type;
        }

        private void WriteAssemblyAttributes(CompilerOptions options, string assemblyName, AssemblyBuilder asm, CompiledAssemblyEqualityAttribute equalityAttr)
        {
            if (!Helpers.IsNullOrEmpty(options.TargetFrameworkName))
            {
                // get [TargetFramework] from mscorlib/equivalent and burn into the new assembly
                Type versionAttribType = null;
                try
                { // this is best-endeavours only
                    versionAttribType = GetType("System.Runtime.Versioning.TargetFrameworkAttribute", MapType(typeof(string)).Assembly);
                }
                catch { /* don't stress */ }
                if (versionAttribType != null)
                {
                    PropertyInfo[] props;
                    object[] propValues;
                    if (Helpers.IsNullOrEmpty(options.TargetFrameworkDisplayName))
                    {
                        props = new PropertyInfo[0];
                        propValues = new object[0];
                    }
                    else
                    {
                        props = new PropertyInfo[1] { versionAttribType.GetProperty("FrameworkDisplayName") };
                        propValues = new object[1] { options.TargetFrameworkDisplayName };
                    }
                    CustomAttributeBuilder builder = new CustomAttributeBuilder(
                        versionAttribType.GetConstructor(new Type[] { MapType(typeof(string)) }),
                        new object[] { options.TargetFrameworkName },
                        props,
                        propValues);
                    asm.SetCustomAttribute(builder);
                }
            }

            // copy assembly:InternalsVisibleTo
            Type internalsVisibleToAttribType = null;
#if !FX11
            try
            {
                internalsVisibleToAttribType = MapType(typeof(System.Runtime.CompilerServices.InternalsVisibleToAttribute));
            }
            catch { /* best endeavors only */ }
#endif
            if (internalsVisibleToAttribType != null)
            {
                BasicList internalAssemblies = new BasicList(), consideredAssemblies = new BasicList();
                foreach (MetaType metaType in _types)
                {
                    Assembly assembly = metaType.Type.Assembly;
                    if (consideredAssemblies.IndexOfReference(assembly) >= 0) continue;
                    consideredAssemblies.Add(assembly);

                    AttributeMap[] assemblyAttribsMap = AttributeMap.Create(this, assembly);
                    for (int i = 0; i < assemblyAttribsMap.Length; i++)
                    {

                        if (assemblyAttribsMap[i].AttributeType != internalsVisibleToAttribType) continue;

                        object privelegedAssemblyObj;
                        assemblyAttribsMap[i].TryGet("AssemblyName", out privelegedAssemblyObj);
                        string privelegedAssemblyName = privelegedAssemblyObj as string;
                        if (privelegedAssemblyName == assemblyName || Helpers.IsNullOrEmpty(privelegedAssemblyName)) continue; // ignore

                        if (internalAssemblies.IndexOfString(privelegedAssemblyName) >= 0) continue; // seen it before
                        internalAssemblies.Add(privelegedAssemblyName);

                        CustomAttributeBuilder builder = new CustomAttributeBuilder(
                            internalsVisibleToAttribType.GetConstructor(new Type[] { MapType(typeof(string)) }),
                            new object[] { privelegedAssemblyName });
                        asm.SetCustomAttribute(builder);
                    }
                }
            }

            if (equalityAttr != null)
            {
                asm.SetCustomAttribute(
                    new CustomAttributeBuilder(MapType(equalityAttr.GetType()).GetConstructors().First(c => c.GetParameters().Length != 0), equalityAttr.GetConstructorArgs()));
            }
        }

        private static MethodContext EmitBoxedSerializer(SerializerMethodPair methodPair, TypeBuilder type, int i, Type valueType, SerializerMethods[] methodPairs, RuntimeTypeModel model, Compiler.CompilerContext.ILVersion ilVersion, string assemblyName)
        {
            MethodInfo dedicated = methodPair.Deserialize;
            string name = "_" + i.ToString();

            MethodContext methodContext;
            model.EmitDefineMethod(
                type,
                name,
                MethodAttributes.Static,
                CallingConventions.Standard,
                model.MapType(typeof(object)),
                new[]
                {
                    new MethodContext.ParameterGenInfo(model.MapType(typeof(object)), "obj", 1),
                    new MethodContext.ParameterGenInfo(model.MapType(typeof(ProtoReader)), "source", 2),
                },
                false,
                out methodContext);
            
            Compiler.CompilerContext ctx = new Compiler.CompilerContext(methodContext, true, false, methodPairs, model, ilVersion, assemblyName, model.MapType(typeof(object)));
            ctx.LoadValue(ctx.InputValue);
            Compiler.CodeLabel @null = ctx.DefineLabel();
            ctx.BranchIfFalse(@null, true);

            Type mappedValueType = valueType;
            ctx.LoadValue(ctx.InputValue);
            ctx.CastFromObject(mappedValueType);
            ctx.LoadReaderWriter();
            ctx.EmitCall(dedicated);
            ctx.CastToObject(mappedValueType);
            ctx.Return();

            ctx.MarkLabel(@null);
            using (Compiler.Local typedVal = new Compiler.Local(ctx, mappedValueType))
            {
                // create a new valueType
                ctx.LoadAddress(typedVal, mappedValueType);
                ctx.EmitCtor(mappedValueType);
                ctx.LoadValue(typedVal);
                ctx.LoadReaderWriter();
                ctx.EmitCall(dedicated);
                ctx.CastToObject(mappedValueType);
                ctx.Return();
            }
            return methodContext;
        }

#endif
        }
}
#endif