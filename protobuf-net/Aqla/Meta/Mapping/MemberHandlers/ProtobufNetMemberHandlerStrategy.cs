// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections;
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

namespace AqlaSerializer.Meta.Mapping.MemberHandlers
{
    public class ProtobufNetMemberHandlerStrategy : IMemberAttributeHandlerStrategy
    {
        public MemberHandlerResult TryRead(AttributeMap attribute, MemberState s, MemberInfo member, RuntimeTypeModel model)
        {
            if (s.Input.IsEnumValueMember) return MemberHandlerResult.NotFound;
            var main = s.MainValue;
            try
            {
                MemberLevelSettingsValue level = s.SerializationSettings.GetSettingsCopy(0).Basic;
                if (main.Tag <= 0) attribute.TryGetNotDefault("Tag", ref main.Tag);
                if (string.IsNullOrEmpty(main.Name)) attribute.TryGetNotEmpty("Name", ref main.Name);
                if (!main.IsRequiredInSchema) attribute.TryGetNotDefault("IsRequired", ref main.IsRequiredInSchema);

                bool isPacked = false;
                attribute.TryGetNotDefault("IsPacked", ref isPacked);
                level.Collection.Format = isPacked ? CollectionFormat.Google : CollectionFormat.GoogleNotPacked;

                bool overwriteList = false;
                attribute.TryGetNotDefault("OverwriteList", ref overwriteList);
                level.Collection.Append = !overwriteList;

                BinaryDataFormat dataFormat = 0;
                attribute.TryGetNotDefault("DataFormat", ref dataFormat);
                level.ContentBinaryFormatHint = dataFormat;

                bool asRefHasValue = false;
                bool notAsReferenceHasValue = false;
                bool notAsReference = false;
#if !FEAT_IKVM
                // IKVM can't access AsReferenceHasValue, but conveniently, AsReference will only be returned if set via ctor or property
                attribute.TryGetNotDefault("AsReferenceHasValue", ref asRefHasValue, publicOnly: false);
                if (asRefHasValue)
#endif
                {
                    bool value = false;
                    asRefHasValue = attribute.TryGetNotDefault("AsReference", ref value);
                    if (asRefHasValue) // if AsReference = true - use defaults
                    {
                        notAsReferenceHasValue = true;
                        notAsReference = !value;
                    }
                }

                if (!asRefHasValue)
                {
                    // those can't be registered and don't have AsReferenceDefault
                    // explicitely set their asReference = false
                    switch (Helpers.GetTypeCode(Helpers.GetMemberType(member)))
                    {
                        case ProtoTypeCode.String:
                        case ProtoTypeCode.ByteArray:
                        case ProtoTypeCode.Type:
                        case ProtoTypeCode.Uri:
                            notAsReference = true;
                            notAsReferenceHasValue = true;
                            break;
                        default:
                            if (!RuntimeTypeModel.CheckTypeCanBeAdded(model, Helpers.GetMemberType(member)))
                            {
                                notAsReference = true;
                                notAsReferenceHasValue = true;
                            }
                            else
                            {
                                // we could check GetContactFamily == null
                                // but type still may be force-added even without contract
                                // so we have to implicitely set fallback to false
                                // the downside is that noone will know why
                                // the default is not as reference for this member
                                // if its EffectiveType is not registered
                                level.EnhancedFormatDefaultFallback = false;
                            }
                            break;
                    }
                }

                if (notAsReferenceHasValue)
                {
                    // it depends on ValueMember.GetAsReferenceDefault() result!
                    // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                    if (notAsReference)
                    {
                        level.EnhancedWriteMode = EnhancedMode.NotSpecified;
                        // -x-
                        //bool isNullable = !Helpers.IsValueType(memberType) || Helpers.GetNullableUnderlyingType(memberType) != null;
                        //// supports null is also affected so we don't change anything or ref type: with compatibility mode Compact will be used otherwise Enhanced
                        //if (!isNullable)
                        // -x-
                        level.EnhancedFormat = false;
                    }
                    else
                    {
                        level.EnhancedWriteMode = EnhancedMode.Reference;
                        level.EnhancedFormat = true;
                    }
                }

                if (!level.WriteAsDynamicType.GetValueOrDefault())
                    attribute.TryGetNotDefault("DynamicType", ref level.WriteAsDynamicType);
                if (level.WriteAsDynamicType.GetValueOrDefault()) level.EnhancedFormat = true;

                s.TagIsPinned = main.Tag > 0;
                s.SerializationSettings.SetSettings(level, 0);

                if (!asRefHasValue)
                {
                    var ds = s.SerializationSettings.DefaultLevel.GetValueOrDefault();
                    ds.Basic.EnhancedFormat = false;
                    s.SerializationSettings.DefaultLevel = ds;
                }
                
                return s.TagIsPinned ? MemberHandlerResult.Done : MemberHandlerResult.Partial; // note minAcceptFieldNumber only applies to non-proto
            }
            finally
            {
                s.MainValue = main;
            }
        }
    }
}
#endif