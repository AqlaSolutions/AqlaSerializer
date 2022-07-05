// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
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
            var main = s.SettingsValue;
            if (a.HasFamily(MetaType.AttributeFamily.ProtoBuf))
            {
                object tmp;
                if (item.TryGet("Name", out tmp)) main.Name = (string)tmp;
                if (Helpers.IsEnum(s.Type)) // note this is subtly different to AsEnum; want to do this even if [Flags]
                {
                    bool isExplicit = false;
#if !FEAT_IKVM
                    // IKVM can't access EnumPassthruHasValue, but conveniently, InferTagFromName will only be returned if set via ctor or property
                    isExplicit = item.TryGet("EnumPassthruHasValue", false, out tmp) && (bool)tmp;
#endif
                    if (item.TryGet("EnumPassthru", out tmp)) 
                    {
                        main.EnumPassthru = (bool)tmp;
                        if (!isExplicit && !main.EnumPassthru.Value && s.Type.IsDefined(model.MapType(typeof(FlagsAttribute)), false)) 
                            main.EnumPassthru = true;
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
                        s.ImplicitFields = (ImplicitFieldsMode)(int)tmp; // note that this uses the bizarre unboxing rules of enums/underlying-types
                    }

                    if (item.TryGet("SkipConstructor", out tmp)) main.SkipConstructor = (bool)tmp;
                    if (item.TryGet("IgnoreListHandling", out tmp)) main.IgnoreListHandling = (bool)tmp;
                    if (item.TryGet("AsReferenceDefault", out tmp))
                    {
                        if ((bool)tmp)
                            main.Member.Format = ValueFormat.Reference;
                        else
                            main.Member.Format = ValueSerializerBuilder.GetDefaultLegacyFormat(s.Type, model);
                    }

                    if (item.TryGet("ImplicitFirstTag", out tmp) && (int)tmp > 0) s.ImplicitFirstTag = (int)tmp;
                    if (item.TryGet("ConstructType", out tmp)) main.ConstructType = (Type)tmp;
                    if (item.TryGet("IsGroup", out tmp))
                    {
                        var v = (bool)tmp;
                        var defaultIsGroup = !model.ProtoCompatibility.UseLengthPrefixedNestingAsDefault;
                        if (v) // if set always disable length prefix
                            main.PrefixLength = false;
                        else if (defaultIsGroup) // if not set enable length prefix only if it's not enabled by default
                            main.PrefixLength = true;
                    }

                }

                s.SettingsValue = main;
                return TypeAttributeHandlerResult.Done;
            }

            return TypeAttributeHandlerResult.Continue;
        }
    }
}

#endif