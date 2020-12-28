// Modified by Vladyslav Taranov for AqlaSerializer, 2021
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using AltLinq;

namespace AqlaSerializer
{
    internal enum TimeSpanScale
    {
        Days = 0,
        Hours = 1,
        Minutes = 2,
        Seconds = 3,
        Milliseconds = 4,
        Ticks = 5,

        MinMax = 15
    }

    /// <summary>
    /// Provides support for common .NET types that do not have a direct representation
    /// in protobuf, using the definitions from bcl.proto
    /// </summary>
    public
#if FX11
    sealed
#else
    static
#endif
        class BclHelpers
    {
        /// <summary>
        /// Creates a new instance of the specified type, bypassing the constructor.
        /// </summary>
        /// <param name="type">The type to create</param>
        /// <returns>The new instance</returns>
        /// <exception cref="NotSupportedException">If the platform does not support constructor-skipping</exception>
        public static object GetUninitializedObject(Type type)
        {
#if NETSTANDARD
            object obj = TryGetUninitializedObjectWithFormatterServices(type);
            if (obj != null) return obj;
#endif
#if PLAT_BINARYFORMATTER && !(WINRT || PHONE8)
            return System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type);
#else
            if (_getUninitializedObject == null)
            {
                try
                {
                    var t = Helpers.GetAssembly(typeof(string)).GetType("System.Runtime.Serialization.FormatterServices");
                    if (t != null)
                    {
                        var formatterServiceType = Helpers.GetTypeInfo(t);
                        MethodInfo method = Helpers.GetStaticMethod(formatterServiceType, "GetUninitializedObject");
                        if (method != null)
                        {
                            _getUninitializedObject = (Func<Type, object>)Helpers.CreateDelegate(typeof(Func<Type, object>), method);
                        }
                    }
                }
                catch  { /* best efforts only */ }
                if(_getUninitializedObject == null) _getUninitializedObject = x => null;
            }
            return _getUninitializedObject(type);
#endif
        }

#if NETSTANDARD // this is inspired by DCS: https://github.com/dotnet/corefx/blob/c02d33b18398199f6acc17d375dab154e9a1df66/src/System.Private.DataContractSerialization/src/System/Runtime/Serialization/XmlFormatReaderGenerator.cs#L854-L894
        static volatile Func<Type, object> getUninitializedObject;
        static internal object TryGetUninitializedObjectWithFormatterServices(Type type)
        {
            if (getUninitializedObject == null)
            {
                try {
                    var formatterServiceType = typeof(string).GetTypeInfo().Assembly.GetType("System.Runtime.Serialization.FormatterServices");
                    MethodInfo method = formatterServiceType?.GetMethod("GetUninitializedObject", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static);
                    if (method != null)
                    {
                        getUninitializedObject = (Func<Type, object>)method.CreateDelegate(typeof(Func<Type, object>));
                    }
                }
                catch  { /* best efforts only */ }
                if(getUninitializedObject == null) getUninitializedObject = x => null;
            }
            return getUninitializedObject(type);
        }
#endif


        static Func<Type, object> _getUninitializedObject;

#if FX11
        private BclHelpers() { } // not a static class for C# 1.2 reasons
#endif
        const int FieldTimeSpanValue = 0x01, FieldTimeSpanScale = 0x02, FieldTimeSpanKind = 0x03;

