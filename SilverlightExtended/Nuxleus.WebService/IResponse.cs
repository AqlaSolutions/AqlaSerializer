// Modified by Vladyslav Taranov for AqlaSerializer, 2016
using System.Collections.Generic;
using System.IO;

namespace Nuxleus.WebService {
    public interface IResponse {
        KeyValuePair<string,string>[] Headers { get; set;}
        MemoryStream Response { get; set; }
    }
}
