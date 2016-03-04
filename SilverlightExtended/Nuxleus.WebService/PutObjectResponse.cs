﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System.Collections.Generic;
using System.IO;

namespace Nuxleus.WebService {

    public struct PutObjectResponse : IResponse {
        public KeyValuePair<string, string>[] Headers { get; set; }
        public MemoryStream Response { get; set; }
    }
}
