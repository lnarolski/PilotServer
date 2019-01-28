using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceDiscovery
{
    public class Types
    {
        public String RegType { get; set; }
        public short Port { get; set; }
        public String Name { get; set; }
        public String Domain { get; set; }
        public String HostName { get; set; }
        public String Address { get; set; }
        public List<TypeDescription> Txt { get; set; }
    }
}
