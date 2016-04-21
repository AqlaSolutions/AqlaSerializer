using AqlaSerializer.Meta;

namespace AqlaSerializer
{
    public class RootHelpers
    {
        public const int CurrentFormatVersion = 4;
        public const int Rc1FormatVersion = 3;
        public const int FieldLateReferenceObjects = 2;
        public const int FieldNetObjectPositions = 3;

        public static void WriteOwnHeader(ProtoWriter dest)
        {
            ProtoWriter.WriteFieldHeaderBegin(RootHelpers.CurrentFormatVersion, dest);
        }

        public static void WriteOwnFooter(ProtoWriter dest)
        {
            int typeKey;
            object obj;
            int refKey;
            SubItemToken? lateRefFieldToken = null;

            while (ProtoWriter.TryGetNextLateReference(out typeKey, out obj, out refKey, dest))
            {
                if (lateRefFieldToken == null)
                {
                    ProtoWriter.WriteFieldHeaderBegin(RootHelpers.FieldLateReferenceObjects, dest);
                    lateRefFieldToken = ProtoWriter.StartSubItem(null, false, dest);
                }
                ProtoWriter.WriteFieldHeaderBegin(refKey + 1, dest);
                ProtoWriter.WriteRecursionSafeObject(obj, typeKey, dest);
            }

            if (lateRefFieldToken != null) ProtoWriter.EndSubItem(lateRefFieldToken.Value, dest);

            int[] arr = ProtoWriter.GetNetObjectKeyToPositionDeltasArray(dest);
            if (arr.Length != 0)
            {
                ProtoWriter.WriteFieldHeaderBegin(RootHelpers.FieldNetObjectPositions, dest);
                ProtoWriter.WriteArrayContent(arr, WireType.Variant, ProtoWriter.WriteInt32, dest);
            }
        }

        public static int ReadOwnHeader(bool seeking, ProtoReader source)
        {
            int pos = source.Position;
            int blockEnd = source.BlockEndPosition;
            int formatVersion = source.ReadFieldHeader();
            switch (formatVersion)
            {
                case RootHelpers.CurrentFormatVersion:
                case RootHelpers.Rc1FormatVersion:
                    break;
                default:
                    throw new ProtoException("Wrong format version, required " + RootHelpers.CurrentFormatVersion + " but actual " + formatVersion);
            }
            if (formatVersion > RootHelpers.Rc1FormatVersion && seeking && source.AllowReferenceVersioningSeeking)
            {
                // skip to the end
                source.SkipField();
                while (source.ReadFieldHeader() != 0 && source.FieldNumber != RootHelpers.FieldNetObjectPositions)
                    source.SkipField();
                if (source.FieldNumber == RootHelpers.FieldNetObjectPositions)
                    source.SetNetObjectPositionDeltas(
                        source.ReadArrayContent(
                            source.Model?.ReferenceVersioningSeekingObjectsListLimit ?? TypeModel.DefaultReferenceVersioningSeekingObjectsListLimit,
                            source.ReadInt32));
                source.SeekAndExchangeBlockEnd(pos, blockEnd);
                var f = source.ReadFieldHeader();
                Helpers.DebugAssert(f == formatVersion);
            }
            return formatVersion;
        } 

        public static void ReadOwnFooter(int formatVersion, ProtoReader source)
        {
            if (formatVersion > Rc1FormatVersion)
            {
                while (source.ReadFieldHeader() > 0 && source.FieldNumber != FieldLateReferenceObjects)
                    source.SkipField();
                if (source.FieldNumber == FieldLateReferenceObjects)
                {
                    SubItemToken t = ProtoReader.StartSubItem(source);
                    ReadLateReferences(source);
                    ProtoReader.EndSubItem(t, source);
                }


                while (source.ReadFieldHeader() > 0 && source.FieldNumber == FieldNetObjectPositions)
                    source.SkipField(); // skip field on endsubitem doesn't work with root
            }
            else ReadLateReferences(source);
        }

        static void ReadLateReferences(ProtoReader source)
        {
            int typeKey;
            object obj;
            int expectedRefKey;
            while (ProtoReader.TryGetNextLateReference(out typeKey, out obj, out expectedRefKey, source))
            {
                int actualRefKey;
                do
                {
                    actualRefKey = source.ReadFieldHeader() - 1;
                    if (actualRefKey != expectedRefKey)
                    {
                        if (actualRefKey <= -1) throw new ProtoException("Expected field for late reference");
                        // should go only up
                        if (actualRefKey > expectedRefKey) throw new ProtoException("Mismatched order of late reference objects");
                        source.SkipField(); // refKey < num
                    }
                } while (actualRefKey < expectedRefKey);
                object lateObj = ProtoReader.ReadObject(obj, typeKey, source);
                if (!ReferenceEquals(lateObj, obj)) throw new ProtoException("Late reference changed during deserializing");
            }
        }
    }
}