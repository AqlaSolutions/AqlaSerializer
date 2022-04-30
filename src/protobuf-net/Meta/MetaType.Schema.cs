// Modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Threading;
using AltLinq; using System.Linq;
using AqlaSerializer;
using AqlaSerializer.Internal;
using AqlaSerializer.Meta.Mapping;
using AqlaSerializer.Serializers;
using AqlaSerializer.Settings;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#if FEAT_COMPILER
using IKVM.Reflection.Emit;
#endif
#else
using System.Reflection;
#if FEAT_COMPILER
using System.Reflection.Emit;

#endif
#endif

namespace AqlaSerializer.Meta
{
    partial class MetaType
    {
        internal string GetSchemaTypeName()
        {
            Serializer.GetHashCode();
            if (_surrogate != null) return _model[_surrogate].GetSchemaTypeName();

            if (!Helpers.IsNullOrEmpty(_settingsValueFinal.Name)) return _settingsValueFinal.Name;

            string typeName = Type.Name;
            if (Type.IsGenericType)
            {
                StringBuilder sb = new StringBuilder(typeName);
                int split = typeName.IndexOf('`');
                if (split >= 0) sb.Length = split;
                foreach (Type arg in Type.GetGenericArguments())
                {
                    sb.Append('_');
                    Type tmp = arg;
                    int key = _model.GetKey(ref tmp);
                    MetaType mt;
                    if (key >= 0 && (mt = _model[tmp]) != null && mt._surrogate == null) // <=== need to exclude surrogate to avoid chance of infinite loop
                    {

                        sb.Append(mt.GetSchemaTypeName());
                    }
                    else
                    {
                        sb.Append(tmp.Name);
                    }
                }
                return sb.ToString();
            }
            return typeName;
        }

