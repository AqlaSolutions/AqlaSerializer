// Used protobuf-net source code modified by Vladyslav Taranov for AqlaSerializer, 2016

#if !NO_RUNTIME
using System;
using System.Collections;
using System.Collections.Generic;
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

namespace AqlaSerializer.Meta.Mapping
{
    public class TypeMapper : ITypeMapper
    {
        protected List<Handler> Handlers { get; }

        public class Handler
        {
            public string Attribute { get; }
            public ITypeAttributeHandler Impl { get; }

            public Handler(string attribute, ITypeAttributeHandler handler)
            {
                Attribute = attribute;
                Impl = handler;
            }
        }

        public TypeMapper(IEnumerable<Handler> handlersCollection)
        {
            if (handlersCollection == null) throw new ArgumentNullException(nameof(handlersCollection));
            Handlers = handlersCollection.ToList();
        }

        public virtual TypeState Map(TypeArgsValue args)
        {
            var s = new TypeState(args);
            s.InferTagByName = args.Model.InferTagFromNameDefault;
            if (args.Family == MetaType.AttributeFamily.ImplicitFallback)
            {
                s.ImplicitFields = args.ImplicitFallbackMode;
                s.ImplicitAqla = true;
                s.ImplicitOnlyWriteable = true;
            }
            ProcessAttributeHandlers(s);
            var m = s.SettingsValue;

            args = s.Input;

            if (args.Family == MetaType.AttributeFamily.AutoTuple)
                m.IsAutoTuple = true;
            
            if (s.ImplicitFields != ImplicitFieldsMode.None)
            {
                if (args.Family == MetaType.AttributeFamily.ImplicitFallback)
                {
                    args.Family = MetaType.AttributeFamily.None;
                    if (args.CanUse(AttributeType.ProtoBuf))
                        args.Family |= MetaType.AttributeFamily.ProtoBuf;
                    if (args.CanUse(AttributeType.Aqla))
                        args.Family |= MetaType.AttributeFamily.Aqla;
                }
                else if (args.HasFamily(MetaType.AttributeFamily.Aqla) || args.HasFamily(MetaType.AttributeFamily.ProtoBuf))
                {
                    if (s.ImplicitAqla)
                        args.Family &= MetaType.AttributeFamily.Aqla;
                    else
                        args.Family &= MetaType.AttributeFamily.ProtoBuf; // with implicit fields, **only** proto attributes are important
                }
            }

            s.Input = args;
            s.SettingsValue = m;

            return s;
        }

        protected virtual void ProcessAttributeHandlers(TypeState s)
        {
            TypeArgsValue a = s.Input;

            // sort attributes with handlers order!!! because isEnum may change
            foreach (
                var item in a.Attributes.OrderBy(x => Handlers.Select((h, i) => new { h, i }).FirstOrDefault(h => h.h.Attribute == x.AttributeType.FullName)?.i ?? -1).ToArray())
            {
                ProcessAttributeHandler(item, s);
            }
        }

        protected virtual void ProcessAttributeHandler(AttributeMap item, TypeState s)
        {
            foreach (Handler handler in Handlers)
            {
                if (handler.Attribute == item.AttributeType.FullName && handler.Impl.TryMap(item, s) == TypeAttributeHandlerResult.Done) break;
            }
        }
    }
}

#endif