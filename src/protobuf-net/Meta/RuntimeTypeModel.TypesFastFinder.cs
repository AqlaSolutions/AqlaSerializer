#if !PORTABLE
#define ENABLED
#endif

// Modified by Vladyslav Taranov for AqlaSerializer, 2016
#if !NO_RUNTIME
using System;
using System.Collections;
using System.Collections.Generic;
#if !PORTABLE
using System.Runtime.Serialization;
#endif
using System.Text;
#if FEAT_IKVM
using Type = IKVM.Reflection.Type;
using IKVM.Reflection;
#if FEAT_COMPILER
using IKVM.Reflection.Emit;
using TriAxis.RunSharp;
#endif
#else
using System.Reflection;
#if FEAT_COMPILER
using System.Reflection.Emit;
using TriAxis.RunSharp;
#endif
#endif
#if FEAT_COMPILER
using AqlaSerializer.Compiler;
#endif
#if !FEAT_IKVM
using AqlaSerializer.Meta.Data;
#endif
using AqlaSerializer;
using AqlaSerializer.Serializers;
using System.Threading;
using System.IO;
using AltLinq;
using System.Linq;


#if ENABLED
#if SLIM
using ApplicationException = System.TimeoutException;
#endif
#endif


namespace AqlaSerializer.Meta
{
    /// <summary>
    /// Provides protobuf serialization support for a number of types that can be defined at runtime
    /// </summary>
    partial class RuntimeTypeModel
    {
        struct TypeInfoElement
        {
            public readonly MetaType MetaType;
            public readonly int Key;

            public TypeInfoElement(int key, MetaType metaType)
            {
                Key = key;
                MetaType = metaType;
            }
        }

        private BasicList _types = new BasicList();

#if ENABLED
        Dictionary<Type, TypeInfoElement> _typesDictionary = new Dictionary<Type, TypeInfoElement>();
        ReaderWriterLock _typesDictionaryLock = new ReaderWriterLock();

#if SLIM
        class ReaderWriterLock
        {
            readonly System.Threading.ReaderWriterLockSlim _lock = new System.Threading.ReaderWriterLockSlim();

            public void AcquireReaderLock(int timeout)
            {
                if (!_lock.TryEnterReadLock(timeout))
                    throw new ApplicationException("Lock enter timeout exceeded");
            }

            public void ReleaseReaderLock()
            {
                _lock.ExitReadLock();
            }

            public void AcquireWriterLock(int timeout)
            {
                if (!_lock.TryEnterReadLock(timeout))
                    throw new ApplicationException("Lock enter timeout exceeded");
            }

            public void ReleaseWriterLock()
            {
                _lock.ExitReadLock();
            }
        }
#endif


#endif
        void ResetTypesDictionary()
        {
#if ENABLED
            _typesDictionary = new Dictionary<Type, TypeInfoElement>();
            _typesDictionaryLock = new ReaderWriterLock();
#endif
        }

        /// <summary>
        /// Used to speedup type key search (instead of IndexOf on linked list)
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        TypeInfoElement? GetTypeInfoFromDictionary(Type type)
        {
#if ENABLED
            bool released = false;
            try
            {
                _typesDictionaryLock.AcquireReaderLock(60000);
                TypeInfoElement v;
                if (_typesDictionary.TryGetValue(type, out v)) return v;
                return null;
            }
            catch (ApplicationException ex)
            {
                released = true;
                throw new TimeoutException("Can't obtain a reader lock on types dictionary", ex);
            }
            finally
            {
                if (!released) _typesDictionaryLock.ReleaseReaderLock();
            }
#endif
            int key = _types.IndexOf(MetaTypeFinder, type);
            if (key < 0) return null;
            return new TypeInfoElement(key, (MetaType)_types[key]);
        }

        int Add(MetaType metaType)
        {
            _overridingManager.SubscribeTo(metaType);
            int key = _types.Add(metaType);
#if ENABLED
            var info = new TypeInfoElement(key, metaType);
            bool released = false;

            try
            {
                _typesDictionaryLock.AcquireWriterLock(60000);
                _typesDictionary.Add(metaType.Type, info);
            }
            catch (ApplicationException)
            {
                throw new TimeoutException("Can't obtain a writer lock on types dictionary to add meta type " + metaType.Type.FullName);
            }
            finally
            {
                if (!released)
                    _typesDictionaryLock.ReleaseWriterLock();
            }
#endif
            return key;
        }
    }
}
#endif