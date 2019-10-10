using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NLog;
using System.Threading;
using Fleck;
using HidSharp;
using HidSharp.Reports;
using HidSharp.Reports.Encodings;
using HidSharp.Utility;


namespace HIDServer
{
    public class DeviceValue
    {
        public DataItem item;
        public string name;

        public long time; // ms values
        public object value;

        public string ToJSON()
        {
            StringWriter str = new StringWriter();
            using (JsonWriter json = new JsonTextWriter(str))
            {
                json.WriteStartObject();
                json.WritePropertyName("name");
                json.WriteValue(name);

                json.WritePropertyName("time");
                json.WriteValue(time);

                json.WritePropertyName("value");
                json.WriteValue(value);
                
                json.WriteEndObject();
            }
            return str.ToString();
        }

        public string ToInfluxLine()
        {
            return "hid " + name + "=" + value + " " + time;
        }
    }

    public class DeviceWatcher
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        SortedDictionary<uint, DeviceValue> values;
        HidStream stream;
        string name;
        public TheSockets sockets;

        public DeviceWatcher(string name, HidDevice device, TheSockets sockets)
        {
            this.name = name;
            values = new SortedDictionary<uint, DeviceValue>();
            this.listen(device);
            this.sockets = sockets;
        }

        private void WriteDeviceItemInputParserResult(HidSharp.Reports.Input.DeviceItemInputParser parser)
        {
            while (parser.HasChanged)
            {
                int changedIndex = parser.GetNextChangedIndex();
                var dataValue = parser.GetValue(changedIndex);
                uint key = dataValue.Usages.FirstOrDefault();
                if (!values.ContainsKey(key))
                {
                    string disp = ((Usage)key).ToString();
                    if(disp.StartsWith("427"))
                    {
                        Console.WriteLine("SKIP: "+disp);
                        continue;
                    }
                    DeviceValue tmp = new DeviceValue();
                    tmp.item = dataValue.DataItem;
                    tmp.name = this.name + disp;
                    values[key] = tmp;
                }
                DeviceValue v = values[key];
                v.time = GrafanaQuery.ToUnixTime(DateTime.Now);
                if(v.item.ElementBits==1)
                {
                    v.value = (dataValue.GetLogicalValue()>0) ? true : false;
                }
                else
                {
                    v.value = dataValue.GetLogicalValue();
                }

                if(sockets != null)
                {
                    sockets.write(v);
                }
                //Console.WriteLine(v.ToJSON());
            }
        }

        public void broadcast()
        {
            if(sockets!=null)
            {
                foreach (DeviceValue v in this.values.Values)
                {
                    sockets.write(v);
                }
            }
        }

        public static HidDevice find(string name)
        {
            foreach (var dev in DeviceList.Local.GetHidDevices())
            {
                if (name.Equals(dev.GetFriendlyName()))
                {
                    return dev;
                }
            }
            return null;
        }

        private void listen(HidDevice dev)
        {
            HidStream hidStream;
            if (!dev.TryOpen(out hidStream))
            {
                return;
            }
            var values = new SortedDictionary<uint, DeviceValue>();

            Console.WriteLine("Opening: " + dev.GetFriendlyName());
            hidStream.ReadTimeout = Timeout.Infinite;
            this.stream = hidStream;

            var reportDescriptor = dev.GetReportDescriptor();
            foreach (var deviceItem in reportDescriptor.DeviceItems)
            {
                foreach (var report in deviceItem.Reports)
                {
                    foreach (var dataItem in report.DataItems)
                    {
                        foreach (var u in dataItem.Usages.GetAllValues())
                        {
                            //Console.WriteLine("  >> " + u.ToString("X4") + " " + ((Usage)u).ToString());
                        }
                    }
                }

                var inputReportBuffer = new byte[dev.GetMaxInputReportLength()];
                var inputReceiver = reportDescriptor.CreateHidDeviceInputReceiver();
                var inputParser = deviceItem.CreateDeviceItemInputParser();

                inputReceiver.Received += (sender, e) =>
                {
                    Report report;
                    while (inputReceiver.TryRead(inputReportBuffer, 0, out report))
                    {
                        // Parse the report if possible.
                        // This will return false if (for example) the report applies to a different DeviceItem.
                        if (inputParser.TryParseReport(inputReportBuffer, 0, report))
                        {
                            this.WriteDeviceItemInputParserResult(inputParser);
                        }
                    }
                };
                inputReceiver.Start(hidStream);
            }
        }
    }
}
