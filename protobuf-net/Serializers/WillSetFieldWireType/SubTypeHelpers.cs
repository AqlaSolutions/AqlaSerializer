#if !NO_RUNTIME
using System;
using System.Diagnostics;
using System.Reflection;
#if FEAT_COMPILER
using AqlaSerializer.Compiler;
using TriAxis.RunSharp;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using Label = IKVM.Reflection.Emit.Label;
using IKVM.Reflection;
#else
using System.Reflection.Emit;
#endif
#endif
using AltLinq;
using AqlaSerializer.Meta;

namespace AqlaSerializer.Serializers
{
    /// <summary>
    /// Used to read and write type number. To provide versioning (e.g. for adding subtypes) 
    /// we don't use any global type identifiers and instead write a hierarchy of sub types.
    /// </summary>
    class SubTypeHelpers
    {
#if !FEAT_IKVM
        public void Write(MetaType metaType, Type actual, ProtoWriter dest)
        {
            Write(metaType, actual, dest, 0);
        }

        void Write(MetaType metaType, Type actual, ProtoWriter dest, int recursionLevel)
        {
            if (metaType.Type != actual)
                foreach (var subType in metaType.GetSubtypes().OrderBy(st => st.FieldNumber))
                {
                    MetaType derivedType = subType.DerivedType;
                    if (derivedType.Type == metaType.Type) continue;
                    if (Helpers.IsAssignableFrom(derivedType.Type, actual))
                    {
                        if (recursionLevel == 0)
                        {
                            if (derivedType.Type == actual)
                            {
                                ProtoWriter.WriteFieldHeaderComplete(WireType.Variant, dest);
                                ProtoWriter.WriteInt32(subType.FieldNumber + 1, dest);
                                return;
                            }

                            var token = ProtoWriter.StartSubItem(null, true, dest);
                            ProtoWriter.WriteFieldHeaderIgnored(WireType.Variant, dest);
                            ProtoWriter.WriteInt32(subType.FieldNumber + 1, dest);
                            Write(derivedType, actual, dest, 1);
                            ProtoWriter.EndSubItem(token, dest);
                        }
                        else
                        {
                            ProtoWriter.WriteFieldHeaderIgnored(WireType.Variant, dest);
                            ProtoWriter.WriteInt32(subType.FieldNumber + 1, dest);
                            Write(derivedType, actual, dest, recursionLevel + 1);
                        }
                        return;
                    }
                }

            if (recursionLevel == 0)
            {
                ProtoWriter.WriteFieldHeaderComplete(WireType.Variant, dest);
                ProtoWriter.WriteInt32(0, dest);
            }
        }

        /// <returns>Null means keep old value</returns>
        public MetaType TryRead(MetaType metaType, Type oldValueActualType, ProtoReader source)
        {
            var r = TryRead(metaType, source, 0);
            while (r != null && !r.Serializer.CanCreateInstance() && r != metaType)
                r = r.BaseType;

            if (r == null || Helpers.IsAssignableFrom(r.Type, oldValueActualType))
                    return null;
            return r;
        }

        MetaType TryRead(MetaType metaType, ProtoReader source, int recursionLevel)
        {
            SubType[] subTypes = metaType.GetSubtypes();
            int fieldNumber;
            SubType subType;
            if (recursionLevel == 0)
            {
                if (source.WireType != WireType.String)
                {
                    fieldNumber = source.ReadInt32() - 1;
                    subType = subTypes.FirstOrDefault(st => st.FieldNumber == fieldNumber);
                    return subType?.DerivedType ?? metaType; // versioning
                }
            }
            SubItemToken? token = null;
            if (recursionLevel == 0)
                token = ProtoReader.StartSubItem(source);
            
            try
            {
                if (!ProtoReader.HasSubValue(WireType.Variant, source))
                    return metaType;

                fieldNumber = source.ReadInt32() - 1;
                subType = subTypes.FirstOrDefault(st => st.FieldNumber == fieldNumber);
                return subType != null // versioning
                            ? TryRead(subType.DerivedType, source, recursionLevel + 1)
                            : metaType;
            }
            finally
            {
                if (token != null)
                    ProtoReader.EndSubItem(token.Value, true, source);
            }
        }

#endif

#if FEAT_COMPILER
        public void EmitWrite(SerializerCodeGen g, MetaType metaType, Local actualValue)
        {
            using (g.ctx.StartDebugBlockAuto(this))
            {
                Debug.Assert(!actualValue.IsNullRef());
                var endLabel = g.DefineLabel();
                using (var actualType = new Local(g.ctx, typeof(System.Type)))
                {
                    if (actualValue.IsNullRef())
                        g.Assign(actualType, null);
                    else
                    {
                        //g.If(actualValue.AsOperand != null);
                        {
                            g.Assign(actualType, actualValue.AsOperand.InvokeGetType());
                        }
                        //g.Else();
                        //{
                        //    g.Assign(actualType, null);
                        //}
                        //g.End();
                    }
                    EmitWrite(g, endLabel, metaType, actualValue, actualType);
                }
                g.MarkLabel(endLabel);
            }
        }

