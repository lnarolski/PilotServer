using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Zeroconf;
using Mono.Zeroconf.Providers;
using System.Threading;

namespace ServiceDiscovery
{
    public class Search
    {
        public List<Types> ServicesTypes { get; private set; }
        private string _regType;
        private string _domain;

        public Search()
        {
            ServicesTypes = new List<Types>();
        }

        public void FindAll(String regType, String domain)
        {
            _regType = regType;
            _domain = domain;
            Task.Factory.StartNew(() => StartingSearch());
        }

        private void StartingSearch()
        {
            Mono.Zeroconf.ServiceBrowser browser = new Mono.Zeroconf.ServiceBrowser();
            browser.ServiceAdded += ServiceAdded;
            browser.Browse(_regType, _domain);
        }

        public void ClearServices()
        {
            ServicesTypes.Clear();
        }

        public void ServiceAdded(object o, ServiceBrowseEventArgs args)
        {
            Types types = new Types();
            types.Name = args.Service.Name;
            types.RegType = args.Service.RegType;
            types.Domain = args.Service.ReplyDomain;
            types.Port = args.Service.Port;
            if (args.Service.HostEntry != null)
            {
                types.HostName = args.Service.HostEntry.HostName;
                types.Address = args.Service.HostEntry.AddressList[0].ToString();
            }


            ITxtRecord record = args.Service.TxtRecord;
            if (record != null)
            {
                for (int i = 0, n = record.Count; i < n; i++)
                {
                    TxtRecordItem item = record.GetItemAt(i);
                    types.Txt.Add(new TypeDescription { Key = item.Key, Value = item.ValueString });
                }
            }

            System.Diagnostics.Debug.WriteLine("Name: " + types.Name);
            ServicesTypes.Add(types);
        }

    }
}
