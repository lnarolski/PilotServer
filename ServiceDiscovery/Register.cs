using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Zeroconf;

namespace ServiceDiscovery
{
    public class Register
    {

        public void RegisterService(Types type)
        {
            RegisterService service = new RegisterService();
            service.Name = type.Name;
            service.RegType = type.RegType;
            service.ReplyDomain = type.Domain;
            service.Port = type.Port;

            //TxtRecord txt_record = new TxtRecord();
            //foreach (TypeDescription item in type.Txt)
            //    txt_record.Add(item.Key, item.Value);
            
            //service.TxtRecord = txt_record;
            service.Register();
        }
    }
}
