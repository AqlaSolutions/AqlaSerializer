// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if (PLAT_XMLSERIALIZER) || (SILVERLIGHT && !PHONE7)
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using AqlaSerializer.Meta;
using System;

namespace AqlaSerializer.ServiceModel
{
    /// <summary>
    /// An xml object serializer that can embed protobuf data in a base-64 hunk (looking like a byte[])
    /// </summary>
    public sealed class XmlProtoSerializer : XmlObjectSerializer
    {
        private readonly TypeModel _model;
        private readonly int _key;
        private readonly bool _isList, _isEnum;
        private readonly Type _type;
        internal XmlProtoSerializer(TypeModel model, int key, Type type, bool isList)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (key < 0) throw new ArgumentOutOfRangeException(nameof(key));
            if (type == null) throw new ArgumentOutOfRangeException(nameof(type));
            this._model = model;
            this._key = key;
            this._isList = isList;
            this._type = type;
            this._isEnum = Helpers.IsEnum(type);
        }
        /// <summary>
        /// Attempt to create a new serializer for the given model and type
        /// </summary>
        /// <returns>A new serializer instance if the type is recognised by the model; null otherwise</returns>
        public static XmlProtoSerializer TryCreate(TypeModel model, Type type)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (type == null) throw new ArgumentNullException(nameof(type));

            bool isList;
            int key = GetKey(model, ref type, out isList);
            if (key >= 0)
            {
                return new XmlProtoSerializer(model, key, type, isList);
            }
            return null;
        }
        /// <summary>
        /// Creates a new serializer for the given model and type
        /// </summary>
        public XmlProtoSerializer(TypeModel model, Type type)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (type == null) throw new ArgumentNullException(nameof(type));

            _key = GetKey(model, ref type, out _isList);
            this._model = model;
            this._type = type;
            this._isEnum = Helpers.IsEnum(type);
            if (_key < 0) throw new ArgumentOutOfRangeException(nameof(type), "Type not recognised by the model: " + type.FullName);
        }
        static int GetKey(TypeModel model, ref Type type, out bool isList)
        {
            if (model != null && type != null)
            {
                int key = model.GetKey(ref type);
                if (key >= 0)
                {
                    isList = false;
                    return key;
                }
                Type itemType = TypeModel.GetListItemType(model, type);
                if (itemType != null)
                {
                    key = model.GetKey(ref itemType);
                    if (key >= 0)
                    {
                        isList = true;
                        return key;
                    }
                }
            }

            isList = false;
            return -1;
            
        }
        /// <summary>
        /// Ends an object in the output
        /// </summary>
        public override void WriteEndObject(System.Xml.XmlDictionaryWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            writer.WriteEndElement();
        }
        /// <summary>
        /// Begins an object in the output
        /// </summary>
        public override void WriteStartObject(System.Xml.XmlDictionaryWriter writer, object graph)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            writer.WriteStartElement(PROTO_ELEMENT);
        }
        private const string PROTO_ELEMENT = "proto";
        /// <summary>
        /// Writes the body of an object in the output
        /// </summary>
        public override void WriteObjectContent(System.Xml.XmlDictionaryWriter writer, object graph)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            if (graph == null)
            {
                writer.WriteAttributeString("nil", "true");
            }
            else
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    if (_isList)
                    {
                        _model.Serialize(ms, graph, null);
                    }
                    else
                    {
                        using (ProtoWriter protoWriter = new ProtoWriter(ms, _model, null))
                        {
                            _model.Serialize(_key, graph, protoWriter, true);
                        }
                    }
                    byte[] buffer = ms.GetBuffer();
                    writer.WriteBase64(buffer, 0, (int)ms.Length);
                }
            }
        }

        /// <summary>
        /// Indicates whether this is the start of an object we are prepared to handle
        /// </summary>
        public override bool IsStartObject(System.Xml.XmlDictionaryReader reader)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            reader.MoveToContent();
            return reader.NodeType == System.Xml.XmlNodeType.Element && reader.Name == PROTO_ELEMENT;
        }

        /// <summary>
        /// Reads the body of an object
        /// </summary>
        public override object ReadObject(System.Xml.XmlDictionaryReader reader, bool verifyObjectName)
        {
            if (reader == null) throw new ArgumentNullException(nameof(reader));
            reader.MoveToContent();
            bool isSelfClosed = reader.IsEmptyElement, isNil = reader.GetAttribute("nil") == "true";
            reader.ReadStartElement(PROTO_ELEMENT);

            // explicitly null
            if (isNil)
            {
                if(!isSelfClosed) reader.ReadEndElement();
                return null;
            }
            if(isSelfClosed) // no real content
            {
                if (_isList || _isEnum)
                {
                    return _model.Deserialize(Stream.Null, null, _type, null);
                }
                ProtoReader protoReader = null;
                try
                {
                    protoReader = ProtoReader.Create(Stream.Null, _model, null, ProtoReader.TO_EOF);
                    return _model.Deserialize(_key, null, protoReader, true);
                }
                finally
                {
                    ProtoReader.Recycle(protoReader);
                }
            }

            object result;
            Helpers.DebugAssert(reader.CanReadBinaryContent, "CanReadBinaryContent");
            using (MemoryStream ms = new MemoryStream(reader.ReadContentAsBase64()))
            {
                if (_isList || _isEnum)
                {
                    result = _model.Deserialize(ms, null, _type, null);
                }
                else
                {
                    ProtoReader protoReader = null;
                    try
                    {
                        protoReader = ProtoReader.Create(ms, _model, null, ProtoReader.TO_EOF);
                        result = _model.Deserialize(_key, null, protoReader, true);
                    }
                    finally
                    {
                        ProtoReader.Recycle(protoReader);
                    }
                }
            }
            reader.ReadEndElement();
            return result;
        }
    }
}
#endif