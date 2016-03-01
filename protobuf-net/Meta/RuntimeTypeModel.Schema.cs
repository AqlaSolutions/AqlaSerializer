// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections;
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
        internal string GetSchemaTypeName(Type effectiveType, BinaryDataFormat dataFormat, bool asReference, bool dynamicType, ref bool requiresBclImport)
        {
            Type tmp = Helpers.GetNullableUnderlyingType(effectiveType);
            if (tmp != null) effectiveType = tmp;

            if (effectiveType == this.MapType(typeof(byte[]))) return "bytes";

            WireType wireType;
            IProtoSerializer ser = this.ValueSerializerBuilder.TryGetSimpleCoreSerializer(dataFormat, effectiveType, out wireType);
            if (ser == null)
            {   // model type
                if (asReference || dynamicType)
                {
                    requiresBclImport = true;
                    return "bcl.NetObjectProxy";
                }
                return this[effectiveType].GetSurrogateOrBaseOrSelf(true).GetSchemaTypeName();
            }
            else
            {
                if (ser is ParseableSerializer)
                {
                    if (asReference) requiresBclImport = true;
                    return asReference ? "bcl.NetObjectProxy" : "string";
                }

                switch (Helpers.GetTypeCode(effectiveType))
                {
                    case ProtoTypeCode.Boolean: return "bool";
                    case ProtoTypeCode.Single: return "float";
                    case ProtoTypeCode.Double: return "double";
                    case ProtoTypeCode.Type:
                    case ProtoTypeCode.String:
                        if (asReference) requiresBclImport = true;
                        return asReference ? "bcl.NetObjectProxy" : "string";
                    case ProtoTypeCode.Byte:
                    case ProtoTypeCode.Char:
                    case ProtoTypeCode.UInt16:
                    case ProtoTypeCode.UInt32:
                        switch (dataFormat)
                        {
                            case BinaryDataFormat.FixedSize: return "fixed32";
                            default: return "uint32";
                        }
                    case ProtoTypeCode.SByte:
                    case ProtoTypeCode.Int16:
                    case ProtoTypeCode.Int32:
                        switch (dataFormat)
                        {
                            case BinaryDataFormat.ZigZag: return "sint32";
                            case BinaryDataFormat.FixedSize: return "sfixed32";
                            default: return "int32";
                        }
                    case ProtoTypeCode.UInt64:
                        switch (dataFormat)
                        {
                            case BinaryDataFormat.FixedSize: return "fixed64";
                            default: return "uint64";
                        }
                    case ProtoTypeCode.Int64:
                        switch (dataFormat)
                        {
                            case BinaryDataFormat.ZigZag: return "sint64";
                            case BinaryDataFormat.FixedSize: return "sfixed64";
                            default: return "int64";
                        }
                    case ProtoTypeCode.DateTime: requiresBclImport = true; return "bcl.DateTime";
                    case ProtoTypeCode.TimeSpan: requiresBclImport = true; return "bcl.TimeSpan";
                    case ProtoTypeCode.Decimal: requiresBclImport = true; return "bcl.Decimal";
                    case ProtoTypeCode.Guid: requiresBclImport = true; return "bcl.Guid";
                    default: throw new NotSupportedException("No .proto map found for: " + effectiveType.FullName);
                }
            }

        }

        // <summary>
        /// Suggest a .proto definition for the given type
        /// </summary>
        /// <param name="type">The type to generate a .proto definition for, or <c>null</c> to generate a .proto that represents the entire model</param>
        /// <returns>The .proto definition as a string</returns>
        public override string GetSchema(Type type)
        {
            BasicList requiredTypes = new BasicList();
            MetaType primaryType = null;
            bool isInbuiltType = false;
            if (type == null)
            { // generate for the entire model
                for (int i = _serviceTypesCount; i < types.Count; i++)
                {
                    MetaType meta = (MetaType)types[i];
                    MetaType tmp = meta.GetSurrogateOrBaseOrSelf(false);
                    if (!requiredTypes.Contains(tmp))
                    {
                        // ^^^ note that the type might have been added as a descendent
                        requiredTypes.Add(tmp);
                        CascadeDependents(requiredTypes, tmp);
                    }
                }
            }
            else
            {
                Type tmp = Helpers.GetNullableUnderlyingType(type);
                if (tmp != null) type = tmp;

                WireType defaultWireType;
                isInbuiltType = (this.ValueSerializerBuilder.TryGetSimpleCoreSerializer(BinaryDataFormat.Default, type, out defaultWireType) != null);
                if (!isInbuiltType)
                {
                    //Agenerate just relative to the supplied type
                    int index = FindOrAddAuto(type, false, false, false);
                    if (index < 0) throw new ArgumentException("The type specified is not a contract-type", "type");

                    // get the required types
                    primaryType = ((MetaType)types[index]).GetSurrogateOrBaseOrSelf(false);
                    requiredTypes.Add(primaryType);
                    CascadeDependents(requiredTypes, primaryType);
                }
            }

            // use the provided type's namespace for the "package"
            StringBuilder headerBuilder = new StringBuilder();
            string package = null;

            if (!isInbuiltType)
            {
                IEnumerable typesForNamespace = primaryType == null ? (IEnumerable)MetaTypes : requiredTypes;
                foreach (MetaType meta in typesForNamespace)
                {
                    if (meta.IsList) continue;
                    string tmp = meta.Type.Namespace;
                    if (!Helpers.IsNullOrEmpty(tmp))
                    {
                        if (tmp.StartsWith("System.")) continue;
                        if (package == null)
                        { // haven't seen any suggestions yet
                            package = tmp;
                        }
                        else if (package == tmp)
                        { // that's fine; a repeat of the one we already saw
                        }
                        else
                        { // something else; have confliucting suggestions; abort
                            package = null;
                            break;
                        }
                    }
                }
            }

            if (!Helpers.IsNullOrEmpty(package))
            {
                headerBuilder.Append("package ").Append(package).Append(';');
                Helpers.AppendLine(headerBuilder);
            }

            bool requiresBclImport = false;
            StringBuilder bodyBuilder = new StringBuilder();
            // sort them by schema-name
            MetaType[] metaTypesArr = new MetaType[requiredTypes.Count];
            requiredTypes.CopyTo(metaTypesArr, 0);
            Array.Sort(metaTypesArr, MetaType.Comparer.Default);

            // write the messages
            if (isInbuiltType)
            {
                Helpers.AppendLine(bodyBuilder).Append("message ").Append(type.Name).Append(" {");
                MetaType.NewLine(bodyBuilder, 1).Append("optional ").Append(GetSchemaTypeName(type, BinaryDataFormat.Default, false, false, ref requiresBclImport))
                    .Append(" value = 1;");
                Helpers.AppendLine(bodyBuilder).Append('}');
            }
            else
            {
                for (int i = 0; i < metaTypesArr.Length; i++)
                {
                    MetaType tmp = metaTypesArr[i];
                    if (tmp.IsList && tmp != primaryType) continue;
                    tmp.WriteSchema(bodyBuilder, 0, ref requiresBclImport);
                }
            }
            if (requiresBclImport)
            {
                headerBuilder.Append("import \"bcl.proto\"; // schema for protobuf-net's handling of core .NET types");
                Helpers.AppendLine(headerBuilder);
            }
            return Helpers.AppendLine(headerBuilder.Append(bodyBuilder)).ToString();
        }
        private void CascadeDependents(BasicList list, MetaType metaType)
        {
            MetaType tmp;
            if (metaType.IsList)
            {
                Type itemType = TypeModel.GetListItemType(this, metaType.Type);
                WireType defaultWireType;
                IProtoSerializer coreSerializer = this.ValueSerializerBuilder.TryGetSimpleCoreSerializer(BinaryDataFormat.Default, itemType, out defaultWireType);
                if (coreSerializer == null)
                {
                    int index = FindOrAddAuto(itemType, false, false, false);
                    if (index >= 0)
                    {
                        tmp = ((MetaType)types[index]).GetSurrogateOrBaseOrSelf(false);
                        if (!list.Contains(tmp))
                        { // could perhaps also implement as a queue, but this should work OK for sane models
                            list.Add(tmp);
                            CascadeDependents(list, tmp);
                        }
                    }
                }
            }
            else
            {
                if (metaType.IsAutoTuple)
                {
                    MemberInfo[] mapping;
                    if (MetaType.ResolveTupleConstructor(metaType.Type, out mapping) != null)
                    {
                        for (int i = 0; i < mapping.Length; i++)
                        {
                            Type type = null;
                            if (mapping[i] is PropertyInfo) type = ((PropertyInfo)mapping[i]).PropertyType;
                            else if (mapping[i] is FieldInfo) type = ((FieldInfo)mapping[i]).FieldType;

                            WireType defaultWireType;
                            IProtoSerializer coreSerializer = this.ValueSerializerBuilder.TryGetSimpleCoreSerializer(BinaryDataFormat.Default, type, out defaultWireType);
                            if (coreSerializer == null)
                            {
                                int index = FindOrAddAuto(type, false, false, false);
                                if (index >= 0)
                                {
                                    tmp = ((MetaType)types[index]).GetSurrogateOrBaseOrSelf(false);
                                    if (!list.Contains(tmp))
                                    { // could perhaps also implement as a queue, but this should work OK for sane models
                                        list.Add(tmp);
                                        CascadeDependents(list, tmp);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    foreach (ValueMember member in metaType.Fields)
                    {
                        member.Serializer.GetHashCode();
                        var s = member.GetSettingsCopy(0);
                        Type type = s.Collection.ItemType;
                        if (type == null) type = member.MemberType;
                        var fieldMetaType = FindWithoutAdd(type);
                        if (fieldMetaType != null)
                            type = fieldMetaType.GetSurrogateOrSelf().Type;
                        WireType defaultWireType;
                        IProtoSerializer coreSerializer = this.ValueSerializerBuilder.TryGetSimpleCoreSerializer(BinaryDataFormat.Default, type, out defaultWireType);
                        if (coreSerializer == null)
                        {
                            // is an interesting type
                            int index = FindOrAddAuto(type, false, false, false);
                            if (index >= 0)
                            {
                                tmp = ((MetaType)types[index]).GetSurrogateOrBaseOrSelf(false);
                                if (!list.Contains(tmp))
                                { // could perhaps also implement as a queue, but this should work OK for sane models
                                    list.Add(tmp);
                                    CascadeDependents(list, tmp);
                                }
                            }
                        }
                    }
                }
                if (metaType.HasSubtypes)
                {
                    foreach (SubType subType in metaType.GetSubtypes())
                    {
                        tmp = subType.DerivedType.GetSurrogateOrSelf(); // note: exclude base-types!
                        if (!list.Contains(tmp))
                        {
                            list.Add(tmp);
                            CascadeDependents(list, tmp);
                        }
                    }
                }
                tmp = metaType.BaseType;
                if (tmp != null) tmp = tmp.GetSurrogateOrSelf(); // note: already walking base-types; exclude base
                if (tmp != null && !list.Contains(tmp))
                {
                    list.Add(tmp);
                    CascadeDependents(list, tmp);
                }
            }
        }
    }
}
#endif