        internal static readonly DateTime[] EpochOrigin = {
            new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
            new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Local)
        };


        
        /// <summary>
        /// Writes a TimeSpan to a protobuf stream
        /// </summary>
        public static void WriteTimeSpan(TimeSpan timeSpan, ProtoWriter dest)
        {
            WriteTimeSpanImpl(timeSpan, dest, DateTimeKind.Unspecified);
        }

        private static void WriteTimeSpanImpl(TimeSpan timeSpan, ProtoWriter dest, DateTimeKind kind)
        {
            if (dest == null) throw new ArgumentNullException(nameof(dest));
            switch(dest.WireType)
            {
                case WireType.String:
                case WireType.StartGroup:
                    TimeSpanScale scale;
                    long value = timeSpan.Ticks;
                    if (timeSpan == TimeSpan.MaxValue)
                    {
                        value = 1;
                        scale = TimeSpanScale.MinMax;
                    }
                    else if (timeSpan == TimeSpan.MinValue)
                    {
                        value = -1;
                        scale = TimeSpanScale.MinMax;
                    }
                    else if (value % TimeSpan.TicksPerDay == 0)
                    {
                        scale = TimeSpanScale.Days;
                        value /= TimeSpan.TicksPerDay;
                    }
                    else if (value % TimeSpan.TicksPerHour == 0)
                    {
                        scale = TimeSpanScale.Hours;
                        value /= TimeSpan.TicksPerHour;
                    }
                    else if (value % TimeSpan.TicksPerMinute == 0)
                    {
                        scale = TimeSpanScale.Minutes;
                        value /= TimeSpan.TicksPerMinute;
                    }
                    else if (value % TimeSpan.TicksPerSecond == 0)
                    {
                        scale = TimeSpanScale.Seconds;
                        value /= TimeSpan.TicksPerSecond;
                    }
                    else if (value % TimeSpan.TicksPerMillisecond == 0)
                    {
                        scale = TimeSpanScale.Milliseconds;
                        value /= TimeSpan.TicksPerMillisecond;
                    }
                    else
                    {
                        scale = TimeSpanScale.Ticks;
                    }

                    SubItemToken token = ProtoWriter.StartSubItemWithoutWritingHeader(null, dest);
            
                    if(value != 0) {
                        ProtoWriter.WriteFieldHeader(FieldTimeSpanValue, WireType.SignedVariant, dest);
                        ProtoWriter.WriteInt64(value, dest);
                    }
                    if(scale != TimeSpanScale.Days) {
                        ProtoWriter.WriteFieldHeader(FieldTimeSpanScale, WireType.Variant, dest);
                        ProtoWriter.WriteInt32((int)scale, dest);
                    }
                    if(kind != DateTimeKind.Unspecified)
                    {
                        ProtoWriter.WriteFieldHeader(FieldTimeSpanKind, WireType.Variant, dest);
                        ProtoWriter.WriteInt32((int)kind, dest);
                    }
                    ProtoWriter.EndSubItem(token, dest);
                    break;
                case WireType.Fixed64:
                    ProtoWriter.WriteInt64(timeSpan.Ticks, dest);
                    break;
                default:
                    throw new ProtoException("Unexpected wire-type: " + dest.WireType.ToString());
            }
        }
        /// <summary>
        /// Parses a TimeSpan from a protobuf stream
        /// </summary>        
        public static TimeSpan ReadTimeSpan(ProtoReader source)
        {
            DateTimeKind kind;
            long ticks = ReadTimeSpanTicks(source, out kind);
            if (ticks == long.MinValue) return TimeSpan.MinValue;
            if (ticks == long.MaxValue) return TimeSpan.MaxValue;
            return TimeSpan.FromTicks(ticks);
        }
        /// <summary>
        /// Parses a DateTime from a protobuf stream
        /// </summary>
        public static DateTime ReadDateTime(ProtoReader source)
        {
            DateTimeKind kind;
            long ticks = ReadTimeSpanTicks(source, out kind);
            if (ticks == long.MinValue) return DateTime.MinValue;
            if (ticks == long.MaxValue) return DateTime.MaxValue;
            return EpochOrigin[(int)kind].AddTicks(ticks);
        }

        /// <summary>
        /// Writes a DateTime to a protobuf stream, excluding the <c>Kind</c>
        /// </summary>
        public static void WriteDateTime(DateTime value, ProtoWriter dest)
        {
            WriteDateTimeImpl(value, dest, false);
        }
        /// <summary>
        /// Writes a DateTime to a protobuf stream, including the <c>Kind</c>
        /// </summary>
        public static void WriteDateTimeWithKind(DateTime value, ProtoWriter dest)
        {
            WriteDateTimeImpl(value, dest, true);
        }

        private static void WriteDateTimeImpl(DateTime value, ProtoWriter dest, bool includeKind)
        {
            if (dest == null) throw new ArgumentNullException(nameof(dest));
            TimeSpan delta;
            switch (dest.WireType)
            {
                case WireType.StartGroup:
                case WireType.String:
                    if (value == DateTime.MaxValue)
                    {
                        delta = TimeSpan.MaxValue;
                        includeKind = false;
                    }
                    else if (value == DateTime.MinValue)
                    {
                        delta = TimeSpan.MinValue;
                        includeKind = false;
                    }
                    else
                    {
                        delta = value - EpochOrigin[0];
                    }
                    break;
                default:
                    delta = value - EpochOrigin[0];
                    break;
            }
            WriteTimeSpanImpl(delta, dest, includeKind ? value.Kind : DateTimeKind.Unspecified);
        }

        private static long ReadTimeSpanTicks(ProtoReader source, out DateTimeKind kind) {
            kind = DateTimeKind.Unspecified;
            switch (source.WireType)
            {
                case WireType.String:
                case WireType.StartGroup:
                    SubItemToken token = ProtoReader.StartSubItem(source);
                    int fieldNumber;
                    TimeSpanScale scale = TimeSpanScale.Days;
                    long value = 0;
                    while ((fieldNumber = source.ReadFieldHeader()) > 0)
                    {
                        switch (fieldNumber)
                        {
                            case FieldTimeSpanScale:
                                scale = (TimeSpanScale)source.ReadInt32();
                                break;
                            case FieldTimeSpanValue:
                                source.Assert(WireType.SignedVariant);
                                value = source.ReadInt64();
                                break;
                            case FieldTimeSpanKind:
                                kind = (DateTimeKind)source.ReadInt32();
                                switch(kind)
                                {
                                    case DateTimeKind.Unspecified:
                                    case DateTimeKind.Utc:
                                    case DateTimeKind.Local:
                                        break; // fine
                                    default:
                                        throw new ProtoException("Invalid date/time kind: " + kind.ToString());
                                }
                                break;
                            default:
                                source.SkipField();
                                break;
                        }
                    }
                    ProtoReader.EndSubItem(token, source);
                    switch (scale)
                    {
                        case TimeSpanScale.Days:
                            return value * TimeSpan.TicksPerDay;
                        case TimeSpanScale.Hours:
                            return value * TimeSpan.TicksPerHour;
                        case TimeSpanScale.Minutes:
                            return value * TimeSpan.TicksPerMinute;
                        case TimeSpanScale.Seconds:
                            return value * TimeSpan.TicksPerSecond;
                        case TimeSpanScale.Milliseconds:
                            return value * TimeSpan.TicksPerMillisecond;
                        case TimeSpanScale.Ticks:
                            return value;
                        case TimeSpanScale.MinMax:
                            switch (value)
                            {
                                case 1: return long.MaxValue;
                                case -1: return long.MinValue;
                                default: throw new ProtoException("Unknown min/max value: " + value.ToString());
                            }
                        default:
                            throw new ProtoException("Unknown timescale: " + scale.ToString());
                    }
                case WireType.Fixed64:
                    return source.ReadInt64();
                default:
                    throw new ProtoException("Unexpected wire-type: " + source.WireType.ToString());
            }
        }

        const int FieldDecimalLow = 0x01, FieldDecimalHigh = 0x02, FieldDecimalSignScale = 0x03;

        /// <summary>
        /// Parses a decimal from a protobuf stream
        /// </summary>
        public static decimal ReadDecimal(ProtoReader reader)
        {
            ulong low = 0;
            uint high = 0;
            uint signScale = 0;

            int fieldNumber;
            SubItemToken token = ProtoReader.StartSubItem(reader);
            while ((fieldNumber = reader.ReadFieldHeader()) > 0)
            {
                switch (fieldNumber)
                {
                    case FieldDecimalLow: low = reader.ReadUInt64(); break;
                    case FieldDecimalHigh: high = reader.ReadUInt32(); break;
                    case FieldDecimalSignScale: signScale = reader.ReadUInt32(); break;
                    default: reader.SkipField(); break;
                }
                
            }
            ProtoReader.EndSubItem(token, reader);

            if (low == 0 && high == 0) return decimal.Zero;

            int lo = (int)(low & 0xFFFFFFFFL),
                mid = (int)((low >> 32) & 0xFFFFFFFFL),
                hi = (int)high;
            bool isNeg = (signScale & 0x0001) == 0x0001;
            byte scale = (byte)((signScale & 0x01FE) >> 1);
            return new decimal(lo, mid, hi, isNeg, scale);
        }
        /// <summary>
        /// Writes a decimal to a protobuf stream
        /// </summary>
        public static void WriteDecimal(decimal value, ProtoWriter writer)
        {
            int[] bits = decimal.GetBits(value);
            ulong a = ((ulong)bits[1]) << 32, b = ((ulong)bits[0]) & 0xFFFFFFFFL;
            ulong low = a | b;
            uint high = (uint)bits[2];
            uint signScale = (uint)(((bits[3] >> 15) & 0x01FE) | ((bits[3] >> 31) & 0x0001));

            SubItemToken token = ProtoWriter.StartSubItemWithoutWritingHeader(null, writer);
            if (low != 0) {
                ProtoWriter.WriteFieldHeader(FieldDecimalLow, WireType.Variant, writer);
                ProtoWriter.WriteUInt64(low, writer);
            }
            if (high != 0)
            {
                ProtoWriter.WriteFieldHeader(FieldDecimalHigh, WireType.Variant, writer);
                ProtoWriter.WriteUInt32(high, writer);
            }
            if (signScale != 0)
            {
                ProtoWriter.WriteFieldHeader(FieldDecimalSignScale, WireType.Variant, writer);
                ProtoWriter.WriteUInt32(signScale, writer);
            }
            ProtoWriter.EndSubItem(token, writer);
        }

        const int FieldGuidLow = 1, FieldGuidHigh = 2;
        /// <summary>
        /// Writes a Guid to a protobuf stream
        /// </summary>        
        public static void WriteGuid(Guid value, ProtoWriter dest)
        {
            byte[] blob = value.ToByteArray();

            SubItemToken token = ProtoWriter.StartSubItemWithoutWritingHeader(null, dest);
            if (value != Guid.Empty)
            {
                ProtoWriter.WriteFieldHeader(FieldGuidLow, WireType.Fixed64, dest);
                ProtoWriter.WriteBytes(blob, 0, 8, dest);
                ProtoWriter.WriteFieldHeader(FieldGuidHigh, WireType.Fixed64, dest);
                ProtoWriter.WriteBytes(blob, 8, 8, dest);
            }
            ProtoWriter.EndSubItem(token, dest);
        }
        /// <summary>
        /// Parses a Guid from a protobuf stream
        /// </summary>
        public static Guid ReadGuid(ProtoReader source)
        {
            ulong low = 0, high = 0;
            int fieldNumber;
            SubItemToken token = ProtoReader.StartSubItem(source);
            while ((fieldNumber = source.ReadFieldHeader()) > 0)
            {
                switch (fieldNumber)
                {
                    case FieldGuidLow: low = source.ReadUInt64(); break;
                    case FieldGuidHigh: high = source.ReadUInt64(); break;
                    default: source.SkipField(); break;
                }
            }
            ProtoReader.EndSubItem(token, source);
            if(low == 0 && high == 0) return Guid.Empty;
            uint a = (uint)(low >> 32), b = (uint)low, c = (uint)(high >> 32), d= (uint)high;
            return new Guid((int)b, (short)a, (short)(a >> 16), 
                (byte)d, (byte)(d >> 8), (byte)(d >> 16), (byte)(d >> 24),
                (byte)c, (byte)(c >> 8), (byte)(c >> 16), (byte)(c >> 24));
            
        }


        /// <summary>
        /// Optional behaviours that introduce .NET-specific functionality
        /// </summary>
        [Flags]
        public enum NetObjectOptions : byte
        {
            /// <summary>
            /// No special behaviour
            /// </summary>
            None = 0,
            /// <summary>
            /// Enables full object-tracking/full-graph support.
            /// </summary>
            AsReference = 1,
            /// <summary>
            /// Embeds the type information into the stream, allowing usage with types not known in advance.
            /// </summary>
            DynamicType = 2,
            /// <summary>
            /// If false, the constructor for the type is bypassed during deserialization, meaning any field initializers
            /// or other initialization code is skipped.
            /// </summary>
            UseConstructor = 4,
            /// <summary>
            /// Should not expect serializer to call NoteObject: usable for serializers of primitive immutable reference types (e.g. String, System.Type) 
            /// </summary>
            LateSet = 8,
            /// <summary>
            /// Not recursive
            /// </summary>
            WriteAsLateReference = 16,
        }
    }
}