        internal void WriteSchema(System.Text.StringBuilder builder, int indent, ref bool requiresBclImport)
        {
            Serializer.GetHashCode();
            if (_surrogate != null) return; // nothing to write


            ValueMember[] fieldsArr = new ValueMember[_fields.Count];
            _fields.CopyTo(fieldsArr, 0);
            Array.Sort(fieldsArr, ValueMember.Comparer.Default);

            if (IsList)
            {
                string itemTypeName = _model.GetSchemaTypeName(TypeModel.GetListItemType(_model, Type), BinaryDataFormat.Default, false, false, ref requiresBclImport);
                NewLine(builder, indent).Append("message ").Append(GetSchemaTypeName()).Append(" {");
                NewLine(builder, indent + 1).Append("repeated ").Append(itemTypeName).Append(" items = 1;");
                NewLine(builder, indent).Append('}');
            }
            else if (_settingsValueFinal.IsAutoTuple)
            { // key-value-pair etc
                MemberInfo[] mapping;
                if (ResolveTupleConstructor(Type, out mapping) != null)
                {
                    NewLine(builder, indent).Append("message ").Append(GetSchemaTypeName()).Append(" {");
                    for (int i = 0; i < mapping.Length; i++)
                    {
                        Type effectiveType;
                        if (mapping[i] is PropertyInfo)
                        {
                            effectiveType = ((PropertyInfo)mapping[i]).PropertyType;
                        }
                        else if (mapping[i] is FieldInfo)
                        {
                            effectiveType = ((FieldInfo)mapping[i]).FieldType;
                        }
                        else
                        {
                            throw new NotSupportedException("Unknown member type: " + mapping[i].GetType().Name);
                        }
                        NewLine(builder, indent + 1)
                            .Append("optional ")
                            .Append(_model.GetSchemaTypeName(effectiveType, BinaryDataFormat.Default, false, false, ref requiresBclImport).Replace('.', '_'))
                            .Append(' ').Append(mapping[i].Name).Append(" = ").Append(i + 1).Append(';');
                    }
                    NewLine(builder, indent).Append('}');
                }
            }
            else if (Helpers.IsEnum(Type))
            {
                NewLine(builder, indent).Append("enum ").Append(GetSchemaTypeName()).Append(" {");
                if (_settingsValueFinal.EnumPassthru.GetValueOrDefault())
                {
                    if (Type.IsDefined(_model.MapType(typeof(FlagsAttribute)), false))
                    {
                        NewLine(builder, indent + 1).Append("// this is a composite/flags enumeration");
                    }
                    else
                    {
                        NewLine(builder, indent + 1).Append("// this enumeration will be passed as a raw value");
                    }
                    foreach (FieldInfo field in Type.GetFields())
                    {
                        if (field.IsStatic && field.IsLiteral)
                        {
                            object enumVal;

#if PORTABLE || CF || FX11 || NETSTANDARD
                            enumVal = Convert.ChangeType(field.GetValue(null), Enum.GetUnderlyingType(field.FieldType), System.Globalization.CultureInfo.InvariantCulture);
#else
                            enumVal = field.GetRawConstantValue();
#endif
                            NewLine(builder, indent + 1).Append(field.Name).Append(" = ").Append(enumVal).Append(";");
                        }
                    }

                }
                else
                {
                    foreach (ValueMember member in fieldsArr)
                    {
                        NewLine(builder, indent + 1).Append(member.Name).Append(" = ").Append(member.FieldNumber).Append(';');
                    }
                }
                NewLine(builder, indent).Append('}');
            }
            else
            {
                NewLine(builder, indent).Append("message ").Append(GetSchemaTypeName()).Append(" {");
                foreach (ValueMember member in fieldsArr)
                {
                    member.Serializer.GetHashCode();
                    MemberLevelSettingsValue s = member.GetFinalSettingsCopy(0);
                    string ordinality = s.Collection.IsCollection ? "repeated" : member.IsRequired ? "required" : "optional";
                    NewLine(builder, indent + 1).Append(ordinality).Append(' ');
                    if (s.ContentBinaryFormatHint.GetValueOrDefault() == BinaryDataFormat.Group) builder.Append("group ");
                    string schemaTypeName = member.GetSchemaTypeName(true, ref requiresBclImport);
                    builder.Append(schemaTypeName).Append(" ")
                        .Append(member.Name).Append(" = ").Append(member.FieldNumber);

                    object defaultValue = member.GetFinalDefaultValue();
                    if (defaultValue != null && member.IsRequired == false)
                    {
                        if (defaultValue is string)
                        {
                            builder.Append(" [default = \"").Append(defaultValue).Append("\"]");
                        }
                        else if (defaultValue is bool)
                        { // need to be lower case (issue 304)
                            builder.Append((bool)defaultValue ? " [default = true]" : " [default = false]");
                        }
                        else
                        {
                            builder.Append(" [default = ").Append(defaultValue).Append(']');
                        }
                    }
                    if (s.Collection.IsCollection && s.Collection.Format == CollectionFormat.Protobuf &&
                        ListDecorator.CanPackProtoCompatible(HelpersInternal.GetWireType(Helpers.GetTypeCode(member.MemberType), s.ContentBinaryFormatHint.GetValueOrDefault())))
                    {
                        builder.Append(" [packed=true]");
                    }
                    builder.Append(';');
                    if (schemaTypeName == "bcl.NetObjectProxy" && (s.Format==ValueFormat.Reference || s.Format == ValueFormat.LateReference)
                        && !s.WriteAsDynamicType.GetValueOrDefault()) // we know what it is; tell the user
                    {
                        builder.Append(" // reference-tracked ").Append(member.GetSchemaTypeName(false, ref requiresBclImport));
                    }
                }
                if (_subTypes != null && _subTypes.Count != 0)
                {
                    NewLine(builder, indent + 1).Append("// the following represent sub-types; at most 1 should have a value");
                    SubType[] subTypeArr = new SubType[_subTypes.Count];
                    _subTypes.CopyTo(subTypeArr, 0);
                    Array.Sort(subTypeArr, SubType.Comparer.Default);
                    foreach (SubType subType in subTypeArr)
                    {
                        string subTypeName = subType.DerivedType.GetSchemaTypeName();
                        NewLine(builder, indent + 1).Append("optional ").Append(subTypeName)
                            .Append(" ").Append(subTypeName).Append(" = ").Append(subType.FieldNumber).Append(';');
                    }
                }
                NewLine(builder, indent).Append('}');
            }
        }

        internal static System.Text.StringBuilder NewLine(System.Text.StringBuilder builder, int indent)
        {
            return Helpers.AppendLine(builder).Append(' ', indent * 3);
        }
    }
}
#endif