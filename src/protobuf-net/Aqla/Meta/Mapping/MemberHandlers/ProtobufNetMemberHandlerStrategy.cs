// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections;
using System.Text;
using AltLinq; using System.Linq;
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
                level.DefaultsMode = MemberDefaultsMode.Legacy;

                if (main.Tag <= 0) attribute.TryGetNotDefault("Tag", ref main.Tag);
                if (string.IsNullOrEmpty(main.Name)) attribute.TryGetNotEmpty("Name", ref main.Name);
                if (!main.IsRequiredInSchema) attribute.TryGetNotDefault("IsRequired", ref main.IsRequiredInSchema);

                var type = Helpers.GetMemberType(member);

                BinaryDataFormat dataFormat = 0;
                attribute.TryGetNotDefault("DataFormat", ref dataFormat);
                level.ContentBinaryFormatHint = dataFormat;

                bool isPacked = false;
                attribute.TryGetNotDefault("IsPacked", ref isPacked);
                level.Collection.Format = isPacked 
                    ? CollectionFormat.Protobuf 
                    : (model.ProtoCompatibility.SuppressCollectionEnhancedFormat
                        ? CollectionFormat.ProtobufNotPacked
                        : CollectionFormat.NotSpecified
                    );
                
                bool overwriteList = false;
                attribute.TryGetNotDefault("OverwriteList", ref overwriteList);
                if (overwriteList)
                    level.Collection.Append = false;

                if (!level.WriteAsDynamicType.GetValueOrDefault())
                    attribute.TryGetNotDefault("DynamicType", ref level.WriteAsDynamicType);
                bool dynamicType = level.WriteAsDynamicType.GetValueOrDefault();
                
                bool asRefHasValue = false;
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
                        notAsReference = !value;
                    }
                }

                if (!asRefHasValue)
                {
                    if (level.Format == ValueFormat.NotSpecified)
                        SetLegacyFormat(ref level, member, model);
                }
                else
                    level.Format = notAsReference ? ValueSerializerBuilder.GetDefaultLegacyFormat(type, model) : ValueFormat.Reference;

                s.TagIsPinned = main.Tag > 0;

                var dl = s.SerializationSettings.DefaultLevel.GetValueOrDefault(new ValueSerializationSettings.LevelValue(level.MakeDefaultNestedLevelForLegacyMember()));

                if (isPacked)
                    dl.Basic.Format = ValueFormat.Compact;
                
                if (dynamicType)
                {
                    // apply dynamic type to items
                    Type itemType = null;
                    Type defaultType = null;
                    MetaType.ResolveListTypes(model, Helpers.GetMemberType(s.Member), ref itemType, ref defaultType);
                    if (itemType != null)
                    {
                        dl.Basic.WriteAsDynamicType = true;
                        dl.Basic.Format = ValueFormat.Reference;

                        if (level.Format == ValueFormat.Compact || level.Format == ValueFormat.MinimalEnhancement)
                            level.WriteAsDynamicType = false;
                    }
                }

                s.SerializationSettings.DefaultLevel = dl;
                s.SerializationSettings.SetSettings(level, 0);

                if (false && isPacked) // TODO (errors on int)
                {
                    var nested = s.SerializationSettings.GetSettingsCopy(1).Basic;
                    nested.ContentBinaryFormatHint = BinaryDataFormat.FixedSize;
                    s.SerializationSettings.SetSettings(level, 1);
                }


                return s.TagIsPinned ? MemberHandlerResult.Done : MemberHandlerResult.Partial; // note minAcceptFieldNumber only applies to non-proto
            }
            finally
            {
                s.MainValue = main;
            }
        }
        
        public void SetLegacyFormat(ref MemberLevelSettingsValue level, MemberInfo member, RuntimeTypeModel model)
        {
            Type type = Helpers.GetMemberType(member);
            ValueFormat legacyFormat = ValueSerializerBuilder.GetDefaultLegacyFormat(type, model);
            switch (Helpers.GetTypeCode(type))
            {
                // these can't be registered and don't have AsReferenceDefault
                // explicitely set their asReference = false
                case ProtoTypeCode.String:
                case ProtoTypeCode.ByteArray:
                case ProtoTypeCode.Type:
                case ProtoTypeCode.Uri:
                    level.Format = legacyFormat;
                    break;
                default:
                    if (!RuntimeTypeModel.CheckTypeCanBeAdded(model, type))
                    {
                        // for primitive types - explicitly
                        level.Format = legacyFormat;
                    }
                    break;
            }
        }
    }
}
#endif