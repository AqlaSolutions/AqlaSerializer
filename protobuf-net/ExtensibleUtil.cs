// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System;
using System.Collections;
#if !NO_GENERICS
using System.Collections.Generic;
#endif
using System.IO;
using AqlaSerializer.Meta;

namespace AqlaSerializer
{
    /// <summary>
    /// This class acts as an internal wrapper allowing us to do a dynamic
    /// methodinfo invoke; an't put into Serializer as don't want on public
    /// API; can't put into Serializer&lt;T&gt; since we need to invoke
    /// accross classes, which isn't allowed in Silverlight)
    /// </summary>
    internal class ExtensibleUtil
    {
        readonly TypeModel _typeModel;

        public ExtensibleUtil(TypeModel typeModel)
        {
            _typeModel = typeModel;
        }

#if !NO_RUNTIME && !NO_GENERICS
        /// <summary>
        /// All this does is call GetExtendedValuesTyped with the correct type for "instance";
        /// this ensures that we don't get issues with subclasses declaring conflicting types -
        /// the caller must respect the fields defined for the type they pass in.
        /// </summary>
        public IEnumerable<TValue> GetExtendedValues<TValue>(IExtensible instance, int tag, BinaryDataFormat format, bool singleton, bool allowDefinedTag)
        {
            foreach (TValue value in GetExtendedValues(_typeModel, typeof(TValue), instance, tag, format, singleton, allowDefinedTag))
            {
                yield return value;
            }
        }
#endif
        /// <summary>
        /// All this does is call GetExtendedValuesTyped with the correct type for "instance";
        /// this ensures that we don't get issues with subclasses declaring conflicting types -
        /// the caller must respect the fields defined for the type they pass in.
        /// </summary>
        public IEnumerable GetExtendedValues(TypeModel model, Type type, IExtensible instance, int tag, BinaryDataFormat format, bool singleton, bool allowDefinedTag)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else

            if (instance == null) throw new ArgumentNullException("instance");
            if (tag <= 0) throw new ArgumentOutOfRangeException("tag");
            IExtension extn = instance.GetExtensionObject(false);

            if (extn == null)
            {
#if FX11
                return new object[0];
#else
                yield break;
#endif
            }

#if FX11
            BasicList result = new BasicList();
#endif
            Stream stream = extn.BeginQuery();
            object value = null;
            ProtoReader reader = null;
            try {
                SerializationContext ctx = new SerializationContext();
                reader = ProtoReader.Create(stream, model, ctx, ProtoReader.TO_EOF);
                while (model.TryDeserializeAuxiliaryType(reader, format, tag, type, ref value, true, false, false, false, true) && value != null)
                {
                    if (!singleton)
                    {
#if FX11
                        result.Add(value);
#else
                        yield return value;
#endif
                        value = null; // fresh item each time
                    }
                }
                if (singleton && value != null)
                {
#if FX11
                    result.Add(value);
#else
                    yield return value;
#endif
                }
#if FX11
                object[] resultArr = new object[result.Count];
                result.CopyTo(resultArr, 0);
                return resultArr;
#endif
            } finally {
                ProtoReader.Recycle(reader);
                extn.EndQuery(stream);
            }
#endif       
        }

        public void AppendExtendValue(TypeModel model, IExtensible instance, int tag, BinaryDataFormat format, object value)
        {
#if FEAT_IKVM
            throw new NotSupportedException();
#else
            if(instance == null) throw new ArgumentNullException("instance");
            if(value == null) throw new ArgumentNullException("value");
            
            // obtain the extension object and prepare to write
            IExtension extn = instance.GetExtensionObject(true);
            if (extn == null) throw new InvalidOperationException("No extension object available; appended data would be lost.");
            bool commit = false;
            Stream stream = extn.BeginAppend();
            try {
                using(ProtoWriter writer = new ProtoWriter(stream, model, null)) {
                    model.TrySerializeAuxiliaryType(writer, null, format, tag, value, false, true);
                    writer.Close();
                }
                commit = true;
            }
            finally {
                extn.EndAppend(stream, commit);
            }
#endif
        }
//#if !NO_GENERICS
//        /// <summary>
//        /// Stores the given value into the instance's stream; the serializer
//        /// is inferred from TValue and format.
//        /// </summary>
//        /// <remarks>Needs to be public to be callable thru reflection in Silverlight</remarks>
//        public void AppendExtendValueTyped<TSource, TValue>(
//            TypeModel model, TSource instance, int tag, DataFormat format, TValue value)
//            where TSource : class, IExtensible
//        {
//            AppendExtendValue(model, instance, tag, format, value);
//        }
//#endif
    }

}