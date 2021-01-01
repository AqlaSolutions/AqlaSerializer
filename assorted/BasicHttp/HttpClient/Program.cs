﻿// Modified by Vladyslav Taranov for AqlaSerializer, 2014
using System;
using System.Net;
using MyDtoLayer;
using System.Collections.Generic;

namespace HttpClient {
    class Program {
        static void Main() {
            // create a request
            var req = new GetCustomerRequest { id = 1 };
            GetCustomerResponse resp;

            // ask the server (IHttpHandler)
            using (var client = new WebClient { BaseAddress = "http://localhost:22174" }) {
                resp = client.UploadProto<GetCustomerResponse>("/MyHandler.ashx", req);
            }

            // write the answer
            WriteCustomer(resp.cust);

            // ask the server (ASP.NET MVC)
            using (var client = new WebClient { BaseAddress = "http://localhost:22174" }) {
                resp = client.UploadProto<GetCustomerResponse>("/Customer/GetCustomer", req);
            }

            // write the answer
            WriteCustomer(resp.cust);
        }

        static void WriteCustomer(Customer cust)
        {
            Console.WriteLine(cust.id);
            Console.WriteLine(cust.name);
            var addr = cust.address;
            Console.WriteLine(addr.line1);
            Console.WriteLine(addr.zip);  
        }
    }
}
