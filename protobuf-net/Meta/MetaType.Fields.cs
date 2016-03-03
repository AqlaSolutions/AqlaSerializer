// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Threading;
using AltLinq; using System.Linq;
using AqlaSerializer;
using AqlaSerializer.Internal;
using AqlaSerializer.Meta.Mapping;
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


namespace AqlaSerializer.Meta
{
    partial class MetaType
    {
        private BasicList _fields = new BasicList();

        internal System.Collections.IEnumerable Fields => this._fields;
        
        public int GetNextFreeFieldNumber()
        {
            return GetNextFreeFieldNumber(1);
        }

        public int GetNextFreeFieldNumber(int start)
        {
            int number = start - 1;
            bool found;
            do
            {
                if (number++ == short.MaxValue) return -1;
                found = false;
                // they are not sorted, so...
                foreach (ValueMember f in Fields)
                {
                    if (f.FieldNumber == number)
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    if (_subTypes != null)
                        foreach (SubType t in _subTypes)
                        {
                            if (t.FieldNumber == number)
                            {
                                found = true;
                                break;
                            }
                        }
                }
            } while (found);
            return number;
        }

        internal bool IsDefined(int fieldNumber)
        {
            foreach (ValueMember field in _fields)
            {
                if (field.FieldNumber == fieldNumber) return true;
            }
            return false;
        }

        private int GetNextFieldNumber()
        {
            int maxField = 0;
            foreach (ValueMember member in _fields)
            {
                if (member.FieldNumber > maxField) maxField = member.FieldNumber;
            }
            if (_subTypes != null)
            {
                foreach (SubType subType in _subTypes)
                {
                    if (subType.FieldNumber > maxField) maxField = subType.FieldNumber;
                }
            }
            return maxField + 1;
        }

        /// <summary>
        /// Adds a member (by name) to the MetaType
        /// </summary>        
        public MetaType Add(int fieldNumber, string memberName)
        {
            AddField(fieldNumber, memberName, null, null, null);
            return this;
        }
        /// <summary>
        /// Adds a member (by name) to the MetaType, returning the ValueMember rather than the fluent API.
        /// This is otherwise identical to Add.
        /// </summary>
        public ValueMember AddField(int fieldNumber, string memberName)
        {
            return AddField(fieldNumber, memberName, null, null, null);
        }

        /// <summary>
        /// Adds a member (by name) to the MetaType
        /// </summary>     
        public MetaType Add(string memberName)
        {
            Add(GetNextFieldNumber(), memberName);
            return this;
        }

        /// <summary>
        /// Adds a set of members (by name) to the MetaType
        /// </summary>     
        public MetaType Add(params string[] memberNames)
        {
            if (memberNames == null) throw new ArgumentNullException("memberNames");
            int next = GetNextFieldNumber();
            for (int i = 0; i < memberNames.Length; i++)
            {
                Add(next++, memberNames[i]);
            }
            return this;
        }


        /// <summary>
        /// Adds a member (by name) to the MetaType
        /// </summary>        
        public MetaType Add(int fieldNumber, string memberName, object defaultValue)
        {
            AddField(fieldNumber, memberName, null, null, defaultValue);
            return this;
        }

        /// <summary>
        /// Adds a member (by name) to the MetaType, including an itemType and defaultType for representing lists
        /// </summary>
        public MetaType Add(int fieldNumber, string memberName, Type itemType, Type defaultType)
        {
            AddField(fieldNumber, memberName, itemType, defaultType, null);
            return this;
        }

        /// <summary>
        /// Adds a member (by name) to the MetaType, including an itemType and defaultType for representing lists, returning the ValueMember rather than the fluent API.
        /// This is otherwise identical to Add.
        /// </summary>
        public ValueMember AddField(int fieldNumber, string memberName, Type itemType, Type defaultType)
        {
            return AddField(fieldNumber, memberName, itemType, defaultType, null);
        }

