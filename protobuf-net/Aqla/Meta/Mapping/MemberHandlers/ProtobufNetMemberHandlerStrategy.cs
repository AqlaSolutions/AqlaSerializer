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
            var main = s.MainValue;
            var levels = s.LevelValues;
            try
            {
                MemberLevelSettingsValue level = levels[0].GetValueOrDefault();
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
                Type memberType = Helpers.GetMemberType(member);
                if (!asRefHasValue && !ValueMember.GetAsReferenceDefault(model, memberType, true, false))
                {
                    // by default enable for ProtoMember
                    notAsReferenceHasValue = true;
                    notAsReference = true;
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
                        level.MemberFormat = MemberFormat.Compact;
                    }
                    else
                    {
                        level.EnhancedWriteMode = EnhancedMode.Reference;
                        level.MemberFormat = MemberFormat.Enhanced;
                    }
                }

                if (!level.WriteAsDynamicType.GetValueOrDefault())
                {
                    if (attribute.TryGetNotDefault("DynamicType", ref level.WriteAsDynamicType) && level.WriteAsDynamicType.Value)
                        level.MemberFormat = MemberFormat.Enhanced;
                }
                s.TagIsPinned = main.Tag > 0;
                levels[0] = level;
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