        void EmitWrite(SerializerCodeGen g, Label? endLabel, MetaType metaType, Local actualValue, Local actualType, int recursionLevel = 0)
        {
            using (g.ctx.StartDebugBlockAuto(this, metaType.Name + ", level = " + recursionLevel))
            {
                WriterGen dest = g.Writer;
                var breakLabel = g.DefineLabel();
                g.If(metaType.Type != actualType.AsOperand);
                {
                    foreach (var subType in metaType.GetSubtypes().OrderBy(st => st.FieldNumber))
                    {
                        MetaType derivedType = subType.DerivedType;
                        if (derivedType.Type == metaType.Type) continue;
                        g.If(actualValue.AsOperand.Is(derivedType.Type));
                        {
                            if (recursionLevel == 0)
                            {
                                g.If(derivedType.Type == actualType.AsOperand);
                                {
                                    dest.WriteFieldHeaderComplete(WireType.Variant);
                                    dest.WriteInt32(subType.FieldNumber + 1);
                                    g.Goto(endLabel == null ? breakLabel : endLabel.Value);
                                }
                                g.End();

                                using (var token = g.ctx.Local(typeof(SubItemToken)))
                                {
                                    g.Assign(token, g.WriterFunc.StartSubItem(null, true));
                                    dest.WriteFieldHeaderIgnored(WireType.Variant);
                                    dest.WriteInt32(subType.FieldNumber + 1);

                                    EmitWrite(g, null, derivedType, actualValue, actualType, 1);

                                    dest.EndSubItem(token);
                                }

                            }
                            else
                            {
                                dest.WriteFieldHeaderIgnored(WireType.Variant);
                                dest.WriteInt32(subType.FieldNumber + 1);
                                EmitWrite(g, null, derivedType, actualValue, actualType, recursionLevel + 1);
                            }
                            g.Goto(endLabel == null ? breakLabel : endLabel.Value);
                        }
                        g.End();
                    }
                }
                g.End();
                g.MarkLabel(breakLabel);
                if (recursionLevel == 0)
                {
                    dest.WriteFieldHeaderComplete(WireType.Variant);
                    dest.WriteInt32(0);
                }
            }
        }

        public void EmitTryRead(SerializerCodeGen g, Local oldValue, MetaType metaType, Action<MetaType> returnGen)
        {
            Debug.Assert(metaType != null);
            using (g.ctx.StartDebugBlockAuto(this, metaType.Name))
            using (var fieldNumber = new Local(g.ctx, typeof(int)))
            {
                var jumpOut = g.DefineLabel();
                returnGen += mt =>
                    {
                        g.ctx.MarkDebug("// jump out of SubTypeHelpers.EmitTryRead");
                        g.Goto(jumpOut);
                    };
                
                EmitTryRead(
                    g,
                    fieldNumber,
                    metaType,
                    0,
                    r =>
                        {
                            while (r != null && !r.Serializer.CanCreateInstance() && r != metaType)
                                r = r.BaseType;

                            if (r == null)
                                returnGen(null);
                            else if (!oldValue.IsNullRef()) // check local exists
                            {
                                g.If(oldValue.AsOperand.Is(r.Type));
                                {
                                    returnGen(null);
                                }
                                g.Else();
                                {
                                    returnGen(r);
                                }
                                g.End();
                            }
                            else returnGen(r);
                        });
                g.MarkLabel(jumpOut);
            }
        }

        void EmitTryRead(SerializerCodeGen g, Local fieldNumber, MetaType metaType, int recursionLevel, Action<MetaType> returnGen)
        {
            Debug.Assert(metaType != null);
            using (g.ctx.StartDebugBlockAuto(this, metaType.Name + ", level = " + recursionLevel))
            {
                SubType[] subTypes = metaType.GetSubtypes();
                if (recursionLevel == 0)
                {
                    g.If(g.ReaderFunc.WireType() != WireType.String);
                    {
                        g.Assign(fieldNumber, g.ReaderFunc.ReadInt32() - 1);
                        EmitTryRead_GenSwitch(g, fieldNumber, metaType, subTypes, returnGen);
                    }
                    g.End();
                }
                Local token = null;
                if (recursionLevel == 0)
                {
                    token = new Local(g.ctx, typeof(SubItemToken));
                    g.Assign(token, g.ReaderFunc.StartSubItem());
                    returnGen = (r => g.Reader.EndSubItem(token)) + returnGen;
                }

                g.If(!g.ReaderFunc.HasSubValue_bool(WireType.Variant));
                {
                    returnGen(metaType);
                }
                g.End();

                g.Assign(fieldNumber, g.ReaderFunc.ReadInt32() - 1);
                EmitTryRead_GenSwitch(
                    g,
                    fieldNumber,
                    metaType,
                    subTypes,
                    r =>
                        {
                            if (r == metaType)
                                returnGen(metaType);
                            else
                                EmitTryRead(g, fieldNumber, r, recursionLevel + 1, returnGen);
                        });

                token?.Dispose();
            }
        }

        void EmitTryRead_GenSwitch(SerializerCodeGen g, Local fieldNumber, MetaType metaType, SubType[] subTypes, Action<MetaType> returnGen)
        {
            using (g.ctx.StartDebugBlockAuto(this))
            {
                if (subTypes.Length == 0)
                {
                    returnGen(metaType);
                    return;
                }

                // may be optimized to check -1
                g.ctx.MarkDebug("// Start of switch");
                g.Switch(fieldNumber);
                {
                    foreach (var subType in subTypes)
                    {
                        g.ctx.MarkDebug("// Switch case");
                        g.Case(subType.FieldNumber);
                        returnGen(subType.DerivedType);
                        g.ctx.MarkDebug("// Switch case - break");
                        g.Break();
                    }

                    g.ctx.MarkDebug("// Default switch case");
                    g.DefaultCase();
                    returnGen(metaType);
                    g.ctx.MarkDebug("// Default switch case - break");
                    g.Break();
                }
                g.ctx.MarkDebug("// End of switch");
                g.End();
            }
        }
#endif
    }
}
#endif