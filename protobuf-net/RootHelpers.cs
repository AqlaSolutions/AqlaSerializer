using AqlaSerializer.Meta;

namespace AqlaSerializer
{
    public class RootHelpers
    {
        public const int CurrentFormatVersion = 4;
        public const int Rc1FormatVersion = 3;
        public const int FieldLateReferenceObjects = 2;
        public const int FieldNetObjectPositions = 3;

        static readonly int[] Int0 = new int[0];

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

            int[] arr = dest.NetCacheKeyPositionsList.ExportNewWithoutRoot();
            
            // always write it
            // and no need to believe in detection
            // we can check array length right now
            if (arr.Length > 0)
            {
                ProtoWriter.WriteFieldHeaderBegin(RootHelpers.FieldNetObjectPositions, dest);
                ProtoWriter.WriteArrayContent(arr, WireType.Variant, ProtoWriter.WriteInt32, dest);
            }
        }

        public static int ReadOwnHeader(bool seeking, ProtoReader source)
        {
            // it's not expected that any root decorator may import new objects inside this
            source.NetCacheKeyPositionsList.EnterImportingLock();

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
                {
                    source.NetCacheKeyPositionsList.ImportRoot();
                    ImportReferencePositions(source);
                }
                else
                    source.NetCacheKeyPositionsList.ImportRoot();
                source.SeekAndExchangeBlockEnd(pos, blockEnd);
                var f = source.ReadFieldHeader();
                if (f != formatVersion) throw new ProtoException("Couldn't correctly rewind to stream start after reading net object positions list");
            }
            else
            {
                // we will call import when reading footer if there is a data
                source.NetCacheKeyPositionsList.ImportRoot();
            }
            return formatVersion;
        }

        static void ImportReferencePositions(ProtoReader source)
        {
            source.NetCacheKeyPositionsList.ImportNextWithoutRoot(
                source.ReadArrayContent(
                    source.Model?.ReferenceVersioningSeekingObjectsListLimit ?? TypeModel.DefaultReferenceVersioningSeekingObjectsListLimit,
                    source.ReadInt32));
        }

        public static void ReadOwnFooter(bool seeking, int formatVersion, ProtoReader source)
        {
            try
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

                    if (source.ReadFieldHeader() > 0 && source.FieldNumber == FieldNetObjectPositions)
                    {
                        if (seeking) // it's important to not read it twice!
                            source.SkipField();
                        else
                        {
                            // for versioning we should read it now if didn't read before, may affect aux lists where multiple roots reuse same net object list
                            // do not call ImportRoot - already imported in header
                            ImportReferencePositions(source);
                        }
                    }
                }
                else ReadLateReferences(source);
            }
            finally
            {
                source.NetCacheKeyPositionsList.ReleaseImportingLock();
            }
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