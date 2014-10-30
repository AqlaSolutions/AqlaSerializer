using demo_rpc_server_mvc.Models;
using AqlaSerializer.Web.Mvc;

namespace demo_rpc_server_mvc.Controllers
{
    public class NorthwindController : ProtoController
    {
        public NorthwindController()
        {
            Add<INorthwind, Northwind>();
        }

    }
}
