﻿using NUnit.Framework;
using ProtoBuf;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace Examples.Issues
{
    [TestFixture]
    public class Issue167
    {
        [Test]
        public void Execute()
        {
            var test = new Problematic();
            try
            {
                using (var memStream = new MemoryStream())
                {
                    AqlaSerializer.Serializer.Serialize<Problematic>(memStream, test); //causes stackoverflow exception.
                }
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.StackTrace);
            }
        }

        [ProtoContract]
        class Problematic : IEnumerable
        {
            private List<Problematic> _children =
                new List<Problematic>();

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return _children.GetEnumerator();
            }

            public Problematic this[int i]
            {
                get { return _children[i]; }
            }
        }
    }

    
}