        private ValueMember AddField(int fieldNumber, string memberName, Type itemType, Type defaultType, object defaultValue)
        {
            if (Type.IsArray) throw new InvalidOperationException("Can't add fields to array type");
            MemberInfo mi = null;
#if WINRT
            mi = Helpers.IsEnum(Type) ? Type.GetTypeInfo().GetDeclaredField(memberName) : Helpers.GetInstanceMember(Type.GetTypeInfo(), memberName);

#else
            MemberInfo[] members = Type.GetMember(memberName, Helpers.IsEnum(Type) ? BindingFlags.Static | BindingFlags.Public : BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (members != null && members.Length == 1) mi = members[0];
#endif
            if (mi == null) throw new ArgumentException("Unable to determine member: " + memberName, "memberName");

            Type miType;
#if WINRT || PORTABLE
            PropertyInfo pi = mi as PropertyInfo;
            if (pi == null)
            {
                FieldInfo fi = mi as FieldInfo;
                if (fi == null)
                {
                    throw new NotSupportedException(mi.GetType().Name);
                }
                else
                {
                    miType = fi.FieldType;
                }
            }
            else
            {
                miType = pi.PropertyType;
            }
#else
            switch (mi.MemberType)
            {
                case MemberTypes.Field:
                    miType = ((FieldInfo)mi).FieldType; break;
                case MemberTypes.Property:
                    miType = ((PropertyInfo)mi).PropertyType; break;
                default:
                    throw new NotSupportedException(mi.MemberType.ToString());
            }
#endif
            // we can't check IgnoreListHandling (because of recursion when adding type) but we don't need to
            // it will be checked in ValueSerializedBuilder.CompleteLevel stage
            ResolveListTypes(_model, miType, ref itemType, ref defaultType);

            var serializationSettings = new ValueSerializationSettings();
            var memberSettings = new MemberMainSettingsValue { Tag = fieldNumber };

            var level0 = serializationSettings.GetSettingsCopy(0).Basic;
            level0.Collection.ConcreteType = defaultType;
            level0.Collection.ItemType = itemType;

            serializationSettings.SetSettings(level0, 0);

            serializationSettings.DefaultValue = defaultValue;

            var def = serializationSettings.DefaultLevel.GetValueOrDefault();
            def.Basic = level0.MakeDefaultNestedLevel();
            serializationSettings.DefaultLevel = def;

            ValueMember newField = new ValueMember(memberSettings, serializationSettings, mi, Type, _model);
            Add(newField);
            return newField;
        }

        public void Add(MappedMember member)
        {
            var serializationSettings = member.MappingState.SerializationSettings.Clone();
            var vm = new ValueMember(member.MainValue, serializationSettings, member.Member, this.Type, _model);
#if WINRT
            TypeInfo finalType = _typeInfo;
#else
            Type finalType = this.Type;
#endif
            PropertyInfo prop = Helpers.GetProperty(finalType, member.Member.Name + "Specified", true);
            MethodInfo getMethod = Helpers.GetGetMethod(prop, true, true);
            if (getMethod == null || getMethod.IsStatic) prop = null;
            if (prop != null)
            {
                vm.SetSpecified(getMethod, Helpers.GetSetMethod(prop, true, true));
            }
            else
            {
                MethodInfo method = Helpers.GetInstanceMethod(finalType, "ShouldSerialize" + member.Member.Name, Helpers.EmptyTypes);
                if (method != null && method.ReturnType == _model.MapType(typeof(bool)))
                {
                    vm.SetSpecified(method, null);
                }
            }
            Add(vm);
        }

        private void Add(ValueMember member)
        {
            int opaqueToken = 0;
            try
            {
                _model.TakeLock(ref opaqueToken);
                ThrowIfFrozen();
                _fields.Add(member);
                member.FinalizingSettings += (s, a) => FinalizingMemberSettings?.Invoke(this, a);
            }
            finally
            {
                _model.ReleaseLock(opaqueToken);
            }
        }
        /// <summary>
        /// Returns the ValueMember that matchs a given field number, or null if not found
        /// </summary>
        public ValueMember this[int fieldNumber]
        {
            get
            {
                foreach (ValueMember member in _fields)
                {
                    if (member.FieldNumber == fieldNumber) return member;
                }
                return null;
            }
        }
        /// <summary>
        /// Returns the ValueMember that matchs a given member (property/field), or null if not found
        /// </summary>
        public ValueMember this[MemberInfo member]
        {
            get
            {
                if (member == null) return null;
                foreach (ValueMember x in _fields)
                {
                    if (x.Member == member) return x;
                }
                return null;
            }
        }
        /// <summary>
        /// Returns the ValueMember instances associated with this type
        /// </summary>
        public ValueMember[] GetFields()
        {
            ValueMember[] arr = new ValueMember[_fields.Count];
            _fields.CopyTo(arr, 0);
            Array.Sort(arr, ValueMember.Comparer.Default);
            return arr;
        }


    }
}
#endif