// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using AltLinq;
using AqlaSerializer;
using AqlaSerializer.Meta;
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

namespace AqlaSerializer.Meta.Mapping.TypeAttributeHandlers
{
    public class ProtoContractHandler : TypeAttributeMappingHandlerBase
    {
        protected override TypeAttributeHandlerResult TryMap(AttributeMap item, TypeState s, TypeArgsValue a, RuntimeTypeModel model)
        {
            object tmp;
            var main = s.SettingsValue;
            if (a.HasFamily(MetaType.AttributeFamily.ProtoBuf))
            {
                if (item.TryGet("Name", out tmp)) main.Name = (string)tmp;
                if (Helpers.IsEnum(s.Type)) // note this is subtly different to AsEnum; want to do this even if [Flags]
                {
#if !FEAT_IKVM
                    // IKVM can't access EnumPassthruHasValue, but conveniently, InferTagFromName will only be returned if set via ctor or property
                    if (item.TryGet("EnumPassthruHasValue", false, out tmp) && (bool)tmp)
#endif
                    {
                        if (item.TryGet("EnumPassthru", out tmp))
                        {
                            main.EnumPassthru = (bool)tmp;
                            if (main.EnumPassthru.GetValueOrDefault()) s.AsEnum = false; // no longer treated as an enum
                        }
                    }
                }
                else
                {
                    if (item.TryGet("DataMemberOffset", out tmp)) s.DataMemberOffset = (int)tmp;

#if !FEAT_IKVM
                    // IKVM can't access InferTagFromNameHasValue, but conveniently, InferTagFromName will only be returned if set via ctor or property
                    if (item.TryGet("InferTagFromNameHasValue", false, out tmp) && (bool)tmp)
#endif
                    {
                        if (item.TryGet("InferTagFromName", out tmp)) s.InferTagByName = (bool)tmp;
                    }

                    if (item.TryGet("ImplicitFields", out tmp) && tmp != null)
                    {
                        s.ImplicitMode = (ImplicitFieldsMode)(int)tmp; // note that this uses the bizarre unboxing rules of enums/underlying-types
                    }
                    if (item.TryGet("SkipConstructor", out tmp)) main.SkipConstructor = (bool)tmp;
                    if (item.TryGet("IgnoreListHandling", out tmp)) main.IgnoreListHandling = (bool)tmp;
                    if (item.TryGet("AsReferenceDefault", out tmp))
                    {
                        if ((bool)tmp)
                        {
                            main.Member.MemberFormat = MemberFormat.Enhanced;
                            main.Member.EnhancedWriteMode = EnhancedMode.Reference;
                        }
                        else
                        {
                            main.Member.MemberFormat = MemberFormat.Compact;
                            main.Member.EnhancedWriteMode = EnhancedMode.NotSpecified;
                        }
                    }
                    if (item.TryGet("ImplicitFirstTag", out tmp) && (int)tmp > 0) s.ImplicitFirstTag = (int)tmp;
                    if (item.TryGet("ConstructType", out tmp)) main.ConcreteType = (Type)tmp;
                }

                s.SettingsValue = main;
                return TypeAttributeHandlerResult.Done;
            }
            return TypeAttributeHandlerResult.Continue;
        }
    }
}

#endif